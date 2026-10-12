using System.Text;
using System.Text.Json;
namespace CatPriceCalculator.Core;

public sealed record ServiceCustomer(string Id, string Name, bool IsPlaceholder=false) { [System.Text.Json.Serialization.JsonIgnore] public string DisplayName => ToString(); [System.Text.Json.Serialization.JsonIgnore] public string DisplayId => IsPlaceholder?"-":Id; public override string ToString() => IsPlaceholder?Name:Name + " · " + Id; }
public sealed record ServiceMachine(string Id, string CustomerId, string Name, string SerialNumber) { [System.Text.Json.Serialization.JsonIgnore] public string DisplayName => ToString(); public override string ToString() => Name + (string.IsNullOrWhiteSpace(SerialNumber) ? "" : " · " + SerialNumber); }
public sealed record ServiceCatalog(ServiceCustomer[] Customers, ServiceMachine[] Machines, DateTimeOffset? ImportedAt=null, string? CustomerFile=null, string? MachineFile=null, ServiceImportSummary? ImportSummary=null);
public sealed record ServiceImportSummary(int MachineRows,int ImportedMachines,int MissingPartner,int MissingName);
public sealed record ServiceStaff(string Id, string Name, string Color) { public string DisplayName => Name; }
public sealed record ServiceJob(Guid Id, DateOnly Date, string StaffId, string CustomerId, string CustomerName, string MachineId, string MachineName, string SerialNumber, string ContactName, string Phone, string Location, string Description, string? RecordedBy=null);
public static class ServiceCalendar
{
    public static IReadOnlyList<string> Recorders { get; } = Array.AsReadOnly(new[]{"Szalai Zoltán","Hották Enikő","Sebesi Sándor","Helembai Gergely"});
    public static IReadOnlyList<ServiceStaff> Staff { get; } = Array.AsReadOnly(new[] {
        new ServiceStaff("szalai", "Szalai Zoltán", "#D5A64C"),
        new ServiceStaff("barabas", "Barabás János", "#9CE7E8"),
        new ServiceStaff("gulyas", "Gulyás Dániel", "#FFB9B4"),
        new ServiceStaff("veges", "Véges Roland", "#9BD9D5"),
        new ServiceStaff("muhovits", "Muhovits József", "#D9B8F4") });
    public static DateOnly MonthGridStart(int year, int month) {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
    }
    public static IEnumerable<ServiceMachine> MachinesFor(ServiceCatalog catalog, string customerId) => catalog.Machines.Where(m => m.CustomerId == customerId);
    public static bool Valid(ServiceJob job) => job is not null && job.Id != Guid.Empty && Staff.Any(s => s.Id == job.StaffId)
        && (job.RecordedBy==null || ValidRecorder(job.RecordedBy))
        && new[] { job.CustomerId, job.CustomerName, job.MachineId, job.MachineName, job.ContactName, job.Phone, job.Location, job.Description }.All(s => !string.IsNullOrWhiteSpace(s));
    private static bool ValidRecorder(string name)=>!string.IsNullOrWhiteSpace(name) && name.Length<=120 && !name.Any(char.IsControl);
    public static ServiceJob CreateRecordedJob(ServiceCatalog catalog, DateOnly date, string staffId, string customerId, string machineId, string contact, string phone, string location, string description, string recordedBy) {
        if(!ValidRecorder(recordedBy)) throw new ArgumentException("Érvényes bejelentkezett rögzítőnév szükséges.");
        return CreateJob(catalog,date,staffId,customerId,machineId,contact,phone,location,description) with {RecordedBy=recordedBy};
    }
    public static ServiceJob CreateJob(ServiceCatalog catalog, DateOnly date, string staffId, string customerId, string machineId, string contact, string phone, string location, string description) {
        var customer = catalog.Customers.SingleOrDefault(c => c.Id == customerId) ?? throw new ArgumentException("Válasszon ügyfelet.");
        var machine = MachinesFor(catalog, customerId).SingleOrDefault(m => m.Id == machineId) ?? throw new ArgumentException("A gép nem a kiválasztott ügyfélhez tartozik.");
        var job = new ServiceJob(Guid.NewGuid(), date, staffId, customer.Id, customer.Name, machine.Id, machine.Name, machine.SerialNumber, contact.Trim(), phone.Trim(), location.Trim(), description.Trim());
        if (!Valid(job)) throw new ArgumentException("Töltse ki az összes kötelező mezőt, és válasszon munkatársat.");
        return job;
    }
}
public static class ServiceCatalogCsv
{
    public static ServiceCatalog Load(string customerFile, string machineFile, bool fillMissingMachineData=false) {
        var customers = Read(customerFile, new[] { "ugyfelazonosito", "nev" }).Select(row => new ServiceCustomer(row["ugyfelazonosito"], row["nev"])).ToArray();
        var machines = Read(machineFile, new[] { "gepazonosito", "ugyfelazonosito", "megnevezes" }, fillMissingMachineData).Select(row => new ServiceMachine(row["gepazonosito"], row["ugyfelazonosito"], row["megnevezes"], row.GetValueOrDefault("gyariszam", ""))).ToArray();
        if (customers.Length == 0) throw new FormatException("Az ügyfélfájl nem tartalmaz ügyfeleket.");
        if (customers.Select(c => c.Id).Distinct().Count() != customers.Length || machines.Select(m => m.Id).Distinct().Count() != machines.Length) throw new FormatException("Ismétlődő ügyfél- vagy gépazonosító található.");
        var ids = customers.Select(c => c.Id).ToHashSet();
        if (machines.Any(m => !string.IsNullOrWhiteSpace(m.CustomerId) && !ids.Contains(m.CustomerId))) throw new FormatException("Van olyan gép, amelynek partnerazonosítója nem található a partnertörzsben. Ellenőrizze, hogy a megfelelő két exportot választotta.");
        var missingPartner=machines.Count(m=>string.IsNullOrWhiteSpace(m.CustomerId));
        var missingName=machines.Count(m=>string.IsNullOrWhiteSpace(m.Name));
        if(!fillMissingMachineData && (missingPartner>0 || missingName>0))throw new FormatException("Hiányos gépsor található az exportban.");
        var placeholderId="__SERVICE_UNASSIGNED__";
        if(missingPartner>0){
            while(ids.Contains(placeholderId))placeholderId+="_";
            customers=customers.Append(new ServiceCustomer(placeholderId,"- hiányzó adat -",true)).ToArray();
        }
        var valid=machines.Select(m=>m with{CustomerId=string.IsNullOrWhiteSpace(m.CustomerId)?placeholderId:m.CustomerId,Name=string.IsNullOrWhiteSpace(m.Name)?"- hiányzó adat -":m.Name}).ToArray();
        var summary=new ServiceImportSummary(machines.Length,valid.Length,missingPartner,missingName);
        return new(customers.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), valid.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(),ImportSummary:summary);
    }
    private static List<string[]> ProjectExport(List<string[]> rows,bool machines){
        if(rows.Count==0)return rows;
        var header=rows[0].Select(NormalizeHeader).ToArray();
        int[] selected;string[] names;
        if(!machines && header.Length>=2 && header[0]=="kod" && header[1]=="nev") {selected=[0,1];names=["ugyfelazonosito","nev"];}
        else if(machines && header.Length>=11 && header[0]=="gep" && header[1]=="megnevezes" && header[9]=="gepazonosito" && header[10]=="partner") {selected=[0,1,9,10];names=["gepazonosito","megnevezes","gyariszam","ugyfelazonosito"];}
        else return rows;
        var projected=new List<string[]>(rows.Count){names};
        foreach(var row in rows.Skip(1)){if(row.Length<=selected.Max())throw new FormatException("Hiányzó oszlop az ügyviteli exportban.");projected.Add(selected.Select(i=>row[i]).ToArray());}
        return projected;
    }
    private static IEnumerable<ImportRow> Read(string path, string[] required, bool allowMissingMachineData=false) {
        var extension=Path.GetExtension(path);
        if(extension.Equals(".xlsx",StringComparison.OrdinalIgnoreCase) || extension.Equals(".xls",StringComparison.OrdinalIgnoreCase)){
            var excelRows=extension.Equals(".xls",StringComparison.OrdinalIgnoreCase)?ServiceLegacyExcel.Read(path):ServiceExcel.Read(path);
            foreach(var row in ValidateRows(ProjectExport(excelRows,required.Contains("gepazonosito")),required,allowMissingMachineData))yield return row;
            yield break;
        }
        var text = File.ReadAllText(path, new UTF8Encoding(false, true)).TrimStart('\uFEFF');
        var first = text.Split('\n', 2)[0];
        var rows = Parse(text, first.Contains(';') ? ';' : ',');
        foreach (var row in ValidateRows(rows, required,allowMissingMachineData)) yield return row;
    }
    private static IEnumerable<ImportRow> ValidateRows(List<string[]> rows, string[] required, bool allowMissingMachineData=false) {
        if (rows.Count == 0) throw new FormatException("Üres CSV-fájl.");
        var headers = rows[0].Select(s => NormalizeHeader(s)).ToArray();
        if (headers.Distinct().Count() != headers.Length || required.Any(h => !headers.Contains(h))) throw new FormatException("Hiányzó vagy ismétlődő oszlop. Szükséges: " + string.Join(", ", required));
        var columns=headers.Select((name,index)=>(name,index)).ToDictionary(x=>x.name,x=>x.index);
        foreach (var row in rows.Skip(1)) {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Length != headers.Length) throw new FormatException("Eltérő oszlopszám a CSV-fájlban.");
            for(int i=0;i<row.Length;i++)row[i]=row[i].Trim();
            foreach(var name in required)if((!allowMissingMachineData || name=="gepazonosito") && string.IsNullOrWhiteSpace(row[columns[name]]))throw new FormatException("Üres kötelező mező a CSV-fájlban.");
            yield return new ImportRow(row,columns);
        }
    }
    // One shared header map per file instead of a dictionary and LINQ pipeline per row.
    private readonly record struct ImportRow(string[] Values,Dictionary<string,int> Columns) {
        public string this[string name]=>Values[Columns[name]];
        public string GetValueOrDefault(string name,string fallback)=>Columns.TryGetValue(name,out var index)?Values[index]:fallback;
    }
    private static string NormalizeHeader(string header) {
        var stripped=new string(header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c)).ToArray());
        return stripped switch {
            "ugyfelkod" or "partnerkod" or "partnerazonosito" => "ugyfelazonosito",
            "ugyfelneve" or "ugyfelnev" or "partnernev" => "nev",
            "gepkod" => "gepazonosito",
            "gepmegnevezese" or "gepmegnevezes" or "gepnev" => "megnevezes",
            "sorozatszam" => "gyariszam",
            _ => stripped
        };
    }
    // Quoted separators, escaped quotes and multiline fields are preserved.
    private static List<string[]> Parse(string text, char delimiter) {
        var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false, endedQuote = false;
        void Field() { row.Add(field.ToString()); field.Clear(); endedQuote = false; }
        void Row() { Field(); rows.Add(row.ToArray()); row.Clear(); }
        for (int i=0; i<text.Length; i++) {
            var c = text[i];
            if (quoted) { if (c=='"') { if (i+1<text.Length && text[i+1]=='"') { field.Append('"'); i++; } else { quoted=false; endedQuote=true; } } else field.Append(c); }
            else if (c==delimiter) Field();
            else if (c=='\r' || c=='\n') { if(c=='\r' && i+1<text.Length && text[i+1]=='\n') i++; Row(); }
            else if (c=='"' && field.Length==0 && !endedQuote) quoted=true;
            else if (endedQuote || c=='"') throw new FormatException("Hibás idézőjelezés a CSV-fájlban.");
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Lezáratlan idézőjel a CSV-fájlban.");
        if (field.Length>0 || row.Count>0 || endedQuote) Row();
        return rows;
    }
}
public sealed class ServiceCalendarStore(string directory)
{
    public string DirectoryPath => directory;
    public ServiceCatalog LoadCatalog() {
        var catalog=Read<ServiceCatalog>("catalog.json") ?? new([], []);
        if(catalog.Customers==null || catalog.Machines==null || catalog.Customers.Any(c=>c==null || string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)) || catalog.Machines.Any(m=>m==null || string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.CustomerId) || string.IsNullOrWhiteSpace(m.Name))) throw new FormatException("Hibás ügyfél- vagy géptörzs; az eredeti fájl megmaradt.");
        var ids=catalog.Customers.Select(c=>c.Id).ToHashSet();
        if(ids.Count!=catalog.Customers.Length || catalog.Machines.Select(m=>m.Id).Distinct().Count()!=catalog.Machines.Length || catalog.Machines.Any(m=>!ids.Contains(m.CustomerId))) throw new FormatException("Hibás ügyfél- vagy géptörzs; az eredeti fájl megmaradt.");
        return catalog;
    }
    public ServiceJob[] LoadJobs() {
        var jobs = Read<ServiceJob[]>("jobs.json") ?? [];
        if (jobs.Any(j => !ServiceCalendar.Valid(j)) || jobs.Select(j => j.Id).Distinct().Count()!=jobs.Length) throw new FormatException("Hibás naptáradatok; az eredeti fájl megmaradt.");
        return jobs;
    }
    private T? Read<T>(string name) {
        var path=Path.Combine(directory,name);
        if (!File.Exists(path)) return default;
        try { return LocalJsonFile.Read<T>(path) ?? throw new FormatException("Üres adatfájl; az eredeti megmaradt."); }
        catch (JsonException) { throw new FormatException("A mentett adatok nem olvashatók; az eredeti fájl megmaradt."); }
    }
    public bool Append(ServiceJob job, out ServiceJob[] updated) {
        updated=[];
        if(!ServiceCalendar.Valid(job))return false;
        try {
            Directory.CreateDirectory(directory);
            using var fileLock=new FileStream(Path.Combine(directory,"jobs.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            updated=LoadJobs().Append(job).ToArray();
            return SaveJobs(updated);
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException) {updated=[];return false;}
    }
    public ServiceImportPaths LoadImportPaths() => Read<ServiceImportPaths>("import-paths.json") ?? new();
    public bool SaveImportPaths(ServiceImportPaths paths) => LocalJsonFile.Write(Path.Combine(directory,"import-paths.json"),paths);
    public bool SaveCatalog(ServiceCatalog catalog) => LocalJsonFile.Write(Path.Combine(directory,"catalog.json"),catalog);
    public bool SaveJobs(ServiceJob[] jobs) => jobs.All(ServiceCalendar.Valid) && jobs.Select(j=>j.Id).Distinct().Count()==jobs.Length && LocalJsonFile.Write(Path.Combine(directory,"jobs.json"),jobs);
}
