using System;
using System.IO;
using System.Windows;

namespace MacBridgeSetup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool isUninstall = false;
        string? explicitDir = null;

        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                isUninstall = true;
            }
            else if (e.Args[i].Equals("--dir", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                explicitDir = e.Args[++i];
            }
        }

        string? procPath = Environment.ProcessPath;
        if (!isUninstall && !string.IsNullOrEmpty(procPath) &&
            Path.GetFileName(procPath).Equals("Uninstall.exe", StringComparison.OrdinalIgnoreCase))
        {
            isUninstall = true;
        }

        var win = new MainWindow(isUninstall, explicitDir);
        MainWindow = win;
        win.Show();
    }
}
