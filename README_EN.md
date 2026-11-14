<div align="center">
  <img src="./assets/icon.png" width="112" alt="MacBridge" />
  <h1>MacBridge</h1>
  <p>Pure User-Mode USB Direct IP Bridge Between macOS and Windows (No Test-Signing Required / Clean Driver Restore)</p>
  <p>
    <a href="./README_EN.md">English</a> | <a href="./README.md">简体中文</a>
  </p>
</div>

---

**MacBridge** establishes a direct, high-speed local IP tunnel between a Mac and a Windows PC over standard USB data cables. Using built-in `libwdi` to bind Microsoft `WinUSB`, combined with the signed `Wintun` virtual network adapter and a minimal built-in DHCPv4 / ARP responder, it requires no Windows `testsigning` mode and leaves anti-cheat compatibility intact.

## Key Features

- **Zero Kernel Driver Pollution**: Runs completely in user mode without `testsigning`. Includes a dedicated restore utility (`MacBridge-Restore.exe`) that removes only MacBridge's own `libwdi` `WinUSB` INF bound to `VID_05AC&PID_1902/1905` without touching iPhone/iPad or other Apple drivers.
- **Native Desktop Apps for Both Platforms**:
  - **Windows (`MacBridgeUI`)**: Built with C# / .NET 10 WPF. Supports one-click detection, WinUSB driver binding, tunnel start/stop, USB link speed & live RTT display, real-time logs, and automatic unplug recovery.
  - **macOS (`MacBridge`)**: Built with Swift 5.9 / SwiftUI (macOS 14+). Menu bar popover and main dashboard monitoring the USB network interface and latency.
- **Auto Language Detection**: Automatically selects English or Simplified Chinese based on the OS language on first launch, with instant in-app switching.
- **Broad Cable & Port Compatibility**: Works over **USB-C to USB-C**, **USB-A with an A-to-C adapter**, and **USB-A to USB-C** data cables (negotiated throughput scales with port/cable spec: USB 2.0 or USB 3.x).
- **Predictable Subnet & Conflict Avoidance**: Prefers `10.13.37.0/24` (Windows `10.13.37.2` <-> Mac `10.13.37.1`) and automatically falls back through alternate private ranges (`10.13.38.0/24` - `10.13.40.0/24`, `100.113.37.0/24`, `172.31.237.0/24`, `192.168.237.0/24`) if a local route conflict is detected.
- **Bidirectional Speed Test**: Built-in throughput test panel with separate, sequential Download (Mac -> Windows) and Upload (Windows -> Mac) passes plus RTT measurement.
- **Clean Disconnect Handling**: Automatically detects physical cable unplugs both during tunnel establishment and active transfers, cleanly exiting instead of hanging or spamming pipe errors.

## Project Structure

```text
MacBridge/
├── assets/
│   └── icon.png                 # App icon
├── macbridge.c                  # Windows CDC-NCM <-> Wintun engine, built-in DHCPv4/ARP & libwdi binder
├── macbridge_restore.c          # One-click Apple USB driver restore utility (WinUSB -> usbccgp)
├── restore_helper.h             # Shared UAC elevation & targeted libwdi INF cleanup helpers
├── macbridge.rc                 # Windows executable icon resource script
├── app.ico                      # Windows icon
├── ui/                          # Windows Desktop UI (C# / .NET 10 WPF)
│   ├── MacBridgeUI.csproj
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── SpeedTestWindow.xaml / SpeedTestWindow.xaml.cs
│   ├── SpeedTestService.cs
│   ├── ActionDialogWindow.xaml / ActionDialogWindow.xaml.cs
│   ├── UsbProbe.cs
│   └── I18n.cs
└── mac/                         # macOS Desktop App (Swift 5.9 / SwiftUI, macOS 14+)
    ├── Package.swift
    ├── Info.plist
    ├── AppIcon.icns
    └── Sources/MacBridgeMac/
        ├── MacBridgeApp.swift
        ├── NetworkMonitor.swift
        ├── Views.swift
        └── Localization.swift
```

## Quick Start

1. Connect your Mac and Windows PC with a data-capable Type-C / USB-A cable.
2. Launch `MacBridgeUI.exe` as Administrator on Windows (or run `MacBridge.exe` directly) and click Connect. On first use, `WinUSB` will be bound automatically to `VID_05AC&PID_1902/1905`.
3. Once connected (default Windows `10.13.37.2`, Mac `10.13.37.1`):
   - On Mac **Finder**, press `Command + K` and connect to `smb://<Windows-Bridge-IP>` (default `smb://10.13.37.2`) for high-speed file transfers;
   - Or connect via `ssh <WindowsUser>@10.13.37.2`, `scp`, or any local TCP/UDP tool.
4. To revert Windows back to the stock Apple `usbccgp` driver at any time, click Restore Driver in the UI or run `MacBridge-Restore.exe`.

### `MacBridge.exe` CLI Options

| Flag | Description |
| :--- | :--- |
| `--pair <0\|1>` | Select the NCM interface pair index (default: `0`) |
| `--no-wait` | Exit immediately with an error if no Apple USB-C NCM device is present |
| `--install-only` | Detect device and bind `WinUSB`, then exit |
| `--restore` | Restore stock Apple USB driver and exit |
| `--guid <GUID>` | Override the target WinUSB device interface GUID |

## Building from Source

### 1. Windows Core Engine (`MacBridge.exe` / `MacBridge-Restore.exe`)

Prerequisites:
- **MinGW-w64 (`gcc`)**
- **[libwdi](https://github.com/pbatard/libwdi)** (`libwdi.h` and static `libwdi.a` under `libwdi/`)
- **[Wintun](https://www.wintun.net/)** (place `wintun.dll` for your architecture alongside the compiled `.exe`)

```bat
windres macbridge.rc -O coff -o macbridge_res.o

gcc -m64 -O2 -Wall -Wextra -Werror -Ilibwdi macbridge.c macbridge_res.o libwdi\libwdi.a ^
  -lsetupapi -lole32 -lnewdev -liphlpapi -lws2_32 -ladvapi32 -lcfgmgr32 -luuid -static ^
  -o MacBridge.exe

gcc -m64 -O2 -Wall -Wextra -Werror macbridge_restore.c macbridge_res.o ^
  -lsetupapi -ladvapi32 -lcfgmgr32 -static -o MacBridge-Restore.exe
```

### 2. Windows Desktop UI (`MacBridgeUI.exe`)

Requires **.NET 10 SDK (Windows)**:

```bat
dotnet publish ui\MacBridgeUI.csproj -c Release -r win-x64 -o out
```

Place `MacBridgeUI.exe` in the same folder as `MacBridge.exe`, `MacBridge-Restore.exe`, and `wintun.dll`.

### 3. macOS App (`MacBridge.app`)

On macOS 14+ with Xcode / Swift 5.9+:

```bash
cd mac
swift build -c release
```

Bundle the compiled `MacBridgeMac` binary with `mac/Info.plist` and `mac/AppIcon.icns` into `MacBridge.app/Contents/`.

## License

Released under the [MIT License](./LICENSE).
