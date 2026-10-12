using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class ServiceCalendarView : UserControl
{
    private readonly ServiceCalendarStore store=new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","ServiceCalendar"));
    private readonly string importDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"LinserHungary","Szerviznaptar","Import");
    private readonly DispatcherTimer importClock=new(){Interval=TimeSpan.FromMinutes(1)};
    private ServiceCatalog catalog=new([],[]);
    private ServiceJob[] jobs=[];
    private ServiceCustomer? customer;
    private ServiceCalendarWindow? calendar;
    private CustomerSearchWindow? search;
    private bool storageReady,blinking;
    private string recorderName="";
    public void SetRecorder(string name){recorderName=name;RecorderInput.Text=name;}
    public event Action? SummaryChanged;
    public string SummaryText{
        get{
            if(!storageReady)return "A szervizadatok nem olvashatók.\nRögzítés letiltva.";
            var today=DateOnly.ParseExact(RateTimestamp.Format(DateTimeOffset.UtcNow)[..10],"yyyy.MM.dd",CultureInfo.InvariantCulture);
            var next=jobs.Where(j=>j.Date>=today).MinBy(j=>j.Date);
            var import=catalog.ImportedAt==null?"Még nincs import":RateTimestamp.Format(catalog.ImportedAt).Replace(" (magyar idő)","");
            return $"Mai feladatok: {jobs.Count(j=>j.Date==today)}\nKövetkező 7 nap: {jobs.Count(j=>j.Date>=today && j.Date<today.AddDays(7))} munka\n\nKövetkező munka:\n"+(next==null?"Nincs tervezett feladat":$"{next.Date:yyyy.MM.dd}\n{ServiceCalendar.Staff.First(s=>s.Id==next.StaffId).Name}")+$"\n\nÜgyfelek: {catalog.Customers.Count(c=>!c.IsPlaceholder)}\nGépek: {catalog.Machines.Length}\n\nUtolsó import:\n{import}";
        }
    }
    public event Action? EntryRequested;
    public void PrepareEntry(DateOnly date,string staffId){DateInput.Text=date.ToString("yyyy.MM.dd",CultureInfo.InvariantCulture);StaffInput.SelectedItem=ServiceCalendar.Staff.First(s=>s.Id==staffId);EntryRequested?.Invoke();SearchButton.Focus();}
    public ServiceCalendarView(){
        InitializeComponent();StaffInput.ItemsSource=ServiceCalendar.Staff;StaffInput.SelectedIndex=-1;
        DateInput.Text=DateTime.Today.ToString("yyyy.MM.dd",CultureInfo.InvariantCulture);
        try{catalog=store.LoadCatalog();jobs=store.LoadJobs();storageReady=true;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException){Status.Text=e is FormatException?e.Message:"A szervizadatok nem olvashatók. Mentés letiltva az eredeti adatok védelmében.";}
        SaveButton.IsEnabled=storageReady;MachineInput.IsEnabled=false;
        importClock.Tick+=(_,_)=>UpdateImportStatus();
        IsVisibleChanged+=(_,_)=>{if(IsVisible){UpdateImportStatus();importClock.Start();}else{importClock.Stop();LastImportStatus.BeginAnimation(OpacityProperty,null);LastImportStatus.Opacity=1;blinking=false;}};
        Unloaded+=(_,_)=>importClock.Stop();UpdateImportStatus();
    }
    private void UpdateImportStatus(){
        SummaryChanged?.Invoke();
        var stale=ServiceCustomerSearch.ImportStale(catalog.ImportedAt,DateTimeOffset.UtcNow);
        LastImportStatus.Text=catalog.ImportedAt==null?"Még nincs rögzített importidő. Frissítés szükséges.":"Utolsó import: "+RateTimestamp.Format(catalog.ImportedAt)+(stale?" · Frissítés szükséges.":"");
        LastImportStatus.SetResourceReference(TextBlock.ForegroundProperty,stale?"ManagerText":"TextMuted");
        if(stale && IsVisible && !blinking){LastImportStatus.BeginAnimation(OpacityProperty,new DoubleAnimation(1,.35,TimeSpan.FromSeconds(.8)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever});blinking=true;}
        else if(!stale && blinking){LastImportStatus.BeginAnimation(OpacityProperty,null);LastImportStatus.Opacity=1;blinking=false;}
    }
    private void StaffChanged(object sender,SelectionChangedEventArgs e)=>StaffInput.SetResourceReference(ForegroundProperty,StaffInput.SelectedItem is ServiceStaff s && s.Id=="szalai"?"ManagerText":"TextPrimary");
    private void SelectCustomer(ServiceCustomer selected){
        // A search opened before reimport must not select an obsolete customer snapshot.
        customer=catalog.Customers.FirstOrDefault(c=>c.Id==selected.Id);
        CustomerDisplay.Text=customer?.DisplayName??"";CustomerDisplay.ToolTip=CustomerDisplay.Text;
        MachineInput.SelectedItem=null;MachineInput.ItemsSource=customer==null?Array.Empty<ServiceMachine>():ServiceCalendar.MachinesFor(catalog,customer.Id).ToArray();MachineInput.IsEnabled=customer!=null;
    }
    private void SearchCustomers(object sender,RoutedEventArgs e){
        if(catalog.Customers.Length==0){Status.Text="Előbb importálja az ügyfeleket a Beállítások / Szerviz lapon.";return;}
        if(search!=null){search.Activate();return;}
        search=new CustomerSearchWindow(catalog.Customers,SelectCustomer){Topmost=false};search.Closed+=(_,_)=>search=null;search.Show();
    }
    public async Task<string> ImportCatalogAsync(string customers,string machines){
        SaveButton.IsEnabled=false;
        try{
            var imported=await Task.Run(()=>ServiceCatalogCsv.Load(customers,machines,fillMissingMachineData:true));
            imported=imported with{ImportedAt=DateTimeOffset.UtcNow,CustomerFile=customers,MachineFile=machines};
            if(!store.SaveCatalog(imported))return "Az adatbázis nem menthető. A korábbi törzs és importidő megmaradt.";
            catalog=imported;search?.Close();ResetForm();UpdateImportStatus();
            var summary=catalog.ImportSummary;
            var warning=summary!=null && (summary.MissingPartner>0 || summary.MissingName>0)?$"\nHiányzó partnerazonosító: {summary.MissingPartner} gép a „- hiányzó adat -” gyűjtőpartnerhez rendelve. Hiányzó gépnév: {summary.MissingName}, kitöltve „- hiányzó adat -” szöveggel.":"";
            return $"Beolvasva: {catalog.Customers.Count(c=>!c.IsPlaceholder)} partner · {catalog.Machines.Length} gép. {RateTimestamp.Format(catalog.ImportedAt)}"+warning;
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException or DecoderFallbackException or System.Xml.XmlException or InvalidOperationException){return e is FormatException?e.Message:"Nem sikerült beolvasni a két Excel-fájlt. A korábbi adatbázis és importidő megmaradt.";}
        finally{SaveButton.IsEnabled=storageReady;}
    }
    public void OpenImportFolder(){
        try{
            Directory.CreateDirectory(importDirectory);
            foreach(var name in new[]{"ugyfelek-minta.xlsx","gepek-minta.xlsx","ugyfelek-proba.xlsx","gepek-proba.xlsx"}){
                var destination=Path.Combine(importDirectory,name);
                if(!File.Exists(destination)){using var input=Application.GetResourceStream(new Uri("/CAT-Price-Calculator;component/Assets/ServiceCalendar/"+name,UriKind.Relative)).Stream;using var output=File.Create(destination);input.CopyTo(output);}
            }
            var instructions=Path.Combine(importDirectory,"IMPORT-UTMUTATO.txt");
            if(!File.Exists(instructions))File.WriteAllText(instructions,"Excel (.xls / .xlsx), első munkalap, első nem üres sor a fejléc.\r\nÜgyviteli partnerexport: A = azonosító (kod), B = név (nev).\r\nÜgyviteli gépexport: A = gépazonosító, B = megnevezés, J = alvázszám, K = partnerazonosító.\r\nHiányzó gépnév: - hiányzó adat -. Partnerazonosító nélküli gépek: kereshető - hiányzó adat - gyűjtőpartner.\r\nMintafájl ügyfélfejléc: ugyfelazonosito;nev\r\nGépfejléc: gepazonosito;ugyfelazonosito;megnevezes;gyariszam\r\nA gyariszam opcionális. Az azonosítókat szövegként exportálja.\r\nA Beállítások / Szerviz lapon tallózza be mindkét fájlt, majd indítsa a beolvasást.\r\n",new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo(importDirectory){UseShellExecute=true});
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){MessageBox.Show("Az importmappa nem nyitható meg: "+importDirectory,"Szerviznaptár");}
    }
    private void ResetForm(){
        customer=null;CustomerDisplay.Clear();MachineInput.SelectedItem=null;MachineInput.ItemsSource=Array.Empty<ServiceMachine>();MachineInput.IsEnabled=false;StaffInput.SelectedIndex=-1;
        RecorderInput.Text=recorderName;DateInput.Clear();ContactInput.Clear();PhoneInput.Clear();LocationInput.Clear();DescriptionInput.Clear();
    }
    private void SaveJob(object sender,RoutedEventArgs e){
        if(!storageReady)return;
        if(customer==null || MachineInput.SelectedItem is not ServiceMachine machine || StaffInput.SelectedItem is not ServiceStaff staff){Status.Text="Válasszon ügyfelet, hozzá tartozó gépet és munkatársat.";return;}
        if(!DateOnly.TryParseExact(DateInput.Text.Trim(),new[]{"yyyy.MM.dd","yyyy-MM-dd","yyyy.MM.dd."},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)){Status.Text="Érvényes dátum szükséges, például 2026.10.12.";return;}
        try{
            if(string.IsNullOrWhiteSpace(recorderName)){Status.Text="A rögzítéshez bejelentkezett felhasználó szükséges.";return;}
            var job=ServiceCalendar.CreateRecordedJob(catalog,date,staff.Id,customer.Id,machine.Id,ContactInput.Text,PhoneInput.Text,LocationInput.Text,DescriptionInput.Text,recorderName);
            if(!store.Append(job,out var updated)){Status.Text="A feladat nem menthető; a korábbi naptáradatok és a kitöltött mezők megmaradtak.";return;}
            jobs=updated;calendar?.Refresh(jobs);ResetForm();SummaryChanged?.Invoke();
            Status.Text=$"A feladat bekerült a naptárba: {date:yyyy.MM.dd} · {staff.Name}. Új feladat rögzíthető.";SearchButton.Focus();
        }catch(ArgumentException ex){Status.Text=ex.Message;}
    }
    public void ApplyTheme(bool light){calendar?.ApplyTheme(light);search?.ApplyTheme(light);}
    public void ClosePopups(){importClock.Stop();search?.Close();calendar?.Close();}
    private void OpenCalendar(object sender,RoutedEventArgs e){
        if(!storageReady){Status.Text="A mentett naptáradatokat előbb helyre kell állítani.";return;}
        if(calendar!=null){calendar.Activate();return;}
        var date=DateOnly.TryParseExact(DateInput.Text.Trim(),new[]{"yyyy.MM.dd","yyyy-MM-dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed:DateOnly.FromDateTime(DateTime.Today);
        calendar=new ServiceCalendarWindow(jobs,date,PrepareEntry){Topmost=false};calendar.Closed+=(_,_)=>calendar=null;calendar.Show();
    }
}
