using System;
using System.Collections.Generic;
using System.Globalization;

namespace MacBridgeSetup;

public enum SetupLang
{
    Zh,
    En
}

/// <summary>
/// Bilingual localization table for MacBridge Windows Setup &amp; Uninstaller (no emojis).
/// </summary>
public static class InstallerI18n
{
    public static SetupLang Current { get; private set; } = DetectSystemLanguage();

    public static SetupLang DetectSystemLanguage()
    {
        try
        {
            var ui = CultureInfo.CurrentUICulture;
            if (ui.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return SetupLang.Zh;
            var installed = CultureInfo.InstalledUICulture;
            if (ui.Name.Length == 0 && installed.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return SetupLang.Zh;
        }
        catch { }

        return SetupLang.En;
    }

    public static void Toggle() => Current = Current == SetupLang.Zh ? SetupLang.En : SetupLang.Zh;

    public static string T(string key)
    {
        var map = Current == SetupLang.Zh ? Zh : En;
        return map.TryGetValue(key, out string? v) ? v : key;
    }

    public static string TF(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, string> Zh = new()
    {
        ["win_title"] = "MacBridge 安装向导",
        ["win_title_uninst"] = "MacBridge 卸载程序",
        ["lang_btn"] = "中文 / EN",
        ["badge_setup"] = "安装向导 v1.0.1",
        ["badge_uninst"] = "卸载向导",
        ["hero_title"] = "MacBridge",
        ["hero_sub"] = "纯用户态 Mac <-> Windows USB 直连高速 IP 网桥（免测试签名）",
        ["lbl_dir"] = "安装目录",
        ["btn_browse"] = "浏览...",
        ["browse_title"] = "选择 MacBridge 安装目录",
        ["lbl_options"] = "安装选项",
        ["chk_desktop"] = "创建桌面快捷方式（MacBridge）",
        ["chk_startmenu"] = "创建开始菜单快捷方式（含驱动还原与卸载入口）",
        ["chk_launch"] = "安装完成后立即启动 MacBridge",
        ["btn_install"] = "立即安装",
        ["btn_cancel"] = "取消",
        ["btn_close"] = "关闭",
        ["btn_finish"] = "完成",
        ["btn_uninstall"] = "立即卸载",
        ["inst_title"] = "正在安装 MacBridge...",
        ["inst_sub"] = "正在复制程序文件并注册系统快捷方式",
        ["done_title"] = "MacBridge 安装完成",
        ["done_sub"] = "已准备就绪，接入 USB-C / USB-A 数据线后即可一键直连 Mac",
        ["done_path"] = "安装路径: {0}",
        ["done_tip"] = "提示: 如需彻底移除并还原系统驱动，可通过开始菜单或 Windows 设置 -> 应用卸载。",
        ["uninst_title"] = "卸载 MacBridge",
        ["uninst_sub"] = "将移除 MacBridge 程序文件、快捷方式及系统卸载条目",
        ["uninst_path"] = "目标目录: {0}",
        ["chk_restore_drv"] = "同时还原 Apple USB 原厂驱动（WinUSB -> usbccgp）",
        ["uninst_running"] = "正在卸载 MacBridge...",
        ["uninst_done_title"] = "MacBridge 已卸载",
        ["uninst_done_sub"] = "程序与系统入口已移除，点击“关闭”后将自动清理剩余目录。",
        ["err_title"] = "操作失败",
        ["err_dir_empty"] = "请选择有效的安装目录。",
        ["err_missing_payload"] = "安装包缺少内嵌组件: {0}",
        ["step_stop"] = "正在检查并关闭运行中的 MacBridge 进程...",
        ["step_extract"] = "正在写入组件: {0}",
        ["step_uninst_exe"] = "正在写入卸载程序...",
        ["step_shortcuts"] = "正在创建系统快捷方式...",
        ["step_registry"] = "正在注册 Windows 应用卸载信息...",
        ["step_restore"] = "正在还原 Apple USB 原厂驱动...",
        ["step_clean_files"] = "正在移除安装文件与注册表...",
        ["step_done"] = "全部步骤完成。",
        ["author_role"] = "开发者 · 开源仓库",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["win_title"] = "MacBridge Setup",
        ["win_title_uninst"] = "MacBridge Uninstaller",
        ["lang_btn"] = "EN / 中文",
        ["badge_setup"] = "Installer v1.0.1",
        ["badge_uninst"] = "Uninstaller",
        ["hero_title"] = "MacBridge",
        ["hero_sub"] = "Pure user-mode USB direct IP bridge between Mac and Windows",
        ["lbl_dir"] = "Install Location",
        ["btn_browse"] = "Browse...",
        ["browse_title"] = "Select MacBridge installation folder",
        ["lbl_options"] = "Options",
        ["chk_desktop"] = "Create Desktop shortcut (MacBridge)",
        ["chk_startmenu"] = "Create Start Menu shortcuts (App, Restore Driver, Uninstall)",
        ["chk_launch"] = "Launch MacBridge after installation",
        ["btn_install"] = "Install",
        ["btn_cancel"] = "Cancel",
        ["btn_close"] = "Close",
        ["btn_finish"] = "Finish",
        ["btn_uninstall"] = "Uninstall",
        ["inst_title"] = "Installing MacBridge...",
        ["inst_sub"] = "Extracting core files and registering shortcuts",
        ["done_title"] = "Installation Complete",
        ["done_sub"] = "MacBridge is ready. Plug in a USB-C / USB-A data cable to connect.",
        ["done_path"] = "Installed to: {0}",
        ["done_tip"] = "Tip: You can cleanly uninstall and revert Apple USB drivers anytime from Apps & Features.",
        ["uninst_title"] = "Uninstall MacBridge",
        ["uninst_sub"] = "Removes MacBridge files, shortcuts, and Windows uninstall entry",
        ["uninst_path"] = "Target directory: {0}",
        ["chk_restore_drv"] = "Also restore stock Apple USB driver (WinUSB -> usbccgp)",
        ["uninst_running"] = "Uninstalling MacBridge...",
        ["uninst_done_title"] = "MacBridge Uninstalled",
        ["uninst_done_sub"] = "Files and shortcuts removed. Remaining directory cleanup finishes after closing.",
        ["err_title"] = "Operation Failed",
        ["err_dir_empty"] = "Please specify a valid installation directory.",
        ["err_missing_payload"] = "Embedded payload missing: {0}",
        ["step_stop"] = "Checking and stopping running MacBridge processes...",
        ["step_extract"] = "Writing component: {0}",
        ["step_uninst_exe"] = "Copying uninstaller...",
        ["step_shortcuts"] = "Creating Desktop & Start Menu shortcuts...",
        ["step_registry"] = "Registering Windows uninstall entry...",
        ["step_restore"] = "Restoring stock Apple USB driver...",
        ["step_clean_files"] = "Removing installed files and registry entries...",
        ["step_done"] = "Completed successfully.",
        ["author_role"] = "Author · GitHub",
    };
}
