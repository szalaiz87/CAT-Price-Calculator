using System.Net.Http;
using System.Security.Cryptography;
using System.Windows.Threading;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
internal sealed class GoogleSyncCoordinator : IDisposable
{
    private readonly GoogleCalendarSettingsStore store;
    private readonly GoogleSyncStateStore stateStore;
    private readonly ServiceCalendarStore calendar;
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(40),MaxResponseContentBufferSize=2*1024*1024};
    private readonly GoogleSheetsCalendar service;
    private readonly CancellationTokenSource lifetime;
    private readonly Func<bool> allowed;
    private readonly DispatcherTimer retry=new(){Interval=TimeSpan.FromMinutes(5)};
    private long revision;
    private bool disposed;
    public GoogleCalendarSettings Settings {get;private set;}
    public GoogleSyncState State {get;private set;}
    public bool Busy {get;private set;}
    public bool StorageReady {get;}
    public event Action? Changed;
    public GoogleSyncCoordinator(string directory,Func<bool> allowed,CancellationToken cancellation){
        this.allowed=allowed;lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        store=new(Path.Combine(directory,"google-calendar.bin"),new WindowsSecretProtector("GoogleCalendar"));
        stateStore=new(Path.Combine(directory,"google-calendar-state.json"));calendar=new(Path.Combine(directory,"ServiceCalendar"));service=new(http);
        var loaded=store.Load();Settings=loaded.Settings;StorageReady=!loaded.Failed;State=stateStore.Load();
        if(!StorageReady)State=State with{Error="A Google-beállítások nem olvashatók. Az eredeti fájl megmaradt."};
        retry.Tick+=async(_,_)=>{if(allowed() && State.Pending)await AutoSyncAsync();};retry.Start();
    }
    public string Summary=>!StorageReady?State.Error:!Settings.IsConfigured?"Google: nincs beállítva":State.Pending?"Google: szinkronra vár"+(State.Error.Length>0?"\nKapcsolati hiba; újrapróbálásra vár.":""):"Google: utolsó szinkron\n"+RateTimestamp.Format(State.LastSuccess);
    public bool SaveSettings(GoogleCalendarSettings settings){
        if(Busy || !StorageReady)return false;
        if(!store.Save(settings))return false;
        Settings=settings;service.ForgetToken();revision++;State=new(Pending:settings.IsConfigured);Persist();return true;
    }
    private void Persist(){if(!stateStore.Save(State))State=State with{Error="A szinkronállapot nem menthető; a helyi naptár megmaradt."};if(!disposed)Changed?.Invoke();}
    public async Task MarkChangedAsync(){
        if(!Settings.IsConfigured)return;revision++;State=State with{Pending=true};Persist();await AutoSyncAsync();
    }
    public Task AutoSyncAsync()=>Settings.AutoSync && StorageReady && allowed()?SyncAsync():Task.CompletedTask;
    public async Task TestAsync(){
        if(Busy)return;Busy=true;Changed?.Invoke();
        try{var title=await service.TestAsync(Settings,lifetime.Token);State=State with{Error="Olvasási kapcsolat rendben: "+title};}
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or FormatException or CryptographicException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException){State=State with{Error=SafeError(e)};}
        finally{Busy=false;if(!disposed)Changed?.Invoke();}
    }
    public async Task SyncAsync(){
        if(Busy || !Settings.IsConfigured || !StorageReady)return;
        Busy=true;var started=revision;State=State with{Pending=true,Error=""};Persist();
        try{
            var jobs=calendar.LoadJobs();await service.SyncAsync(Settings,jobs,lifetime.Token);
            if(disposed)return;State=new(DateTimeOffset.UtcNow,jobs.Length,revision!=started,"");
        }catch(Exception e) when(e is HttpRequestException or OperationCanceledException or FormatException or CryptographicException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or KeyNotFoundException or ArgumentException){State=State with{Pending=true,Error=SafeError(e)};}
        finally{Busy=false;if(!disposed)Persist();}
    }
    private static string SafeError(Exception error)=>error is HttpRequestException or FormatException?error.Message:error is OperationCanceledException?"A Google-kérés megszakadt vagy túllépte az időkeretet. A helyi naptár megmaradt.":"Nem sikerült a Google-művelet. Ellenőrizze a kapcsolatot; a helyi naptár megmaradt.";
    public void Dispose(){disposed=true;retry.Stop();lifetime.Cancel();service.ForgetToken();http.Dispose();lifetime.Dispose();}
}
