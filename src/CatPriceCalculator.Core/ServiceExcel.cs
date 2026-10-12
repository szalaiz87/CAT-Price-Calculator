using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
namespace CatPriceCalculator.Core;
// Read-only first-worksheet XLSX import. No Excel installation, macros or external links are executed.
internal static class ServiceExcel
{
    private static readonly XNamespace Sheet="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relation="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static List<string[]> Read(string path) {
        using var zip=ZipFile.OpenRead(path);
        if(zip.Entries.Sum(e=>e.Length)>100*1024*1024L)throw new FormatException("Túl nagy Excel-fájl (legfeljebb 100 MB kibontva).");
        XDocument Document(string name) {
            var entry=zip.GetEntry(name)??throw new FormatException("Hiányzó Excel-munkalap vagy hibás fájl.");
            using var stream=entry.Open();using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=50*1024*1024});return XDocument.Load(reader);
        }
        var strings=zip.GetEntry("xl/sharedStrings.xml") is null?Array.Empty<string>():Document("xl/sharedStrings.xml").Descendants(Sheet+"si").Select(x=>string.Concat(x.Descendants(Sheet+"t").Select(t=>t.Value))).ToArray();
        var first=Document("xl/workbook.xml").Descendants(Sheet+"sheet").FirstOrDefault()??throw new FormatException("Az Excel-fájlban nincs munkalap.");
        var rel=Document("xl/_rels/workbook.xml.rels").Root!.Elements().SingleOrDefault(e=>(string?)e.Attribute("Id")== (string?)first.Attribute(Relation+"id"))??throw new FormatException("Nem található az első munkalap.");
        if((string?)rel.Attribute("TargetMode")=="External")throw new FormatException("Külső munkalap nem importálható.");
        var target=(string?)rel.Attribute("Target")??"";
        var parts=(target.StartsWith('/')?target.TrimStart('/'):"xl/"+target).Split('/');var normalized=new List<string>();
        foreach(var part in parts){if(part=="..") {if(normalized.Count==0)throw new FormatException("Hibás munkalapútvonal.");normalized.RemoveAt(normalized.Count-1);}else if(part!=".")normalized.Add(part);}
        var rows=new List<string[]>();int width=0;
        foreach(var row in Document(string.Join('/',normalized)).Descendants(Sheet+"sheetData").Elements(Sheet+"row")) {
            var values=new SortedDictionary<int,string>();
            foreach(var cell in row.Elements(Sheet+"c")) {
                var reference=(string?)cell.Attribute("r")??throw new FormatException("Hiányzó Excel-cellaazonosító.");int column=0;
                foreach(var ch in reference.TakeWhile(char.IsLetter)){column=column*26+(char.ToUpperInvariant(ch)-'A'+1);}
                if(column<1 || column>100)throw new FormatException("Túl sok oszlop az Excel-fájlban.");
                var type=(string?)cell.Attribute("t");string value;
                if(cell.Element(Sheet+"f")!=null)throw new FormatException("Képletek helyett értékeket tartalmazó Excel-export szükséges.");
                if(type=="inlineStr")value=string.Concat(cell.Descendants(Sheet+"t").Select(t=>t.Value));
                else {value=cell.Element(Sheet+"v")?.Value??"";if(type=="s"){if(!int.TryParse(value,out var index)||index<0||index>=strings.Length)throw new FormatException("Hibás Excel-szöveghivatkozás.");value=strings[index];}}
                if(!string.IsNullOrEmpty(value))values[column-1]=value;
            }
            if(values.Count==0)continue;
            if(rows.Count==0)width=values.Keys.Max()+1;
            if(values.Keys.Max()>=width)throw new FormatException("Adat a fejlécen kívüli Excel-oszlopban.");
            rows.Add(Enumerable.Range(0,width).Select(i=>values.GetValueOrDefault(i,"")).ToArray());
            if(rows.Count>100000)throw new FormatException("Legfeljebb 100 000 sor importálható.");
        }
        return rows;
    }
}
