using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
namespace CatPriceCalculator.Core;

internal sealed class OAuthTrackingClient<T>(ManualTrackingSession session, string carrier,
    Func<T, bool> valid, Func<T, HttpRequestMessage> authentication, Func<T, string, HttpRequestMessage> tracking,
    Func<JsonElement, string, ParcelTrackingResult> parse, Func<string, bool> validNumber) where T : class
{
    private T? authenticatedSettings;
    private string? accessToken;
    private DateTimeOffset tokenExpires;
    public void ForgetToken() { accessToken = null; authenticatedSettings = null; tokenExpires = default; }
    public Task<ParcelTrackingResult> FetchAsync(T settings, string number, CancellationToken token)
    {
        number = (number ?? "").Trim();
        return session.RunAsync(number, () =>
        {
            if (!valid(settings)) throw new ParcelTrackingException(TrackingErrorCode.Configuration, $"Előbb mentsd a hozzáférést a Beállítások / {carrier} lapon.");
            if (!validNumber(number)) throw new ParcelTrackingException(TrackingErrorCode.InvalidNumber, $"Adj meg érvényes {carrier} csomagszámot.");
        }, async context =>
        {
            int limit = settings switch { UpsConnectionSettings s => s.DailyLimit, FedExConnectionSettings s => s.DailyLimit, _ => throw new InvalidOperationException() };
            async Task Authenticate()
            {
                using var request = authentication(settings);
                using var response = await context.SendAsync(request, limit, "OAuth");
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new ParcelTrackingException(TrackingErrorCode.Authentication, $"A {carrier} hitelesítés sikertelen. Ellenőrizd a két projektkulcsot és az alkalmazás hozzáférését.");
                if (!response.IsSuccessStatusCode) throw new ParcelTrackingException(TrackingErrorCode.Offline, $"A {carrier} hitelesítési szolgáltatása most nem elérhető. Próbáld később.");
                using var document = await TrackingJson.ReadAsync(response.Content, token);
                var root = document.RootElement; string? value = TrackingJson.Text(root, "access_token");
                if (string.IsNullOrEmpty(value) || value.Length > 16384 || value.Any(c => c <= 32 || c >= 127) ||
                    !string.Equals(TrackingJson.Text(root, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase) ||
                    !int.TryParse(TrackingJson.Number(root, "expires_in"), NumberStyles.None, CultureInfo.InvariantCulture, out int ttl) || ttl is <= 0 or > 2592000)
                    throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, $"A {carrier} nem adott értelmezhető hozzáférési tokent. A titkos értékeket nem naplózzuk.");
                accessToken = value; authenticatedSettings = settings;
                tokenExpires = session.Now().AddSeconds(ttl - Math.Min(60, ttl / 2));
                context.Trace($"{carrier} hitelesítés sikeres; token csak memóriában tárolva.");
            }
            if (accessToken == null || !EqualityComparer<T>.Default.Equals(authenticatedSettings, settings) || session.Now() >= tokenExpires) await Authenticate();
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var request = tracking(settings, number);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await context.SendAsync(request, limit, "Követés");
                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { ForgetToken(); await Authenticate(); continue; }
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new ParcelTrackingException(TrackingErrorCode.Authentication, $"A {carrier} nem engedélyezte a követést. Ellenőrizd a Tracking API éles hozzáférését a fejlesztői projektednél.");
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    throw new ParcelTrackingException(TrackingErrorCode.NotFound, $"A {carrier} nem találja ezt a csomagot. Ellenőrizd a számot; friss feladásnál próbáld később.");
                if (!response.IsSuccessStatusCode) throw new ParcelTrackingException(TrackingErrorCode.Offline, $"A {carrier} követési szolgáltatása most nem elérhető. A korábbi adatok megmaradnak.");
                context.Stage = "Feldolgozás";
                using var document = await TrackingJson.ReadAsync(response.Content, token);
                return parse(document.RootElement, number);
            }
            throw new ParcelTrackingException(TrackingErrorCode.Authentication, $"A {carrier} hitelesítést nem sikerült megújítani.");
        }, token);
    }
}
