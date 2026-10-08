using System.Diagnostics;
using System.Windows;
namespace CatPriceCalculator;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 3 && e.Args[0] == "--apply-update")
        {
            string? target = null, backup = null;
            bool replaced = false;
            try
            {
                target = Path.GetFullPath(e.Args[1]);
                if (Path.GetFileName(target) != "CAT-Price-Calculator.exe" || !int.TryParse(e.Args[2], out var pid)) throw new InvalidDataException();
                try { using var old = Process.GetProcessById(pid); await old.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
                catch (ArgumentException) { }
                backup = target + ".previous";
                File.Copy(target, backup, true);
                var ownExe = Environment.ProcessPath ?? throw new InvalidOperationException();
                for (int attempt = 0; ; attempt++)
                {
                    try { var incoming = target + ".update.tmp"; File.Copy(ownExe, incoming, true); File.Move(incoming, target, true); replaced = true; break; }
                    catch (IOException) when (attempt < 10) { await Task.Delay(500); }
                }
                using var restarted = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target)! }) ?? throw new InvalidOperationException();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException or System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                if (replaced && target != null && backup != null)
                {
                    try { File.Copy(backup, target, true); }
                    catch (Exception recovery) when (recovery is IOException or UnauthorizedAccessException) { }
                }
                MessageBox.Show("A frissítés nem fejezhető be. A letöltővel kézzel is frissíthet. A mentett árfolyamok megmaradtak.", "LS Germany – frissítés", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Shutdown();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
