# MacBridge

纯用户态 **Mac <-> Windows USB 直连 IP 网桥**（支持双 Type-C、USB-A 转 C 接头及纯 USB-A to C 数据线），无需开启 Windows `testsigning`（不影响游戏反作弊）。

通过内置 `libwdi` 自动检测 Apple USB CDC-NCM 设备并绑定系统 `WinUSB` 驱动，配合官方签名 `Wintun` 虚拟网卡与内置极简 DHCPv4 / ARP 转发，在常规 USB 数据线上直接建立 Mac <-> Windows 局域网直连通道。

---

## 核心特性

- **零额外内网驱动污染**：纯用户态实现，无需启用 Windows 测试签名模式；自带一键还原原厂驱动工具（`MacBridge-Restore.exe`），卸载无残留。
- **开箱即用双端桌面 UI**：
  - **Windows 端（`MacBridgeUI`）**：C# / WPF（`.NET 10`）桌面端，支持一键识别并绑定 WinUSB、启停直连通道、USB 速率与实时 RTT 检测、实时日志监控，以及中/英文（跟随系统语言自动选择或手动切换）。
  - **macOS 端（`MacBridge`）**：Swift / SwiftUI 菜单栏常驻 + 主窗口应用（macOS 14+），实时监听 USB 桥接网卡、连接状态、双向测速联动与自动跟随系统语言。
- **多接口兼容**：支持直连 **C-to-C**、**USB-A + A 转 C 转接头**、**A-to-C** 数据线（实际协商带宽取决于端口与线缆规格，支持 USB 2.0 / USB 3.x）。
- **智能子网冲突避让**：优先使用 `10.13.37.0/24`（Windows `10.13.37.2`，Mac `10.13.37.1`）；若检测到本机已有路由冲突，自动回退备选网段（`10.13.38.0/24` ~ `10.13.40.0/24`、`100.113.37.0/24`、`172.31.237.0/24`、`192.168.237.0/24`）。
- **双向带宽测速 & 断线自检**：内置独立的下行（Mac -> Windows）与上行（Windows -> Mac）独立测速面板；物理拔线、握手中途断联或 USB 管道异常时自动识别并退出，避免进程或界面卡死。

---

## 目录结构

```text
MacBridge/
├── macbridge.c            # Windows 核心 CDC-NCM <-> Wintun 收发引擎、内置 DHCPv4/ARP 与 libwdi 自动绑定
├── macbridge_restore.c    # 一键还原 Apple USB 原厂驱动 (WinUSB -> usbccgp) 命令行工具
├── restore_helper.h       # 管理员提权及安全卸载仅限 MacBridge 生成的 libwdi INF 公共辅助逻辑
├── macbridge.rc           # Windows 可执行文件图标资源脚本
├── app.ico                # 应用图标
├── ui/                    # Windows 桌面端 (C# / .NET 10 WPF)
│   ├── MacBridgeUI.csproj
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── SpeedTestWindow.xaml / SpeedTestWindow.xaml.cs
│   ├── SpeedTestService.cs
│   ├── ActionDialogWindow.xaml / ActionDialogWindow.xaml.cs
│   ├── UsbProbe.cs
│   └── I18n.cs
└── mac/                   # macOS 桌面端 (Swift 5.9 / SwiftUI, macOS 14+)
    ├── Package.swift
    ├── Info.plist
    ├── AppIcon.icns
    └── Sources/MacBridgeMac/
        ├── MacBridgeApp.swift
        ├── NetworkMonitor.swift
        ├── Views.swift
        └── Localization.swift
```

---

## 快速使用（默认地址 `10.13.37.0/24`）

1. 使用支持数据传输的 Type-C / USB-A 线缆连接 Mac 与 Windows。
2. 在 Windows 上以管理员权限运行 `MacBridgeUI.exe`（或双击终端程序 `MacBridge.exe`），点击一键识别并连接；首次连接时会自动为 `VID_05AC&PID_1902/1905` 绑定 `WinUSB`。
3. 状态变为已连接后（默认 Windows 为 `10.13.37.2`，Mac 自动获取 `10.13.37.1`）：
   - 在 Mac **访达**按 `Command + K`，输入 `smb://<Windows直连IP>`（默认 `smb://10.13.37.2`）即可直接互传文件；
   - 或在终端使用 `ssh <Windows用户名>@10.13.37.2`、`scp`、SMB 等任意局域网协议。
4. 可在两侧 UI 打开“立即测速”分别测试下行/上行吞吐；不再使用时点击“卸载并恢复原厂驱动”或运行 `MacBridge-Restore.exe` 即可还原系统原厂驱动（不会影响 iPhone/iPad 等其他 Apple 设备）。

### `MacBridge.exe` 命令行参数

- `--pair <0|1>`：选择 NCM 接口对索引（默认 `0`）。
- `--no-wait`：未检测到设备时不阻塞等待，直接报错退出。
- `--install-only`：仅执行 `WinUSB` 驱动检测与绑定后退出。
- `--restore`：直接执行驱动恢复并退出。
- `--guid <GUID>`：指定自定义 WinUSB 设备接口 GUID。

---

## 从源码编译

### 1. Windows 核心引擎 (`MacBridge.exe` / `MacBridge-Restore.exe`)

需准备：
- **MinGW-w64 (`gcc`)**
- **[libwdi](https://github.com/pbatard/libwdi)**（静态库 `libwdi.a` 与头文件 `libwdi.h` 置于 `libwdi/`）
- **[Wintun](https://www.wintun.net/)**（运行目录需同级放置对应架构的 `wintun.dll`）

```bat
windres macbridge.rc -O coff -o macbridge_res.o

gcc -m64 -O2 -Wall -Wextra -Werror -Ilibwdi macbridge.c macbridge_res.o libwdi\libwdi.a ^
  -lsetupapi -lole32 -lnewdev -liphlpapi -lws2_32 -ladvapi32 -lcfgmgr32 -luuid -static ^
  -o MacBridge.exe

gcc -m64 -O2 -Wall -Wextra -Werror macbridge_restore.c macbridge_res.o ^
  -lsetupapi -ladvapi32 -lcfgmgr32 -static -o MacBridge-Restore.exe
```

### 2. Windows 桌面端 (`MacBridgeUI.exe`)

需安装 **.NET 10 SDK (Windows)**：

```bat
dotnet publish ui\MacBridgeUI.csproj -c Release -r win-x64 -o out
```

发布后将 `MacBridgeUI.exe` 与 `MacBridge.exe`、`MacBridge-Restore.exe`、`wintun.dll` 放在同一目录即可运行。

### 3. macOS 桌面端 (`MacBridge.app`)

在 macOS 14+（安装 Xcode / Swift 5.9+ 工具链）下：

```bash
cd mac
swift build -c release
```

可将编译生成的二进制 `MacBridgeMac` 按 `mac/Info.plist` 与 `mac/AppIcon.icns` 打包进 `MacBridge.app/Contents/` 运行。

---

## 开源协议

基于 [MIT License](LICENSE) 开源。
