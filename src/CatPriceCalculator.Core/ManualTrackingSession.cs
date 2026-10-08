using System.Diagnostics;
using System.Net;
namespace CatPriceCalculator.Core;

// Shared lifecycle for explicit manual calls only: no scheduling or automatic authentication.
internal sealed class ManualTrackingSession(HttpClient client, TrackingRequestBudget budget, string carrier,
    Func<DateTimeOffset>? clock = null, Action<ApiLogEntry>? log = null)
{
    private readonly HttpClient transport = client;
    private readonly TrackingRequestBudget quota = budget;
    private readonly string provider = carrier;
    private readonly Action<ApiLogEntry>? logger = log;
    private readonly SemaphoreSlim gate = new(1, 1);
    public readonly Func<DateTimeOffset> Now = clock ?? (() => DateTimeOffset.UtcNow);
    private DateTimeOffset blockedUntil;
    public async Task<ParcelTrackingResult> RunAsync(string number, Action validate,
        Func<Request, Task<ParcelTrackingResult>> operation, CancellationToken token)
    {
        var request = new Request(this, number, token); bool acquired = false;
        try
        {
            request.Trace($"Kézzel indított {provider} lekérés."); validate();
            request.Stage = "Várakozás";
            await gate.WaitAsync(token); acquired = true;
            var result = await operation(request);
            request.Trace($"A {provider} követési válasza sikeresen feldolgozva.");
            return result;
        }
        catch (ParcelTrackingException ex) { request.Trace(ex.Message, true); throw; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { string message = $"A {provider} lekérése túllépte az időkorlátot. Próbáld később."; request.Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.Offline, message); }
        catch (OperationCanceledException) { request.Trace($"A kézi {provider} lekérés megszakítva."); throw; }
        catch (HttpRequestException)
        { string message = $"A {provider} nem érhető el. Ellenőrizd az internetkapcsolatot; a korábbi adatok megmaradnak."; request.Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.Offline, message); }
        catch (System.Text.Json.JsonException)
        { string message = $"A {provider} válasza nem értelmezhető. A korábbi adatok megmaradnak."; request.Trace(message, true); throw new ParcelTrackingException(TrackingErrorCode.InvalidResponse, message); }
        finally { if (acquired) gate.Release(); }
    }
    internal sealed class Request(ManualTrackingSession session, string number, CancellationToken token)
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly string reference = "…" + new string((number ?? "").Where(char.IsLetterOrDigit).TakeLast(4).ToArray());
        public string Stage { get; set; } = "Beállítás";
        public void Trace(string message, bool error = false, int? status = null)
        {
            try { session.logger?.Invoke(new(session.Now(), Stage, message, status, reference,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, error, session.provider)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, int limit, string stage)
        {
            Stage = stage;
            if (session.Now() < session.blockedUntil) throw new ParcelTrackingException(TrackingErrorCode.Quota, $"A {session.provider} korlátozta a lekéréseket. Próbáld újra később.");
            await session.quota.ReserveAsync(limit, token);
            message.Headers.UserAgent.ParseAdd(BuildInfo.UserAgent);
            Trace($"{message.Method} kérés a {session.provider} éles végpontjára.");
            var response = await session.transport.SendAsync(message, token);
            Trace($"{session.provider} HTTP-válasz érkezett.", !response.IsSuccessStatusCode, (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Block(response.Headers.RetryAfter?.Date ?? session.Now() + (response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15)));
                response.Dispose();
                throw new ParcelTrackingException(TrackingErrorCode.Quota, $"A {session.provider} lekérdezési limitet jelzett. Próbáld később.");
            }
            return response;
        }
        public void Block(DateTimeOffset until) => session.blockedUntil = until > session.Now() ? until : session.Now().AddSeconds(5);
    }
}
