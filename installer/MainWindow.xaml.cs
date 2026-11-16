using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace MacBridgeSetup;

public partial class MainWindow : Window
{
    private const string UninstallRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MacBridge";
    private const string RepoUrl = "https://github.com/KSLengYu/MacBridge";

    private static readonly string[] PayloadFiles =
    [
        "MacBridgeUI.exe",
        "MacBridge.exe",
        "MacBridge-Restore.exe",
        "wintun.dll"
    ];

    private readonly bool _isUninstallMode;
    private readonly string _uninstallDir;
    private bool _isWorking;
    private bool _isFinished;
    private string? _pendingSelfDeleteDir;

    public MainWindow(bool isUninstallMode, string? explicitDir)
    {
        InitializeComponent();
        _isUninstallMode = isUninstallMode;
        _uninstallDir = ResolveUninstallDirectory(explicitDir);

        string defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "MacBridge");
        TxtInstallDir.Text = defaultDir;

        if (_isUninstallMode)
        {
            ShowPage(PageUninstall);
            BtnPrimary.Style = (Style)Application.Current.Resources["DangerBtn"];
        }
        else
        {
            ShowPage(PageSetup);
        }

        ApplyLanguage();
        Closing += (_, _) => ScheduleSelfCleanupIfNeeded();
    }

    private static string ResolveUninstallDirectory(string? explicitDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir))
            return Path.GetFullPath(explicitDir);

        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(UninstallRegPath);
            if (key?.GetValue("InstallLocation") is string regDir && !string.IsNullOrWhiteSpace(regDir))
                return regDir;
        }
        catch { }

        return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    }

    private void ShowPage(UIElement target)
    {
        PageSetup.Visibility = ReferenceEquals(target, PageSetup) ? Visibility.Visible : Visibility.Collapsed;
        PageProgress.Visibility = ReferenceEquals(target, PageProgress) ? Visibility.Visible : Visibility.Collapsed;
        PageDone.Visibility = ReferenceEquals(target, PageDone) ? Visibility.Visible : Visibility.Collapsed;
        PageUninstall.Visibility = ReferenceEquals(target, PageUninstall) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyLanguage()
    {
        Title = InstallerI18n.T(_isUninstallMode ? "win_title_uninst" : "win_title");
        TxtWinTitle.Text = Title;
        TxtTopBadge.Text = InstallerI18n.T(_isUninstallMode ? "badge_uninst" : "badge_setup");
        BtnLang.Content = InstallerI18n.T("lang_btn");

        TxtHeroTitle.Text = InstallerI18n.T("hero_title");
        TxtHeroSub.Text = InstallerI18n.T("hero_sub");
        LblInstallDir.Text = InstallerI18n.T("lbl_dir");
        BtnBrowse.Content = InstallerI18n.T("btn_browse");
        LblOptions.Text = InstallerI18n.T("lbl_options");
        ChkDesktop.Content = InstallerI18n.T("chk_desktop");
        ChkStartMenu.Content = InstallerI18n.T("chk_startmenu");
        ChkLaunchAfter.Content = InstallerI18n.T("chk_launch");

        TxtUninstTitle.Text = InstallerI18n.T("uninst_title");
        TxtUninstSub.Text = InstallerI18n.T("uninst_sub");
        TxtUninstPath.Text = InstallerI18n.TF("uninst_path", _uninstallDir);
        ChkRestoreDriver.Content = InstallerI18n.T("chk_restore_drv");

        if (_isFinished)
        {
            if (_isUninstallMode)
            {
                TxtDoneTitle.Text = InstallerI18n.T("uninst_done_title");
                TxtDoneSub.Text = InstallerI18n.T("uninst_done_sub");
                TxtDonePath.Text = InstallerI18n.TF("uninst_path", _uninstallDir);
                TxtDoneTip.Text = string.Empty;
                BtnSecondary.Visibility = Visibility.Collapsed;
                BtnPrimary.Style = (Style)Application.Current.Resources["PrimaryBtn"];
                BtnPrimary.Content = InstallerI18n.T("btn_close");
            }
            else
            {
                TxtDoneTitle.Text = InstallerI18n.T("done_title");
                TxtDoneSub.Text = InstallerI18n.T("done_sub");
                TxtDonePath.Text = InstallerI18n.TF("done_path", TxtInstallDir.Text.Trim());
                TxtDoneTip.Text = InstallerI18n.T("done_tip");
                BtnSecondary.Visibility = Visibility.Collapsed;
                BtnPrimary.Content = InstallerI18n.T("btn_finish");
            }
        }
        else if (_isWorking)
        {
            TxtProgTitle.Text = InstallerI18n.T(_isUninstallMode ? "uninst_running" : "inst_title");
            TxtProgSub.Text = InstallerI18n.T(_isUninstallMode ? "uninst_sub" : "inst_sub");
            BtnSecondary.Content = InstallerI18n.T("btn_cancel");
            BtnPrimary.Content = InstallerI18n.T(_isUninstallMode ? "btn_uninstall" : "btn_install");
        }
        else
        {
            BtnSecondary.Content = InstallerI18n.T("btn_cancel");
            BtnPrimary.Content = InstallerI18n.T(_isUninstallMode ? "btn_uninstall" : "btn_install");
        }
    }

    private void AppendStep(string msg, double progress)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
            TxtLog.ScrollToEnd();
            ProgBar.Value = progress;
        });
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        InstallerI18n.Toggle();
        ApplyLanguage();
    }

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (!_isWorking) Close();
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        if (!_isWorking) Close();
    }

    private void BtnAuthor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch { }
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = InstallerI18n.T("browse_title"),
            InitialDirectory = Directory.Exists(TxtInstallDir.Text)
                ? TxtInstallDir.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };
        if (dlg.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dlg.FolderName))
        {
            string picked = dlg.FolderName.TrimEnd(Path.DirectorySeparatorChar);
            if (!Path.GetFileName(picked).Equals("MacBridge", StringComparison.OrdinalIgnoreCase))
                picked = Path.Combine(picked, "MacBridge");
            TxtInstallDir.Text = picked;
        }
    }

    private async void BtnPrimary_Click(object sender, RoutedEventArgs e)
    {
        if (_isWorking) return;
        if (_isFinished)
        {
            if (!_isUninstallMode && ChkLaunchAfter.IsChecked == true)
            {
                string uiExe = Path.Combine(TxtInstallDir.Text.Trim(), "MacBridgeUI.exe");
                if (File.Exists(uiExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(uiExe)
                        {
                            WorkingDirectory = Path.GetDirectoryName(uiExe)!,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            Close();
            return;
        }

        if (_isUninstallMode)
            await RunUninstallAsync();
        else
            await RunInstallAsync();
    }

    private async Task RunInstallAsync()
    {
        string targetDir = TxtInstallDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show(this, InstallerI18n.T("err_dir_empty"), InstallerI18n.T("err_title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool mkDesktop = ChkDesktop.IsChecked == true;
        bool mkStartMenu = ChkStartMenu.IsChecked == true;

        _isWorking = true;
        BtnPrimary.IsEnabled = false;
        BtnSecondary.IsEnabled = false;
        BtnClose.IsEnabled = false;
        ShowPage(PageProgress);
        ApplyLanguage();

        try
        {
            await Task.Run(() =>
            {
                AppendStep(InstallerI18n.T("step_stop"), 12);
                StopRunningMacBridgeProcesses();

                Directory.CreateDirectory(targetDir);
                var asm = Assembly.GetExecutingAssembly();
                string[] allNames = asm.GetManifestResourceNames();

                double step = 12;
                double perFile = 48.0 / PayloadFiles.Length;
                foreach (string fileName in PayloadFiles)
                {
                    AppendStep(InstallerI18n.TF("step_extract", fileName), step);
                    string? resName = allNames.FirstOrDefault(
                        n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
                    if (resName is null)
                        throw new FileNotFoundException(InstallerI18n.TF("err_missing_payload", fileName));

                    string dst = Path.Combine(targetDir, fileName);
                    using Stream src = asm.GetManifestResourceStream(resName)!;
                    using FileStream fs = File.Create(dst);
                    src.CopyTo(fs);
                    step += perFile;
                }

                AppendStep(InstallerI18n.T("step_uninst_exe"), 68);
                string uninstExe = Path.Combine(targetDir, "Uninstall.exe");
                string? selfExe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(selfExe) && File.Exists(selfExe) &&
                    !selfExe.Equals(uninstExe, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(selfExe, uninstExe, overwrite: true);
                }

                if (mkDesktop || mkStartMenu)
                {
                    AppendStep(InstallerI18n.T("step_shortcuts"), 82);
                    CreateShortcuts(targetDir, uninstExe, mkDesktop, mkStartMenu);
                }

                AppendStep(InstallerI18n.T("step_registry"), 94);
                WriteUninstallRegistry(targetDir, uninstExe);
                AppendStep(InstallerI18n.T("step_done"), 100);
            });

            _isFinished = true;
            ShowPage(PageDone);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, InstallerI18n.T("err_title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            ShowPage(PageSetup);
        }
        finally
        {
            _isWorking = false;
            BtnPrimary.IsEnabled = true;
            BtnSecondary.IsEnabled = true;
            BtnClose.IsEnabled = true;
            ApplyLanguage();
        }
    }

    private async Task RunUninstallAsync()
    {
        bool restoreDriver = ChkRestoreDriver.IsChecked == true;
        _isWorking = true;
        BtnPrimary.IsEnabled = false;
        BtnSecondary.IsEnabled = false;
        BtnClose.IsEnabled = false;
        ShowPage(PageProgress);
        ApplyLanguage();

        try
        {
            await Task.Run(() =>
            {
                AppendStep(InstallerI18n.T("step_stop"), 15);
                StopRunningMacBridgeProcesses();

                if (restoreDriver)
                {
                    string restoreExe = Path.Combine(_uninstallDir, "MacBridge-Restore.exe");
                    if (File.Exists(restoreExe))
                    {
                        AppendStep(InstallerI18n.T("step_restore"), 40);
                        using var p = Process.Start(new ProcessStartInfo(restoreExe, "--no-pause")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = _uninstallDir
                        });
                        p?.WaitForExit(15000);
                    }
                }

                AppendStep(InstallerI18n.T("step_clean_files"), 70);
                RemoveShortcuts();

                try
                {
                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .DeleteSubKeyTree(UninstallRegPath, throwOnMissingSubKey: false);
                }
                catch { }

                foreach (string name in PayloadFiles)
                {
                    string f = Path.Combine(_uninstallDir, name);
                    try { if (File.Exists(f)) File.Delete(f); } catch { }
                }
                string drvCache = Path.Combine(_uninstallDir, "drv_cache");
                try { if (Directory.Exists(drvCache)) Directory.Delete(drvCache, recursive: true); } catch { }

                _pendingSelfDeleteDir = _uninstallDir;
                AppendStep(InstallerI18n.T("step_done"), 100);
            });

            _isFinished = true;
            ShowPage(PageDone);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, InstallerI18n.T("err_title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            ShowPage(PageUninstall);
        }
        finally
        {
            _isWorking = false;
            BtnPrimary.IsEnabled = true;
            BtnSecondary.IsEnabled = true;
            BtnClose.IsEnabled = true;
            ApplyLanguage();
        }
    }

    private static void StopRunningMacBridgeProcesses()
    {
        foreach (string procName in new[] { "MacBridgeUI", "MacBridge" })
        {
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(3500);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
    }

    private static string PsEscape(string s) => s.Replace("'", "''");

    private static void CreateShortcuts(string installDir, string uninstExe, bool desktop, bool startMenu)
    {
        string uiExe = Path.Combine(installDir, "MacBridgeUI.exe");
        string restoreExe = Path.Combine(installDir, "MacBridge-Restore.exe");
        var sb = new StringBuilder();
        sb.AppendLine("$ws = New-Object -ComObject WScript.Shell");

        if (desktop)
        {
            string lnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                "MacBridge.lnk");
            sb.AppendLine($"$s = $ws.CreateShortcut('{PsEscape(lnk)}'); $s.TargetPath = '{PsEscape(uiExe)}'; $s.WorkingDirectory = '{PsEscape(installDir)}'; $s.IconLocation = '{PsEscape(uiExe)},0'; $s.Save()");
        }

        if (startMenu)
        {
            string smDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "MacBridge");
            Directory.CreateDirectory(smDir);

            string lnkApp = Path.Combine(smDir, "MacBridge.lnk");
            string lnkRes = Path.Combine(smDir, "Restore Apple USB Driver.lnk");
            string lnkUninst = Path.Combine(smDir, "Uninstall MacBridge.lnk");

            sb.AppendLine($"$s = $ws.CreateShortcut('{PsEscape(lnkApp)}'); $s.TargetPath = '{PsEscape(uiExe)}'; $s.WorkingDirectory = '{PsEscape(installDir)}'; $s.IconLocation = '{PsEscape(uiExe)},0'; $s.Save()");
            sb.AppendLine($"$s = $ws.CreateShortcut('{PsEscape(lnkRes)}'); $s.TargetPath = '{PsEscape(restoreExe)}'; $s.WorkingDirectory = '{PsEscape(installDir)}'; $s.IconLocation = '{PsEscape(restoreExe)},0'; $s.Save()");
            sb.AppendLine($"$s = $ws.CreateShortcut('{PsEscape(lnkUninst)}'); $s.TargetPath = '{PsEscape(uninstExe)}'; $s.Arguments = '--uninstall'; $s.WorkingDirectory = '{PsEscape(installDir)}'; $s.IconLocation = '{PsEscape(uninstExe)},0'; $s.Save()");
        }

        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(sb.ToString()));
        using var p = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
        p?.WaitForExit(10000);
    }

    private static void RemoveShortcuts()
    {
        try
        {
            string desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                "MacBridge.lnk");
            if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            string userDesktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "MacBridge.lnk");
            if (File.Exists(userDesktopLnk)) File.Delete(userDesktopLnk);

            string smDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "MacBridge");
            if (Directory.Exists(smDir)) Directory.Delete(smDir, recursive: true);
        }
        catch { }
    }

    private static void WriteUninstallRegistry(string installDir, string uninstExe)
    {
        long totalBytes = 0;
        foreach (string f in Directory.GetFiles(installDir))
        {
            try { totalBytes += new FileInfo(f).Length; } catch { }
        }

        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .CreateSubKey(UninstallRegPath);
        key.SetValue("DisplayName", "MacBridge");
        key.SetValue("DisplayVersion", "1.0.1");
        key.SetValue("Publisher", "KSLengYu");
        key.SetValue("URLInfoAbout", RepoUrl);
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", Path.Combine(installDir, "MacBridgeUI.exe"));
        key.SetValue("UninstallString", $"\"{uninstExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{uninstExe}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)Math.Max(1, totalBytes / 1024), RegistryValueKind.DWord);
    }

    private void ScheduleSelfCleanupIfNeeded()
    {
        if (!_isUninstallMode || !_isFinished || string.IsNullOrEmpty(_pendingSelfDeleteDir))
            return;

        try
        {
            string dir = _pendingSelfDeleteDir.Replace("\"", string.Empty);
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{dir}\"")
            {
                WorkingDirectory = Environment.SystemDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }
}
