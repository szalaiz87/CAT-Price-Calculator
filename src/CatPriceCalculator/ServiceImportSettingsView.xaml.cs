using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class ServiceImportSettingsView : UserControl
{
    private readonly ServiceCalendarStore store=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","ServiceCalendar"));
    private ServiceImportPaths paths=new();
    private Func<string,string,Task<string>>? import;
    private Action? openFolder;
    public ServiceImportSettingsView() {
        InitializeComponent();
        try {paths=store.LoadImportPaths();Status.Text=paths.CustomerFile!=null || paths.MachineFile!=null?"A korábban tallózott fájlútvonalak betöltve.":"Tallózza be az ügyfél- és a gépfájlt.";} catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException){Status.Text="A korábbi fájlútvonalak nem olvashatók. Tallózza be újra a fájlokat.";}
        ShowPaths();
    }
    public void Configure(Func<string,string,Task<string>> importer,Action folder){import=importer;openFolder=folder;}
    private void ShowPaths(){CustomerPath.Text=paths.CustomerFile??"Nincs ügyfélfájl kiválasztva.";MachinePath.Text=paths.MachineFile??"Nincs gépfájl kiválasztva.";CustomerPath.ToolTip=CustomerPath.Text;MachinePath.ToolTip=MachinePath.Text;}
    private void ChooseCustomers(object sender,RoutedEventArgs e)=>Choose(true);
    private void ChooseMachines(object sender,RoutedEventArgs e)=>Choose(false);
    private void Choose(bool customer){
        var picker=new Microsoft.Win32.OpenFileDialog{Filter="Excel-munkafüzet (*.xlsx)|*.xlsx",CheckFileExists=true,Title=customer?"Ügyféladatbázis":"Gépadatbázis"};
        if(picker.ShowDialog()!=true)return;
        paths=customer?paths with{CustomerFile=picker.FileName}:paths with{MachineFile=picker.FileName};ShowPaths();
        Status.Text=store.SaveImportPaths(paths)?"Fájlválasztás mentve. A törzs frissítéséhez indítsa a beolvasást.":"Az útvonal nem menthető. Ebben a munkamenetben használható.";
    }
    private void OpenFolder(object sender,RoutedEventArgs e)=>openFolder?.Invoke();
    private async void Import(object sender,RoutedEventArgs e){
        if(paths.CustomerFile==null || paths.MachineFile==null){Status.Text="Tallózza be az ügyfél- és a gépfájlt is.";return;}
        if(import==null)return;
        ImportButton.IsEnabled=CustomerButton.IsEnabled=MachineButton.IsEnabled=FolderButton.IsEnabled=false;
        Status.Text="Beolvasás folyamatban…";
        try{Status.Text=await import(paths.CustomerFile,paths.MachineFile);}
        finally{ImportButton.IsEnabled=CustomerButton.IsEnabled=MachineButton.IsEnabled=FolderButton.IsEnabled=true;}
    }
}
