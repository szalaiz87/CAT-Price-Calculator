using System.Text;
using System.Text.Json;
namespace CatPriceCalculator.Core;

public sealed record ServiceCustomer(string Id, string Name) { public override string ToString() => Name + " · " + Id; }
public sealed record ServiceMachine(string Id, string CustomerId, string Name, string SerialNumber) { public override string ToString() => Name + (string.IsNullOrWhiteSpace(SerialNumber) ? "" : " · " + SerialNumber); }
public sealed record ServiceCatalog(ServiceCustomer[] Customers, ServiceMachine[] Machines);
public sealed record ServiceStaff(string Id, string Name, string Color);
public sealed record ServiceJob(Guid Id, DateOnly Date, string StaffId, string CustomerId, string CustomerName, string MachineId, string MachineName, string SerialNumber, string ContactName, string Phone, string Location, string Description);
public static class ServiceCalendar
{
    public static IReadOnlyList<ServiceStaff> Staff { get; } = Array.AsReadOnly(new[] {
        new ServiceStaff("barabas", "Barabás János", "#9CE7E8"),
        new ServiceStaff("gulyas", "Gulyás Dániel", "#FFB9B4"),
        new ServiceStaff("veges", "Véges Roland", "#9BD9D5"),
        new ServiceStaff("muhovits", "Muhovits József", "#D9B8F4"),
        new ServiceStaff("szalai", "Szalai Zoltán", "#AFE0B7") });
    public static DateOnly MonthGridStart(int year, int month) {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
    }
    public static IEnumerable<ServiceMachine> MachinesFor(ServiceCatalog catalog, string customerId) => catalog.Machines.Where(m => m.CustomerId == customerId);
    public static bool Valid(ServiceJob job) => job is not null && job.Id != Guid.Empty && Staff.Any(s => s.Id == job.StaffId)
        && new[] { job.CustomerId, job.CustomerName, job.MachineId, job.MachineName, job.ContactName, job.Phone, job.Location, job.Description }.All(s => !string.IsNullOrWhiteSpace(s));
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
    public static ServiceCatalog Load(string customerFile, string machineFile) {
        var customers = Read(customerFile, new[] { "ugyfelazonosito", "nev" }).Select(row => new ServiceCustomer(row["ugyfelazonosito"], row["nev"])).ToArray();
        var machines = Read(machineFile, new[] { "gepazonosito", "ugyfelazonosito", "megnevezes" }).Select(row => new ServiceMachine(row["gepazonosito"], row["ugyfelazonosito"], row["megnevezes"], row.GetValueOrDefault("gyariszam", ""))).ToArray();
        if (customers.Length == 0) throw new FormatException("Az ügyfélfájl nem tartalmaz ügyfeleket.");
        if (customers.Select(c => c.Id).Distinct().Count() != customers.Length || machines.Select(m => m.Id).Distinct().Count() != machines.Length) throw new FormatException("Ismétlődő ügyfél- vagy gépazonosító található.");
        var ids = customers.Select(c => c.Id).ToHashSet();
        if (machines.Any(m => !ids.Contains(m.CustomerId))) throw new FormatException("Van olyan gép, amelynek ügyfélazonosítója nem található az ügyfélfájlban.");
        return new(customers.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), machines.OrderBy(m => m.Name, StringComparer.CurrentCultureIgnoreCase).ToArray());
    }
    private static IEnumerable<Dictionary<string,string>> Read(string path, string[] required) {
        if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) {
            foreach (var row in ValidateRows(ServiceExcel.Read(path), required)) yield return row;
            yield break;
        }
        var text = File.ReadAllText(path, new UTF8Encoding(false, true)).TrimStart('\uFEFF');
        var first = text.Split('\n', 2)[0];
        var rows = Parse(text, first.Contains(';') ? ';' : ',');
        foreach (var row in ValidateRows(rows, required)) yield return row;
    }
    private static IEnumerable<Dictionary<string,string>> ValidateRows(List<string[]> rows, string[] required) {
        if (rows.Count == 0) throw new FormatException("Üres CSV-fájl.");
        var headers = rows[0].Select(s => NormalizeHeader(s)).ToArray();
        if (headers.Distinct().Count() != headers.Length || required.Any(h => !headers.Contains(h))) throw new FormatException("Hiányzó vagy ismétlődő oszlop. Szükséges: " + string.Join(", ", required));
        foreach (var row in rows.Skip(1)) {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (row.Length != headers.Length) throw new FormatException("Eltérő oszlopszám a CSV-fájlban.");
            var values = headers.Select((h,i) => (h, value:row[i].Trim())).ToDictionary(x => x.h, x => x.value);
            if (required.Any(h => string.IsNullOrWhiteSpace(values[h]))) throw new FormatException("Üres kötelező mező a CSV-fájlban.");
            yield return values;
        }
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
    public bool SaveCatalog(ServiceCatalog catalog) => LocalJsonFile.Write(Path.Combine(directory,"catalog.json"),catalog);
    public bool SaveJobs(ServiceJob[] jobs) => jobs.All(ServiceCalendar.Valid) && jobs.Select(j=>j.Id).Distinct().Count()==jobs.Length && LocalJsonFile.Write(Path.Combine(directory,"jobs.json"),jobs);
}
