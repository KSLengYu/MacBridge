import Foundation
import Combine

enum AppLang: String, CaseIterable {
    case zh
    case en
}

/// Centralized bilingual localization store for MacBridge Mac companion app (no emojis).
@MainActor
final class L10n: ObservableObject {
    static let shared = L10n()
    @Published var lang: AppLang = L10n.detectInitialLanguage()

    private static func detectInitialLanguage() -> AppLang {
        if let saved = UserDefaults.standard.string(forKey: "macbridge_lang") {
            return saved == "en" ? .en : .zh
        }
        if let preferred = Locale.preferredLanguages.first?.lowercased(), preferred.hasPrefix("zh") {
            return .zh
        }
        return .en
    }

    func toggle() {
        lang = (lang == .zh) ? .en : .zh
        UserDefaults.standard.set(lang.rawValue, forKey: "macbridge_lang")
    }

    func t(_ key: String, _ args: CVarArg...) -> String {
        let pair = Self.table[key] ?? (key, key)
        let template = (lang == .zh) ? pair.0 : pair.1
        if args.isEmpty { return template }
        return String(format: template, arguments: args)
    }

    private static let table: [String: (String, String)] = [
        "status_connected": ("双 C 线已连通 · %@", "USB-C Tunnel Active · %@"),
        "status_connected_sub": ("Windows (%@) 直连正常，可随时一键打开 Windows 共享或测速", "Direct link to Windows (%@) ready for SMB & benchmark."),
        "status_ready_no_peer": ("Mac 接口已就绪 · 等待 Windows", "Mac Interface Ready · Waiting for Windows"),
        "status_ready_no_peer_sub": ("本机 %@ 已拿到 %@，请在 Windows 端启动 MacBridge 连接", "Local %@ holds %@. Start MacBridge on Windows."),
        "status_waiting": ("等待双 C 线隧道", "Waiting for USB-C Bridge"),
        "status_waiting_sub": ("请连接双 C 线并在 Windows 运行 MacBridge；可点击刷新接口重检", "Connect USB-C cable and start MacBridge on Windows."),

        "role_title": ("角色: 直连就绪", "Role: Tunnel Ready"),
        "role_paired": ("对端 Windows 已在线", "Windows Peer Online"),
        "role_standby": ("待机 · 等待对端", "Standby · Waiting Peer"),

        "btn_refresh": ("刷新接口", "Refresh Link"),
        "btn_open_smb": ("一键进入 Windows SMB", "Open Windows SMB"),
        "btn_smb_short": ("进入 SMB", "Open SMB"),
        "btn_speed_test": ("立即测速", "Speed Test"),
        "btn_open_window": ("打开主窗口", "Open Main Window"),
        "btn_quit": ("退出 MacBridge", "Quit MacBridge"),
        "btn_clear": ("清空", "Clear"),

        "card_port": ("端口与线缆速率", "Port & Cable Speed"),
        "card_addr": ("直连地址与共享", "Direct Link & SMB"),
        "card_telemetry": ("延迟与配对状态", "Latency & Pairing"),

        "if_none": ("未检测到 NCM 活动端口", "No active NCM interface"),
        "if_active": ("接口: %@ (%@)", "Interface: %@ (%@)"),
        "speed_fmt": ("线缆速率: %@", "Cable Speed: %@"),
        "speed_unknown": ("线缆速率: 等待 Windows 端汇报", "Cable Speed: Waiting Windows info"),
        "smb_hint": ("直接挂载 smb://%@", "Mount smb://%@ in Finder"),
        "peer_up": ("Windows: %@ (在线)", "Windows: %@ (Online)"),
        "peer_down": ("Windows: %@ (未响应)", "Windows: %@ (Offline)"),
        "rtt_fmt": ("直连延迟: %.2f ms", "RTT Latency: %.2f ms"),
        "rtt_none": ("直连延迟: -- ms", "RTT Latency: -- ms"),

        "log_title": ("实时日志", "Live Console"),
        "footer_tag": ("Mac 菜单栏配对端 · 双 C 线直连", "Mac Companion · USB-C Direct Tunnel"),

        "prog_refreshing": ("正在重新探测本地网络接口与直连通道...", "Probing NCM interface and tunnel peer..."),
        "prog_done": ("接口与链路状态已更新", "Interface & link status updated"),

        "st_title": ("双 C 线实时测速", "USB-C Link Speed Test"),
        "st_sub": ("纯内存直连压测 · %@ <-> %@（绕过磁盘与 SMB）", "Pure memory-to-memory TCP benchmark over %@ <-> %@"),
        "st_phase_ready": ("准备就绪，点击「开始测速」依次完成下行与上行测试", "Ready. Click Start Test to measure Download and Upload."),
        "st_phase_idle": ("测速已取消", "Benchmark Cancelled"),
        "st_phase_ping": ("正在测试直连往返延迟 (RTT)...", "Measuring round-trip latency (RTT)..."),
        "st_phase_down": ("正在测试下行速率 (Win -> Mac)...", "Testing Download (Win -> Mac)..."),
        "st_phase_up": ("正在测试上行速率 (Mac -> Win)...", "Testing Upload (Mac -> Win)..."),
        "st_phase_done": ("下行与上行测速均已完成", "Download & Upload Tests Completed"),
        "st_down_lbl": ("下行 (Win -> Mac)", "Download (Win -> Mac)"),
        "st_up_lbl": ("上行 (Mac -> Win)", "Upload (Mac -> Win)"),
        "st_lat_lbl": ("直连延迟", "Latency"),
        "st_peak_fmt": ("峰值 %.1f Mbps", "Peak %.1f Mbps"),
        "st_start": ("开始测速", "Start Test"),
        "st_testing": ("测速中...", "Testing..."),
        "st_retest": ("重新测速", "Run Again"),
        "st_close": ("关闭", "Close"),
        "st_no_peer": ("无法连接 Windows 测速服务 (%@:19876)。请先在 Windows 打开新版 MacBridgeUI.exe。", "Cannot reach Windows MacBridgeUI (%@:19876). Open updated MacBridgeUI.exe on Windows.")
    ]
}
