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
        var entry=zip.GetEntry(string.Join('/',normalized))??throw new FormatException("Hiányzó munkalap.");
        using var stream=entry.Open();
        using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=50*1024*1024});
        var rows=new List<string[]>();int width=0,maxUsed=-1,sheetDepth=-1,rowDepth=-1;string[]? values=null;
        while(reader.Read()) {
            if(reader.NamespaceURI!=Sheet.NamespaceName)continue;
            if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="sheetData"){sheetDepth=reader.Depth;continue;}
            if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="row" && reader.Depth==sheetDepth+1 && sheetDepth>=0){
                values=reader.IsEmptyElement?null:new string[width==0?100:width];maxUsed=-1;rowDepth=reader.Depth;continue;
            }
            if(reader.NodeType==XmlNodeType.Element && reader.LocalName=="c" && values!=null && reader.Depth==rowDepth+1){
                var reference=reader.GetAttribute("r")??throw new FormatException("Hiányzó Excel-cellaazonosító.");
                int column=0,position=0;
                while(position<reference.Length && char.IsAsciiLetter(reference[position])){
                    column=column*26+(char.ToUpperInvariant(reference[position++])-'A'+1);
                    if(column>100)throw new FormatException("Túl sok oszlop az Excel-fájlban.");
                }
                if(column<1 || position==reference.Length || !int.TryParse(reference.AsSpan(position),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var rowNumber) || rowNumber<1 || rowNumber>1048576)throw new FormatException("Hibás Excel-cellaazonosító.");
                var type=reader.GetAttribute("t");string value="",inline="";
                if(type=="e")throw new FormatException("Az Excel hibás cellaértéket tartalmaz. Javítsa az exportot.");
                var cellDepth=reader.Depth;
                if(!reader.IsEmptyElement){
                    reader.Read();
                    while(!reader.EOF && !(reader.NodeType==XmlNodeType.EndElement && reader.Depth==cellDepth)){
                        if(reader.NodeType==XmlNodeType.Element && reader.NamespaceURI==Sheet.NamespaceName){
                            if(reader.LocalName=="f")throw new FormatException("Képletek helyett értékeket tartalmazó Excel-export szükséges.");
                            if(reader.LocalName=="v"){value=reader.ReadElementContentAsString();continue;}
                            if(reader.LocalName=="t"){inline+=reader.ReadElementContentAsString();continue;}
                        }
                        reader.Read();
                    }
                }
                if(type=="inlineStr")value=inline;
                else if(type=="s"){
                    if(!int.TryParse(value,out var index)||index<0||index>=strings.Length)throw new FormatException("Hibás Excel-szöveghivatkozás.");
                    value=strings[index];
                }
                if(value.Length>0){if(column>values.Length)throw new FormatException("Adat a fejlécen kívüli Excel-oszlopban.");values[column-1]=value;maxUsed=Math.Max(maxUsed,column-1);}
            }
            else if(reader.NodeType==XmlNodeType.EndElement && reader.LocalName=="row" && values!=null && reader.Depth==rowDepth){
                if(maxUsed>=0){
                    var row=values;if(width==0){width=maxUsed+1;Array.Resize(ref row,width);}
                    for(int i=0;i<row.Length;i++)row[i]??="";
                    rows.Add(row);if(rows.Count>100000)throw new FormatException("Legfeljebb 100 000 sor importálható.");
                }
                values=null;
            }
            else if(reader.NodeType==XmlNodeType.EndElement && reader.LocalName=="sheetData")sheetDepth=-1;
        }
        return rows;
    }
}
