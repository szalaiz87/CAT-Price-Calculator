namespace CatPriceCalculator.Core;
public static class ServiceMonthIndex
{
    public static Dictionary<(DateOnly Date,string StaffId),ServiceJob[]> Build(IEnumerable<ServiceJob> jobs,DateOnly start)=>jobs.Where(j=>j.Date.DayNumber>=start.DayNumber && j.Date.DayNumber-start.DayNumber<42).GroupBy(j=>(j.Date,j.StaffId)).ToDictionary(g=>g.Key,g=>g.ToArray());
}
