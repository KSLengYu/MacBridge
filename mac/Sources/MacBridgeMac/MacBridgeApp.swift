import AppKit
import SwiftUI
import Combine

@main
@MainActor
final class MacBridgeAppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    private var statusItem: NSStatusItem!
    private var popover: NSPopover!
    private var mainWindow: NSWindow?
    private var cancellables = Set<AnyCancellable>()
    private let monitor = BridgeMonitor.shared
    private let l10n = L10n.shared

    static func main() {
        let app = NSApplication.shared
        let delegate = MacBridgeAppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.accessory)
        app.run()
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        if let appIcon = Bundle.main.image(forResource: "AppIcon") {
            NSApp.applicationIconImage = appIcon
        }
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = statusItem.button {
            button.image = makeStatusIcon()
            button.imagePosition = .imageLeading
            button.action = #selector(togglePopover(_:))
            button.target = self
        }

        popover = NSPopover()
        popover.behavior = .transient
        popover.appearance = NSAppearance(named: .darkAqua)
        popover.contentViewController = NSHostingController(
            rootView: MenuBarPopoverView(
                monitor: monitor,
                onOpenMainWindow: { [weak self] openSpeedTest in
                    self?.popover.performClose(nil)
                    self?.showMainWindow(openSpeedTest: openSpeedTest)
                },
                onQuit: { NSApp.terminate(nil) }
            )
        )

        Publishers.CombineLatest3(monitor.$peerOnline, monitor.$rttMs, l10n.$lang)
            .receive(on: DispatchQueue.main)
            .sink { [weak self] online, rtt, lang in
                self?.updateMenuBarTitle(online: online, rtt: rtt, lang: lang)
            }
            .store(in: &cancellables)
    }

    private func updateMenuBarTitle(online: Bool, rtt: Double?, lang: AppLang) {
        guard let btn = statusItem.button else { return }
        if online {
            let ms = rtt.map { String(format: "%.1fms", $0) } ?? (lang == .zh ? "已连接" : "UP")
            btn.title = " \(ms)"
        } else {
            btn.title = lang == .zh ? " 离线" : " Idle"
        }
    }

    private func makeStatusIcon() -> NSImage {
        let img = NSImage(size: NSSize(width: 16, height: 14), flipped: false) { _ in
            NSColor.labelColor.setStroke()
            let p = NSBezierPath()
            p.lineWidth = 1.5
            p.lineCapStyle = .round
            p.lineJoinStyle = .round
            p.move(to: NSPoint(x: 2, y: 7))
            p.line(to: NSPoint(x: 14, y: 7))
            p.move(to: NSPoint(x: 5, y: 3.5))
            p.line(to: NSPoint(x: 1.8, y: 7))
            p.line(to: NSPoint(x: 5, y: 10.5))
            p.move(to: NSPoint(x: 11, y: 3.5))
            p.line(to: NSPoint(x: 14.2, y: 7))
            p.line(to: NSPoint(x: 11, y: 10.5))
            p.stroke()
            return true
        }
        img.isTemplate = true
        return img
    }

    @objc private func togglePopover(_ sender: Any?) {
        guard let btn = statusItem.button else { return }
        if popover.isShown {
            popover.performClose(sender)
        } else {
            popover.show(relativeTo: btn.bounds, of: btn, preferredEdge: .minY)
            popover.contentViewController?.view.window?.makeKey()
        }
    }

    private func showMainWindow(openSpeedTest: Bool) {
        if mainWindow == nil {
            let win = NSWindow(
                contentRect: NSRect(x: 0, y: 0, width: 780, height: 610),
                styleMask: [.titled, .closable, .miniaturizable, .resizable],
                backing: .buffered,
                defer: false
            )
            win.title = "MacBridge"
            win.titlebarAppearsTransparent = true
            win.backgroundColor = NSColor(red: 0x0B / 255.0, green: 0x0E / 255.0, blue: 0x14 / 255.0, alpha: 1)
            win.appearance = NSAppearance(named: .darkAqua)
            win.isReleasedWhenClosed = false
            win.delegate = self
            win.center()
            win.contentViewController = NSHostingController(rootView: MainWindowView(monitor: monitor))
            mainWindow = win
        }

        mainWindow?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)

        if openSpeedTest {
            DispatchQueue.main.async { [weak self] in
                self?.monitor.openSpeedTestSheet()
            }
        }
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        sender.orderOut(nil)
        return false
    }

    func applicationWillTerminate(_ notification: Notification) {
        monitor.stop()
    }
}
