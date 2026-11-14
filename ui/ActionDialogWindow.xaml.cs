using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MacBridgeUI;

public enum DialogChoice
{
    Cancel,
    Primary,
    Danger
}

public partial class ActionDialogWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public DialogChoice Choice { get; private set; } = DialogChoice.Cancel;

    public ActionDialogWindow(
        Window owner,
        string title,
        string message,
        string primaryLabel,
        string cancelLabel,
        string? dangerLabel = null)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        TxtTitle.Text = title;
        TxtMessage.Text = message;
        BtnPrimary.Content = primaryLabel;
        BtnCancel.Content = cancelLabel;

        if (!string.IsNullOrEmpty(dangerLabel))
        {
            BtnDanger.Content = dangerLabel;
            BtnDanger.Visibility = Visibility.Visible;
        }

        SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int round = 2;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        };
    }

    private void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        Choice = DialogChoice.Primary;
        DialogResult = true;
    }

    private void BtnDanger_Click(object sender, RoutedEventArgs e)
    {
        Choice = DialogChoice.Danger;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = DialogChoice.Cancel;
        DialogResult = false;
    }
}
