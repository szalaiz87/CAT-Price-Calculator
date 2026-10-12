using CatPriceCalculator.Core;
internal static class ServiceErpChecks
{
    public static void Run(Action<bool,string> check){
        var root=Path.Combine(AppContext.BaseDirectory,"Fixtures");
        var catalog=ServiceCatalogCsv.Load(Path.Combine(root,"partners-erp.xls"),Path.Combine(root,"machines-erp.xls"),true);
        check(catalog.Customers.Count(c=>!c.IsPlaceholder)==2 && catalog.Machines.Length==4,"Binary XLS ERP exports preserve every machine");
        var machine=catalog.Machines.Single(m=>m.Id=="0001");
        check(machine.CustomerId=="001" && machine.Name=="Próba gép" && machine.SerialNumber=="DEMO-CHASSIS-1","ERP A/B/J/K mapping preserves leading zeros and joins K to partner A");
        check(machine.DisplayName.Contains("DEMO-CHASSIS-1"),"Machine dropdown includes chassis number");
        var placeholder=catalog.Customers.Single(c=>c.IsPlaceholder);
        check(placeholder.DisplayName=="- hiányzó adat -" && placeholder.DisplayId=="-","Missing partner has a readable label without internal identifier");
        check(ServiceCalendar.MachinesFor(catalog,placeholder.Id).Count()==2,"All unassigned machines belong to the placeholder partner");
        check(catalog.Machines.Count(m=>m.Name=="- hiányzó adat -")==2,"Missing machine names are filled even when the partner is also missing");
        check(ServiceCustomerSearch.Find(catalog.Customers,"hianyzo",false).Any(c=>c.IsPlaceholder),"Placeholder partner is searchable without accents");
        check(catalog.ImportSummary is {MachineRows:4,ImportedMachines:4,MissingPartner:2,MissingName:2},"Import summary reports missing fields without skipping rows");
        var temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try{
            Directory.CreateDirectory(temp);
            var customerPath=Path.Combine(temp,"customers.xlsx");var machinePath=Path.Combine(temp,"machines.xlsx");
            ServiceCalendarChecks.WriteExcel(customerPath,[["kod","nev"],["001","Próba Partner"]]);
            ServiceCalendarChecks.WriteExcel(machinePath,[["gep","megnevezes","napiora","koltseg","amort","evnap","energia","karban","karbanora","gep_azonosito","partner"],["M1","Gép","","","","","","","","CHASSIS","001"]]);
            var converted=ServiceCatalogCsv.Load(customerPath,machinePath,true);
            check(converted.Machines.Single() is {Id:"M1",CustomerId:"001",SerialNumber:"CHASSIS"},"ERP layout also works in XLSX format");
            ServiceCalendarChecks.WriteExcel(machinePath,[["gepazonosito"],["M1"]]);
            try{ServiceCatalogCsv.Load(customerPath,machinePath,true);check(false,"Missing header rejected");}catch(FormatException){check(true,"Missing-data policy still rejects missing required columns");}
            ServiceCalendarChecks.WriteExcel(machinePath,[["gepazonosito","ugyfelazonosito","megnevezes"],["M1","UNKNOWN",""]]);
            try{ServiceCatalogCsv.Load(customerPath,machinePath,true);check(false,"Unknown partner rejected");}catch(FormatException){check(true,"Nonempty unknown partner IDs cannot silently become unassigned");}
            var store=new ServiceCalendarStore(temp);
            check(store.SaveCatalog(catalog) && store.LoadCatalog().Customers.Single(c=>c.IsPlaceholder).Name==placeholder.Name,"Placeholder partner survives local storage and restart");
        }finally{if(Directory.Exists(temp))Directory.Delete(temp,true);}
    }
}
