using System.Text.Json;
namespace CatPriceCalculator.Core;
public sealed class LoginPreferenceStore(string path)
{
    private sealed record Preference(string Username);
    public string Load(){
        try{var name=LocalJsonFile.Read<Preference>(path)?.Username;return Valid(name)?name!:"";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException){return "";}
    }
    public bool Save(string username)=>Valid(username) && LocalJsonFile.Write(path,new Preference(username));
    private static bool Valid(string? username)=>!string.IsNullOrWhiteSpace(username) && username.Length<=80 && !username.Any(char.IsControl);
}
