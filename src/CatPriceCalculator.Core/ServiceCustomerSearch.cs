using System.Globalization;
using System.Text;
namespace CatPriceCalculator.Core;
public static class ServiceCustomerSearch
{
    // Hungarian collation may retain ő/ű distinctions even with IgnoreNonSpace.
    // Explicit Unicode decomposition guarantees the requested accent-insensitive behavior.
    internal static string Normalize(string text)=>new string(text.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray());
    public static ServiceCustomer[] Find(IEnumerable<ServiceCustomer> customers,string query,bool startsWith)=>new ServiceCustomerSearchIndex(customers).Find(query,startsWith);
    public static bool ImportStale(DateTimeOffset? imported,DateTimeOffset now)=>imported==null || now-imported.Value>TimeSpan.FromDays(7);
}
public sealed class ServiceCustomerSearchIndex
{
    private readonly (ServiceCustomer Customer,string Name)[] entries;
    public ServiceCustomerSearchIndex(IEnumerable<ServiceCustomer> customers)=>entries=customers.Select(c=>(c,ServiceCustomerSearch.Normalize(c.Name))).ToArray();
    public ServiceCustomer[] Find(string query,bool startsWith){
        var text=ServiceCustomerSearch.Normalize(query.Trim());
        return entries.Where(e=>text.Length==0 || (startsWith?e.Name.StartsWith(text,StringComparison.OrdinalIgnoreCase):e.Name.Contains(text,StringComparison.OrdinalIgnoreCase))).Select(e=>e.Customer).ToArray();
    }
}
public sealed record ServiceImportPaths(string? CustomerFile=null,string? MachineFile=null);
