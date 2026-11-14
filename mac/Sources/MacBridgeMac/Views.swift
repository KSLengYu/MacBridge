import SwiftUI
import AppKit

private extension Color {
    static let bgCanvas = Color(red: 0x0B / 255.0, green: 0x0E / 255.0, blue: 0x14 / 255.0)
    static let bgCard = Color(red: 0x12 / 255.0, green: 0x17 / 255.0, blue: 0x22 / 255.0)
    static let bgRecessed = Color(red: 0x08 / 255.0, green: 0x0B / 255.0, blue: 0x10 / 255.0)
    static let borderSubtle = Color(red: 0x1F / 255.0, green: 0x29 / 255.0, blue: 0x37 / 255.0)
    static let textPrimary = Color(red: 0xF3 / 255.0, green: 0xF4 / 255.0, blue: 0xF6 / 255.0)
    static let textSecondary = Color(red: 0x94 / 255.0, green: 0xA3 / 255.0, blue: 0xB8 / 255.0)
    static let textMuted = Color(red: 0x64 / 255.0, green: 0x74 / 255.0, blue: 0x8B / 255.0)
    static let accentBlue = Color(red: 0x3B / 255.0, green: 0x82 / 255.0, blue: 0xF6 / 255.0)
    static let accentBlueFill = Color(red: 0x25 / 255.0, green: 0x63 / 255.0, blue: 0xEB / 255.0)
    static let accentSky = Color(red: 0x7D / 255.0, green: 0xD3 / 255.0, blue: 0xFC / 255.0)
    static let statusGreen = Color(red: 0x10 / 255.0, green: 0xB9 / 255.0, blue: 0x81 / 255.0)
    static let statusAmber = Color(red: 0xF5 / 255.0, green: 0x9E / 255.0, blue: 0x0B / 255.0)
    static let statusSlate = Color(red: 0x47 / 255.0, green: 0x55 / 255.0, blue: 0x69 / 255.0)
}

/// Pure vector bridge emblem matching Windows MacBridgeUI and AppIcon.
struct BridgeLogoIcon: View {
    var size: CGFloat = 24
    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: size * 0.25)
                .fill(Color(red: 0x16 / 255.0, green: 0x21 / 255.0, blue: 0x36 / 255.0))
                .overlay(
                    RoundedRectangle(cornerRadius: size * 0.25)
                        .stroke(Color(red: 0x26 / 255.0, green: 0x3B / 255.0, blue: 0x5E / 255.0), lineWidth: max(1, size * 0.042))
                )
            DoubleArrowEmblemShape()
                .stroke(
                    Color(red: 0x60 / 255.0, green: 0xA5 / 255.0, blue: 0xFA / 255.0),
                    style: StrokeStyle(lineWidth: max(1.3, size * 0.088), lineCap: .round, lineJoin: .round)
                )
                .padding(size * 0.22)
        }
        .frame(width: size, height: size)
    }
}

struct DoubleArrowEmblemShape: Shape {
    func path(in rect: CGRect) -> Path {
        var p = Path()
        let midY = rect.midY
        let leftX = rect.minX
        let rightX = rect.maxX
        let chevW = rect.width * 0.30
        let chevH = rect.height * 0.34
        p.move(to: CGPoint(x: leftX + rect.width * 0.05, y: midY))
        p.addLine(to: CGPoint(x: rightX - rect.width * 0.05, y: midY))
        p.move(to: CGPoint(x: leftX + chevW, y: midY - chevH))
        p.addLine(to: CGPoint(x: leftX, y: midY))
        p.addLine(to: CGPoint(x: leftX + chevW, y: midY + chevH))
        p.move(to: CGPoint(x: rightX - chevW, y: midY - chevH))
        p.addLine(to: CGPoint(x: rightX, y: midY))
        p.addLine(to: CGPoint(x: rightX - chevW, y: midY + chevH))
        return p
    }
}

/// Official GitHub Octocat vector mark (zero image files required).
struct GitHubMarkShape: Shape {
    func path(in rect: CGRect) -> Path {
        let sx = rect.width / 24.0
        let sy = rect.height / 24.0
        var p = Path()
        p.move(to: CGPoint(x: 12 * sx, y: 0.3 * sy))
        p.addCurve(to: CGPoint(x: 0 * sx, y: 12.3 * sy), control1: CGPoint(x: 5.4 * sx, y: 0.3 * sy), control2: CGPoint(x: 0 * sx, y: 5.7 * sy))
        p.addCurve(to: CGPoint(x: 8.2 * sx, y: 23.7 * sy), control1: CGPoint(x: 0 * sx, y: 17.6 * sy), control2: CGPoint(x: 3.4 * sx, y: 22.1 * sy))
        p.addCurve(to: CGPoint(x: 9.0 * sx, y: 23.1 * sy), control1: CGPoint(x: 8.8 * sx, y: 23.8 * sy), control2: CGPoint(x: 9.0 * sx, y: 23.4 * sy))
        p.addLine(to: CGPoint(x: 9.0 * sx, y: 21.0 * sy))
        p.addCurve(to: CGPoint(x: 5.0 * sx, y: 19.4 * sy), control1: CGPoint(x: 5.7 * sx, y: 21.7 * sy), control2: CGPoint(x: 5.0 * sx, y: 19.4 * sy))
        p.addCurve(to: CGPoint(x: 3.7 * sx, y: 17.6 * sy), control1: CGPoint(x: 4.5 * sx, y: 18.0 * sy), control2: CGPoint(x: 3.7 * sx, y: 17.6 * sy))
        p.addCurve(to: CGPoint(x: 3.8 * sx, y: 16.9 * sy), control1: CGPoint(x: 2.6 * sx, y: 16.9 * sy), control2: CGPoint(x: 3.8 * sx, y: 16.9 * sy))
        p.addCurve(to: CGPoint(x: 5.6 * sx, y: 18.1 * sy), control1: CGPoint(x: 5.0 * sx, y: 17.0 * sy), control2: CGPoint(x: 5.6 * sx, y: 18.1 * sy))
        p.addCurve(to: CGPoint(x: 9.1 * sx, y: 19.1 * sy), control1: CGPoint(x: 6.7 * sx, y: 19.9 * sy), control2: CGPoint(x: 8.4 * sx, y: 19.4 * sy))
        p.addCurve(to: CGPoint(x: 9.9 * sx, y: 17.5 * sy), control1: CGPoint(x: 9.2 * sx, y: 18.3 * sy), control2: CGPoint(x: 9.5 * sx, y: 17.8 * sy))
        p.addCurve(to: CGPoint(x: 4.4 * sx, y: 11.6 * sy), control1: CGPoint(x: 7.2 * sx, y: 17.2 * sy), control2: CGPoint(x: 4.4 * sx, y: 16.2 * sy))
        p.addCurve(to: CGPoint(x: 5.6 * sx, y: 8.4 * sy), control1: CGPoint(x: 4.4 * sx, y: 10.3 * sy), control2: CGPoint(x: 4.9 * sx, y: 9.2 * sy))
        p.addCurve(to: CGPoint(x: 5.7 * sx, y: 5.2 * sy), control1: CGPoint(x: 5.5 * sx, y: 8.1 * sy), control2: CGPoint(x: 5.1 * sx, y: 6.9 * sy))
        p.addCurve(to: CGPoint(x: 9.0 * sx, y: 6.4 * sy), control1: CGPoint(x: 5.7 * sx, y: 5.2 * sy), control2: CGPoint(x: 6.7 * sx, y: 4.9 * sy))
        p.addCurve(to: CGPoint(x: 12.0 * sx, y: 6.0 * sy), control1: CGPoint(x: 10.0 * sx, y: 6.1 * sy), control2: CGPoint(x: 11.0 * sx, y: 6.0 * sy))
        p.addCurve(to: CGPoint(x: 15.0 * sx, y: 6.4 * sy), control1: CGPoint(x: 13.0 * sx, y: 6.0 * sy), control2: CGPoint(x: 14.0 * sx, y: 6.1 * sy))
        p.addCurve(to: CGPoint(x: 18.3 * sx, y: 5.2 * sy), control1: CGPoint(x: 17.3 * sx, y: 4.8 * sy), control2: CGPoint(x: 18.3 * sx, y: 5.2 * sy))
        p.addCurve(to: CGPoint(x: 18.4 * sx, y: 8.4 * sy), control1: CGPoint(x: 19.0 * sx, y: 6.9 * sy), control2: CGPoint(x: 18.5 * sx, y: 8.1 * sy))
        p.addCurve(to: CGPoint(x: 19.6 * sx, y: 11.6 * sy), control1: CGPoint(x: 19.2 * sx, y: 9.2 * sy), control2: CGPoint(x: 19.6 * sx, y: 10.3 * sy))
        p.addCurve(to: CGPoint(x: 14.1 * sx, y: 17.5 * sy), control1: CGPoint(x: 19.6 * sx, y: 16.2 * sy), control2: CGPoint(x: 16.8 * sx, y: 17.2 * sy))
        p.addCurve(to: CGPoint(x: 14.9 * sx, y: 19.7 * sy), control1: CGPoint(x: 14.5 * sx, y: 17.9 * sy), control2: CGPoint(x: 14.9 * sx, y: 18.6 * sy))
        p.addLine(to: CGPoint(x: 14.9 * sx, y: 23.0 * sy))
        p.addCurve(to: CGPoint(x: 15.7 * sx, y: 23.6 * sy), control1: CGPoint(x: 14.9 * sx, y: 23.3 * sy), control2: CGPoint(x: 15.1 * sx, y: 23.7 * sy))
        p.addCurve(to: CGPoint(x: 24.0 * sx, y: 12.3 * sy), control1: CGPoint(x: 20.6 * sx, y: 22.1 * sy), control2: CGPoint(x: 24.0 * sx, y: 17.6 * sy))
        p.addCurve(to: CGPoint(x: 12.0 * sx, y: 0.3 * sy), control1: CGPoint(x: 24.0 * sx, y: 5.7 * sy), control2: CGPoint(x: 18.6 * sx, y: 0.3 * sy))
        p.closeSubpath()
        return p
    }
}

private struct HoverButtonSurface<Content: View>: View {
    let cornerRadius: CGFloat
    let shadowColor: Color
    let shadowRadius: CGFloat
    @ViewBuilder let content: (Bool) -> Content
    @State private var hovered = false

    var body: some View {
        content(hovered)
            .contentShape(Rectangle())
            .cornerRadius(cornerRadius)
            .shadow(color: hovered ? shadowColor : .clear, radius: hovered ? shadowRadius : 0)
            .animation(.easeOut(duration: 0.14), value: hovered)
            .onHover { inside in
                hovered = inside
                if inside {
                    NSCursor.pointingHand.set()
                } else {
                    NSCursor.arrow.set()
                }
            }
            .onDisappear {
                if hovered {
                    hovered = false
                    NSCursor.arrow.set()
                }
            }
    }
}

struct PillButtonStyle: ButtonStyle {
    var fg: Color = Color(red: 0x93 / 255.0, green: 0xC5 / 255.0, blue: 0xFD / 255.0)
    var bg: Color = Color(red: 0x15 / 255.0, green: 0x22 / 255.0, blue: 0x38 / 255.0)
    var border: Color = Color(red: 0x25 / 255.0, green: 0x3A / 255.0, blue: 0x5E / 255.0)

    func makeBody(configuration: Configuration) -> some View {
        HoverButtonSurface(cornerRadius: 4, shadowColor: Color.accentBlue.opacity(0.35), shadowRadius: 5) { hovered in
            configuration.label
                .font(.system(size: 11, weight: .medium))
                .foregroundColor(hovered ? Color(red: 0xE0 / 255.0, green: 0xF2 / 255.0, blue: 0xFE / 255.0) : fg)
                .padding(.horizontal, 9)
                .padding(.vertical, 3)
                .background(
                    configuration.isPressed
                        ? Color.accentBlueFill
                        : (hovered ? Color(red: 0x1E / 255.0, green: 0x31 / 255.0, blue: 0x52 / 255.0) : bg)
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 4)
                        .stroke(hovered || configuration.isPressed ? Color(red: 0x60 / 255.0, green: 0xA5 / 255.0, blue: 0xFA / 255.0) : border, lineWidth: 1)
                )
        }
    }
}

struct PrimaryActionStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        HoverButtonSurface(cornerRadius: 8, shadowColor: Color.accentBlue.opacity(0.45), shadowRadius: 7) { hovered in
            configuration.label
                .font(.system(size: 12.5, weight: .semibold))
                .foregroundColor(.white)
                .padding(.horizontal, 14)
                .padding(.vertical, 8)
                .background(
                    configuration.isPressed
                        ? Color(red: 0x1D / 255.0, green: 0x4E / 255.0, blue: 0xD8 / 255.0)
                        : (hovered ? Color.accentBlue : Color.accentBlueFill)
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(hovered ? Color(red: 0x93 / 255.0, green: 0xC5 / 255.0, blue: 0xFD / 255.0) : Color.accentBlue, lineWidth: 1)
                )
        }
    }
}

struct GhostActionStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        HoverButtonSurface(cornerRadius: 8, shadowColor: Color.accentBlue.opacity(0.20), shadowRadius: 5) { hovered in
            configuration.label
                .font(.system(size: 12, weight: .medium))
                .foregroundColor(hovered ? Color(red: 0xF8 / 255.0, green: 0xFA / 255.0, blue: 0xFC / 255.0) : Color(red: 0xCB / 255.0, green: 0xD5 / 255.0, blue: 0xE1 / 255.0))
                .padding(.horizontal, 12)
                .padding(.vertical, 8)
                .background(
                    configuration.isPressed
                        ? Color.bgRecessed
                        : (hovered ? Color(red: 0x1E / 255.0, green: 0x29 / 255.0, blue: 0x3B / 255.0) : Color(red: 0x16 / 255.0, green: 0x1D / 255.0, blue: 0x2B / 255.0))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 8)
                        .stroke(hovered ? Color(red: 0x47 / 255.0, green: 0x55 / 255.0, blue: 0x69 / 255.0) : Color(red: 0x26 / 255.0, green: 0x32 / 255.0, blue: 0x48 / 255.0), lineWidth: 1)
                )
        }
    }
}

struct SubtleTextButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        HoverButtonSurface(cornerRadius: 4, shadowColor: .clear, shadowRadius: 0) { hovered in
            configuration.label
                .font(.system(size: 11.5))
                .foregroundColor(hovered ? Color(red: 0xF8 / 255.0, green: 0xFA / 255.0, blue: 0xFC / 255.0) : Color.textMuted)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(hovered ? Color(red: 0x16 / 255.0, green: 0x1D / 255.0, blue: 0x2B / 255.0) : Color.clear)
        }
    }
}

struct GitHubLinkButtonStyle: ButtonStyle {
    let iconOnlyColor: Color = .textSecondary
    func makeBody(configuration: Configuration) -> some View {
        HoverButtonSurface(cornerRadius: 5, shadowColor: Color.accentBlue.opacity(0.22), shadowRadius: 4) { hovered in
            HStack(spacing: 6) {
                GitHubMarkShape()
                    .fill(hovered ? Color(red: 0x60 / 255.0, green: 0xA5 / 255.0, blue: 0xFA / 255.0) : iconOnlyColor)
                    .frame(width: 14, height: 14)
                configuration.label
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundColor(hovered ? Color(red: 0xF8 / 255.0, green: 0xFA / 255.0, blue: 0xFC / 255.0) : .textSecondary)
            }
            .padding(.horizontal, 6)
            .padding(.vertical, 3)
            .background(hovered ? Color(red: 0x16 / 255.0, green: 0x21 / 255.0, blue: 0x36 / 255.0).opacity(0.6) : Color.clear)
            .contentShape(Rectangle())
        }
    }
}

// MARK: - Menu Bar Popover

struct MenuBarPopoverView: View {
    @ObservedObject var monitor: BridgeMonitor
    @ObservedObject var l10n = L10n.shared
    let onOpenMainWindow: (Bool) -> Void
    let onQuit: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                BridgeLogoIcon(size: 22)
                Text("MacBridge")
                    .font(.system(size: 13.5, weight: .semibold))
                    .foregroundColor(.textPrimary)
                Spacer()
                Button(l10n.lang == .zh ? "EN" : "中文") {
                    l10n.toggle()
                }
                .buttonStyle(PillButtonStyle())
            }

            // Connection + Latency + Role Summary
            VStack(alignment: .leading, spacing: 6) {
                HStack(spacing: 8) {
                    Circle()
                        .fill(monitor.peerOnline ? Color.statusGreen : (monitor.localActive ? Color.accentBlue : Color.statusSlate))
                        .frame(width: 8, height: 8)
                    Text(monitor.peerOnline
                         ? l10n.t("status_connected", monitor.activeLocalIp)
                         : (monitor.localActive ? l10n.t("status_ready_no_peer") : l10n.t("status_waiting")))
                        .font(.system(size: 12.5, weight: .semibold))
                        .foregroundColor(.textPrimary)
                        .lineLimit(1)
                }

                HStack {
                    Text(monitor.peerOnline ? l10n.t("role_paired") : l10n.t("role_standby"))
                        .font(.system(size: 11))
                        .foregroundColor(.textSecondary)
                    Spacer()
                    Text(monitor.rttMs.map { String(format: "%.2f ms", $0) } ?? "-- ms")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.accentSky)
                }
            }
            .padding(11)
            .background(Color.bgCard)
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.borderSubtle, lineWidth: 1))
            .cornerRadius(8)

            // Direct Address + One-Click SMB
            VStack(alignment: .leading, spacing: 8) {
                HStack {
                    Text(l10n.t("card_addr"))
                        .font(.system(size: 10.5, weight: .semibold))
                        .foregroundColor(.textMuted)
                    Spacer()
                    Text("\(monitor.activeLocalIp) <-> \(monitor.activePeerIp)")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.textSecondary)
                }

                Button(action: { monitor.openWindowsSmb() }) {
                    HStack {
                        Spacer()
                        Text(l10n.t("btn_open_smb"))
                        Spacer()
                    }
                }
                .buttonStyle(PrimaryActionStyle())
            }
            .padding(11)
            .background(Color.bgCard)
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.borderSubtle, lineWidth: 1))
            .cornerRadius(8)

            // Cable Speed + Quick Speed Test
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text(monitor.usbSpeed == "--" ? l10n.t("speed_unknown") : l10n.t("speed_fmt", monitor.usbSpeed))
                        .font(.system(size: 11.5, weight: .medium))
                        .foregroundColor(.accentSky)
                        .lineLimit(1)
                }
                Spacer()
                Button(l10n.t("btn_speed_test")) {
                    onOpenMainWindow(true)
                }
                .buttonStyle(PillButtonStyle())
            }
            .padding(.horizontal, 4)

            Divider().background(Color.borderSubtle)

            HStack(spacing: 8) {
                Button(action: { onOpenMainWindow(false) }) {
                    HStack {
                        Spacer()
                        Text(l10n.t("btn_open_window"))
                        Spacer()
                    }
                }
                .buttonStyle(GhostActionStyle())

                Button(l10n.t("btn_quit")) {
                    onQuit()
                }
                .buttonStyle(GhostActionStyle())
            }
        }
        .padding(14)
        .frame(width: 332)
        .background(Color.bgCanvas)
    }
}

// MARK: - Main Control Window (Matches Windows MacBridgeUI)

struct MainWindowView: View {
    @ObservedObject var monitor: BridgeMonitor
    @ObservedObject var l10n = L10n.shared

    var body: some View {
        VStack(spacing: 14) {
            // Header
            HStack {
                BridgeLogoIcon(size: 24)
                Text("MacBridge")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(.textPrimary)
                Spacer()
                Button(l10n.lang == .zh ? "EN" : "中文") {
                    l10n.toggle()
                }
                .buttonStyle(PillButtonStyle())
            }
            .padding(.top, 2)

            // Hero Status & Action Card
            VStack(alignment: .leading, spacing: 12) {
                HStack(alignment: .center, spacing: 14) {
                    VStack(alignment: .leading, spacing: 6) {
                        HStack(spacing: 9) {
                            Circle()
                                .fill(statusColor)
                                .frame(width: 9, height: 9)
                            Text(headlineText)
                                .font(.system(size: 16.5, weight: .semibold))
                                .foregroundColor(.textPrimary)
                        }
                        Text(sublineText)
                            .font(.system(size: 12))
                            .foregroundColor(.textSecondary)
                            .padding(.leading, 18)
                    }

                    Spacer()

                    HStack(spacing: 8) {
                        Button(l10n.t("btn_open_smb")) {
                            monitor.openWindowsSmb()
                        }
                        .buttonStyle(PrimaryActionStyle())

                        Button(l10n.t("btn_refresh")) {
                            monitor.refreshInterface()
                        }
                        .buttonStyle(GhostActionStyle())
                    }
                }

                if monitor.isActionBusy {
                    VStack(alignment: .leading, spacing: 5) {
                        ProgressView(value: monitor.actionProgress, total: 100)
                            .progressViewStyle(.linear)
                            .tint(Color.accentBlue)
                            .animation(.easeInOut(duration: 0.32), value: monitor.actionProgress)
                        Text(monitor.actionHint)
                            .font(.system(size: 11.5))
                            .foregroundColor(Color(red: 0x93 / 255.0, green: 0xC5 / 255.0, blue: 0xFD / 255.0))
                    }
                    .transition(.opacity)
                }
            }
            .padding(18)
            .background(Color.bgCard)
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.borderSubtle, lineWidth: 1))
            .cornerRadius(12)

            // 3-Column Telemetry Cards
            HStack(spacing: 12) {
                // Card 1: Port & Cable Speed
                VStack(alignment: .leading, spacing: 6) {
                    Text(l10n.t("card_port"))
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundColor(.textMuted)
                    Text(monitor.localActive ? l10n.t("if_active", monitor.ifaceName, monitor.localIp) : l10n.t("if_none"))
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.textPrimary)
                        .lineLimit(1)
                    Text(monitor.usbSpeed == "--" ? l10n.t("speed_unknown") : l10n.t("speed_fmt", monitor.usbSpeed))
                        .font(.system(size: 12))
                        .foregroundColor(.accentSky)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(14)
                .background(Color.bgCard)
                .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.borderSubtle, lineWidth: 1))
                .cornerRadius(10)

                // Card 2: Direct Address & One-Click Windows SMB
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Text(l10n.t("card_addr"))
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.textMuted)
                        Spacer()
                        Button(l10n.t("btn_smb_short")) {
                            monitor.openWindowsSmb()
                        }
                        .buttonStyle(PillButtonStyle())
                    }
                    Text("\(monitor.activeLocalIp) <-> \(monitor.activePeerIp)")
                        .font(.system(size: 12, weight: .semibold, design: .monospaced))
                        .foregroundColor(.textPrimary)
                        .lineLimit(1)
                        .minimumScaleFactor(0.65)
                    Text(l10n.t("smb_hint", monitor.activePeerIp))
                        .font(.system(size: 12))
                        .foregroundColor(.textSecondary)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(14)
                .background(Color.bgCard)
                .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.borderSubtle, lineWidth: 1))
                .cornerRadius(10)

                // Card 3: Latency, Pairing Status & Speed Test Trigger
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        Text(l10n.t("card_telemetry"))
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundColor(.textMuted)
                        Spacer()
                        Button(l10n.t("btn_speed_test")) {
                            monitor.openSpeedTestSheet()
                        }
                        .buttonStyle(PillButtonStyle())
                    }
                    Text(monitor.peerOnline
                         ? l10n.t("peer_up", monitor.activePeerIp)
                         : l10n.t("peer_down", monitor.activePeerIp))
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(monitor.peerOnline ? .statusGreen : .textPrimary)
                        .lineLimit(1)
                    Text(monitor.rttMs.map { l10n.t("rtt_fmt", $0) } ?? l10n.t("rtt_none"))
                        .font(.system(size: 12, design: .monospaced))
                        .foregroundColor(.textSecondary)
                        .lineLimit(1)
                    Spacer(minLength: 0)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(14)
                .background(Color.bgCard)
                .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.borderSubtle, lineWidth: 1))
                .cornerRadius(10)
            }
            .frame(height: 102)

            // Log Drawer (isolated observer so main status & speed ticks never re-layout logs)
            ConsoleDrawerView(logStore: monitor.logStore, l10n: l10n)

            // Footer with GitHub Icon + Clickable KSLengYu
            HStack {
                Spacer()
                Button("KSLengYu") {
                    monitor.openGitHubProfile()
                }
                .buttonStyle(GitHubLinkButtonStyle())
                .help("https://github.com/KSLengYu")
            }
        }
        .padding(20)
        .frame(minWidth: 720, minHeight: 560)
        .background(Color.bgCanvas)
        .sheet(isPresented: $monitor.showSpeedTestSheet, onDismiss: {
            monitor.cancelSpeedTest()
        }) {
            SpeedTestSheetView(monitor: monitor, speed: monitor.speedState)
        }
    }

    private var statusColor: Color {
        if monitor.peerOnline { return .statusGreen }
        if monitor.localActive { return .accentBlue }
        return .statusSlate
    }

    private var headlineText: String {
        if monitor.peerOnline { return l10n.t("status_connected", monitor.activeLocalIp) }
        if monitor.localActive { return l10n.t("status_ready_no_peer") }
        return l10n.t("status_waiting")
    }

    private var sublineText: String {
        if monitor.peerOnline { return l10n.t("status_connected_sub", monitor.activePeerIp) }
        if monitor.localActive { return l10n.t("status_ready_no_peer_sub", monitor.ifaceName, monitor.activeLocalIp) }
        return l10n.t("status_waiting_sub")
    }
}

// MARK: - Isolated Console Drawer

struct ConsoleDrawerView: View {
    @ObservedObject var logStore: BridgeLogStore
    @ObservedObject var l10n: L10n

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text(l10n.t("log_title"))
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundColor(.textMuted)
                Spacer()
                Button(l10n.t("btn_clear")) {
                    logStore.clear()
                }
                .buttonStyle(SubtleTextButtonStyle())
            }
            .padding(.horizontal, 14)
            .frame(height: 34)
            .overlay(Rectangle().fill(Color(red: 0x15 / 255.0, green: 0x1C / 255.0, blue: 0x28 / 255.0)).frame(height: 1), alignment: .bottom)

            ScrollViewReader { proxy in
                ScrollView {
                    Text(logStore.text)
                        .font(.system(size: 11.5, design: .monospaced))
                        .foregroundColor(Color(red: 0xCB / 255.0, green: 0xD5 / 255.0, blue: 0xE1 / 255.0))
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .textSelection(.enabled)
                        .padding(12)
                        .id("logEnd")
                }
                .onChange(of: logStore.text) { _, _ in
                    proxy.scrollTo("logEnd", anchor: .bottom)
                }
            }
        }
        .background(Color.bgRecessed)
        .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color(red: 0x1A / 255.0, green: 0x22 / 255.0, blue: 0x32 / 255.0), lineWidth: 1))
        .cornerRadius(10)
    }
}

// MARK: - Speed Test Modal Sheet

struct SpeedTestSheetView: View {
    let monitor: BridgeMonitor
    @ObservedObject var speed: SpeedTestState
    @ObservedObject var l10n = L10n.shared

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text(l10n.t("st_title"))
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(.textPrimary)
                Text(monitor.usbSpeed == "--" ? "\(monitor.activeLocalIp) <-> \(monitor.activePeerIp)" : monitor.usbSpeed)
                    .font(.system(size: 10.5, weight: .semibold))
                    .foregroundColor(.accentSky)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(Color(red: 0x13 / 255.0, green: 0x1B / 255.0, blue: 0x29 / 255.0))
                    .overlay(RoundedRectangle(cornerRadius: 4).stroke(Color(red: 0x23 / 255.0, green: 0x30 / 255.0, blue: 0x44 / 255.0), lineWidth: 1))
                    .cornerRadius(4)
                Spacer()
            }

            Text(l10n.t("st_sub", monitor.activeLocalIp, monitor.activePeerIp))
                .font(.system(size: 11.5))
                .foregroundColor(.textMuted)

            // Live Readout Card
            VStack(alignment: .leading, spacing: 10) {
                HStack(alignment: .firstTextBaseline) {
                    Text(String(format: "%.1f", speed.liveMbps))
                        .font(.system(size: 34, weight: .bold, design: .monospaced))
                        .foregroundColor(.textPrimary)
                    Text("Mbps")
                        .font(.system(size: 13.5, weight: .semibold))
                        .foregroundColor(Color(red: 0x60 / 255.0, green: 0xA5 / 255.0, blue: 0xFA / 255.0))
                    Text(String(format: "(%.1f MB/s)", speed.liveMbps / 8.0))
                        .font(.system(size: 12.5, design: .monospaced))
                        .foregroundColor(.textSecondary)
                    Spacer()
                    Text(phaseBadgeText)
                        .font(.system(size: 11, weight: .semibold, design: .monospaced))
                        .foregroundColor(.accentSky)
                }

                Text(speed.error.isEmpty ? l10n.t(speed.phaseKey) : speed.error)
                    .font(.system(size: 12))
                    .foregroundColor(speed.error.isEmpty ? .textSecondary : .statusAmber)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .padding(16)
            .background(Color.bgCard)
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.borderSubtle, lineWidth: 1))
            .cornerRadius(10)

            // 3 Summary Tiles (Down & Up have independent progress bars)
            HStack(spacing: 10) {
                speedTile(
                    title: l10n.t("st_down_lbl"),
                    main: speed.downAvg.map { String(format: "%.1f Mbps", $0) } ?? "-- Mbps",
                    sub: speed.downPeak.map { l10n.t("st_peak_fmt", $0) } ?? "--",
                    percent: speed.downPercent,
                    color: .statusGreen,
                    activeBorder: (speed.running && speed.phaseKey == "st_phase_down") ? .statusGreen : .borderSubtle
                )
                speedTile(
                    title: l10n.t("st_up_lbl"),
                    main: speed.upAvg.map { String(format: "%.1f Mbps", $0) } ?? "-- Mbps",
                    sub: speed.upPeak.map { l10n.t("st_peak_fmt", $0) } ?? "--",
                    percent: speed.upPercent,
                    color: .accentBlue,
                    activeBorder: (speed.running && speed.phaseKey == "st_phase_up") ? .accentBlue : .borderSubtle
                )
                VStack(alignment: .leading, spacing: 5) {
                    Text(l10n.t("st_lat_lbl"))
                        .font(.system(size: 11, weight: .semibold))
                        .foregroundColor(.textMuted)
                    Text(speed.rttMs.map { String(format: "%.2f ms", $0) } ?? "-- ms")
                        .font(.system(size: 14.5, weight: .semibold, design: .monospaced))
                        .foregroundColor(.textPrimary)
                    Text("\(monitor.activeLocalIp) <-> \(monitor.activePeerIp)")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.textSecondary)
                        .lineLimit(1)
                        .minimumScaleFactor(0.75)
                    Spacer(minLength: 0)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
                .padding(12)
                .background(Color.bgCard)
                .overlay(RoundedRectangle(cornerRadius: 10).stroke(Color.borderSubtle, lineWidth: 1))
                .cornerRadius(10)
            }
            .fixedSize(horizontal: false, vertical: true)

            HStack(spacing: 8) {
                Spacer()
                Button(speed.running ? l10n.t("st_testing") : (speed.hasCompletedOnce ? l10n.t("st_retest") : l10n.t("st_start"))) {
                    monitor.startSpeedTest()
                }
                .buttonStyle(PrimaryActionStyle())
                .disabled(speed.running)

                Button(l10n.t("st_close")) {
                    monitor.cancelSpeedTest()
                    monitor.showSpeedTestSheet = false
                }
                .buttonStyle(GhostActionStyle())
            }
        }
        .padding(20)
        .frame(width: 530)
        .background(Color.bgCanvas)
    }

    private var phaseBadgeText: String {
        switch speed.phaseKey {
        case "st_phase_ping": return "RTT"
        case "st_phase_down": return "DOWN"
        case "st_phase_up": return "UP"
        case "st_phase_done": return "DONE"
        default: return ""
        }
    }

    private func speedTile(title: String, main: String, sub: String, percent: Int, color: Color, activeBorder: Color) -> some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack {
                Text(title)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundColor(.textMuted)
                Spacer()
                Text("\(percent)%")
                    .font(.system(size: 10.5, weight: .semibold, design: .monospaced))
                    .foregroundColor(color)
            }
            Text(main)
                .font(.system(size: 14.5, weight: .semibold, design: .monospaced))
                .foregroundColor(color)
            Text(sub)
                .font(.system(size: 11, design: .monospaced))
                .foregroundColor(.textSecondary)
            ProgressView(value: Double(percent), total: 100)
                .progressViewStyle(.linear)
                .tint(color)
                .animation(.easeOut(duration: 0.2), value: percent)
                .padding(.top, 4)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(Color.bgCard)
        .overlay(RoundedRectangle(cornerRadius: 10).stroke(activeBorder, lineWidth: 1))
        .cornerRadius(10)
    }
}
