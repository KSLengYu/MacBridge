using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MacBridgeUI;

public partial class SpeedTestWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly SolidColorBrush BorderIdle = FreezeBrush(0x1F, 0x29, 0x37);
    private static readonly SolidColorBrush BorderDown = FreezeBrush(0x10, 0xB9, 0x81);
    private static readonly SolidColorBrush BorderUp = FreezeBrush(0x3B, 0x82, 0xF6);

    private readonly string _peerIp;
    private readonly string _winIp;
    private CancellationTokenSource? _runCts;

    private static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public SpeedTestWindow(string peerIp, string winIp, string usbSpeedLabel)
    {
        InitializeComponent();
        _peerIp = peerIp;
        _winIp = winIp;
        TxtLinkBadge.Text = string.IsNullOrWhiteSpace(usbSpeedLabel) || usbSpeedLabel == "--"
            ? $"{winIp} <-> {peerIp}"
            : usbSpeedLabel;

        ApplyTexts();
        SourceInitialized += (_, _) => ApplyDarkChrome();
    }

    private void ApplyDarkChrome()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int dark = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        int round = 2;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
    }

    private void ApplyTexts()
    {
        Title = I18n.T("st_win_title");
        TxtTitle.Text = I18n.T("st_win_title");
        TxtSub.Text = I18n.TF("st_win_sub", _winIp, _peerIp);
        LblDown.Text = I18n.T("st_down_lbl");
        LblUp.Text = I18n.T("st_up_lbl");
        LblLat.Text = I18n.T("st_lat_lbl");
        TxtLatPair.Text = $"{_winIp} <-> {_peerIp}";
        BtnStartTest.Content = I18n.T("st_start");
        BtnClose.Content = I18n.T("st_btn_close");
        TxtPhase.Text = I18n.T("st_phase_idle");
        TxtPhaseBadge.Text = string.Empty;
    }

    private void UpdateActiveCardHighlight(string phaseKey)
    {
        CardDown.BorderBrush = phaseKey == "st_phase_down" ? BorderDown : BorderIdle;
        CardUp.BorderBrush = phaseKey == "st_phase_up" ? BorderUp : BorderIdle;
        TxtPhaseBadge.Text = phaseKey switch
        {
            "st_phase_ping" => "RTT",
            "st_phase_down" => "DOWN",
            "st_phase_up" => "UP",
            "st_phase_done" => "DONE",
            _ => string.Empty
        };
    }

    private static void AnimateBar(RangeBase bar, double target)
    {
        var anim = new DoubleAnimation
        {
            To = Math.Clamp(target, 0, 100),
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        bar.BeginAnimation(RangeBase.ValueProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private async Task StartTestAsync()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var ct = _runCts.Token;

        BtnStartTest.IsEnabled = false;
        BtnStartTest.Content = I18n.T("st_testing");
        ProgDown.BeginAnimation(RangeBase.ValueProperty, null);
        ProgUp.BeginAnimation(RangeBase.ValueProperty, null);
        ProgDown.Value = 0;
        ProgUp.Value = 0;
        TxtDownPct.Text = "0%";
        TxtUpPct.Text = "0%";
        TxtBigMbps.Text = "0.0";
        TxtBigMBps.Text = "(0.0 MB/s)";
        TxtDownAvg.Text = "-- Mbps";
        TxtDownPeak.Text = "--";
        TxtUpAvg.Text = "-- Mbps";
        TxtUpPeak.Text = "--";
        TxtLat.Text = "-- ms";
        UpdateActiveCardHighlight("st_phase_ping");

        var progress = new Progress<SpeedTestProgress>(p =>
        {
            AnimateBar(ProgDown, p.DownPercent);
            AnimateBar(ProgUp, p.UpPercent);
            TxtDownPct.Text = $"{p.DownPercent}%";
            TxtUpPct.Text = $"{p.UpPercent}%";
            TxtPhase.Text = I18n.T(p.PhaseKey);
            UpdateActiveCardHighlight(p.PhaseKey);

            if (p.LiveMbps > 0)
            {
                TxtBigMbps.Text = $"{p.LiveMbps:F1}";
                TxtBigMBps.Text = $"({p.LiveMbps / 8.0:F1} MB/s)";
            }
            if (p.RttMs is { } rtt)
                TxtLat.Text = $"{rtt:F2} ms";
            if (p.DownAvgMbps is { } da)
                TxtDownAvg.Text = $"{da:F1} Mbps";
            if (p.DownPeakMbps is { } dp)
                TxtDownPeak.Text = I18n.TF("st_peak_fmt", dp);
            if (p.UpAvgMbps is { } ua)
                TxtUpAvg.Text = $"{ua:F1} Mbps";
            if (p.UpPeakMbps is { } up)
                TxtUpPeak.Text = I18n.TF("st_peak_fmt", up);
        });

        SpeedTestResult res;
        try
        {
            res = await Task.Run(() => SpeedTestService.RunBenchmarkAsync(_peerIp, progress, ct), ct);
        }
        catch (OperationCanceledException)
        {
            UpdateActiveCardHighlight(string.Empty);
            BtnStartTest.Content = I18n.T("st_start");
            BtnStartTest.IsEnabled = true;
            return;
        }
        catch (Exception ex)
        {
            UpdateActiveCardHighlight(string.Empty);
            TxtPhase.Text = I18n.TF("st_phase_err", ex.Message);
            BtnStartTest.Content = I18n.T("st_retest");
            BtnStartTest.IsEnabled = true;
            return;
        }
        if (ct.IsCancellationRequested) return;

        UpdateActiveCardHighlight(string.Empty);
        if (res.Success)
        {
            double best = Math.Max(res.DownAvgMbps, res.UpAvgMbps);
            TxtBigMbps.Text = $"{best:F1}";
            TxtBigMBps.Text = $"({best / 8.0:F1} MB/s)";
            TxtLat.Text = $"{res.RttMs:F2} ms";
            TxtDownAvg.Text = $"{res.DownAvgMbps:F1} Mbps";
            TxtDownPeak.Text = I18n.TF("st_peak_fmt", res.DownPeakMbps);
            TxtUpAvg.Text = $"{res.UpAvgMbps:F1} Mbps";
            TxtUpPeak.Text = I18n.TF("st_peak_fmt", res.UpPeakMbps);
            TxtPhase.Text = I18n.T("st_phase_done");
            TxtPhaseBadge.Text = "DONE";
            AnimateBar(ProgDown, 100);
            AnimateBar(ProgUp, 100);
            TxtDownPct.Text = "100%";
            TxtUpPct.Text = "100%";
        }
        else
        {
            TxtPhase.Text = I18n.TF("st_no_peer", _peerIp);
        }

        BtnStartTest.Content = I18n.T("st_retest");
        BtnStartTest.IsEnabled = true;
    }

    private async void BtnStartTest_Click(object sender, RoutedEventArgs e) => await StartTestAsync();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        base.OnClosing(e);
    }
}
