using System.Windows;

namespace MacBridgeUI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        I18n.LoadSavedLanguage();
        base.OnStartup(e);
        var win = new MainWindow();
        MainWindow = win;
        win.Show();
    }
}
