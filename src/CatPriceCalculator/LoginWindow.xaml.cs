using System.Windows;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class LoginWindow : Window
{
    private readonly UserAccountStore accounts;
    private readonly LoginPreferenceStore preference=new(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","login-preference.json"));
    private bool busy;
    public UserSession? Session {get;private set;}
    public LoginWindow(UserAccountStore accounts,bool storageReady){
        this.accounts=accounts;InitializeComponent();
        UsernameInput.Text=preference.Load();
        LoginButton.IsEnabled=storageReady;
        if(!storageReady)Status.Text="A felhasználói adatbázis nem olvasható vagy nem menthető. A meglévő fájlt megőriztük; bejelentkezés nem lehetséges.";
        Width=System.Math.Min(Width,SystemParameters.WorkArea.Width-24);Height=System.Math.Min(Height,SystemParameters.WorkArea.Height-24);
        SourceInitialized+=(_,_)=>WindowTheme.Apply(this,new AppearanceStore(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),"CAT-Price-Calculator","appearance.json")).Load()==Appearance.Light);
        Loaded+=(_,_)=>{if(UsernameInput.Text.Length==0)UsernameInput.Focus();else PasswordInput.FocusInput();};
        Deactivated+=(_,_)=>PasswordInput.HideValue();
        Closed+=(_,_)=>{PasswordInput.HideValue();PasswordInput.Value="";};
    }
    private async void SignIn(object sender,RoutedEventArgs e){
        if(busy || !LoginButton.IsEnabled)return;
        busy=true;LoginButton.IsEnabled=false;UsernameInput.IsEnabled=PasswordInput.IsEnabled=false;
        string username=UsernameInput.Text,password=PasswordInput.Value;
        PasswordInput.HideValue();Status.Text="Bejelentkezés…";
        try{
            var session=await System.Threading.Tasks.Task.Run(()=>accounts.Authenticate(username,password));
            if(!IsVisible)return;
            if(session!=null){preference.Save(session.Username);Session=session;DialogResult=true;return;}
            Status.Text="Hibás felhasználónév vagy jelszó.";PasswordInput.Value="";
            await System.Threading.Tasks.Task.Delay(1000);
        }finally{busy=false;if(IsVisible){LoginButton.IsEnabled=true;UsernameInput.IsEnabled=PasswordInput.IsEnabled=true;}}
    }
}
