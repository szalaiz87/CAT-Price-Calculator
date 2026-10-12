using System.Globalization;
using System.Text;
using ExcelDataReader;
namespace CatPriceCalculator.Core;
internal static class ServiceLegacyExcel
{
    public static List<string[]> Read(string path){
        if(new FileInfo(path).Length>100*1024*1024L)throw new FormatException("Túl nagy XLS-fájl (legfeljebb 100 MB).");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try{
            using var stream=File.OpenRead(path);using var reader=ExcelReaderFactory.CreateBinaryReader(stream);
            if(reader.FieldCount>100)throw new FormatException("Túl sok oszlop az XLS-fájlban.");
            var rows=new List<string[]>();
            while(reader.Read()){
                var row=new string[reader.FieldCount];bool any=false;
                for(int c=0;c<row.Length;c++){
                    var value=reader.GetValue(c);
                    row[c]=value switch{null=>"",string s=>s,double d when double.IsFinite(d)=>d.ToString("G15",CultureInfo.InvariantCulture),_=>Convert.ToString(value,CultureInfo.InvariantCulture)??""};
                    any|=!string.IsNullOrWhiteSpace(row[c]);
                }
                if(!any)continue;rows.Add(row);
                if(rows.Count>100000)throw new FormatException("Legfeljebb 100 000 sor importálható.");
            }
            return rows;
        }catch(ExcelDataReader.Exceptions.ExcelReaderException){throw new FormatException("Az XLS-fájl nem olvasható. Ellenőrizze, hogy valódi, nem jelszavas Excel-exportot választott.");}
    }
}
