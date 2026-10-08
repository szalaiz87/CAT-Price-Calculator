using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace CatPriceCalculator;
internal static class WindowTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    public static void Apply(Window window, bool light = false)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var dark = light ? 0 : 1;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) < 0)
            DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
        // Windows 11 supports exact caption, text and border colors. Older Windows ignores unsupported attributes.
        var caption = light ? 0x00FBF5F1 : 0x0022130A; // COLORREF: #0A1322
        var text = light ? 0x00432B17 : 0x00F9EFE7;
        var border = light ? 0x00E5D3C4 : 0x005B402A;
        DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
    }
}
