using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;
public partial class UserSettingsView : UserControl
{
    private UserAccountStore? accounts;
    private UserSession? actor;
    private readonly List<CheckBox> permissionBoxes=[];
    private Guid? editing;
    private int page;
    public event Action<UserSession>? AccountChanged;
    public UserSettingsView(){InitializeComponent();BuildPermissions();IsVisibleChanged+=(_,_)=>{HideSecrets();if(IsVisible && accounts!=null)Render();};RoleInput.SelectedIndex=0;}
    public void Configure(UserAccountStore accounts,UserSession actor){this.accounts=accounts;this.actor=actor;NewUser(this,new RoutedEventArgs());Render();}
    private void BuildPermissions(){
        foreach(var (options,panel) in new[]{(UserAccess.Menus,MenuPermissions),(UserAccess.Settings,SettingsPermissions)})
            foreach(var option in options){var box=new CheckBox{Content=option.Name,Tag=option.Id,Height=21,FontSize=12,VerticalContentAlignment=VerticalAlignment.Center};box.SetResourceReference(ForegroundProperty,"TextPrimary");permissionBoxes.Add(box);panel.Children.Add(box);}
    }
    private void RoleChanged(object sender,SelectionChangedEventArgs e)=>UpdateRole();
    private void UpdateRole(){
        if(permissionBoxes==null)return;
        var admin=RoleInput.SelectedIndex==1;
        foreach(var box in permissionBoxes){var general=(string)box.Tag==UserAccess.General;box.IsEnabled=!admin && !general;if(admin || general)box.IsChecked=true;}
    }
    public void HideSecrets(){PasswordInput.HideValue();foreach(var border in UserRows.Children.OfType<Border>())if(border.Child is Grid row){foreach(var text in row.Children.OfType<TextBox>().Where(t=>t.Tag is Guid))text.Text="••••••••";foreach(var button in row.Children.OfType<Button>().Where(b=>b.Tag is Guid))SetEye(button,false);}}
    private void NewUser(object sender,RoutedEventArgs e){
        editing=null;EditorTitle.Text="Új felhasználó";NameInput.Clear();UsernameInput.Clear();PasswordInput.HideValue();PasswordInput.Value="";RoleInput.SelectedIndex=0;
        foreach(var box in permissionBoxes)box.IsChecked=UserAccess.Menus.Any(p=>p.Id==(string)box.Tag) || (string)box.Tag==UserAccess.General;
        UpdateRole();Status.Text="Az Általános fül mindig elérhető. Adminisztrátorként minden hozzáférés korlátlan.";
    }
    private void Render(){
        if(accounts==null || actor==null || !actor.IsAdministrator)return;
        HideSecrets();UserRows.Children.Clear();var users=accounts.List(actor);page=Math.Clamp(page,0,Math.Max(0,(users.Length-1)/3));
        foreach(var user in users.Skip(page*3).Take(3)){
            var row=new Grid{Margin=new Thickness(0,3,0,3)};
            foreach(var width in new[]{new GridLength(1,GridUnitType.Star),new GridLength(115),new GridLength(34),new GridLength(34),new GridLength(34)})row.ColumnDefinitions.Add(new ColumnDefinition{Width=width});
            var identity=new TextBlock{Text=user.Name+" · "+(user.Role==UserRole.Administrator?"Adminisztrátor":"Felhasználó")+"\n"+user.Username,FontSize=11,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,ToolTip=user.Name+" · "+user.Username};row.Children.Add(identity);
            var secret=new TextBox{Text="••••••••",Tag=user.Id,FontSize=12,Height=30,IsReadOnly=true,Padding=new Thickness(4,2,4,2),VerticalAlignment=VerticalAlignment.Center};secret.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Hidden);System.Windows.Automation.AutomationProperties.SetName(secret,"Jelszó, alapból rejtett");Grid.SetColumn(secret,1);row.Children.Add(secret);
            var eye=IconButton("EyeIcon","Jelszó megjelenítése",2);eye.Tag=user.Id;eye.Click+=(_,_)=>{
                if(accounts==null || actor==null)return;
                bool reveal=((System.Windows.Shapes.Path)eye.Content).Data==(Geometry)FindResource("EyeIcon");secret.Text=reveal?accounts.RevealPassword(actor,user.Id):"••••••••";SetEye(eye,reveal);
            };row.Children.Add(eye);
            var edit=IconButton("DocumentIcon","Felhasználó szerkesztése",3);edit.Click+=(_,_)=>Edit(user);row.Children.Add(edit);
            var delete=IconButton("TrashIcon","Felhasználó törlése",4);delete.IsEnabled=user.Id!=actor.Id;delete.Click+=(_,_)=>Delete(user);row.Children.Add(delete);
            UserRows.Children.Add(new Border{Child=row,BorderThickness=new Thickness(0,0,0,1),BorderBrush=(Brush)FindResource("PanelBorder")});
        }
        PageLabel.Text=$"{page+1} / {Math.Max(1,(users.Length+2)/3)}";PreviousButton.IsEnabled=page>0;NextButton.IsEnabled=(page+1)*3<users.Length;
    }
    private Button IconButton(string icon,string tooltip,int column){var button=new Button{Width=30,Height=30,Padding=new Thickness(7),ToolTip=tooltip};button.Content=new System.Windows.Shapes.Path{Data=(Geometry)FindResource(icon),Style=(Style)FindResource("ButtonIcon"),Width=14,Height=14,Margin=new Thickness(0)};Grid.SetColumn(button,column);System.Windows.Automation.AutomationProperties.SetName(button,tooltip);return button;}
    private void SetEye(Button button,bool visible){((System.Windows.Shapes.Path)button.Content).Data=(Geometry)FindResource(visible?"EyeOffIcon":"EyeIcon");button.ToolTip=visible?"Jelszó elrejtése":"Jelszó megjelenítése";System.Windows.Automation.AutomationProperties.SetName(button,(string)button.ToolTip);}
    private void Edit(UserListItem user){
        if(accounts==null || actor==null)return;HideSecrets();editing=user.Id;EditorTitle.Text="Felhasználó szerkesztése";NameInput.Text=user.Name;UsernameInput.Text=user.Username;PasswordInput.Value=accounts.RevealPassword(actor,user.Id);RoleInput.SelectedIndex=user.Role==UserRole.Administrator?1:0;
        foreach(var box in permissionBoxes)box.IsChecked=user.Role==UserRole.Administrator || (string)box.Tag==UserAccess.General || user.Permissions.Contains((string)box.Tag);
        UpdateRole();Status.Text="A módosításokat a Felhasználó mentése gomb rögzíti.";
    }
    private async void Save(object sender,RoutedEventArgs e){
        if(accounts==null || actor==null)return;
        var store=accounts;var admin=actor;var id=editing;var username=UsernameInput.Text;var name=NameInput.Text;var password=PasswordInput.Value;var role=RoleInput.SelectedIndex==1?UserRole.Administrator:UserRole.User;var permissions=permissionBoxes.Where(b=>b.IsChecked==true).Select(b=>(string)b.Tag).ToArray();
        IsEnabled=false;PasswordInput.HideValue();
        try{
            if(!await Task.Run(()=>store.Save(admin,id,username,name,password,role,permissions))){Status.Text="A felhasználó nem menthető. A korábbi adatok megmaradtak.";return;}
            if(!IsVisible)return;
            actor=store.Current(admin.Id);AccountChanged?.Invoke(actor);NewUser(this,new RoutedEventArgs());Render();Status.Text="A felhasználó és a hozzáférések mentve.";
        }catch(Exception ex) when(ex is ArgumentException or UnauthorizedAccessException){Status.Text=ex.Message;}
        finally{IsEnabled=true;}
    }
    private void Delete(UserListItem user){
        if(accounts==null || actor==null)return;
        if(MessageBox.Show($"Törli ezt a felhasználót: {user.Name}?","Felhasználó törlése",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        try{if(!accounts.Delete(actor,user.Id)){Status.Text="A törlés nem menthető.";return;}if(editing==user.Id)NewUser(this,new RoutedEventArgs());Render();Status.Text="A felhasználó törölve.";}catch(Exception ex) when(ex is ArgumentException or UnauthorizedAccessException){Status.Text=ex.Message;}
    }
    private void Previous(object sender,RoutedEventArgs e){page--;Render();}
    private void Next(object sender,RoutedEventArgs e){page++;Render();}
}
