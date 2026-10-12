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
        ("SuccessText", "#60DFAE", "#12744F"),
        ("SuccessSurface", "#133B37", "#E4F5EB"),
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
        // Replace complete brushes so nested resource dictionaries cannot retain the original palette.
        Color Resolve(string value) => value.StartsWith("{DynamicResource ")
            ? (Color)resources[value[17..^1]] : (Color)ColorConverter.ConvertFromString(value);
        var Palette65B5FF = new SolidColorBrush(Resolve("#65B5FF"));
        Palette65B5FF.Freeze(); resources["Palette65B5FF"] = Palette65B5FF;
        var Palette9CADC3 = new SolidColorBrush(Resolve("#9BB1C9"));
        Palette9CADC3.Freeze(); resources["Palette9CADC3"] = Palette9CADC3;
        var WindowSurface = new SolidColorBrush(Resolve("{DynamicResource Color0B1522}"));
        WindowSurface.Freeze(); resources["WindowSurface"] = WindowSurface;
        var SidebarSurface = new SolidColorBrush(Resolve("{DynamicResource Color101D2D}"));
        SidebarSurface.Freeze(); resources["SidebarSurface"] = SidebarSurface;
        var InputSurface = new SolidColorBrush(Resolve("{DynamicResource Color202C3C}"));
        InputSurface.Freeze(); resources["InputSurface"] = InputSurface;
        var ButtonSurface = new SolidColorBrush(Resolve("{DynamicResource Color29384D}"));
        ButtonSurface.Freeze(); resources["ButtonSurface"] = ButtonSurface;
        var HoverSurface = new SolidColorBrush(Resolve("{DynamicResource Color142E4A}"));
        HoverSurface.Freeze(); resources["HoverSurface"] = HoverSurface;
        var PressedSurface = new SolidColorBrush(Resolve("{DynamicResource Color112D46}"));
        PressedSurface.Freeze(); resources["PressedSurface"] = PressedSurface;
        var PanelBorder = new SolidColorBrush(Resolve("{DynamicResource Color28425E}"));
        PanelBorder.Freeze(); resources["PanelBorder"] = PanelBorder;
        var InputBorder = new SolidColorBrush(Resolve("{DynamicResource Color235A83}"));
        InputBorder.Freeze(); resources["InputBorder"] = InputBorder;
        var Accent = new SolidColorBrush(Resolve("{DynamicResource Color389CFF}"));
        Accent.Freeze(); resources["Accent"] = Accent;
        var FocusBorder = new SolidColorBrush(Resolve("{DynamicResource Color4EEAFF}"));
        FocusBorder.Freeze(); resources["FocusBorder"] = FocusBorder;
        var AccentText = new SolidColorBrush(Resolve("{DynamicResource Color65B5FF}"));
        AccentText.Freeze(); resources["AccentText"] = AccentText;
        var TextPrimary = new SolidColorBrush(Resolve("{DynamicResource ColorE8EDF5}"));
        TextPrimary.Freeze(); resources["TextPrimary"] = TextPrimary;
        var TextSecondary = new SolidColorBrush(Resolve("{DynamicResource ColorB9CEE8}"));
        TextSecondary.Freeze(); resources["TextSecondary"] = TextSecondary;
        var TextMuted = new SolidColorBrush(Resolve("{DynamicResource Color9CADC3}"));
        TextMuted.Freeze(); resources["TextMuted"] = TextMuted;
        var SuccessText = new SolidColorBrush(Resolve("{DynamicResource ColorSuccessText}"));
        SuccessText.Freeze(); resources["SuccessText"] = SuccessText;
        var SuccessSurface = new SolidColorBrush(Resolve("{DynamicResource ColorSuccessSurface}"));
        SuccessSurface.Freeze(); resources["SuccessSurface"] = SuccessSurface;
        var PanelGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        PanelGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource Color152A43}"), 0));
        PanelGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource Color0D192A}"), 1));
        PanelGradient.Freeze(); resources["PanelGradient"] = PanelGradient;
        var SummaryGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        SummaryGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource Color1B3352}"), 0));
        SummaryGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource Color152A43}"), 1));
        SummaryGradient.Freeze(); resources["SummaryGradient"] = SummaryGradient;
        var SelectedGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        SelectedGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorSelectedStart}"), 0));
        SelectedGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorSelectedEnd}"), 1));
        SelectedGradient.Freeze(); resources["SelectedGradient"] = SelectedGradient;
        var NavigationSelectedGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        NavigationSelectedGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorNavigationSelectedStart}"), 0));
        NavigationSelectedGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorNavigationSelectedEnd}"), 1));
        NavigationSelectedGradient.Freeze(); resources["NavigationSelectedGradient"] = NavigationSelectedGradient;
        var ResultGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        ResultGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorResultStart}"), 0));
        ResultGradient.GradientStops.Add(new GradientStop(Resolve("{DynamicResource ColorResultEnd}"), 1));
        ResultGradient.Freeze(); resources["ResultGradient"] = ResultGradient;
        var SelectedBorder = new SolidColorBrush(Resolve("{DynamicResource ColorSelectionEdge}"));
        SelectedBorder.Freeze(); resources["SelectedBorder"] = SelectedBorder;
        var PressedSelectedSurface = new SolidColorBrush(Resolve("#0758AF"));
        PressedSelectedSurface.Freeze(); resources["PressedSelectedSurface"] = PressedSelectedSurface;
        var NavigationSelectedForeground = new SolidColorBrush(Resolve("{DynamicResource ColorNavigationSelectedText}"));
        NavigationSelectedForeground.Freeze(); resources["NavigationSelectedForeground"] = NavigationSelectedForeground;
        var ResultForeground = new SolidColorBrush(Resolve("{DynamicResource ColorResultText}"));
        ResultForeground.Freeze(); resources["ResultForeground"] = ResultForeground;
        var ResultDetailForeground = new SolidColorBrush(Resolve("{DynamicResource ColorResultDetail}"));
        ResultDetailForeground.Freeze(); resources["ResultDetailForeground"] = ResultDetailForeground;
        var HeroForeground = new SolidColorBrush(Resolve("{DynamicResource ColorHeroText}"));
        HeroForeground.Freeze(); resources["HeroForeground"] = HeroForeground;
        var HeroDetailForeground = new SolidColorBrush(Resolve("{DynamicResource ColorHeroDetail}"));
        HeroDetailForeground.Freeze(); resources["HeroDetailForeground"] = HeroDetailForeground;
        return resources;
    }
    private static readonly Lazy<Dictionary<string, object>> Dark = new(() => Build(false)), Light = new(() => Build(true));
    private static bool? applied;
    public static void Apply(bool light)
    {
        if (applied == light) return;
        var applicationResources = Application.Current.Resources;
        var palette = applicationResources.MergedDictionaries.First(dictionary =>
            dictionary.Source?.OriginalString.EndsWith("Styles/Colors.xaml", StringComparison.Ordinal) == true);
        foreach (var resource in (light ? Light : Dark).Value)
        {
            palette[resource.Key] = resource.Value;
            applicationResources[resource.Key] = resource.Value;
        }
        applied = light;
    }
}
