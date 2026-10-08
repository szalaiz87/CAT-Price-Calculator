using System.Windows;
using System.Windows.Media;
namespace CatPriceCalculator;
internal static class ThemePalette
{
    private static readonly (string Key, string Dark, string Light)[] Colors = [
        ("0B1522", "#0A1322", "#F1F5FB"),
        ("0D192A", "#101D30", "#F7FAFE"),
        ("101D2D", "#0E1B2D", "#EAF1F9"),
        ("112D46", "#173751", "#CDDEF2"),
        ("142E4A", "#203F5E", "#DCEBFA"),
        ("152A43", "#15283E", "#FFFFFF"),
        ("1B3352", "#19334F", "#E4EFFB"),
        ("202C3C", "#0D1A2B", "#FFFFFF"),
        ("235A83", "#2C4A68", "#7192B7"),
        ("28425E", "#2A405B", "#C4D3E5"),
        ("29384D", "#1B2E46", "#EAF1FA"),
        ("389CFF", "#389CFF", "#096AC0"),
        ("4EEAFF", "#57D5FF", "#087BBE"),
        ("65B5FF", "#65B5FF", "#0869B1"),
        ("9CADC3", "#9BB1C9", "#4C637E"),
        ("B9CEE8", "#BDCEE1", "#36526F"),
        ("E8EDF5", "#E7EFF9", "#172B43"),
        ("SelectedStart", "#148EFF", "#0B65C8"),
        ("SelectedEnd", "#0867DD", "#084FA5"),
        ("NavigationSelectedStart", "#163D67", "#DCEBFC"),
        ("NavigationSelectedEnd", "#112D50", "#C8DFF7"),
        ("NavigationSelectedText", "#FFFFFF", "#12375F"),
        ("SelectionEdge", "#70D6FF", "#2876BF"),
        ("ResultStart", "#057DE5", "#E4F1FF"),
        ("ResultEnd", "#123B87", "#C3DDF7"),
        ("ResultText", "#FFFFFF", "#15375B"),
        ("ResultDetail", "#DEEEFF", "#264D76"),
        ("HeroStart", "#F20A1423", "#F2F7FAFF"),
        ("HeroMiddle", "#D90A1423", "#E6F7FAFF"),
        ("HeroEnd", "#300A1423", "#40F7FAFF"),
        ("HeroText", "#FFFFFF", "#15375B"),
        ("HeroDetail", "#C8DFF4", "#375775"),
    ];
    private static Dictionary<string, object> Build(bool light)
    {
        var resources = new Dictionary<string, object>();
        foreach (var entry in Colors)
        {
            var color = (Color)ColorConverter.ConvertFromString(light ? entry.Light : entry.Dark);
            resources["Color" + entry.Key] = color;
            if (entry.Key is "65B5FF" or "9CADC3")
            {
                var brush = new SolidColorBrush(color); brush.Freeze();
                resources["Palette" + entry.Key] = brush;
            }
        }
        return resources;
    }
    private static readonly Lazy<Dictionary<string, object>> Dark = new(() => Build(false)), Light = new(() => Build(true));
    private static bool? applied;
    public static void Apply(bool light)
    {
        if (applied == light) return;
        foreach (var resource in (light ? Light : Dark).Value) Application.Current.Resources[resource.Key] = resource.Value;
        applied = light;
    }
}
