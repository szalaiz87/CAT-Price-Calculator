using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class ServiceCalendarView : UserControl
{
    private readonly ServiceCalendarStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CAT-Price-Calculator", "ServiceCalendar"));
    private readonly string importDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LinserHungary", "Szerviznaptar", "Import");
    private ServiceCatalog catalog = new([],[]);
    private ServiceJob[] jobs = [];
    private ServiceCalendarWindow? calendar;
    private bool storageReady;
    private string? customerFile, machineFile;
    private void ChooseCustomers(object sender, RoutedEventArgs e) => ChooseFile(true);
    private void ChooseMachines(object sender, RoutedEventArgs e) => ChooseFile(false);
    private void ChooseFile(bool customers) {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter="Excel-munkafüzet (*.xlsx)|*.xlsx", CheckFileExists=true, Title=customers?"Ügyféladatbázis kiválasztása":"Gépadatbázis kiválasztása" };
        if(picker.ShowDialog(Window.GetWindow(this))!=true)return;
        if(customers)customerFile=picker.FileName;else machineFile=picker.FileName;
        ImportStatus.Text=$"Ügyfél: {Path.GetFileName(customerFile ?? "ugyfelek.xlsx")} · Gép: {Path.GetFileName(machineFile ?? "gepek.xlsx")}";
        ImportStatus.ToolTip=$"Ügyfél: {customerFile}\nGép: {machineFile}";
    }
    public ServiceCalendarView() {
        InitializeComponent(); StaffInput.ItemsSource = ServiceCalendar.Staff; StaffInput.SelectedIndex = 0;
        DateInput.Text = DateTime.Today.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        try { catalog=store.LoadCatalog(); jobs=store.LoadJobs(); storageReady=true; }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException) { Status.Text=e is FormatException ? e.Message : "A szervizadatok nem olvashatók. Mentés letiltva az eredeti adatok védelmében."; }
        SaveButton.IsEnabled=storageReady;
        CustomerInput.ItemsSource=catalog.Customers; MachineInput.IsEnabled=false;
        ImportStatus.Text=$"{catalog.Customers.Length} ügyfél · {catalog.Machines.Length} gép. Az importmappában ugyfelek.xlsx és gepek.xlsx szükséges.";
    }
    private void CustomerChanged(object sender, SelectionChangedEventArgs e) {
        if(MachineInput==null) return;
        MachineInput.SelectedItem=null;
        MachineInput.ItemsSource=CustomerInput.SelectedItem is ServiceCustomer c ? ServiceCalendar.MachinesFor(catalog,c.Id).ToArray() : Array.Empty<ServiceMachine>();
        MachineInput.IsEnabled=CustomerInput.SelectedItem!=null;
    }
    private void OpenImportFolder(object sender,RoutedEventArgs e) {
        try {
            Directory.CreateDirectory(importDirectory);
            foreach(var name in new[]{"ugyfelek-minta.xlsx","gepek-minta.xlsx"}) {
                var destination=Path.Combine(importDirectory,name);
                if(!File.Exists(destination)) {
                    using var input=Application.GetResourceStream(new Uri("/CAT-Price-Calculator;component/Assets/ServiceCalendar/"+name,UriKind.Relative)).Stream;
                    using var output=File.Create(destination);input.CopyTo(output);
                }
            }
            var instructions=Path.Combine(importDirectory,"IMPORT-UTMUTATO.txt");
            if(!File.Exists(instructions)) File.WriteAllText(instructions, "Excel (.xlsx), első munkalap, első nem üres sor a fejléc.\r\nugyfelek.xlsx fejléc: ugyfelazonosito;nev\r\ngepek.xlsx fejléc: gepazonosito;ugyfelazonosito;megnevezes;gyariszam\r\nA gyariszam opcionális. Az azonosítókat szövegként exportálja, hogy a kezdő nullák megmaradjanak. Az ügyfélazonosító kapcsolja össze a két fájlt.\r\nA fájlokban egyedi azonosítók szükségesek. A beolvasás nem törli a naptár feladatait.\r\nMinden feladat helyben mentődik; más számítógéppel nincs szinkronizálás.\r\n",new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo(importDirectory){UseShellExecute=true});
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { Status.Text="Az importmappa nem nyitható meg: "+importDirectory; }
    }
    private async void ImportCatalog(object sender,RoutedEventArgs e) {
        var button=(Button)sender; button.IsEnabled=false; SaveButton.IsEnabled=false;
        try {
            var customers=customerFile ?? Path.Combine(importDirectory,"ugyfelek.xlsx");
            var machines=machineFile ?? Path.Combine(importDirectory,"gepek.xlsx");
            var imported=await Task.Run(()=>ServiceCatalogCsv.Load(customers,machines));
            if(!store.SaveCatalog(imported)) {Status.Text="Az ügyfél- és gépadatok nem menthetők. A korábbi adatbázis megmaradt.";return;}
            catalog=imported; CustomerInput.ItemsSource=catalog.Customers; CustomerInput.SelectedItem=null; CustomerChanged(CustomerInput,null!);
            ImportStatus.Text=$"Beolvasva: {catalog.Customers.Length} ügyfél · {catalog.Machines.Length} gép."; Status.Text="Adatbázis frissítve. A naptár korábbi feladatai megmaradtak.";
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException or DecoderFallbackException or System.Xml.XmlException or InvalidDataException) {Status.Text=ex is FormatException ? ex.Message : "Nem sikerült beolvasni a két Excel-fájlt. Ellenőrizze az importmappát és az útmutatót.";}
        finally {button.IsEnabled=true;SaveButton.IsEnabled=storageReady;}
    }
    private void SaveJob(object sender,RoutedEventArgs e) {
        if(!storageReady)return;
        if(CustomerInput.SelectedItem is not ServiceCustomer customer || MachineInput.SelectedItem is not ServiceMachine machine || StaffInput.SelectedItem is not ServiceStaff staff) {Status.Text="Válasszon ügyfelet, hozzá tartozó gépet és munkatársat.";return;}
        if(!DateOnly.TryParseExact(DateInput.Text.Trim(),new[]{"yyyy.MM.dd","yyyy-MM-dd","yyyy.MM.dd."},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)) {Status.Text="Érvényes dátum szükséges, például 2026.10.12.";return;}
        try {
            var job=ServiceCalendar.CreateJob(catalog,date,staff.Id,customer.Id,machine.Id,ContactInput.Text,PhoneInput.Text,LocationInput.Text,DescriptionInput.Text);
            if(!store.Append(job,out var updated)){Status.Text="A feladat nem menthető; a korábbi naptáradatok megmaradtak.";return;}
            jobs=updated; calendar?.Refresh(jobs);
            Status.Text=$"Mentve: {date:yyyy.MM.dd} · {staff.Name}. A feladat bekerült a naptárba.";
            DescriptionInput.Clear(); DescriptionInput.Focus();
        } catch(ArgumentException ex){Status.Text=ex.Message;}
    }
    public void ApplyTheme(bool light) => calendar?.ApplyTheme(light);
    private void OpenCalendar(object sender,RoutedEventArgs e) {
        if(!storageReady){Status.Text="A mentett naptáradatokat előbb helyre kell állítani.";return;}
        if(calendar!=null){calendar.Activate();return;}
        var date=DateOnly.TryParse(DateInput.Text,CultureInfo.InvariantCulture,out var parsed)?parsed:DateOnly.FromDateTime(DateTime.Today);
        calendar=new ServiceCalendarWindow(jobs,date){Owner=Window.GetWindow(this)};
        calendar.Closed+=(_,_)=>calendar=null;calendar.Show();
    }
}
