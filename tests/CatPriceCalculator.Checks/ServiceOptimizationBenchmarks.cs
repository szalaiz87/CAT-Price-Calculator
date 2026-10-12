using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CatPriceCalculator.Core;
internal static class ServiceOptimizationBenchmarks
{
    public static void Run(string output){
        var folder=Path.Combine(Path.GetTempPath(),"service-benchmark-"+Guid.NewGuid());Directory.CreateDirectory(folder);
        try{
            var customers=Path.Combine(folder,"customers.xlsx");var machines=Path.Combine(folder,"machines.xlsx");
            Write(customers,new[]{"ugyfelazonosito","nev"},Enumerable.Range(0,5000).Select(i=>new[]{$"U{i:D5}",$"Minta Építő Ügyfél {i:D5}"}));
            Write(machines,new[]{"gepazonosito","ugyfelazonosito","megnevezes","gyariszam"},Enumerable.Range(0,10000).Select(i=>new[]{$"G{i:D5}",$"U{i/2:D5}",$"CAT gép {i:D5}",$"DEMO-{i:D5}"}));
            var jobs=Enumerable.Range(0,100000).Select(i=>new ServiceJob(GuidFrom(i),new DateOnly(2019,1,1).AddDays(i%3650),ServiceCalendar.Staff[(i/3650)%5].Id,"U1","Minta ügyfél","G1","Minta gép","DEMO","Kapcsolat","000","Minta hely","Teszt")).ToArray();
            var start=ServiceCalendar.MonthGridStart(2026,10);
            Func<int> import=()=>{var result=ServiceCatalogCsv.Load(customers,machines);if(result.Customers.Length!=5000 || result.Machines.Length!=10000)throw new Exception("Import mismatch");return result.Machines.Length;};
            Func<int> calendar=()=>{var lookup=ServiceMonthIndex.Build(jobs,start);int count=0;for(int d=0;d<42;d++)foreach(var staff in ServiceCalendar.Staff)count+=lookup.GetValueOrDefault((start.AddDays(d),staff.Id),[]).Length;return count;};
            var expected=jobs.Count(j=>j.Date.DayNumber>=start.DayNumber && j.Date.DayNumber-start.DayNumber<42);
            if(calendar()!=expected)throw new Exception("Calendar mismatch");
            var result=new{version=BuildInfo.Current.ToString(),runtime=Environment.Version.ToString(),method="Release, 3 warmups, median of 7 runs; GC allocations on current thread; Linux, warm local files; fixture creation excluded",customerCount=5000,machineCount=10000,jobCount=jobs.Length,visibleJobCount=expected,import=Measure(import),calendar=Measure(calendar)};
            File.WriteAllText(output,JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(File.ReadAllText(output));
        }finally{Directory.Delete(folder,true);}
    }
    private static object Measure(Func<int> action){for(int i=0;i<3;i++)action();var times=new List<double>();var allocations=new List<long>();for(int i=0;i<7;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();var before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();action();watch.Stop();allocations.Add(GC.GetAllocatedBytesForCurrentThread()-before);times.Add(watch.Elapsed.TotalMilliseconds);}times.Sort();allocations.Sort();return new{medianMilliseconds=times[3],medianAllocatedBytes=allocations[3],runs=7};}
    private static Guid GuidFrom(int number){var bytes=new byte[16];BitConverter.GetBytes(number+1).CopyTo(bytes,0);return new Guid(bytes);}
    private static void Write(string path,string[] headers,IEnumerable<string[]> rows){
        using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
        void Entry(string name,string content){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(content);}
        Entry("xl/workbook.xml","<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Adatok\" sheetId=\"1\" r:id=\"r1\"/></sheets></workbook>");
        Entry("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Target=\"worksheets/data.xml\"/></Relationships>");
        using var sheet=new StreamWriter(zip.CreateEntry("xl/worksheets/data.xml").Open());sheet.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");int r=0;
        foreach(var row in rows.Prepend(headers)){r++;sheet.Write($"<row r=\"{r}\">");for(int c=0;c<row.Length;c++)sheet.Write($"<c r=\"{(char)('A'+c)}{r}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(row[c])}</t></is></c>");sheet.Write("</row>");}sheet.Write("</sheetData></worksheet>");
    }
}
