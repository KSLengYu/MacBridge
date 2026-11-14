<div align="center">
  <img src="./assets/icon.png" width="112" alt="MacBridge" />
  <h1>MacBridge</h1>
  <p>纯用户态 Mac &lt;-&gt; Windows USB 直连高速 IP 网桥（无需开启 Windows 测试签名 / 一键无痕还原原厂驱动）</p>
  <p>
    <a href="./README_EN.md">English</a> | <a href="./README.md">简体中文</a>
  </p>
</div>

---

通过内置 `libwdi` 自动检测 Apple USB CDC-NCM 设备并绑定系统 `WinUSB` 驱动，配合官方签名 `Wintun` 虚拟网卡与内置极简 DHCPv4 / ARP 转发，在普通 USB 数据线上直接建立 Mac <-> Windows 局域网直连通道。

## 核心特性

- **纯用户态免测试签名**：无需开启 Windows `testsigning`（不影响游戏反作弊），自带一键还原工具（`MacBridge-Restore.exe`），仅清理自身绑定的 Apple `05AC:1902/1905` `WinUSB` INF，不触碰 iPhone/iPad 等其他 Apple 设备。
- **双端原生桌面应用**：
  - **Windows 端（`MacBridgeUI`）**：基于 C# / .NET 10 WPF，支持一键识别绑定与建链、USB 速率/延迟监控、实时日志与断线自动识别退出。
  - **macOS 端（`MacBridge`）**：基于 Swift 5.9 / SwiftUI（macOS 14+），提供菜单栏常驻弹窗与主窗口状态监控。
- **双语自动跟随 & 手动切换**：默认根据系统语言自动选择简体中文或英文，也可在界面内随时切换。
- **多接口线缆兼容**：支持双 Type-C、USB-A + A 转 C 转接头、以及纯 USB-A to Type-C 数据线（实际带宽取决于端口与线缆规格，支持 USB 2.0 / USB 3.x）。
- **默认固定网段 & 冲突自动避让**：优先分配 `10.13.37.0/24`（Windows `10.13.37.2` <-> Mac `10.13.37.1`）；若检测到本机路由冲突，自动回退至备选网段（`10.13.38.0/24` ~ `10.13.40.0/24`、`100.113.37.0/24`、`172.31.237.0/24`、`192.168.237.0/24`）。
- **独立双向带宽测速**：支持单独一键触发并分步测量下行（Mac -> Windows）与上行（Windows -> Mac）吞吐率与 RTT 延迟。
- **断联自检退出**：无论在建链握手中途或传输过程中拔线断联，C 核心引擎与 UI 均可自动检测并安全退出，避免卡在“正在连接”状态或错误日志刷屏。

## 目录结构

```text
MacBridge/
├── assets/
│   └── icon.png                 # 项目图标
├── macbridge.c                  # Windows 核心 CDC-NCM <-> Wintun 引擎、DHCPv4/ARP 与 libwdi 自动绑定
├── macbridge_restore.c          # 一键恢复 Apple USB 原厂驱动 (WinUSB -> usbccgp)
├── restore_helper.h             # 管理员提权与安全清理 libwdi INF 公共辅助逻辑
├── macbridge.rc                 # Windows 可执行文件资源脚本
├── app.ico                      # Windows 应用图标
├── ui/                          # Windows 桌面端 (C# / .NET 10 WPF)
│   ├── MacBridgeUI.csproj
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── SpeedTestWindow.xaml / SpeedTestWindow.xaml.cs
│   ├── SpeedTestService.cs
│   ├── ActionDialogWindow.xaml / ActionDialogWindow.xaml.cs
│   ├── UsbProbe.cs
│   └── I18n.cs
└── mac/                         # macOS 桌面端 (Swift 5.9 / SwiftUI, macOS 14+)
    ├── Package.swift
    ├── Info.plist
    ├── AppIcon.icns
    └── Sources/MacBridgeMac/
        ├── MacBridgeApp.swift
        ├── NetworkMonitor.swift
        ├── Views.swift
        └── Localization.swift
```

## 快速使用

1. 使用支持数据传输的 Type-C / USB-A 线缆连接 Mac 与 Windows。
2. 在 Windows 上以管理员权限启动 `MacBridgeUI.exe`（或命令行工具 `MacBridge.exe`），点击一键识别并连接；首次连接将自动为 `VID_05AC&PID_1902/1905` 绑定 `WinUSB`。
3. 隧道建立成功后（默认 Windows `10.13.37.2`，Mac `10.13.37.1`）：
   - 在 Mac **访达**按 `Command + K`，输入 `smb://<Windows直连IP>`（默认 `smb://10.13.37.2`）即可直接高速互传文件；
   - 或直接使用 `ssh <Windows用户名>@10.13.37.2`、`scp`、SMB 等局域网服务。
4. 不再使用时，在界面点击“卸载并恢复原厂驱动”或运行 `MacBridge-Restore.exe` 即可一键还原为系统默认 `usbccgp` 驱动。

### `MacBridge.exe` 命令行参数

| 参数 | 说明 |
| :--- | :--- |
| `--pair <0\|1>` | 选择 NCM 接口对索引（默认 `0`） |
| `--no-wait` | 未检测到 Apple USB-C NCM 设备时不阻塞等待，直接报错退出 |
| `--install-only` | 仅执行 `WinUSB` 驱动检测与绑定后退出 |
| `--restore` | 直接还原为系统原厂驱动后退出 |
| `--guid <GUID>` | 指定自定义 WinUSB 设备接口 GUID |

## 从源码编译

### 1. Windows 核心引擎（`MacBridge.exe` / `MacBridge-Restore.exe`）

准备工作：
- **MinGW-w64 (`gcc`)**
- **[libwdi](https://github.com/pbatard/libwdi)**（将 `libwdi.h` 与静态库 `libwdi.a` 放在 `libwdi/` 目录）
- **[Wintun](https://www.wintun.net/)**（将对应架构的 `wintun.dll` 放在运行目录同级）

```bat
windres macbridge.rc -O coff -o macbridge_res.o

gcc -m64 -O2 -Wall -Wextra -Werror -Ilibwdi macbridge.c macbridge_res.o libwdi\libwdi.a ^
  -lsetupapi -lole32 -lnewdev -liphlpapi -lws2_32 -ladvapi32 -lcfgmgr32 -luuid -static ^
  -o MacBridge.exe

gcc -m64 -O2 -Wall -Wextra -Werror macbridge_restore.c macbridge_res.o ^
  -lsetupapi -ladvapi32 -lcfgmgr32 -static -o MacBridge-Restore.exe
```

### 2. Windows 桌面端（`MacBridgeUI.exe`）

需安装 **.NET 10 SDK (Windows)**：

```bat
dotnet publish ui\MacBridgeUI.csproj -c Release -r win-x64 -o out
```

将生成的 `MacBridgeUI.exe` 与 `MacBridge.exe`、`MacBridge-Restore.exe`、`wintun.dll` 放置在同级目录运行。

### 3. macOS 桌面端（`MacBridge.app`）

在 macOS 14+（Xcode / Swift 5.9+）环境下执行：

```bash
cd mac
swift build -c release
```

将编译输出的 `MacBridgeMac` 可执行文件结合 `mac/Info.plist` 与 `mac/AppIcon.icns` 打包至 `MacBridge.app/Contents/` 即可运行。

## 开源协议

本项目基于 [MIT License](./LICENSE) 开源。
