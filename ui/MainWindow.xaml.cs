using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MacBridgeUI;

public partial class MainWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const string DefaultWinIp = "10.13.37.2";
    private const string DefaultPeerMacIp = "10.13.37.1";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private readonly CancellationTokenSource _cts = new();
    private readonly string _baseDir;
    private readonly SpeedTestService _speedService;
    private readonly DispatcherTimer _progressTimer;

    private Process? _bridgeProc;
    private volatile bool _isBusy;
    private volatile bool _isConnecting;
    private volatile bool _isInstallingDriver;
    private volatile bool _isDisconnecting;
    private int _stateEpoch;
    private bool _usbDetected;
    private bool _isWinUsbBound;
    private bool _allowImmediateExit;
    private bool _isClosePromptOpen;

    private int _progressSeq;
    private double _progressTarget;
    private double _progressCeil;

    private string _winIp = DefaultWinIp;
    private string _peerMacIp = DefaultPeerMacIp;
    private string _lastPeerMac = string.Empty;
    private string _lastUsbSpeed = "--";
    private int _lastUsbMbps;
    private bool _lastBridgeUp;
    private int _missingUsbTicks;

    private static readonly SolidColorBrush BrushGreen = Freeze(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush BrushBlue = Freeze(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly SolidColorBrush BrushAmber = Freeze(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush BrushSlate = Freeze(Color.FromRgb(0x47, 0x55, 0x69));

    private static readonly Regex StatRx = new(@"RX_frames=(\d+)\s+TX_frames=(\d+)\s+peerMAC=(\S+)", RegexOptions.Compiled);
    private static readonly Regex MacRx = new(@"Learned peer MAC:\s*([0-9a-fA-F:]+)", RegexOptions.Compiled);
    private static readonly Regex TunnelPairRx = new(@"Tunnel UP\s+\((\d+\.\d+\.\d+\.\d+)\s+<->\s+(\d+\.\d+\.\d+\.\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReadyIpRx = new(@"\[READY\]\s+Windows IP:\s*(\d+\.\d+\.\d+\.\d+)/24", RegexOptions.Compiled);

    private static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public MainWindow()
    {
        InitializeComponent();
        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        if (!File.Exists(Path.Combine(_baseDir, "MacBridge.exe")) && File.Exists(@"C:\MacBridge\MacBridge.exe"))
        {
            _baseDir = @"C:\MacBridge";
        }

        _speedService = new SpeedTestService(() => (_lastUsbSpeed, _lastUsbMbps, _lastBridgeUp));
        _speedService.StartServer();

        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _progressTimer.Tick += (_, _) =>
        {
            if (ActionProgressPanel.Visibility != Visibility.Visible) return;
            if (_progressTarget < _progressCeil)
            {
                double next = Math.Min(_progressCeil, _progressTarget + 1.4);
                AnimateProgressTo(next);
            }
        };

        I18n.LanguageChanged += () => Dispatcher.BeginInvoke(() =>
        {
            ApplyLocalizedChrome();
            _ = RefreshOnceAsync();
        });

        SourceInitialized += (_, _) => ApplyWindowDarkTheme();
        ApplyLocalizedChrome();
        AppendLog($"[UI] Working dir: {_baseDir}");
        _ = Task.Run(EnsureFirewallRuleAsync);
        _ = RunBackgroundProbeLoopAsync(_cts.Token);
    }

    private void ApplyWindowDarkTheme()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private static void EnsureFirewallRuleAsync()
    {
        try
        {
            var checkPsi = new ProcessStartInfo("netsh", "advfirewall firewall show rule name=\"MacBridge SpeedTest 19876\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var chk = Process.Start(checkPsi);
            chk?.WaitForExit(3000);
            if (chk is { ExitCode: 0 }) return;

            var addPsi = new ProcessStartInfo(
                "netsh",
                "advfirewall firewall add rule name=\"MacBridge SpeedTest 19876\" dir=in action=allow protocol=TCP localport=19876 profile=any")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var add = Process.Start(addPsi);
            add?.WaitForExit(3000);
        }
        catch { }
    }

    private void ApplyLocalizedChrome()
    {
        BtnMin.ToolTip = I18n.T("app_min");
        BtnClose.ToolTip = I18n.T("app_close");
        BtnConnect.Content = I18n.T("btn_connect");
        BtnStop.Content = I18n.T("btn_stop");
        UpdateDriverActionButton();
        LblCardPort.Text = I18n.T("card_port");
        LblCardAddr.Text = I18n.T("card_addr");
        LblCardTelemetry.Text = I18n.T("card_telemetry");
        BtnCopySmb.Content = I18n.T("btn_copy_smb");
        BtnSpeedTest.Content = I18n.T("btn_speed_test");
        LblLogTitle.Text = I18n.T("log_title");
        BtnClearLog.Content = I18n.T("btn_clear");
        TxtAddrPair.Text = $"{_winIp}  <->  {_peerMacIp}";
        TxtSmbHint.Text = I18n.TF("smb_hint", _winIp);
        BtnLang.Content = I18n.Current == AppLang.Zh ? "EN" : "中文";
        TxtPeerMac.Text = string.IsNullOrEmpty(_lastPeerMac)
            ? I18n.T("peer_unknown")
            : I18n.TF("peer_mac", _lastPeerMac);
    }

    private void UpdateDriverActionButton()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateDriverActionButton);
            return;
        }

        if (_isWinUsbBound)
        {
            BtnRestore.Content = I18n.T("btn_restore");
            BtnRestore.Style = (Style)FindResource("DangerOutlineBtn");
            BtnRestore.IsEnabled = !_isBusy && !_isConnecting;
        }
        else
        {
            BtnRestore.Content = I18n.T("btn_install_drv");
            BtnRestore.Style = (Style)FindResource("GhostBtn");
            BtnRestore.IsEnabled = _usbDetected && !_isBusy && !_isConnecting;
        }
    }

    private void AnimateProgressTo(double targetValue, int durationMs = 380)
    {
        double clamped = Math.Clamp(targetValue, 0, 100);
        _progressTarget = clamped;
        var anim = new DoubleAnimation
        {
            To = clamped,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ActionProgressBar.BeginAnimation(RangeBase.ValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private void SetProgress(bool visible, double value = 0, string hint = "", double? ceil = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetProgress(visible, value, hint, ceil));
            return;
        }

        _progressSeq++;
        if (!visible)
        {
            _progressTimer.Stop();
            _progressCeil = 0;
            _progressTarget = 0;
            ActionProgressBar.BeginAnimation(RangeBase.ValueProperty, null);
            ActionProgressBar.Value = 0;
            ActionProgressPanel.Visibility = Visibility.Collapsed;
            TxtProgressHint.Text = string.Empty;
            return;
        }

        if (ActionProgressPanel.Visibility != Visibility.Visible)
        {
            ActionProgressBar.BeginAnimation(RangeBase.ValueProperty, null);
            ActionProgressBar.Value = 0;
            _progressTarget = 0;
            ActionProgressPanel.Visibility = Visibility.Visible;
        }

        _progressCeil = Math.Clamp(ceil ?? value, 0, 100);
        TxtProgressHint.Text = hint;
        AnimateProgressTo(Math.Max(_progressTarget, value));
        if (_progressCeil > _progressTarget) _progressTimer.Start();
        else _progressTimer.Stop();
    }

    private async Task CompleteProgressAndHideAsync(string doneHint, int holdMs = 480)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(() => CompleteProgressAndHideAsync(doneHint, holdMs)).Task.Unwrap();
            return;
        }

        _progressTimer.Stop();
        _progressCeil = 100;
        int seq = ++_progressSeq;
        if (ActionProgressPanel.Visibility == Visibility.Visible)
        {
            TxtProgressHint.Text = doneHint;
            AnimateProgressTo(100, 300);
            try { await Task.Delay(holdMs, _cts.Token); } catch (OperationCanceledException) { return; }
            if (seq == _progressSeq)
            {
                SetProgress(false);
            }
        }
    }

    private string ExePath(string name) => Path.Combine(_baseDir, name);

    private int BumpEpoch() => Interlocked.Increment(ref _stateEpoch);

    private bool HasExternalMacBridge(out int pid)
    {
        pid = 0;
        int myChildPid = _bridgeProc is { HasExited: false } ? _bridgeProc.Id : -1;
        foreach (var p in Process.GetProcessesByName("MacBridge"))
        {
            bool match = false;
            int id = 0;
            try
            {
                id = p.Id;
                match = id != myChildPid && !p.HasExited;
            }
            catch { }
            finally { p.Dispose(); }

            if (match)
            {
                pid = id;
                return true;
            }
        }
        return false;
    }

    private static (string winIp, string peerIp)? DetectActiveTunnelIps()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!ni.Name.Equals("MacBridge", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] b = ua.Address.GetAddressBytes();
                    if (b.Length == 4 && b[3] == 2)
                    {
                        return ($"{b[0]}.{b[1]}.{b[2]}.2", $"{b[0]}.{b[1]}.{b[2]}.1");
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private void SetTunnelIps(string winIp, string peerIp)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetTunnelIps(winIp, peerIp));
            return;
        }
        if (_winIp == winIp && _peerMacIp == peerIp) return;
        _winIp = winIp;
        _peerMacIp = peerIp;
        TxtAddrPair.Text = $"{_winIp}  <->  {_peerMacIp}";
        TxtSmbHint.Text = I18n.TF("smb_hint", _winIp);
    }

    private async Task<(UsbAppleStatus usb, bool childRunning, bool extRunning, int extPid, double? rttMs, (string winIp, string peerIp)? activeIps)> CollectSnapshotAsync(CancellationToken ct)
    {
        var usbTask = Task.Run(() =>
        {
            var usb = UsbProbe.DetectAppleNcm();
            bool childRunning = _bridgeProc is { HasExited: false };
            bool extRunning = HasExternalMacBridge(out int extPid);
            var activeIps = (childRunning || extRunning) ? DetectActiveTunnelIps() : null;
            return (usb, childRunning, extRunning, extPid, activeIps);
        }, ct);

        var (usb, childRunning, extRunning, extPid, activeIps) = await usbTask;
        string targetPeer = activeIps?.peerIp ?? Volatile.Read(ref _peerMacIp);
        double? rtt = (usb.Detected && (childRunning || extRunning))
            ? await SpeedTestService.ProbeLatencyAsync(targetPeer, ct)
            : null;

        return (usb, childRunning, extRunning, extPid, rtt, activeIps);
    }

    private async Task RunBackgroundProbeLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_isBusy && !_isDisconnecting)
            {
                try
                {
                    int epoch = Volatile.Read(ref _stateEpoch);
                    var snap = await CollectSnapshotAsync(ct);
                    if (!_isBusy && !_isDisconnecting && !ct.IsCancellationRequested &&
                        epoch == Volatile.Read(ref _stateEpoch))
                    {
                        ApplyStatusSnapshot(snap.usb, snap.childRunning, snap.extRunning, snap.extPid, snap.rttMs, snap.activeIps);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
            try { await Task.Delay(1800, ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RefreshOnceAsync()
    {
        if (_isBusy || _isDisconnecting) return;
        try
        {
            int epoch = Volatile.Read(ref _stateEpoch);
            var snap = await CollectSnapshotAsync(_cts.Token);
            if (!_isBusy && !_isDisconnecting && epoch == Volatile.Read(ref _stateEpoch))
            {
                ApplyStatusSnapshot(snap.usb, snap.childRunning, snap.extRunning, snap.extPid, snap.rttMs, snap.activeIps);
            }
        }
        catch { }
    }

    private void ApplyStatusSnapshot(UsbAppleStatus usb, bool childRunning, bool extRunning, int extPid, double? rttMs, (string winIp, string peerIp)? activeIps)
    {
        if (_isDisconnecting) return;
        _lastUsbSpeed = usb.UsbSpeedLabel;
        _lastUsbMbps = usb.UsbSpeedMbps;
        _lastBridgeUp = childRunning || extRunning;
        _usbDetected = usb.Detected;
        _isWinUsbBound = usb.Detected && usb.Service.Equals("WinUSB", StringComparison.OrdinalIgnoreCase);

        if ((childRunning || extRunning) && activeIps is { } detectedIps)
        {
            SetTunnelIps(detectedIps.winIp, detectedIps.peerIp);
        }
        else if (!childRunning && !extRunning && !_isConnecting && !_isInstallingDriver)
        {
            SetTunnelIps(DefaultWinIp, DefaultPeerMacIp);
        }

        if (!usb.Detected)
        {
            TxtDevice.Text = I18n.T("dev_none");
            TxtDriver.Text = I18n.T("drv_none");
            TxtUsbSpeed.Text = I18n.T("speed_unknown");
        }
        else
        {
            string label = string.IsNullOrWhiteSpace(usb.FriendlyName) ? "MacBook USB-C Gadget" : usb.FriendlyName;
            TxtDevice.Text = label;
            string svc = string.IsNullOrWhiteSpace(usb.Service) ? I18n.T("drv_unbound") : usb.Service;
            TxtDriver.Text = _isWinUsbBound ? I18n.T("drv_ready") : I18n.TF("drv_need", svc);
            TxtUsbSpeed.Text = string.IsNullOrWhiteSpace(usb.UsbSpeedLabel) || usb.UsbSpeedLabel == "--"
                ? I18n.T("speed_unknown")
                : I18n.TF("speed_label", usb.UsbSpeedLabel);
        }

        TxtLatency.Text = rttMs is { } ms
            ? $"Pair 0 · RTT {ms:F2} ms"
            : "Pair 0 · RTT -- ms";

        UpdateDriverActionButton();

        if (childRunning || extRunning)
        {
            if (!_isInstallingDriver && !usb.Detected)
            {
                if (++_missingUsbTicks >= 2 && !_isBusy)
                {
                    _missingUsbTicks = 0;
                    _ = HandleUnexpectedUsbDisconnectAsync();
                    return;
                }
            }
            else
            {
                _missingUsbTicks = 0;
            }
        }
        else
        {
            _missingUsbTicks = 0;
        }

        if (childRunning)
        {
            if (_isInstallingDriver || _isConnecting)
            {
                if (rttMs.HasValue)
                {
                    MarkTunnelConnected();
                }
                return;
            }
            StatusDot.Fill = BrushGreen;
            TxtHeadline.Text = I18n.TF("st_connected", _winIp);
            TxtSubline.Text = I18n.TF("st_connected_sub", _winIp);
            BtnConnect.IsEnabled = false;
            BtnStop.IsEnabled = true;
            UpdateDriverActionButton();
            if (ActionProgressPanel.Visibility == Visibility.Visible)
            {
                _ = CompleteProgressAndHideAsync(I18n.TF("st_connected", _winIp));
            }
            return;
        }

        if (extRunning)
        {
            _isConnecting = false;
            _isInstallingDriver = false;
            StatusDot.Fill = BrushGreen;
            TxtHeadline.Text = I18n.TF("st_ext", extPid);
            TxtSubline.Text = I18n.TF("st_ext_sub", _winIp);
            BtnConnect.IsEnabled = false;
            BtnStop.IsEnabled = true;
            UpdateDriverActionButton();
            SetProgress(false);
            return;
        }

        if (_isConnecting || _isInstallingDriver)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_lastPeerMac) || TxtStats.Text != "RX 0  /  TX 0")
        {
            _lastPeerMac = string.Empty;
            TxtStats.Text = "RX 0  /  TX 0";
            TxtPeerMac.Text = I18n.T("peer_unknown");
        }

        BtnStop.IsEnabled = false;
        BtnConnect.IsEnabled = true;
        UpdateDriverActionButton();

        if (!usb.Detected)
        {
            StatusDot.Fill = BrushSlate;
            TxtHeadline.Text = I18n.T("st_wait");
            TxtSubline.Text = I18n.T("st_wait_sub");
        }
        else if (_isWinUsbBound)
        {
            StatusDot.Fill = BrushBlue;
            TxtHeadline.Text = I18n.T("st_ready");
            TxtSubline.Text = I18n.TF("st_ready_sub", usb.HardwareId);
        }
        else
        {
            StatusDot.Fill = BrushAmber;
            TxtHeadline.Text = I18n.T("st_need_drv");
            TxtSubline.Text = I18n.T("st_need_drv_sub");
        }
    }

    private void MarkTunnelConnected()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(MarkTunnelConnected);
            return;
        }

        bool wasTransitioning = _isConnecting || _isInstallingDriver;
        _isConnecting = false;
        _isInstallingDriver = false;
        StatusDot.Fill = BrushGreen;
        TxtHeadline.Text = I18n.TF("st_connected", _winIp);
        TxtSubline.Text = I18n.TF("st_connected_sub", _winIp);
        BtnConnect.IsEnabled = false;
        BtnStop.IsEnabled = true;
        UpdateDriverActionButton();

        if (wasTransitioning || ActionProgressPanel.Visibility == Visibility.Visible)
        {
            _ = CompleteProgressAndHideAsync(I18n.TF("st_connected", _winIp));
        }
    }

    private void AppendLog(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => AppendLog(line));
            return;
        }
        if (TxtLog.Text.Length > 32000)
        {
            TxtLog.Text = TxtLog.Text[^16000..];
        }
        TxtLog.AppendText(line + Environment.NewLine);
        TxtLog.ScrollToEnd();
    }

    private void ParseBridgeLine(string line)
    {
        var mStat = StatRx.Match(line);
        if (mStat.Success)
        {
            string rx = mStat.Groups[1].Value;
            string tx = mStat.Groups[2].Value;
            Dispatcher.BeginInvoke(() => TxtStats.Text = $"RX {rx}  /  TX {tx}");
            if (_isConnecting || _isInstallingDriver)
            {
                MarkTunnelConnected();
            }
            return;
        }
        var mMac = MacRx.Match(line);
        if (mMac.Success)
        {
            string mac = mMac.Groups[1].Value;
            _lastPeerMac = mac;
            Dispatcher.BeginInvoke(() => TxtPeerMac.Text = I18n.TF("peer_mac", mac));
        }

        if (line.Contains("[DRIVER] Preparing WinUSB", StringComparison.Ordinal) ||
            line.Contains("[DRIVER] Generating WinUSB", StringComparison.Ordinal))
        {
            _isInstallingDriver = true;
            SetProgress(true, 32, I18n.T("prog_installing"), ceil: 48);
        }
        else if (line.Contains("[DRIVER] Installing WinUSB", StringComparison.Ordinal))
        {
            _isInstallingDriver = true;
            Dispatcher.BeginInvoke(() =>
            {
                StatusDot.Fill = BrushBlue;
                TxtHeadline.Text = I18n.T("st_binding");
                TxtSubline.Text = I18n.T("st_binding_sub");
            });
            SetProgress(true, 52, I18n.T("prog_installing"), ceil: 70);
        }
        else if (line.Contains("[OK] WinUSB", StringComparison.Ordinal))
        {
            _isInstallingDriver = false;
            SetProgress(true, 76, I18n.T("prog_connecting"), ceil: 85);
        }
        else if (line.Contains("[USB] Cable disconnected", StringComparison.Ordinal))
        {
            BeginUsbUnplugTransition();
        }
        else if (line.Contains("[USB]", StringComparison.Ordinal) ||
                 line.Contains("[NCM]", StringComparison.Ordinal) ||
                 line.Contains("[TIP]   Mac Finder", StringComparison.Ordinal))
        {
            _isInstallingDriver = false;
            SetProgress(true, 88, I18n.T("prog_connecting"), ceil: 95);
        }
        else if (TunnelPairRx.Match(line) is { Success: true } mPair)
        {
            SetTunnelIps(mPair.Groups[1].Value, mPair.Groups[2].Value);
            MarkTunnelConnected();
        }
        else if (ReadyIpRx.Match(line) is { Success: true } mReady)
        {
            string winIp = mReady.Groups[1].Value;
            int lastDot = winIp.LastIndexOf('.');
            string peerIp = lastDot > 0 ? winIp[..lastDot] + ".1" : DefaultPeerMacIp;
            SetTunnelIps(winIp, peerIp);
            MarkTunnelConnected();
        }
        else if (line.Contains("[READY] Windows IP", StringComparison.Ordinal) ||
                 line.Contains("Tunnel UP", StringComparison.OrdinalIgnoreCase))
        {
            MarkTunnelConnected();
        }
    }

    private void BeginUsbUnplugTransition()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(BeginUsbUnplugTransition);
            return;
        }
        if (_isBusy || _isDisconnecting) return;

        BumpEpoch();
        _isDisconnecting = true;
        _isConnecting = false;
        _isInstallingDriver = false;
        _missingUsbTicks = 0;

        StatusDot.Fill = BrushAmber;
        TxtHeadline.Text = I18n.T("st_stopping");
        TxtSubline.Text = I18n.T("st_stopping_sub");
        BtnConnect.IsEnabled = false;
        BtnStop.IsEnabled = false;
        UpdateDriverActionButton();
        SetProgress(true, 62, I18n.T("prog_stopping"), ceil: 90);
    }

    private static void KillAnyMacBridge()
    {
        foreach (var p in Process.GetProcessesByName("MacBridge"))
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(true);
                    p.WaitForExit(3500);
                }
            }
            catch { }
            finally { p.Dispose(); }
        }

        // Wait briefly for OS process table teardown so stale PIDs are never polled.
        for (int i = 0; i < 8; i++)
        {
            var rem = Process.GetProcessesByName("MacBridge");
            bool anyAlive = false;
            foreach (var p in rem)
            {
                try { if (!p.HasExited) anyAlive = true; } catch { }
                finally { p.Dispose(); }
            }
            if (!anyAlive) break;
            Thread.Sleep(100);
        }
    }

    private async Task StopActiveBridgeAsync()
    {
        BumpEpoch();
        _isDisconnecting = true;
        Process? proc = _bridgeProc;
        _bridgeProc = null;

        await Task.Run(() =>
        {
            if (proc is not null)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                        proc.WaitForExit(3500);
                    }
                }
                catch { }
                finally { proc.Dispose(); }
            }
            KillAnyMacBridge();
        });
    }

    private async Task HandleUnexpectedUsbDisconnectAsync()
    {
        if (_isBusy || _isInstallingDriver || _isDisconnecting) return;
        bool wasConnecting = _isConnecting;
        BumpEpoch();
        _isBusy = true;
        _isDisconnecting = true;
        _isConnecting = false;
        AppendLog(wasConnecting
            ? "[UI] USB-C cable unplugged while connecting, aborting..."
            : "[UI] USB-C cable unplugged, disconnecting active tunnel...");
        StatusDot.Fill = BrushAmber;
        TxtHeadline.Text = I18n.T("st_stopping");
        TxtSubline.Text = I18n.T("st_stopping_sub");
        BtnConnect.IsEnabled = false;
        BtnStop.IsEnabled = false;
        UpdateDriverActionButton();
        SetProgress(true, 45, I18n.T("prog_stopping"), ceil: 85);

        await StopActiveBridgeAsync();
        await FinishProgressAndSyncSnapshotAsync(I18n.T("prog_stopped"), 260);
    }

    private async Task FinishProgressAndSyncSnapshotAsync(string doneHint, int holdMs = 340)
    {
        BumpEpoch();
        SetProgress(true, 92, doneHint, ceil: 98);
        try
        {
            var snap = await CollectSnapshotAsync(_cts.Token);
            _isDisconnecting = false;
            _isBusy = false;
            ApplyStatusSnapshot(snap.usb, snap.childRunning, snap.extRunning, snap.extPid, snap.rttMs, snap.activeIps);
        }
        catch
        {
            _isDisconnecting = false;
            _isBusy = false;
        }
        await CompleteProgressAndHideAsync(doneHint, holdMs);
    }

    private async Task RunToolProcessAsync(string exePath, string arguments, Action<string> onOutputLine)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = _baseDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, ev) =>
        {
            if (ev.Data is null) return;
            AppendLog(ev.Data);
            onOutputLine(ev.Data);
        };
        proc.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data)) AppendLog("[ERR] " + ev.Data.Trim());
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(_cts.Token);
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        string bridgeExe = ExePath("MacBridge.exe");
        if (!File.Exists(bridgeExe))
        {
            AppendLog($"[ERR] Missing executable: {bridgeExe}");
            return;
        }

        BumpEpoch();
        _isDisconnecting = false;
        _isConnecting = true;
        _isInstallingDriver = false;
        _missingUsbTicks = 0;
        BtnConnect.IsEnabled = false;
        UpdateDriverActionButton();
        StatusDot.Fill = BrushBlue;
        TxtHeadline.Text = I18n.T("prog_connecting");
        TxtSubline.Text = I18n.T("st_connecting_sub");
        SetProgress(true, 18, I18n.T("prog_connecting"), ceil: 30);

        if (_bridgeProc is not null || HasExternalMacBridge(out _))
        {
            AppendLog("[UI] Stopping existing MacBridge instance before connect...");
            await StopActiveBridgeAsync();
            _isDisconnecting = false;
            BumpEpoch();
        }

        _lastPeerMac = string.Empty;
        TxtStats.Text = "RX 0  /  TX 0";
        TxtPeerMac.Text = I18n.T("peer_learning");
        AppendLog("[UI] Starting MacBridge.exe --pair 0 --no-wait ...");
        SetProgress(true, 34, I18n.T("prog_connecting"), ceil: 68);

        var psi = new ProcessStartInfo
        {
            FileName = bridgeExe,
            Arguments = "--pair 0 --no-wait",
            WorkingDirectory = _baseDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, ev) =>
        {
            if (ev.Data is null) return;
            ParseBridgeLine(ev.Data);
            if (!ev.Data.StartsWith("[STAT]", StringComparison.Ordinal))
            {
                AppendLog(ev.Data);
            }
        };
        proc.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data)) AppendLog("[ERR] " + ev.Data);
        };
        proc.Exited += (_, _) =>
        {
            int code = -1;
            try { code = proc.ExitCode; } catch { }
            Dispatcher.BeginInvoke(async () =>
            {
                if (ReferenceEquals(_bridgeProc, proc))
                {
                    _bridgeProc = null;
                }
                proc.Dispose();

                AppendLog($"[UI] MacBridge exited (code {code})");
                if (!_isBusy)
                {
                    int epoch = BumpEpoch();
                    bool hadDisconnectProgress = _isDisconnecting || ActionProgressPanel.Visibility == Visibility.Visible;
                    _isConnecting = false;
                    _isInstallingDriver = false;
                    _isDisconnecting = false;
                    _missingUsbTicks = 0;
                    _lastPeerMac = string.Empty;
                    TxtStats.Text = "RX 0  /  TX 0";
                    TxtPeerMac.Text = I18n.T("peer_unknown");
                    TxtLatency.Text = "Pair 0 · RTT -- ms";
                    SetTunnelIps(DefaultWinIp, DefaultPeerMacIp);

                    var usb = await Task.Run(() => UsbProbe.DetectAppleNcm());
                    if (epoch != Volatile.Read(ref _stateEpoch) || _isBusy) return;

                    ApplyStatusSnapshot(usb, childRunning: false, extRunning: false, extPid: 0, rttMs: null, activeIps: null);
                    if (hadDisconnectProgress)
                    {
                        await CompleteProgressAndHideAsync(I18n.T("prog_stopped"), 240);
                    }
                    else
                    {
                        SetProgress(false);
                    }
                }
            });
        };

        try
        {
            _bridgeProc = proc;
            proc.Start();
            BtnStop.IsEnabled = true;
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await RefreshOnceAsync();
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_bridgeProc, proc)) _bridgeProc = null;
            proc.Dispose();
            _isConnecting = false;
            _isInstallingDriver = false;
            _isDisconnecting = false;
            SetProgress(false);
            AppendLog($"[ERR] Launch failed: {ex.Message}");
            BtnConnect.IsEnabled = true;
            UpdateDriverActionButton();
        }
    }

    private async void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("[UI] Stopping tunnel...");
        BumpEpoch();
        _isBusy = true;
        _isDisconnecting = true;
        _isConnecting = false;
        _isInstallingDriver = false;
        _missingUsbTicks = 0;
        BtnStop.IsEnabled = false;
        BtnConnect.IsEnabled = false;
        UpdateDriverActionButton();
        StatusDot.Fill = BrushAmber;
        TxtHeadline.Text = I18n.T("st_stopping");
        TxtSubline.Text = I18n.T("st_stopping_sub");
        SetProgress(true, 45, I18n.T("prog_stopping"), ceil: 85);

        await StopActiveBridgeAsync();
        await FinishProgressAndSyncSnapshotAsync(I18n.T("prog_stopped"), 300);
    }

    private async void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        if (_isWinUsbBound)
        {
            await RunRestoreDriverAsync(exitAfter: false);
        }
        else
        {
            await RunInstallDriverOnlyAsync();
        }
    }

    private async Task RunInstallDriverOnlyAsync()
    {
        string bridgeExe = ExePath("MacBridge.exe");
        if (!File.Exists(bridgeExe))
        {
            AppendLog($"[ERR] Missing executable: {bridgeExe}");
            return;
        }

        BumpEpoch();
        _isBusy = true;
        _isDisconnecting = false;
        _isInstallingDriver = true;
        BtnConnect.IsEnabled = false;
        BtnStop.IsEnabled = false;
        UpdateDriverActionButton();
        StatusDot.Fill = BrushBlue;
        TxtHeadline.Text = I18n.T("st_binding");
        TxtSubline.Text = I18n.T("st_binding_sub");
        SetProgress(true, 20, I18n.T("prog_installing"), ceil: 72);

        AppendLog("[UI] Installing WinUSB driver (MacBridge.exe --install-only --no-wait) ...");

        try
        {
            await RunToolProcessAsync(bridgeExe, "--install-only --no-wait", line =>
            {
                if (line.Contains("Preparing WinUSB", StringComparison.Ordinal))
                    SetProgress(true, 40, I18n.T("prog_installing"), ceil: 55);
                else if (line.Contains("Installing WinUSB", StringComparison.Ordinal))
                    SetProgress(true, 58, I18n.T("prog_installing"), ceil: 86);
                else if (line.Contains("[OK] WinUSB", StringComparison.Ordinal) || line.Contains("[DONE]", StringComparison.Ordinal))
                    SetProgress(true, 92, I18n.T("drv_install_ok"), ceil: 98);
            });
            _isInstallingDriver = false;
            await FinishProgressAndSyncSnapshotAsync(I18n.T("drv_install_ok"), 380);
        }
        catch (Exception ex)
        {
            _isInstallingDriver = false;
            _isBusy = false;
            AppendLog($"[ERR] Driver install failed: {ex.Message}");
            SetProgress(false);
            await RefreshOnceAsync();
        }
    }

    private async Task RunRestoreDriverAsync(bool exitAfter)
    {
        string restoreExe = ExePath("MacBridge-Restore.exe");
        if (!File.Exists(restoreExe))
        {
            AppendLog($"[ERR] Missing executable: {restoreExe}");
            if (exitAfter)
            {
                _allowImmediateExit = true;
                Application.Current.Shutdown();
            }
            return;
        }

        BumpEpoch();
        _isBusy = true;
        _isDisconnecting = false;
        _isConnecting = false;
        _isInstallingDriver = false;
        BtnConnect.IsEnabled = false;
        BtnStop.IsEnabled = false;
        UpdateDriverActionButton();
        StatusDot.Fill = BrushAmber;
        TxtHeadline.Text = I18n.T("st_restoring");
        TxtSubline.Text = I18n.T("st_restoring_sub");
        SetProgress(true, 18, I18n.T(exitAfter ? "prog_closing_restore" : "prog_stopping"), ceil: 34);

        AppendLog("[UI] Stopping tunnel and running MacBridge-Restore.exe ...");

        try
        {
            await StopActiveBridgeAsync();
            SetProgress(true, 38, I18n.T("prog_restoring"), ceil: 60);

            await RunToolProcessAsync(restoreExe, "--no-pause", line =>
            {
                if (line.Contains("pnputil", StringComparison.OrdinalIgnoreCase) || line.Contains("卸载", StringComparison.Ordinal))
                    SetProgress(true, 64, I18n.T("prog_restoring"), ceil: 82);
                else if (line.Contains("重新扫描", StringComparison.Ordinal) || line.Contains("Scanning", StringComparison.OrdinalIgnoreCase))
                    SetProgress(true, 86, I18n.T("prog_restoring"), ceil: 95);
            });

            if (exitAfter)
            {
                await CompleteProgressAndHideAsync(I18n.T("prog_restored"), 320);
                _isDisconnecting = false;
                _isBusy = false;
                _allowImmediateExit = true;
                Application.Current.Shutdown();
                return;
            }

            await FinishProgressAndSyncSnapshotAsync(I18n.T("prog_restored"), 340);
        }
        catch (Exception ex)
        {
            _isDisconnecting = false;
            _isBusy = false;
            AppendLog($"[ERR] Restore failed: {ex.Message}");
            SetProgress(false);
            if (exitAfter)
            {
                _allowImmediateExit = true;
                Application.Current.Shutdown();
            }
            else
            {
                await RefreshOnceAsync();
            }
        }
    }

    private void BtnCopySmb_Click(object sender, RoutedEventArgs e)
    {
        string smbUrl = $"smb://{_winIp}";
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(smbUrl, true);
                TxtSmbHint.Text = I18n.TF("smb_copied", smbUrl);
                AppendLog($"[UI] Copied {smbUrl}");
                return;
            }
            catch
            {
                Thread.Sleep(25);
            }
        }
        AppendLog("[ERR] Clipboard temporarily locked by another app.");
    }

    private void BtnSpeedTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var modal = new SpeedTestWindow(_peerMacIp, _winIp, _lastUsbSpeed)
            {
                Owner = this
            };
            modal.ShowDialog();
        }
        catch (Exception ex)
        {
            AppendLog($"[ERR] SpeedTestWindow error: {ex.Message}");
        }
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        I18n.SetLanguage(I18n.Current == AppLang.Zh ? AppLang.En : AppLang.Zh);
    }

    private void BtnGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/KSLengYu") { UseShellExecute = true });
        }
        catch { }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TxtLog.Clear();
    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private async Task HandleClosePromptAsync()
    {
        if (_isClosePromptOpen || _isBusy) return;
        _isClosePromptOpen = true;
        try
        {
            var dlg = new ActionDialogWindow(
                this,
                I18n.T("close_dlg_title"),
                I18n.T("close_dlg_msg"),
                I18n.T("close_btn_min"),
                I18n.T("close_btn_cancel"),
                dangerLabel: I18n.T("close_btn_quit"));
            dlg.ShowDialog();

            if (dlg.Choice == DialogChoice.Primary)
            {
                WindowState = WindowState.Minimized;
            }
            else if (dlg.Choice == DialogChoice.Danger)
            {
                await RunRestoreDriverAsync(exitAfter: true);
            }
        }
        finally
        {
            _isClosePromptOpen = false;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowImmediateExit)
        {
            e.Cancel = true;
            Dispatcher.BeginInvoke(() => _ = HandleClosePromptAsync());
            return;
        }

        _progressTimer.Stop();
        _cts.Cancel();
        _speedService.Dispose();
        try
        {
            if (_bridgeProc is { HasExited: false })
            {
                _bridgeProc.Kill(true);
            }
            _bridgeProc?.Dispose();
            _bridgeProc = null;
        }
        catch { }
        base.OnClosing(e);
    }
}
