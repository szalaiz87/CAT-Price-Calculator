using CatPriceCalculator.Core;
internal static class ActualExportChecks
{
    public static void Run(string customers,string machines){
        var catalog=ServiceCatalogCsv.Load(customers,machines,fillMissingMachineData:true);
        var ids=catalog.Customers.Select(c=>c.Id).ToHashSet();
        if(catalog.Customers.Count(c=>!c.IsPlaceholder)!=5144 || catalog.Machines.Length!=6323 || catalog.Machines.Any(m=>!ids.Contains(m.CustomerId)))throw new Exception("Actual export mapping mismatch");
        var placeholder=catalog.Customers.Single(c=>c.IsPlaceholder);
        if(placeholder.Name!="- hiányzó adat -" || ServiceCalendar.MachinesFor(catalog,placeholder.Id).Count()!=27 || catalog.Machines.Count(m=>m.Name=="- hiányzó adat -")!=5)throw new Exception("Actual missing-field handling mismatch");
        if(ServiceCustomerSearch.Find(catalog.Customers,"hianyzo",false).All(c=>c.Id!=placeholder.Id))throw new Exception("Placeholder customer not searchable");
        Console.WriteLine($"ACTUAL EXPORT PASS: {catalog.Customers.Count(c=>!c.IsPlaceholder)} partners, {catalog.Machines.Length} machines, 27 missing partner IDs grouped, 5 missing machine names filled. No customer/machine values logged.");
    }
}
