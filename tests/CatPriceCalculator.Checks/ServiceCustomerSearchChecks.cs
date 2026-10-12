using CatPriceCalculator.Core;
internal static class ServiceCustomerSearchChecks
{
    public static void Run(Action<bool,string> check){
        ServiceCustomer[] customers=[new("1","Árvíztűrő Tükörfúrógép Kft."),new("2","Minta Építő Zrt."),new("3","Építő Minta Kft.")];
        check(ServiceCustomerSearch.Find(customers,"arvizturo",true).Single().Id=="1","Customer prefix ignores Hungarian accents");
        check(ServiceCustomerSearch.Find(customers,"VIZ",false).Single().Id=="1","Customer substring ignores case and accents inside a word");
        check(ServiceCustomerSearch.Find(customers,"epito",true).Select(c=>c.Id).SequenceEqual(new[]{"3"}),"Prefix search excludes interior matches");
        check(ServiceCustomerSearch.Find(customers,"epito",false).Length==2,"Anywhere search includes both start and interior name matches");
        check(ServiceCustomerSearch.Find(customers,"  tukorfurogep  ",false).Single().Id=="1","Customer search trims spaces and matches accented words");
        var many=Enumerable.Range(0,5000).Select(i=>new ServiceCustomer(i.ToString(),$"Minta ügyfél {i:D4}")).ToArray();
        check(ServiceCustomerSearch.Find(many,"",false).Length==5000,"Empty search retains all 5000 customers");
        check(ServiceCustomerSearch.Find(many,"ugyfel 4999",false).Single().Id=="4999","Find the last customer in a 5000-customer database");
        var now=new DateTimeOffset(2026,10,12,12,0,0,TimeSpan.Zero);
        check(!ServiceCustomerSearch.ImportStale(now.AddDays(-7),now),"Exactly seven days old import is not yet stale");
        check(ServiceCustomerSearch.ImportStale(now.AddDays(-7).AddSeconds(-1),now),"Import older than seven days needs refresh");
        check(ServiceCustomerSearch.ImportStale(null,now),"Legacy unknown import date requires refresh without inventing a timestamp");
        check(ServiceCalendar.Staff[0].Id=="szalai" && ServiceCalendar.Staff[0].Name=="Szalai Zoltán","Service manager is first without changing saved staff IDs");
        var folder=Path.Combine(Path.GetTempPath(),"service-import-"+Guid.NewGuid());
        try{
            var store=new ServiceCalendarStore(folder);var catalog=new ServiceCatalog(customers,[],now,"C:\\export\\ugyfelek.xlsx","C:\\export\\gepek.xlsx");
            check(store.SaveCatalog(catalog) && store.LoadCatalog().ImportedAt==now,"Successful import timestamp survives restart with the catalog");
            var paths=new ServiceImportPaths("C:\\export\\uj-ugyfelek.xlsx",null);
            check(store.SaveImportPaths(paths) && store.LoadImportPaths()==paths,"Partially browsed import file paths survive restart");
            check(store.LoadCatalog().ImportedAt==now,"Browsing paths does not change the last successful import time");
        }finally{Directory.Delete(folder,true);}
    }
}
