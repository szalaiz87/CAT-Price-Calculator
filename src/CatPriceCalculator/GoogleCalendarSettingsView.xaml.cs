using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class GoogleCalendarSettingsView : UserControl
{
    private GoogleSyncCoordinator? sync;
    private Func<bool> allowed=()=>false;
    private GoogleCalendarSettings credential=new();
    private bool draft;
    public GoogleCalendarSettingsView(){InitializeComponent();}
    internal void Configure(GoogleSyncCoordinator coordinator,Func<bool> allowed){sync=coordinator;this.allowed=allowed;credential=sync.Settings;SheetInput.Text=credential.SpreadsheetId;AutoSyncInput.IsChecked=credential.AutoSync;sync.Changed+=Render;Render();}
    private void DraftChanged(object sender,RoutedEventArgs e){if(sync!=null)Render();}
    private void Render(){
        if(sync==null)return;
        EmailText.Text=credential.ClientEmail;
        if(!draft)KeyStatus.Text=credential.PrivateKey.Length>0?"Kulcs mentve · Windows-fiókhoz kötött titkosítással":"Még nincs kulcs betöltve.";
        Status.Text=(sync.Busy?"Google-művelet folyamatban…":sync.State.Error.Length>0?sync.State.Error:sync.State.Pending?"A naptár szinkronizálásra vár.":"A kapcsolat beállítható.")+"\nUtolsó siker: "+RateTimestamp.Format(sync.State.LastSuccess)+(sync.State.LastSuccess!=null?$" · {sync.State.Jobs} munka":"");
        SaveButton.IsEnabled=ClearButton.IsEnabled=ImportKeyButton.IsEnabled=SheetInput.IsEnabled=AutoSyncInput.IsEnabled=allowed() && !sync.Busy && sync.StorageReady;
        var unsaved=draft || SheetInput.Text.Trim()!=sync.Settings.SpreadsheetId || (AutoSyncInput.IsChecked==true)!=sync.Settings.AutoSync;
        TestButton.IsEnabled=SyncButton.IsEnabled=allowed() && !sync.Busy && sync.StorageReady && sync.Settings.IsConfigured && !unsaved;
    }
    private bool Allowed()=>allowed() && sync!=null && !sync.Busy;
    private void ImportKey(object sender,RoutedEventArgs e){
        if(!Allowed())return;
        var picker=new Microsoft.Win32.OpenFileDialog{Filter="Google szolgáltatásfiók (*.json)|*.json",CheckFileExists=true};if(picker.ShowDialog()!=true)return;
        try{if(new FileInfo(picker.FileName).Length>65536)throw new FormatException("Túl nagy kulcsfájl.");credential=GoogleCalendarSettings.ImportCredential(File.ReadAllText(picker.FileName));draft=true;KeyStatus.Text="Betöltve: "+Path.GetFileName(picker.FileName)+" · mentés szükséges";EmailText.Text=credential.ClientEmail;Status.Text="Ossza meg a táblázatot ezzel az e-mail-címmel Szerkesztőként, majd mentse a beállításokat.";}
        catch(Exception ex) when(ex is FormatException or IOException or UnauthorizedAccessException){Status.Text=ex is FormatException?ex.Message:"A kulcsfájl nem olvasható.";}
    }
    private void Save(object sender,RoutedEventArgs e){
        if(!Allowed())return;
        try{var settings=credential with{SpreadsheetId=GoogleCalendarSettings.ParseId(SheetInput.Text),AutoSync=AutoSyncInput.IsChecked==true};
            if(!settings.IsConfigured){Status.Text="Tallózza be a Google-kulcsot is.";return;}
            if(!sync!.SaveSettings(settings)){Status.Text="Nem sikerült menteni. A korábbi kapcsolat megmaradt.";return;}credential=settings;draft=false;SheetInput.Text=settings.SpreadsheetId;Render();Status.Text="Google-kapcsolat mentve. Tesztelje, majd szinkronizálja a naptárat.";
        }catch(FormatException ex){Status.Text=ex.Message;}
    }
    private void Clear(object sender,RoutedEventArgs e){
        if(!Allowed())return;
        if(MessageBox.Show("Törli a helyi Google-kapcsolat beállításait? A Google-táblázat és a helyi naptár megmarad.","Google-kapcsolat",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        if(!sync!.SaveSettings(new())){Status.Text="A kapcsolat nem törölhető.";return;}credential=new();draft=false;SheetInput.Clear();AutoSyncInput.IsChecked=false;Render();
    }
    private async void Test(object sender,RoutedEventArgs e){if(Allowed())await sync!.TestAsync();}
    private async void Sync(object sender,RoutedEventArgs e){if(Allowed())await sync!.SyncAsync();}
    private void OpenSheet(object sender,RoutedEventArgs e){if(!allowed())return;try{var id=GoogleCalendarSettings.ParseId(SheetInput.Text);Process.Start(new ProcessStartInfo("https://docs.google.com/spreadsheets/d/"+id+"/edit"){UseShellExecute=true});}catch(Exception ex) when(ex is FormatException or System.ComponentModel.Win32Exception or InvalidOperationException){Status.Text="A táblázat nem nyitható meg. Ellenőrizze a hivatkozást.";}}
    private void Guide(object sender,RoutedEventArgs e){
        if(!allowed())return;
        var path=Path.Combine(Path.GetTempPath(),"Linser-Google-Sheets-Utmutato.txt");
        try{using var stream=Application.GetResourceStream(new Uri("pack://application:,,,/Assets/GOOGLE-SHEETS.md"))!.Stream;using var reader=new StreamReader(stream);File.WriteAllText(path,reader.ReadToEnd());Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){Status.Text="Az útmutató nem nyitható meg.";}
    }
}
