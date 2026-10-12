using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class CustomerSearchWindow : Window
{
    private readonly ServiceCustomerSearchIndex index;
    private readonly Action<ServiceCustomer> chosen;
    private ServiceCustomer[] filtered=[];
    private bool prefix;
    private int page;
    private const int PageSize=12;
    public CustomerSearchWindow(ServiceCustomer[] source,Action<ServiceCustomer> selected){
        index=new ServiceCustomerSearchIndex(source);chosen=selected;InitializeComponent();Filter();
        Loaded+=(_,_)=>Query.Focus();
        SourceInitialized+=(_,_)=>WindowTheme.Apply(this,new AppearanceStore(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","appearance.json")).Load()==Appearance.Light);
    }
    public void ApplyTheme(bool light)=>WindowTheme.Apply(this,light);
    private void QueryChanged(object sender,TextChangedEventArgs e){if(Results!=null && Count!=null && NextButton!=null)Filter();}
    private void PrefixSearch(object sender,RoutedEventArgs e){prefix=true;Filter();}
    private void AnywhereSearch(object sender,RoutedEventArgs e){prefix=false;Filter();}
    private void Filter(){filtered=index.Find(Query.Text,prefix);page=0;ShowPage();}
    private void ShowPage(){
        Results.ItemsSource=filtered.Skip(page*PageSize).Take(PageSize).ToArray();
        PreviousButton.IsEnabled=page>0;NextButton.IsEnabled=(page+1)*PageSize<filtered.Length;
        ChooseButton.IsEnabled=false;
        Count.Text=$"{filtered.Length} találat · {page+1}/{Math.Max(1,(filtered.Length+PageSize-1)/PageSize)}";
        PrefixButton.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty,prefix);
        AnywhereButton.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty,!prefix);
    }
    private void ResultChanged(object sender,SelectionChangedEventArgs e){if(ChooseButton!=null)ChooseButton.IsEnabled=Results.SelectedItem is ServiceCustomer;}
    private void PreviousPage(object sender,RoutedEventArgs e){if(page>0){page--;ShowPage();}}
    private void NextPage(object sender,RoutedEventArgs e){if((page+1)*PageSize<filtered.Length){page++;ShowPage();}}
    private void Choose(object sender,RoutedEventArgs e){if(Results.SelectedItem is ServiceCustomer customer){chosen(customer);Close();}}
    private void ChooseDoubleClick(object sender,MouseButtonEventArgs e){if(Results.SelectedItem is ServiceCustomer)Choose(sender,e);}
}
