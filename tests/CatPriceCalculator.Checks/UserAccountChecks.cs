using System.Text;
using CatPriceCalculator.Core;
internal static class UserAccountChecks
{
    public static void Run(Action<bool,string> check){
        var root=Path.Combine(Path.GetTempPath(),"cat-users-"+Guid.NewGuid());Directory.CreateDirectory(root);
        using var protector=new DhlChecks.TestProtector();var path=Path.Combine(root,"users.bin");
        try{
            var store=new UserAccountStore(path,protector);
            check(store.Authenticate("Szalai Zoltán","12345")==null,"No login before account storage initializes");
            check(store.Initialize("Szalai Zoltán"),"First installation creates the requested administrator");
            var admin=store.Authenticate("Szalai Zoltán","12345")!;
            check(admin is {IsAdministrator:true,Name:"Szalai Zoltán",Username:"Szalai Zoltán"},"Administrator login has the requested name and role");
            check(UserAccess.Menus.Concat(UserAccess.Settings).All(p=>admin.Can(p.Id)) && admin.Can(UserAccess.Users),"Administrator has every menu and settings permission");
            check(!admin.Can("unknown.permission"),"Unknown permissions are denied even for administrator");
            check(store.Authenticate("Szalai Zoltán","wrong")==null && store.Authenticate("unknown","12345")==null,"Invalid username and password never authenticate");
            check(store.Authenticate("  szalai zoltán  ","12345")!=null,"Usernames ignore casing and outer spaces");
            check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("Szalai Zoltán"),"User records are encrypted on disk");
            check(store.Save(admin,null,"tester","Próba Felhasználó"," MixedCase! ",UserRole.User,["menu.service","settings.service",UserAccess.Users]),"Administrator adds a named user with selected permissions");
            var user=store.Authenticate("tester"," MixedCase! ")!;
            check(user is {Role:UserRole.User} && user.Can("menu.service") && user.Can("settings.service"),"User receives the selected menu and settings access");
            check(user.Can(UserAccess.General) && !user.Can(UserAccess.Users) && !user.Can("menu.cat") && !user.Can("settings.dhl"),"General is always open; user management and unchecked areas stay denied");
            check(store.Authenticate("tester","MixedCase!")==null && store.Authenticate("tester"," mixedcase! ")==null,"Password whitespace and casing are preserved exactly");
            foreach(var operation in new Action[]{()=>store.List(user),()=>store.RevealPassword(user,admin.Id),()=>store.Save(user,null,"bad","Bad","x",UserRole.Administrator,[]),()=>store.Delete(user,admin.Id)}){
                try{operation();check(false,"Unauthorized administration");}catch(UnauthorizedAccessException){check(true,"Ordinary user cannot administer or reveal passwords");}
            }
            try{store.List(user with{Role=UserRole.Administrator});check(false,"Forged role");}catch(UnauthorizedAccessException){check(true,"Administration checks persisted role rather than caller-supplied role");}
            check(store.RevealPassword(admin,user.Id)==" MixedCase! ","Only administrator can recover a saved password");
            try{store.Save(admin,null,"TESTER","Duplicate","x",UserRole.User,[]);check(false,"Duplicate username");}catch(ArgumentException){check(true,"Duplicate username is rejected without casing differences");}
            try{store.Delete(admin,admin.Id);check(false,"Self deletion");}catch(ArgumentException){check(true,"Active administrator cannot delete their own account");}
            try{store.Save(admin,admin.Id,admin.Username,admin.Name,"12345",UserRole.User,[]);check(false,"Self demotion");}catch(ArgumentException){check(true,"Active administrator cannot remove their own admin role");}
            var restarted=new UserAccountStore(path,protector);
            check(restarted.Initialize("Different seed") && restarted.Authenticate("tester"," MixedCase! ")!=null && restarted.List(admin).Length==2,"Accounts and permissions survive restart without reseeding");
            check(store.Save(admin,user.Id,"tester","Updated User","NewPass!",UserRole.User,[]),"Administrator changes password and revokes permissions");
            check(store.Authenticate("tester"," MixedCase! ")==null && store.Authenticate("tester","NewPass!") is { } changed && !changed.Can("menu.service") && changed.Can(UserAccess.General),"Changed password replaces the old one and revoked access takes effect");
            var before=File.ReadAllBytes(path);protector.Fail=true;
            check(!store.Save(admin,user.Id,"tester","Not saved","Unsaved!",UserRole.User,[]) && File.ReadAllBytes(path).SequenceEqual(before),"Encryption failure preserves previous account file");
            check(store.Authenticate("tester","NewPass!")!=null && store.Authenticate("tester","Unsaved!")==null,"Failed save does not change in-memory authentication");protector.Fail=false;
            check(store.Delete(admin,user.Id) && store.Authenticate("tester","NewPass!")==null,"Deleted user can no longer sign in");
            check(store.Save(admin,null,"secondadmin","Second Admin","AdminPass!",UserRole.Administrator,[]),"Administrator can add another unrestricted administrator");
            var second=store.Authenticate("secondadmin","AdminPass!")!;
            check(second.Can(UserAccess.Users) && second.Can("menu.cat"),"Additional administrator also has unlimited access");
            check(store.Save(second,admin.Id,admin.Username,admin.Name,"Changed!",UserRole.User,[]),"Another administrator may change an account role while retaining an admin");
            try{store.List(admin);check(false,"Stale admin session");}catch(UnauthorizedAccessException){check(true,"Revoked administrator session cannot continue user administration");}
            File.WriteAllBytes(path,[1,2,3]);var broken=new UserAccountStore(path,protector);
            check(!broken.Initialize("Szalai Zoltán") && broken.Authenticate("Szalai Zoltán","12345")==null && File.ReadAllBytes(path).SequenceEqual(new byte[]{1,2,3}),"Corrupt account file fails closed and never recreates a default admin");
        }finally{Directory.Delete(root,true);}
    }
}
