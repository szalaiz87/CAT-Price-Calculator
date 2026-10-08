using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CatPriceCalculator;

public partial class SecretEntry : UserControl
{
    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(nameof(Caption), typeof(string), typeof(SecretEntry), new PropertyMetadata(""));
    public string Caption { get => (string)GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public string Value { get => HiddenInput.Password; set => HiddenInput.Password = value; }
    private bool syncing;
    public event EventHandler? ValueChanged;
    public SecretEntry()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => { if (!IsVisible) HideValue(); };
        Loaded += (_, _) => { System.Windows.Automation.AutomationProperties.SetName(HiddenInput, Caption + ", rejtett"); System.Windows.Automation.AutomationProperties.SetName(VisibleInput, Caption + ", látható"); };
    }
    private void Changed()
    {
        int length = HiddenInput.Password.Length;
        PresenceText.Text = length == 0 ? "Nincs beírt érték" : $"Beírva: {length} karakter";
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }
    private void HiddenChanged(object sender, RoutedEventArgs e) { if (!syncing && PresenceText != null) Changed(); }
    private void VisibleChanged(object sender, TextChangedEventArgs e)
    {
        if (syncing || HiddenInput == null) return;
        syncing = true; HiddenInput.Password = VisibleInput.Text; syncing = false; Changed();
    }
    public void HideValue()
    {
        if (RevealButton == null) return;
        syncing = true; HiddenInput.Visibility = Visibility.Visible; VisibleInput.Visibility = Visibility.Collapsed;
        VisibleInput.Clear(); syncing = false;
        RevealIcon.Data = (Geometry)FindResource("EyeIcon"); RevealButton.ToolTip = "Érték megjelenítése";
        System.Windows.Automation.AutomationProperties.SetName(RevealButton, "Érték megjelenítése");
    }
    private void ToggleVisibility(object sender, RoutedEventArgs e)
    {
        if (VisibleInput.Visibility == Visibility.Visible) { HideValue(); HiddenInput.Focus(); return; }
        syncing = true; VisibleInput.Text = Value; syncing = false;
        HiddenInput.Visibility = Visibility.Collapsed; VisibleInput.Visibility = Visibility.Visible;
        RevealIcon.Data = (Geometry)FindResource("EyeOffIcon"); RevealButton.ToolTip = "Érték elrejtése";
        System.Windows.Automation.AutomationProperties.SetName(RevealButton, "Érték elrejtése");
        VisibleInput.Focus(); VisibleInput.CaretIndex = VisibleInput.Text.Length;
    }
}
