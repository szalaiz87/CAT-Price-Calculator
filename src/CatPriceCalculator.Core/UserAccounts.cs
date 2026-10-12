using System.Security.Cryptography;
namespace CatPriceCalculator.Core;

public enum UserRole { User, Administrator }
public sealed record AccessOption(string Id,string Name);
public static class UserAccess
{
    public const string General="settings.general", Users="settings.users";
    public static IReadOnlyList<AccessOption> Menus { get; }=Array.AsReadOnly(new[]{
        new AccessOption("menu.cat","CAT árkalkulátor"),new("menu.eur","EUR árkalkulátor"),new("menu.service","Szerviznaptár"),new("menu.robo","Robo Sanyi"),new("menu.updates","Frissítések")});
    public static IReadOnlyList<AccessOption> Settings { get; }=Array.AsReadOnly(new[]{
        new AccessOption(General,"Általános"),new("settings.dhl","DHL API"),new("settings.ups","UPS API"),new("settings.fedex","FedEx API"),new("settings.gls","GLS API"),new("settings.log","API-napló"),new("settings.service","Szerviznaptár import")});
    public static bool Known(string permission)=>permission==Users || Menus.Concat(Settings).Any(p=>p.Id==permission);
    public static string ForSettings(string section)=>section switch {"General"=>General,"Users"=>Users,"DHL"=>"settings.dhl","UPS"=>"settings.ups","FedEx"=>"settings.fedex","GLS"=>"settings.gls","Log"=>"settings.log","Service"=>"settings.service",_=>""};
}
public sealed record UserSession(Guid Id,string Username,string Name,UserRole Role,string[] Permissions)
{
    public bool IsAdministrator=>Role==UserRole.Administrator;
    public bool Can(string permission)=>UserAccess.Known(permission) && (IsAdministrator || permission==UserAccess.General || (permission!=UserAccess.Users && Permissions.Contains(permission)));
}
// This record is serialized only inside the Windows-protected account database.
public sealed record StoredUser(Guid Id,string Username,string Name,UserRole Role,string Password,string Salt,string PasswordHash,string[] Permissions)
{
    public override string ToString()=>Username;
    public UserSession Session()=>new(Id,Username,Name,Role,Permissions.ToArray());
}
public sealed record UserDatabase(int Version,StoredUser[] Users);
public sealed record UserListItem(Guid Id,string Username,string Name,UserRole Role,string[] Permissions);
public sealed class UserAccountStore
{
    private readonly string path;
    private readonly ProtectedSettingsStore<UserDatabase> storage;
    private UserDatabase database=new(1,[]);
    private bool ready;
    private const int Iterations=210_000;
    public UserAccountStore(string path,ISecretProtector protector){this.path=path;storage=new(path,protector,()=>new(1,[]),Valid);}
    public bool Initialize(string administratorUsername){
        if(File.Exists(path)){
            var loaded=storage.Load();if(loaded.Failed)return false;database=loaded.Settings;ready=true;return true;
        }
        var initial=new UserDatabase(1,[Create(Guid.NewGuid(),administratorUsername,"Szalai Zoltán","12345",UserRole.Administrator,[])]);
        if(!storage.Save(initial))return false;database=initial;ready=true;return true;
    }
    public UserSession? Authenticate(string username,string password){
        if(!ready)return null;
        var user=database.Users.SingleOrDefault(u=>u.Username.Equals(username.Trim(),StringComparison.OrdinalIgnoreCase));
        // Equal work for unknown names; password text is exact and case sensitive.
        var salt=user==null?new byte[16]:Convert.FromBase64String(user.Salt);
        if(password.Length>512)return null;
        var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
        try{return user!=null && CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(user.PasswordHash))?user.Session():null;}
        finally{CryptographicOperations.ZeroMemory(actual);}
    }
    private StoredUser RequireAdmin(UserSession actor){
        if(!ready || database.Users.SingleOrDefault(u=>u.Id==actor.Id) is not {Role:UserRole.Administrator} user)throw new UnauthorizedAccessException("Ehhez adminisztrátori jogosultság szükséges.");
        return user;
    }
    public UserListItem[] List(UserSession actor){RequireAdmin(actor);return database.Users.OrderBy(u=>u.Name,StringComparer.CurrentCultureIgnoreCase).Select(u=>new UserListItem(u.Id,u.Username,u.Name,u.Role,u.Permissions.ToArray())).ToArray();}
    public string RevealPassword(UserSession actor,Guid id){RequireAdmin(actor);return database.Users.Single(u=>u.Id==id).Password;}
    public UserSession Current(Guid id)=>database.Users.Single(u=>u.Id==id).Session();
    public bool Save(UserSession actor,Guid? id,string username,string name,string password,UserRole role,IEnumerable<string> permissions){
        RequireAdmin(actor);username=username.Trim();name=name.Trim();
        if(username.Length is <1 or >80 || name.Length is <1 or >120 || username.Any(char.IsControl) || name.Any(char.IsControl) || password.Length is <1 or >512 || string.IsNullOrWhiteSpace(password) || !Enum.IsDefined(role))throw new ArgumentException("Adja meg a nevet, felhasználónevet és jelszót. A felhasználónév legfeljebb 80, a név 120, a jelszó 512 karakter lehet.");
        if(database.Users.Any(u=>u.Id!=id && u.Username.Equals(username,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Ez a felhasználónév már létezik.");
        if(id!=null && !database.Users.Any(u=>u.Id==id))throw new ArgumentException("A felhasználó már nem található.");
        if(id==actor.Id && role!=UserRole.Administrator)throw new ArgumentException("A saját adminisztrátori szerepkörét nem vonhatja vissza.");
        var granted=permissions.Where(p=>UserAccess.Known(p) && p!=UserAccess.Users).Append(UserAccess.General).Distinct().ToArray();
        var updated=Create(id??Guid.NewGuid(),username,name,password,role,granted);
        var users=database.Users.Where(u=>u.Id!=updated.Id).Append(updated).ToArray();
        if(!users.Any(u=>u.Role==UserRole.Administrator))throw new ArgumentException("Legalább egy adminisztrátor szükséges.");
        return Commit(users);
    }
    public bool Delete(UserSession actor,Guid id){
        RequireAdmin(actor);if(id==actor.Id)throw new ArgumentException("A saját bejelentkezett fiókját nem törölheti.");
        var users=database.Users.Where(u=>u.Id!=id).ToArray();
        if(!users.Any(u=>u.Role==UserRole.Administrator))throw new ArgumentException("Legalább egy adminisztrátor szükséges.");
        return Commit(users);
    }
    private bool Commit(StoredUser[] users){var next=new UserDatabase(1,users);if(!storage.Save(next))return false;database=next;return true;}
    private static StoredUser Create(Guid id,string username,string name,string password,UserRole role,string[] permissions){
        var salt=RandomNumberGenerator.GetBytes(16);var hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
        try{return new(id,username.Trim(),name.Trim(),role,password,Convert.ToBase64String(salt),Convert.ToBase64String(hash),permissions);}
        finally{CryptographicOperations.ZeroMemory(hash);}
    }
    private static bool Valid(UserDatabase db){
        if(db.Version!=1 || db.Users==null || db.Users.Length==0 || !db.Users.Any(u=>u!=null && u.Role==UserRole.Administrator))return false;
        if(db.Users.Any(u=>u==null || u.Id==Guid.Empty || string.IsNullOrWhiteSpace(u.Username) || u.Username.Length>80 || string.IsNullOrWhiteSpace(u.Name) || u.Name.Length>120 || u.Password==null || u.Password.Length is <1 or >512 || !Enum.IsDefined(u.Role) || u.Permissions==null || u.Permissions.Any(p=>!UserAccess.Known(p)) || !ValidHash(u)))return false;
        return db.Users.Select(u=>u.Id).Distinct().Count()==db.Users.Length && db.Users.Select(u=>u.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count()==db.Users.Length;
    }
    private static bool ValidHash(StoredUser user){try{return Convert.FromBase64String(user.Salt).Length==16 && Convert.FromBase64String(user.PasswordHash).Length==32;}catch(Exception e) when(e is FormatException or ArgumentNullException){return false;}}
}
