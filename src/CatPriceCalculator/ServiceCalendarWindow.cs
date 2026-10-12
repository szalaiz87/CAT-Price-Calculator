using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public sealed class ServiceCalendarWindow : Window
{
    private ServiceJob[] jobs;
    private int year,month;
    private readonly Grid calendar=new();
    private readonly TextBlock title=new(){FontSize=25,FontWeight=FontWeights.SemiBold};
    private readonly UniformGridHost monthTabs=new();
    private static readonly CultureInfo Hungarian=CultureInfo.GetCultureInfo("hu-HU");
    public ServiceCalendarWindow(ServiceJob[] tasks,DateOnly date) {
        jobs=tasks;year=date.Year;month=date.Month;
        Title="Linser Hungary · Szerviznaptár";Width=1380;Height=920;MinWidth=900;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty,"WindowSurface");FontFamily=new FontFamily("Segoe UI");
        var root=new Grid{Width=1400,Height=900,Margin=new Thickness(12)};
        root.RowDefinitions.Add(new(){Height=new GridLength(58)});root.RowDefinitions.Add(new(){Height=new GridLength(40)});root.RowDefinitions.Add(new(){Height=new GridLength(12)});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=new GridLength(32)});
        var header=new DockPanel();title.SetResourceReference(TextBlock.ForegroundProperty,"TextPrimary");
        var navigation=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(navigation,Dock.Right);
        navigation.Children.Add(Action("‹ Előző hónap",()=>MoveMonth(-1)));navigation.Children.Add(Action("Ma",()=>{year=DateTime.Today.Year;month=DateTime.Today.Month;Render();}));navigation.Children.Add(Action("Következő hónap ›",()=>MoveMonth(1)));
        header.Children.Add(navigation);header.Children.Add(title);root.Children.Add(header);
        Grid.SetRow(monthTabs,1);root.Children.Add(monthTabs);
        Grid.SetRow(calendar,3);root.Children.Add(calendar);
        var footer=new TextBlock{Text="Munkatársanként külön színsáv · Egy napra több feladat · Kattintson egy nap és munkatárs cellájára a részletekhez.",FontSize=13,VerticalAlignment=VerticalAlignment.Center};footer.SetResourceReference(TextBlock.ForegroundProperty,"TextSecondary");Grid.SetRow(footer,4);root.Children.Add(footer);
        Content=new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=root};
        SourceInitialized+=(_,_)=>WindowTheme.Apply(this,new AppearanceStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","appearance.json")).Load()==Appearance.Light);
        Render();
    }
    private static Button Action(string label,Action action) {var b=new Button{Content=label,Margin=new Thickness(3),Padding=new Thickness(12,6,12,6)};b.Click+=(_,_)=>action();return b;}
    private void MoveMonth(int delta){var next=new DateOnly(year,month,1);if(delta<0 && year==1 && month==1 || delta>0 && year==9999 && month==12)return;next=next.AddMonths(delta);year=next.Year;month=next.Month;Render();}
    public void ApplyTheme(bool light) => WindowTheme.Apply(this,light);
    public void Refresh(ServiceJob[] tasks){jobs=tasks;Render();}
    private void Render(){
        title.Text=$"SZERVIZNAPTÁR · {year}. {Hungarian.DateTimeFormat.GetMonthName(month)}";
        monthTabs.Children.Clear();
        for(int m=1;m<=12;m++){var selected=m;var button=Action(Hungarian.DateTimeFormat.GetAbbreviatedMonthName(m),()=>{month=selected;Render();});button.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty,m==month);monthTabs.Children.Add(button);}
        calendar.Children.Clear();calendar.RowDefinitions.Clear();calendar.ColumnDefinitions.Clear();
        calendar.ColumnDefinitions.Add(new(){Width=new GridLength(145)});for(int i=0;i<7;i++)calendar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        calendar.RowDefinitions.Add(new(){Height=new GridLength(28)});
        for(int w=0;w<6;w++){calendar.RowDefinitions.Add(new(){Height=new GridLength(24)});for(int s=0;s<5;s++)calendar.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});}
        string[] days={"Hétfő","Kedd","Szerda","Csütörtök","Péntek","Szombat","Vasárnap"};
        for(int d=0;d<7;d++){var label=new TextBlock{Text=days[d],FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center};label.SetResourceReference(TextBlock.ForegroundProperty,"TextPrimary");Place(label,0,d+1);}
        var start=ServiceCalendar.MonthGridStart(year,month);
        var lookup=jobs.GroupBy(j=>(j.Date,j.StaffId)).ToDictionary(g=>g.Key,g=>g.ToArray());
        for(int week=0;week<6;week++){
            int row=1+week*6;
            for(int s=0;s<5;s++){var staff=ServiceCalendar.Staff[s];Place(Band(new TextBlock{Text=staff.Name,FontSize=12,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,2,0),Foreground=new SolidColorBrush(Color.FromRgb(20,43,61))},staff.Color),row+1+s,0);}
            for(int day=0;day<7;day++){
                if(start.DayNumber+week*7+day>DateOnly.MaxValue.DayNumber)continue;
                var date=start.AddDays(week*7+day);
                var number=new TextBlock{Text=date.ToString("MM.dd",Hungarian),FontSize=13,FontWeight=FontWeights.Bold,Opacity=date.Month==month?1:.45,HorizontalAlignment=HorizontalAlignment.Center};number.SetResourceReference(TextBlock.ForegroundProperty,"TextPrimary");
                var dateBand=new Border{Child=number,BorderThickness=new Thickness(date==DateOnly.FromDateTime(DateTime.Today)?2:0),CornerRadius=new CornerRadius(4)};dateBand.SetResourceReference(Border.BackgroundProperty,"SummaryGradient");dateBand.SetResourceReference(Border.BorderBrushProperty,"Accent");Place(dateBand,row,day+1);
                for(int s=0;s<5;s++){
                    var staff=ServiceCalendar.Staff[s];var tasks=lookup.GetValueOrDefault((date,staff.Id),[]);
                    var summary=tasks.Length==0?"—":tasks[0].CustomerName+" · "+tasks[0].MachineName;
                    var content=new TextBlock{Text=summary,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Foreground=new SolidColorBrush(Color.FromRgb(20,43,61)),VerticalAlignment=VerticalAlignment.Center};
                    var cellContent=new DockPanel();
                    if(tasks.Length>1){var count=new TextBlock{Text=$"+{tasks.Length-1}",FontWeight=FontWeights.Bold,FontSize=11,Margin=new Thickness(3,0,0,0),VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(20,43,61))};DockPanel.SetDock(count,Dock.Right);cellContent.Children.Add(count);}
                    cellContent.Children.Add(content);
                    var b=new Button{Content=cellContent,Padding=new Thickness(5,0,5,0),Margin=new Thickness(1),BorderThickness=new Thickness(0),Opacity=date.Month==month?1:.42,ToolTip=$"{date:yyyy.MM.dd} · {staff.Name}\n{tasks.Length} feladat"};
                    b.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(staff.Color));
                    // Cell colors follow the reference calendar; templates keep fixed bounds and sharp focus.
                    b.Click+=(_,_)=>ShowDay(date,staff);Place(b,row+1+s,day+1);
                }
            }
        }
    }
    private static Border Band(UIElement content,string color)=>new(){Child=content,Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),Margin=new Thickness(1),CornerRadius=new CornerRadius(3)};
    private void Place(UIElement child,int row,int column){Grid.SetRow(child,row);Grid.SetColumn(child,column);calendar.Children.Add(child);}
    private void ShowDay(DateOnly date,ServiceStaff staff){
        var tasks=jobs.Where(j=>j.Date==date && j.StaffId==staff.Id).ToArray();int index=0, page=0;
        var dialog=new Window{Owner=this,Title=$"{date:yyyy.MM.dd} · {staff.Name}",Width=670,Height=650,MinWidth=520,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner};dialog.SetResourceReference(BackgroundProperty,"WindowSurface");
        var root=new Grid{Width=610,Height=560,Margin=new Thickness(18)};root.RowDefinitions.Add(new(){Height=new GridLength(55)});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=new GridLength(45)});
        var heading=new TextBlock{FontSize=20,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};heading.SetResourceReference(TextBlock.ForegroundProperty,"TextPrimary");root.Children.Add(heading);
        var body=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontSize=14,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(body,1);root.Children.Add(body);
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(buttons,2);root.Children.Add(buttons);
        void Display(){heading.Text=$"{date:yyyy.MM.dd} · {staff.Name}";var full=tasks.Length==0?"Erre a napra még nincs rögzített feladat.":$"Feladat {index+1} / {tasks.Length}\n\nÜgyfél: {tasks[index].CustomerName}\nGép: {tasks[index].MachineName}\nGyári szám: {tasks[index].SerialNumber}\nKapcsolattartó: {tasks[index].ContactName}\nTelefon: {tasks[index].Phone}\nLokáció: {tasks[index].Location}\n\nHibaleírás:\n{tasks[index].Description}";
            var pages=DetailPages(full);page=Math.Clamp(page,0,pages.Count-1);
            heading.Text+=$" · {page+1}/{pages.Count}";body.Text=pages[page];}
        buttons.Children.Add(Action("‹ Részlet",()=>{if(page>0){page--;Display();}}));buttons.Children.Add(Action("Részlet ›",()=>{page++;Display();}));
        buttons.Children.Add(Action("‹ Feladat",()=>{if(index>0){index--;page=0;Display();}}));buttons.Children.Add(Action("Feladat ›",()=>{if(index+1<tasks.Length){index++;page=0;Display();}}));buttons.Children.Add(Action("Bezárás",()=>dialog.Close()));
        Display();dialog.Content=new Viewbox{StretchDirection=StretchDirection.DownOnly,Child=root};dialog.ShowDialog();
    }
    private static List<string> DetailPages(string text) {
        var pages=new List<string>();var current=new System.Text.StringBuilder();int lines=0;
        foreach(var line in text.Replace("\r","").Split('\n')) {
            for(int offset=0;offset<Math.Max(1,line.Length);offset+=65) {
                if(lines==16){pages.Add(current.ToString());current.Clear();lines=0;}
                current.AppendLine(line.Substring(Math.Min(offset,line.Length),Math.Min(65,Math.Max(0,line.Length-offset))));lines++;
            }
        }
        if(current.Length>0)pages.Add(current.ToString());return pages;
    }
    private sealed class UniformGridHost : System.Windows.Controls.Primitives.UniformGrid { public UniformGridHost(){Columns=12;Rows=1;} }
}
