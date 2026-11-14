using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MacBridgeUI;

public enum AppLang
{
    Zh,
    En
}

/// <summary>
/// Centralized bilingual localization catalog for MacBridgeUI with persisted instant language switching (no emojis).
/// </summary>
public static class I18n
{
    public static AppLang Current { get; private set; } = AppLang.Zh;

    public static event Action? LanguageChanged;

    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MacBridge");

    private static readonly string LangFilePath = Path.Combine(ConfigDir, "lang.cfg");

    public static AppLang DetectSystemLanguage()
    {
        try
        {
            var ui = CultureInfo.CurrentUICulture;
            if (ui.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return AppLang.Zh;
            var installed = CultureInfo.InstalledUICulture;
            if (ui.Name.Length == 0 && installed.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return AppLang.Zh;
        }
        catch { }

        return AppLang.En;
    }

    public static void LoadSavedLanguage()
    {
        try
        {
            if (File.Exists(LangFilePath))
            {
                string raw = File.ReadAllText(LangFilePath).Trim();
                Current = raw.Equals("en", StringComparison.OrdinalIgnoreCase) ? AppLang.En : AppLang.Zh;
                return;
            }
        }
        catch { }

        Current = DetectSystemLanguage();
    }

    public static void SetLanguage(AppLang lang)
    {
        if (Current == lang) return;
        Current = lang;
        SaveLanguage(lang);
        LanguageChanged?.Invoke();
    }

    private static void SaveLanguage(AppLang lang)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(LangFilePath, lang == AppLang.En ? "en" : "zh");
        }
        catch { }
    }

    private static readonly Dictionary<string, (string Zh, string En)> Strings = new()
    {
        ["app_min"] = ("最小化", "Minimize"),
        ["app_close"] = ("关闭", "Close"),
        ["btn_connect"] = ("一键识别并连接", "Detect & Connect"),
        ["btn_stop"] = ("断开通道", "Disconnect"),
        ["btn_install_drv"] = ("安装驱动", "Install Driver"),
        ["btn_restore"] = ("恢复原厂驱动", "Restore Driver"),
        ["card_port"] = ("端口与驱动状态", "Port & Driver"),
        ["card_addr"] = ("直连地址", "Direct Link"),
        ["card_telemetry"] = ("链路遥测", "Link Telemetry"),
        ["btn_copy_smb"] = ("复制 SMB", "Copy SMB"),
        ["btn_speed_test"] = ("立即测速", "Speed Test"),
        ["log_title"] = ("实时日志", "Live Console"),
        ["btn_clear"] = ("清空", "Clear"),

        ["dev_none"] = ("未检测到 Mac 设备", "No Mac device detected"),
        ["drv_none"] = ("当前驱动: --", "Driver: --"),
        ["drv_unbound"] = ("(未绑定)", "(Unbound)"),
        ["drv_ready"] = ("当前驱动: WinUSB (就绪)", "Driver: WinUSB (Ready)"),
        ["drv_need"] = ("当前驱动: {0} (需安装 WinUSB)", "Driver: {0} (Install WinUSB)"),
        ["drv_install_ok"] = ("WinUSB 驱动安装完成", "WinUSB Driver Installed"),
        ["speed_label"] = ("线缆速率: {0}", "Cable Speed: {0}"),
        ["speed_unknown"] = ("线缆速率: --", "Cable Speed: --"),

        ["st_connected"] = ("通道已连接 · {0}", "Tunnel Connected · {0}"),
        ["st_connected_sub"] = ("USB-C 直连网桥运行中，Mac 访达可使用 smb://{0} 互传文件", "USB-C bridge active. Open smb://{0} in Mac Finder."),
        ["st_ext"] = ("后台通道运行中 (PID {0})", "Background Tunnel (PID {0})"),
        ["st_ext_sub"] = ("后台直连通道运行中，Mac 访达可使用 smb://{0}；如需重新连接请先点击“断开通道”", "Background tunnel is active (smb://{0}). Click Disconnect first to reconnect."),
        ["st_wait"] = ("等待接入双 C 口线缆", "Waiting for USB-C Cable"),
        ["st_wait_sub"] = ("请用 Type-C 线连接 Windows 与 Mac；插入后点击“安装驱动”或“一键识别并连接”", "Connect Windows and Mac via USB-C, then click Install Driver or Connect."),
        ["st_ready"] = ("端口已就绪 · 可直接启动", "Port Ready · Launch Available"),
        ["st_ready_sub"] = ("已识别 {0}，WinUSB 驱动已绑定", "Detected {0} with WinUSB bound."),
        ["st_need_drv"] = ("已识别 Mac 端口 · 待安装驱动", "Mac Port Detected · Driver Needed"),
        ["st_need_drv_sub"] = ("当前为原厂 usbccgp，可点击“安装驱动”或“一键识别并连接”自动绑定 WinUSB", "Stock usbccgp active. Click Install Driver or Connect to bind WinUSB."),

        ["smb_hint"] = ("Mac 访达 Cmd+K: smb://{0}", "Mac Finder Cmd+K: smb://{0}"),
        ["smb_copied"] = ("已复制 smb://{0} 到剪贴板", "Copied smb://{0} to clipboard"),
        ["peer_unknown"] = ("对端 MAC: 未学习", "Peer MAC: Learning..."),
        ["peer_learning"] = ("对端 MAC: 学习中", "Peer MAC: Learning..."),
        ["peer_mac"] = ("对端 MAC: {0}", "Peer MAC: {0}"),

        ["prog_connecting"] = ("正在建立直连通道...", "Establishing direct tunnel..."),
        ["st_connecting_sub"] = ("正在打开 USB 通道并初始化虚拟网卡...", "Opening WinUSB pipe and initializing virtual adapter..."),
        ["prog_installing"] = ("正在自动安装 WinUSB 驱动 (首次约需 15-40 秒)...", "Installing WinUSB driver automatically (15-40s first time)..."),
        ["prog_stopping"] = ("正在停止后台通道进程...", "Stopping bridge process..."),
        ["prog_stopped"] = ("已断开直连通道", "Tunnel Disconnected"),
        ["prog_restoring"] = ("正在卸载 WinUSB 并恢复原厂 usbccgp...", "Uninstalling WinUSB & restoring usbccgp..."),
        ["prog_restored"] = ("已恢复原厂驱动", "Stock Driver Restored"),
        ["prog_closing_restore"] = ("正在停止通道并自动卸载驱动...", "Stopping bridge & uninstalling WinUSB driver..."),
        ["st_stopping"] = ("正在断开直连通道...", "Disconnecting Tunnel..."),
        ["st_stopping_sub"] = ("正在停止后台网桥并释放网卡通道", "Stopping bridge process and releasing virtual adapter."),
        ["st_binding"] = ("正在自动安装 WinUSB 驱动...", "Installing WinUSB Driver..."),
        ["st_binding_sub"] = ("首次切换驱动期间请勿拔掉双 C 线缆", "Do not unplug the USB-C cable while installing driver."),
        ["st_restoring"] = ("正在恢复原厂驱动...", "Restoring Stock Driver..."),
        ["st_restoring_sub"] = ("卸载 libwdi WinUSB INF 并触发 USB 重新枚举", "Removing libwdi INF and rescanning USB devices."),

        // Speed test modal
        ["st_win_title"] = ("双 C 线实时测速", "USB-C Link Speed Test"),
        ["st_win_sub"] = ("纯内存直连压测 · {0} <-> {1}（绕过磁盘与 SMB）", "Pure memory-to-memory TCP benchmark over {0} <-> {1}"),
        ["st_phase_idle"] = ("准备就绪，点击「开始测速」依次完成下行与上行测试", "Ready. Click Start Test to measure Download and Upload."),
        ["st_phase_ping"] = ("正在测试直连往返延迟 (RTT)...", "Measuring round-trip latency (RTT)..."),
        ["st_phase_down"] = ("正在测试下行速率 (Mac -> Windows)...", "Testing Download (Mac -> Windows)..."),
        ["st_phase_up"] = ("正在测试上行速率 (Windows -> Mac)...", "Testing Upload (Windows -> Mac)..."),
        ["st_phase_done"] = ("下行与上行测速均已完成", "Download & Upload Tests Completed"),
        ["st_phase_err"] = ("测速未连接: {0}", "Benchmark failed: {0}"),
        ["st_down_lbl"] = ("下行 (Mac -> Win)", "Download (Mac -> Win)"),
        ["st_up_lbl"] = ("上行 (Win -> Mac)", "Upload (Win -> Mac)"),
        ["st_lat_lbl"] = ("直连延迟", "Latency"),
        ["st_peak_fmt"] = ("峰值 {0:F1} Mbps", "Peak {0:F1} Mbps"),
        ["st_start"] = ("开始测速", "Start Test"),
        ["st_testing"] = ("测速中...", "Testing..."),
        ["st_retest"] = ("重新测速", "Run Again"),
        ["st_btn_close"] = ("关闭", "Close"),
        ["st_no_peer"] = ("无法连接 Mac 测速服务 ({0}:19876)。请确认双 C 线隧道已连接，并在 Mac 端打开 MacBridge.app。", "Cannot reach Mac MacBridge ({0}:19876). Ensure tunnel is up and MacBridge.app is open on Mac."),

        // Close Dialog
        ["close_dlg_title"] = ("关闭 MacBridge", "Close MacBridge"),
        ["close_dlg_msg"] = ("请选择最小化到任务栏继续运行，或关闭程序并自动卸载 WinUSB 驱动恢复原厂状态。", "Minimize to taskbar to keep running, or close the program and automatically uninstall WinUSB to restore stock driver."),
        ["close_btn_min"] = ("最小化", "Minimize"),
        ["close_btn_quit"] = ("关闭程序并卸载驱动", "Close & Uninstall Driver"),
        ["close_btn_cancel"] = ("取消", "Cancel")
    };

    public static string T(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
            return Current == AppLang.Zh ? pair.Zh : pair.En;
        return key;
    }

    public static string TF(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }
}
