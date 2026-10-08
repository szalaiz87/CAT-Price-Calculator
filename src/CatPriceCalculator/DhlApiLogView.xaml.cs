using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CatPriceCalculator.Core;
namespace CatPriceCalculator;

public partial class DhlApiLogView : UserControl
{
    private sealed record LogRow(ApiLogEntry Entry)
    {
        public bool IsError => Entry.IsError;
        public string Message => Entry.Message;
        public string Metadata => RateTimestamp.Format(Entry.Timestamp) + " • " + Entry.Carrier + " • " + Entry.Stage +
            (Entry.HttpStatus.HasValue ? " • HTTP " + Entry.HttpStatus : "") +
            (Entry.TrackingReference != null ? " • " + Entry.TrackingReference : "");
        public string Details => Metadata + "\n" + Message + (Entry.ElapsedMilliseconds.HasValue ? "\nEltelt idő: " + Entry.ElapsedMilliseconds + " ms" : "");
    }
    private const int PageSize = 6;
    private ApiLogStore? store;
    private List<ApiLogEntry> entries = [];
    private int page;
    public DhlApiLogView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => { if (IsVisible) { page = 0; Reload(); } };
    }
    public void Configure(ApiLogStore logStore) { store = logStore; LogLocation.Text = $"Legfeljebb {ApiLogStore.MaxEntries} helyi bejegyzés.\nFájl: {store.FilePath}"; Reload(); }
    private void Reload()
    {
        if (store == null) return;
        var loaded = store.Load(); entries = loaded.Entries.AsEnumerable().Reverse().ToList();
        LogStatus.Text = loaded.Failed ? "A napló nem olvasható. A követési adatok és a hozzáférések megmaradnak." : store.LastWriteFailed ? "A legutóbbi naplóírás nem sikerült; bejegyzések hiányozhatnak. A követés működését nem akadályozza." : "Helyi napló • újraindítás után is megmarad. Frissítése nem indít API-kérést.";
        EmptyLog.Text = loaded.Failed ? "A naplófájl hibás vagy nem elérhető." : "Még nincs API-lekérés. Indíts kézi tesztet vagy csomagfrissítést.";
        Render();
    }
    private void Render()
    {
        int pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        LogRows.ItemsSource = entries.Skip(page * PageSize).Take(PageSize).Select(e => new LogRow(e)).ToArray();
        EmptyLog.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LogPager.Text = entries.Count == 0 ? "0 bejegyzés" : $"{page + 1} / {pages} oldal • {entries.Count} bejegyzés";
        LogPrev.IsEnabled = page > 0; LogNext.IsEnabled = page + 1 < pages;
    }
    private void RefreshLog(object sender, RoutedEventArgs e) { page = 0; Reload(); }
    private void PreviousPage(object sender, RoutedEventArgs e) { page--; Render(); }
    private void NextPage(object sender, RoutedEventArgs e) { page++; Render(); }
    private void ClearLog(object sender, RoutedEventArgs e)
    {
        if (store?.Clear() != true) { LogStatus.Text = "A napló törlése nem menthető. A korábbi fájl megmaradt."; return; }
        page = 0; Reload(); LogStatus.Text = "Csak az API-napló törölve; a hozzáférések, csomagok és keret megmaradtak.";
    }
    private void OpenFolder(object sender, RoutedEventArgs e)
    {
        if (store == null) return;
        try { string folder = Path.GetDirectoryName(store.FilePath)!; Directory.CreateDirectory(folder); using var process = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception) { LogStatus.Text = "A mappa nem nyitható meg automatikusan. Az elérési út alul látható."; }
    }
}
