using System.IO.Compression;
using System.Text;
using CatPriceCalculator.Core;
internal static class ServiceCalendarChecks
{
    public static void Run(Action<bool,string> check) {
        var directory=Path.Combine(Path.GetTempPath(),"service-checks-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try {
            var demo=ServiceCatalogCsv.Load(Path.Combine(AppContext.BaseDirectory,"Fixtures","ugyfelek-proba.xlsx"),Path.Combine(AppContext.BaseDirectory,"Fixtures","gepek-proba.xlsx"));
            check(demo.Customers.Length==4 && demo.Machines.Length==8,"Shipped demo Excel databases import successfully");
            check(demo.Customers.All(c=>ServiceCalendar.MachinesFor(demo,c.Id).Count()==2),"Demo database assigns exactly two machines to every customer");
            check(ServiceCalendar.Staff.All(s=>s.DisplayName==s.Name),"Staff display labels contain only the human-readable name");
            var customers=Path.Combine(directory,"customers.csv");var machines=Path.Combine(directory,"machines.csv");
            File.WriteAllText(customers,"ugyfelazonosito;nev\n001;\"Első; ügyfél\"\n002;Második\n",new UTF8Encoding(true));
            File.WriteAllText(machines,"gepazonosito;ugyfelazonosito;megnevezes;gyariszam\nM1;001;CAT 320;000123\nM2;002;CAT 330;xyz\n");
            var catalog=ServiceCatalogCsv.Load(customers,machines);
            check(catalog.Customers.Length==2 && catalog.Customers[0].Name=="Első; ügyfél" && catalog.Machines[0].SerialNumber=="000123","CSV quote/BOM and leading-zero identifiers");
            check(ServiceCalendar.MachinesFor(catalog,"001").Select(m=>m.Id).SequenceEqual(new[]{"M1"}),"Only customer-owned machines are offered");
            var date=new DateOnly(2026,10,12);
            var job=ServiceCalendar.CreateJob(catalog,date,"muhovits","001","M1"," Kapcsolat "," +36 30 1234567 "," Győr "," Első sor\nMásodik sor ");
            check(job.ContactName=="Kapcsolat" && job.Description.Contains('\n') && job.Date==date,"Service job preserves date and multiline description, trims fields");
            void Reject(Action action,string name){try{action();throw new Exception(name+" was accepted");}catch(ArgumentException){check(true,name);}}
            Reject(()=>ServiceCalendar.CreateJob(catalog,date,"muhovits","001","M2","név","tel","hely","hiba"),"Reject a machine belonging to another customer");
            Reject(()=>ServiceCalendar.CreateJob(catalog,date,"unknown","001","M1","név","tel","hely","hiba"),"Reject unknown staff");
            Reject(()=>ServiceCalendar.CreateJob(catalog,date,"muhovits","001","M1","","tel","hely","hiba"),"Reject incomplete service form");
            var store=new ServiceCalendarStore(Path.Combine(directory,"saved"));var second=ServiceCalendar.CreateJob(catalog,date,"muhovits","001","M1","név","tel","hely","másik hiba");
            check(store.SaveCatalog(catalog) && store.SaveJobs(new[]{job,second}),"Persist two tasks for the same worker and date");
            check(new ServiceCalendarStore(store.DirectoryPath).LoadJobs().SequenceEqual(new[]{job,second}),"Service jobs survive restart");
            check(store.SaveCatalog(new(new[]{new ServiceCustomer("001","Új ügyfélnév")},Array.Empty<ServiceMachine>())) && store.LoadJobs()[0].CustomerName=="Első; ügyfél","Reimport preserves booked task snapshots");
            check(!store.SaveJobs(new[]{job,job}) && store.LoadJobs().Length==2,"Reject duplicate task IDs without overwriting existing calendar");
            File.WriteAllText(machines,"gepazonosito;ugyfelazonosito;megnevezes\nM1;NOPE;Gép\n");
            try{ServiceCatalogCsv.Load(customers,machines);throw new Exception("Orphan accepted");}catch(FormatException){check(true,"Reject orphan machine on import");}
            File.WriteAllText(customers,"ugyfelazonosito;nev\n001;A\n001;B\n");
            try{ServiceCatalogCsv.Load(customers,machines);throw new Exception("Duplicate accepted");}catch(FormatException){check(true,"Reject duplicate customer IDs");}
            check(ServiceCalendar.Staff.Count==5 && ServiceCalendar.Staff.Select(s=>s.Id).Distinct().Count()==5,"All four mechanics and the service manager have distinct calendar rows");
            for(int month=1;month<=12;month++){
                var start=ServiceCalendar.MonthGridStart(2026,month);var last=new DateOnly(2026,month,DateTime.DaysInMonth(2026,month));
                check(start.DayOfWeek==DayOfWeek.Monday && start<=new DateOnly(2026,month,1) && start.AddDays(41)>=last,"Monday-first six-week month coverage "+month);
            }
            check(ServiceCalendar.MonthGridStart(2028,2).AddDays(41)>=new DateOnly(2028,2,29),"Calendar covers leap February");
            var cx=Path.Combine(directory,"customers.xlsx");var mx=Path.Combine(directory,"machines.xlsx");
            WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Magyar ügyfél & társai"}});
            WriteExcel(mx,new[]{new[]{"gepazonosito","ugyfelazonosito","megnevezes","gyariszam"},new[]{"M1","001","CAT 320","0000123"}});
            var excel=ServiceCatalogCsv.Load(cx,mx);
            check(excel.Customers[0].Id=="001" && excel.Machines[0].SerialNumber=="0000123" && excel.Customers[0].Name.Contains('&'),"XLSX inline text identifiers and escaped customer names");
            WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Közös szöveg"}},shared:true);
            check(ServiceCatalogCsv.Load(cx,mx).Customers[0].Name=="Közös szöveg","XLSX shared-string exports");
            var gridStart=ServiceCalendar.MonthGridStart(2026,10);
            ServiceJob[] monthJobs=[job with{Date=gridStart.AddDays(-1)},job with{Date=gridStart},second with{Date=gridStart},job with{Date=gridStart.AddDays(41)},job with{Date=gridStart.AddDays(42)}];
            var monthIndex=ServiceMonthIndex.Build(monthJobs,gridStart);
            check(monthIndex.Values.Sum(v=>v.Length)==3,"Month grouping excludes history and dates beyond the displayed 42 days");
            check(monthIndex[(gridStart,job.StaffId)].SequenceEqual(monthJobs.Skip(1).Take(2)),"Month grouping preserves task order and duplicates on the same day");
            WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Minta"}});
            RewriteSheet(cx,s=>s.Replace("<t>Minta</t>","<r><t>Árvíz</t></r><r><t>tűrő ügyfél</t></r>"));
            check(ServiceCatalogCsv.Load(cx,mx).Customers[0].Name=="Árvíztűrő ügyfél","Streaming XLSX preserves rich-text fragments");
            WriteExcel(mx,new[]{new[]{"gepazonosito","ugyfelazonosito","megnevezes","gyariszam"},new[]{"M1","001","CAT",""}});
            check(ServiceCatalogCsv.Load(cx,mx).Machines[0].SerialNumber=="","Streaming XLSX preserves missing optional cells");
            foreach(var invalidReference in new[]{"A0","A+2","A9999999"}){
                WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Minta"}});
                RewriteSheet(cx,s=>s.Replace("r=\"A2\"","r=\""+invalidReference+"\""));
                try{ServiceCatalogCsv.Load(cx,mx);throw new Exception("Invalid cell reference accepted");}catch(FormatException){check(true,"Reject invalid Excel cell coordinate "+invalidReference);}
            }
            WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Minta"}});
            RewriteSheet(cx,s=>s.Replace("r=\"B2\" t=\"inlineStr\"","r=\"B2\" t=\"e\""));
            try{ServiceCatalogCsv.Load(cx,mx);throw new Exception("Excel error accepted");}catch(FormatException){check(true,"Reject Excel error cells instead of importing error text");}
            WriteExcel(cx,new[]{new[]{"ugyfelazonosito","nev"},new[]{"001","Minta"}});
            RewriteSheet(cx,s=>s.Replace("<c r=\"B2\" t=\"inlineStr\"><is>","<c r=\"B2\" t=\"inlineStr\"><f>NOW()</f><is>"));
            try{ServiceCatalogCsv.Load(cx,mx);throw new Exception("Excel formula accepted");}catch(FormatException){check(true,"Streaming XLSX retains formula rejection");}
            var recorderStore=new ServiceCalendarStore(Path.Combine(directory,"recorded"));
            foreach(var recorder in ServiceCalendar.Recorders){
                var recorded=ServiceCalendar.CreateRecordedJob(catalog,date,"szalai","001","M1","név","tel","hely","hiba",recorder);
                check(recorded.RecordedBy==recorder && recorderStore.SaveJobs(new[]{recorded}) && recorderStore.LoadJobs().Single().RecordedBy==recorder,"Recorder persists: "+recorder);
            }
            var custom=ServiceCalendar.CreateRecordedJob(catalog,date,"szalai","001","M1","név","tel","hely","hiba","Új Bejelentkezett Felhasználó");
            check(recorderStore.SaveJobs([custom]) && recorderStore.LoadJobs().Single().RecordedBy=="Új Bejelentkezett Felhasználó","Any signed-in user name persists as recorder");
            Reject(()=>ServiceCalendar.CreateRecordedJob(catalog,date,"szalai","001","M1","név","tel","hely","hiba",""),"Reject empty recorder");
            var legacy=System.Text.Json.JsonSerializer.SerializeToNode(job)!.AsObject();legacy.Remove("RecordedBy");
            File.WriteAllText(Path.Combine(recorderStore.DirectoryPath,"jobs.json"),"["+legacy.ToJsonString()+"]");
            check(recorderStore.LoadJobs().Single().RecordedBy==null,"Legacy job without a recorder remains readable without inventing a name");
            var next=ServiceCalendar.CreateJob(catalog,date,"gulyas","001","M1","név","tel","hely","harmadik");
            check(new ServiceCalendarStore(store.DirectoryPath).Append(next,out var refreshed) && refreshed.Length==3 && refreshed.Contains(job),"Appending from another store preserves existing tasks");
            using(var locked=new FileStream(Path.Combine(store.DirectoryPath,"jobs.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                check(!store.Append(next with{Id=Guid.NewGuid()},out _) && store.LoadJobs().Length==3,"Concurrent calendar file lock refuses overwrite");
            File.WriteAllText(Path.Combine(store.DirectoryPath,"jobs.json"),"not json");
            try{store.LoadJobs();throw new Exception("Corrupt jobs ignored");}catch(FormatException){check(File.ReadAllText(Path.Combine(store.DirectoryPath,"jobs.json"))=="not json","Corrupt calendar remains untouched");}
        } finally{Directory.Delete(directory,true);}
    }
    private static void RewriteSheet(string path,Func<string,string> change) {
        using var zip=ZipFile.Open(path,ZipArchiveMode.Update);var entry=zip.GetEntry("xl/worksheets/data.xml")!;string content;
        using(var reader=new StreamReader(entry.Open()))content=reader.ReadToEnd();entry.Delete();
        using var writer=new StreamWriter(zip.CreateEntry("xl/worksheets/data.xml").Open());writer.Write(change(content));
    }
    internal static void WriteExcel(string path,string[][] rows,bool shared=false) {
        if(File.Exists(path)) File.Delete(path);
        using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
        void Entry(string name,string content){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(content);}
        Entry("xl/workbook.xml","<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Adatok\" sheetId=\"1\" r:id=\"r1\"/></sheets></workbook>");
        Entry("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"r1\" Target=\"worksheets/data.xml\"/></Relationships>");
        var strings=rows.SelectMany(r=>r).ToArray();
        if(shared)Entry("xl/sharedStrings.xml","<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"+string.Concat(strings.Select(s=>"<si><t>"+System.Security.SecurityElement.Escape(s)+"</t></si>"))+"</sst>");
        int index=0;var xml=new StringBuilder("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        for(int r=0;r<rows.Length;r++){xml.Append($"<row r=\"{r+1}\">");for(int c=0;c<rows[r].Length;c++){var value=System.Security.SecurityElement.Escape(rows[r][c]);xml.Append(shared?$"<c r=\"{(char)('A'+c)}{r+1}\" t=\"s\"><v>{index++}</v></c>":$"<c r=\"{(char)('A'+c)}{r+1}\" t=\"inlineStr\"><is><t>{value}</t></is></c>");}xml.Append("</row>");}xml.Append("</sheetData></worksheet>");Entry("xl/worksheets/data.xml",xml.ToString());
    }
}
