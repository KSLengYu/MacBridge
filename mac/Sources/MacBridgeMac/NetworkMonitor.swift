import Foundation
import AppKit
import Darwin
import os

struct BridgeSnapshot: Sendable {
    let ifaceName: String
    let localIp: String
    let peerIp: String
    let localActive: Bool
    let peerOnline: Bool
    let rttMs: Double?
    let usbSpeed: String
    let usbMbps: Int
}

/// Isolated observable log storage so high-frequency progress/state updates do not re-layout console text.
@MainActor
final class BridgeLogStore: ObservableObject {
    @Published var text: String = ""

    func append(_ stampedLine: String) {
        text += stampedLine
        if text.count > 16000 {
            text = String(text.suffix(8000))
        }
    }

    func clear() {
        text = ""
    }
}

/// Isolated observable speed-test state so benchmark updates do not re-render the main console window.
@MainActor
final class SpeedTestState: ObservableObject {
    @Published var running: Bool = false
    @Published var hasCompletedOnce: Bool = false
    @Published var phaseKey: String = "st_phase_ready"
    @Published var downPercent: Int = 0
    @Published var upPercent: Int = 0
    @Published var liveMbps: Double = 0
    @Published var rttMs: Double? = nil
    @Published var downAvg: Double? = nil
    @Published var downPeak: Double? = nil
    @Published var upAvg: Double? = nil
    @Published var upPeak: Double? = nil
    @Published var error: String = ""
}

/// Manages local NCM detection, TCP benchmark server/client (port 19876), and Finder SMB launch.
@MainActor
final class BridgeMonitor: ObservableObject {
    static let shared = BridgeMonitor()

    nonisolated static let defaultLocalIp = "10.13.37.1"
    nonisolated static let defaultPeerIp = "10.13.37.2"
    nonisolated static let candidatePrefixes = [
        "10.13.37", "10.13.38", "10.13.39",
        "10.13.137", "10.13.138",
        "10.13.237", "10.13.238", "10.13.239",
        "100.113.37", "172.31.237", "192.168.237"
    ]
    nonisolated static let servicePort: UInt16 = 19876

    @Published var ifaceName: String = ""
    @Published var localIp: String = ""
    @Published var activeLocalIp: String = defaultLocalIp
    @Published var activePeerIp: String = defaultPeerIp
    @Published var localActive: Bool = false
    @Published var peerOnline: Bool = false
    @Published var rttMs: Double? = nil
    @Published var usbSpeed: String = "--"
    @Published var usbMbps: Int = 0

    // Action progress bar state
    @Published var isActionBusy: Bool = false
    @Published var actionProgress: Double = 0
    @Published var actionHint: String = ""

    @Published var showSpeedTestSheet: Bool = false
    let logStore = BridgeLogStore()
    let speedState = SpeedTestState()

    private struct SpeedCancelState: Sendable {
        var cancelled = false
        var activeFd: Int32 = -1
    }

    private static let logDateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "HH:mm:ss"
        return f
    }()

    private var serverFd: Int32 = -1
    private let stopLock = OSAllocatedUnfairLock(initialState: false)
    private let cancelLock = OSAllocatedUnfairLock(initialState: SpeedCancelState())
    private let activePairLock = OSAllocatedUnfairLock(initialState: (localIp: defaultLocalIp, peerIp: defaultPeerIp))
    private let probeQueue = DispatchQueue(label: "com.kslengyu.macbridge.probe", qos: .utility)
    private let refreshQueue = DispatchQueue(label: "com.kslengyu.macbridge.refresh", qos: .userInitiated)

    private init() {
        appendLog("[UI] MacBridge Mac companion started")
        startBenchmarkServer()
        startProbeLoop()
    }

    func stop() {
        stopLock.withLock { $0 = true }
        cancelSpeedTest()
        if serverFd >= 0 {
            Darwin.close(serverFd)
            serverFd = -1
        }
    }

    func appendLog(_ line: String) {
        let stamped = "[\(Self.logDateFormatter.string(from: Date()))] \(line)\n"
        logStore.append(stamped)
    }

    func clearLogs() {
        logStore.clear()
    }

    func openWindowsSmb() {
        let targetIp = activePeerIp
        guard let url = URL(string: "smb://\(targetIp)") else { return }
        NSWorkspace.shared.open(url)
        appendLog("[SMB] Opening smb://\(targetIp) in Finder")
    }

    func openGitHubProfile() {
        guard let url = URL(string: "https://github.com/KSLengYu") else { return }
        NSWorkspace.shared.open(url)
    }

    func refreshInterface() {
        guard !isActionBusy else { return }
        isActionBusy = true
        actionProgress = 22
        actionHint = L10n.shared.t("prog_refreshing")
        appendLog("[NET] Refreshing local USB-C NCM interface and peer probe...")

        refreshQueue.async { [weak self] in
            guard let self else { return }
            DispatchQueue.main.async {
                self.actionProgress = 58
            }
            let snap = Self.collectSnapshot()
            DispatchQueue.main.async {
                self.actionProgress = 88
                self.applySnapshot(snap)
                self.actionProgress = 100
                self.actionHint = L10n.shared.t("prog_done")
                DispatchQueue.main.asyncAfter(deadline: .now() + 0.45) {
                    self.isActionBusy = false
                    self.actionProgress = 0
                    self.actionHint = ""
                }
            }
        }
    }

    private func applySnapshot(_ snap: BridgeSnapshot) {
        let prevOnline = peerOnline
        let prevLocal = activeLocalIp
        let nextLocal = snap.localIp.isEmpty ? Self.defaultLocalIp : snap.localIp
        let nextPeer = snap.peerIp.isEmpty ? Self.defaultPeerIp : snap.peerIp

        ifaceName = snap.ifaceName
        localIp = snap.localIp
        activeLocalIp = nextLocal
        activePeerIp = nextPeer
        activePairLock.withLock { state in
            state.localIp = nextLocal
            state.peerIp = nextPeer
        }
        localActive = snap.localActive
        peerOnline = snap.peerOnline
        rttMs = snap.rttMs
        if !snap.usbSpeed.isEmpty && snap.usbSpeed != "--" {
            usbSpeed = snap.usbSpeed
            usbMbps = snap.usbMbps
        } else if !snap.peerOnline {
            usbSpeed = "--"
            usbMbps = 0
        }

        if prevOnline != snap.peerOnline || (snap.peerOnline && prevLocal != nextLocal) {
            appendLog(snap.peerOnline
                ? "[OK] Direct link UP (\(snap.ifaceName) \(nextLocal) <-> \(nextPeer))"
                : "[INFO] Windows peer offline")
        }
    }

    private func startProbeLoop() {
        probeQueue.async { [weak self] in
            while let self, !self.stopLock.withLock({ $0 }) {
                let snap = Self.collectSnapshot()
                DispatchQueue.main.async {
                    self.applySnapshot(snap)
                }
                Thread.sleep(forTimeInterval: 2.0)
            }
        }
    }

    // MARK: - Interface & Peer Probing (Non-blocking BSD Sockets + getifaddrs)

    private nonisolated static func collectSnapshot() -> BridgeSnapshot {
        struct CandidateIface {
            let ifaceName: String
            let localIp: String
            let peerIp: String
            let isUp: Bool
            let priority: Int
        }

        var candidates: [CandidateIface] = []
        var ifaddrPtr: UnsafeMutablePointer<ifaddrs>?
        if getifaddrs(&ifaddrPtr) == 0, let first = ifaddrPtr {
            defer { freeifaddrs(ifaddrPtr) }
            var cur: UnsafeMutablePointer<ifaddrs>? = first
            while let p = cur {
                defer { cur = p.pointee.ifa_next }
                let name = String(cString: p.pointee.ifa_name)
                guard name.hasPrefix("en"), let addr = p.pointee.ifa_addr else { continue }
                if addr.pointee.sa_family == UInt8(AF_INET) {
                    var hostBuf = [CChar](repeating: 0, count: Int(NI_MAXHOST))
                    if getnameinfo(addr, socklen_t(addr.pointee.sa_len), &hostBuf, socklen_t(hostBuf.count), nil, 0, NI_NUMERICHOST) == 0 {
                        let ipStr = String(cString: hostBuf)
                        if let idx = candidatePrefixes.firstIndex(where: { ipStr == "\($0).1" }) {
                            // Verify /24 mask when netmask is reported so unrelated interfaces are not matched.
                            var maskOk = true
                            if let netmask = p.pointee.ifa_netmask, netmask.pointee.sa_family == UInt8(AF_INET) {
                                var maskBuf = [CChar](repeating: 0, count: Int(NI_MAXHOST))
                                if getnameinfo(netmask, socklen_t(netmask.pointee.sa_len), &maskBuf, socklen_t(maskBuf.count), nil, 0, NI_NUMERICHOST) == 0 {
                                    maskOk = String(cString: maskBuf) == "255.255.255.0"
                                }
                            }
                            if maskOk {
                                let flags = Int32(p.pointee.ifa_flags)
                                let isUp = (flags & IFF_UP) != 0 && (flags & IFF_RUNNING) != 0
                                candidates.append(CandidateIface(
                                    ifaceName: name,
                                    localIp: ipStr,
                                    peerIp: "\(candidatePrefixes[idx]).2",
                                    isUp: isUp,
                                    priority: idx
                                ))
                            }
                        }
                    }
                }
            }
        }

        candidates.sort {
            if $0.isUp != $1.isUp { return $0.isUp && !$1.isUp }
            return $0.priority < $1.priority
        }

        var foundIface = candidates.first?.ifaceName ?? ""
        var foundIp = candidates.first?.localIp ?? ""
        var foundPeer = candidates.first?.peerIp ?? defaultPeerIp
        var isUp = candidates.first?.isUp ?? false

        var peerUp = false
        var measuredRtt: Double? = nil
        var remoteUsbSpeed = "--"
        var remoteUsbMbps = 0

        for cand in candidates where cand.isUp {
            if let info = queryPeerInfo(ip: cand.peerIp, port: servicePort) {
                foundIface = cand.ifaceName
                foundIp = cand.localIp
                foundPeer = cand.peerIp
                isUp = true
                peerUp = true
                remoteUsbSpeed = info.usbSpeed
                remoteUsbMbps = info.usbMbps
                measuredRtt = measurePingRtt(ip: cand.peerIp, port: servicePort)
                break
            } else if let tcpRtt = measureConnectRtt(ip: cand.peerIp, port: 445) ?? measureConnectRtt(ip: cand.peerIp, port: 22) {
                foundIface = cand.ifaceName
                foundIp = cand.localIp
                foundPeer = cand.peerIp
                isUp = true
                peerUp = true
                measuredRtt = tcpRtt
                break
            }
        }

        return BridgeSnapshot(
            ifaceName: foundIface,
            localIp: foundIp,
            peerIp: foundPeer,
            localActive: isUp,
            peerOnline: peerUp,
            rttMs: measuredRtt,
            usbSpeed: remoteUsbSpeed,
            usbMbps: remoteUsbMbps
        )
    }

    private nonisolated static func applySocketTimeouts(_ fd: Int32, seconds: Int) {
        var tv = timeval(tv_sec: seconds, tv_usec: 0)
        setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))
        setsockopt(fd, SOL_SOCKET, SO_SNDTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))
    }

    private nonisolated static func connectSocket(ip: String, port: UInt16, timeoutMs: Int32) -> Int32 {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { return -1 }

        var one: Int32 = 1
        setsockopt(fd, IPPROTO_TCP, TCP_NODELAY, &one, socklen_t(MemoryLayout<Int32>.size))
        var noSigPipe: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &noSigPipe, socklen_t(MemoryLayout<Int32>.size))

        let flags = fcntl(fd, F_GETFL, 0)
        _ = fcntl(fd, F_SETFL, flags | O_NONBLOCK)

        var sa = sockaddr_in()
        sa.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        sa.sin_family = sa_family_t(AF_INET)
        sa.sin_port = port.bigEndian
        inet_pton(AF_INET, ip, &sa.sin_addr)

        let rc = withUnsafePointer(to: &sa) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }

        if rc != 0 && errno != EINPROGRESS {
            Darwin.close(fd)
            return -1
        }

        if rc != 0 {
            var pfd = pollfd(fd: fd, events: Int16(POLLOUT), revents: 0)
            let pr = Darwin.poll(&pfd, 1, timeoutMs)
            if pr <= 0 {
                Darwin.close(fd)
                return -1
            }
            var soErr: Int32 = 0
            var len = socklen_t(MemoryLayout<Int32>.size)
            getsockopt(fd, SOL_SOCKET, SO_ERROR, &soErr, &len)
            if soErr != 0 {
                Darwin.close(fd)
                return -1
            }
        }

        _ = fcntl(fd, F_SETFL, flags)
        applySocketTimeouts(fd, seconds: 3)
        return fd
    }

    private nonisolated static func sendAll(_ fd: Int32, _ data: UnsafeRawPointer, _ count: Int) -> Bool {
        var sent = 0
        while sent < count {
            let n = Darwin.send(fd, data.advanced(by: sent), count - sent, 0)
            if n <= 0 { return false }
            sent += n
        }
        return true
    }

    private nonisolated static func sendLine(_ fd: Int32, _ line: String) -> Bool {
        guard let bytes = (line + "\n").data(using: .utf8) else { return false }
        return bytes.withUnsafeBytes { ptr in
            guard let base = ptr.baseAddress else { return false }
            return sendAll(fd, base, bytes.count)
        }
    }

    private nonisolated static func readLine(_ fd: Int32) -> String? {
        var buf = [UInt8]()
        var b: UInt8 = 0
        while buf.count < 1024 {
            let n = Darwin.recv(fd, &b, 1, 0)
            if n <= 0 { break }
            if b == 0x0A { break }
            if b != 0x0D { buf.append(b) }
        }
        if buf.isEmpty { return nil }
        return String(bytes: buf, encoding: .utf8)
    }

    private nonisolated static func measureConnectRtt(ip: String, port: UInt16) -> Double? {
        let t0 = DispatchTime.now().uptimeNanoseconds
        let fd = connectSocket(ip: ip, port: port, timeoutMs: 800)
        guard fd >= 0 else { return nil }
        let t1 = DispatchTime.now().uptimeNanoseconds
        Darwin.close(fd)
        return Double(t1 - t0) / 1_000_000.0
    }

    private nonisolated static func measurePingRtt(ip: String, port: UInt16) -> Double? {
        let fd = connectSocket(ip: ip, port: port, timeoutMs: 900)
        guard fd >= 0 else { return nil }
        defer { Darwin.close(fd) }
        let t0 = DispatchTime.now().uptimeNanoseconds
        guard sendLine(fd, "PING"), readLine(fd) == "PONG" else { return nil }
        let t1 = DispatchTime.now().uptimeNanoseconds
        return Double(t1 - t0) / 1_000_000.0
    }

    private nonisolated static func queryPeerInfo(ip: String, port: UInt16) -> (usbSpeed: String, usbMbps: Int)? {
        let fd = connectSocket(ip: ip, port: port, timeoutMs: 900)
        guard fd >= 0 else { return nil }
        defer { Darwin.close(fd) }
        guard sendLine(fd, "INFO"), let jsonStr = readLine(fd),
              let data = jsonStr.data(using: .utf8),
              let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return nil
        }
        let sp = (obj["usbSpeed"] as? String) ?? "--"
        let mbps = (obj["usbMbps"] as? Int) ?? 0
        return (sp, mbps)
    }

    // MARK: - Benchmark Server (Port 19876)

    private func startBenchmarkServer() {
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { return }

        var yes: Int32 = 1
        setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &yes, socklen_t(MemoryLayout<Int32>.size))

        var sa = sockaddr_in()
        sa.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        sa.sin_family = sa_family_t(AF_INET)
        sa.sin_port = Self.servicePort.bigEndian
        sa.sin_addr = in_addr(s_addr: INADDR_ANY.bigEndian)

        let bindOk = withUnsafePointer(to: &sa) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size)) == 0
            }
        }
        guard bindOk, Darwin.listen(fd, 16) == 0 else {
            Darwin.close(fd)
            return
        }
        serverFd = fd

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            while let self, !self.stopLock.withLock({ $0 }) {
                var addr = sockaddr_in()
                var len = socklen_t(MemoryLayout<sockaddr_in>.size)
                let client = withUnsafeMutablePointer(to: &addr) {
                    $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                        Darwin.accept(fd, $0, &len)
                    }
                }
                if client < 0 {
                    if self.stopLock.withLock({ $0 }) { break }
                    continue
                }
                DispatchQueue.global(qos: .userInitiated).async {
                    self.handleServerClient(client)
                }
            }
        }
    }

    private nonisolated func handleServerClient(_ client: Int32) {
        defer { Darwin.close(client) }
        var one: Int32 = 1
        setsockopt(client, IPPROTO_TCP, TCP_NODELAY, &one, socklen_t(MemoryLayout<Int32>.size))
        setsockopt(client, SOL_SOCKET, SO_NOSIGPIPE, &one, socklen_t(MemoryLayout<Int32>.size))
        Self.applySocketTimeouts(client, seconds: 5)

        var bufSize: Int32 = 512 * 1024
        setsockopt(client, SOL_SOCKET, SO_SNDBUF, &bufSize, socklen_t(MemoryLayout<Int32>.size))
        setsockopt(client, SOL_SOCKET, SO_RCVBUF, &bufSize, socklen_t(MemoryLayout<Int32>.size))

        guard let line = Self.readLine(client) else { return }

        if line == "INFO" {
            let curLocalIp = activePairLock.withLock { $0.localIp }
            let reply = "{\"role\":\"mac\",\"os\":\"macOS\",\"ip\":\"\(curLocalIp)\"}"
            _ = Self.sendLine(client, reply)
            return
        }

        if line == "PING" {
            guard Self.sendLine(client, "PONG") else { return }
            while !stopLock.withLock({ $0 }), let next = Self.readLine(client), next == "PING" {
                if !Self.sendLine(client, "PONG") { break }
            }
            return
        }

        if line.hasPrefix("DOWN") {
            let parts = line.split(separator: " ")
            let durMs = min(10000, max(500, Int(parts.dropFirst().first ?? "3000") ?? 3000))
            let chunk = [UInt8](repeating: 0x5A, count: 128 * 1024)
            let start = DispatchTime.now().uptimeNanoseconds
            let limitNs = UInt64(durMs) * 1_000_000
            chunk.withUnsafeBytes { ptr in
                guard let base = ptr.baseAddress else { return }
                while DispatchTime.now().uptimeNanoseconds - start < limitNs && !stopLock.withLock({ $0 }) {
                    if !Self.sendAll(client, base, chunk.count) { break }
                }
            }
            Darwin.shutdown(client, SHUT_WR)
            return
        }

        if line == "UP" {
            var buf = [UInt8](repeating: 0, count: 128 * 1024)
            var total: Int64 = 0
            let start = DispatchTime.now().uptimeNanoseconds
            while !stopLock.withLock({ $0 }) && DispatchTime.now().uptimeNanoseconds - start < 15_000_000_000 {
                let n = buf.withUnsafeMutableBytes { ptr in
                    Darwin.recv(client, ptr.baseAddress, ptr.count, 0)
                }
                if n <= 0 { break }
                total += Int64(n)
            }
            let elapsedMs = max(1, Int64((DispatchTime.now().uptimeNanoseconds - start) / 1_000_000))
            _ = Self.sendLine(client, "DONE \(total) \(elapsedMs)")
        }
    }

    // MARK: - Benchmark Client

    func cancelSpeedTest() {
        let fdToWake = cancelLock.withLock { state -> Int32 in
            state.cancelled = true
            return state.activeFd
        }
        if fdToWake >= 0 {
            Darwin.shutdown(fdToWake, SHUT_RDWR)
        }
    }

    func openSpeedTestSheet() {
        if !speedState.running {
            speedState.phaseKey = "st_phase_ready"
            speedState.downPercent = 0
            speedState.upPercent = 0
            speedState.liveMbps = 0
            speedState.error = ""
        }
        showSpeedTestSheet = true
    }

    func startSpeedTest() {
        guard !speedState.running else { return }
        cancelLock.withLock { $0 = SpeedCancelState() }
        speedState.running = true
        speedState.phaseKey = "st_phase_ping"
        speedState.downPercent = 0
        speedState.upPercent = 0
        speedState.liveMbps = 0
        speedState.rttMs = nil
        speedState.downAvg = nil
        speedState.downPeak = nil
        speedState.upAvg = nil
        speedState.upPeak = nil
        speedState.error = ""

        let targetIp = activePeerIp
        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            guard let self else { return }
            self.executeSpeedTest(targetIp: targetIp)
        }
    }

    private nonisolated func registerSpeedFd(_ fd: Int32) -> Bool {
        guard fd >= 0 else { return false }
        let ok = cancelLock.withLock { state -> Bool in
            if state.cancelled { return false }
            state.activeFd = fd
            return true
        }
        if !ok {
            Darwin.close(fd)
        }
        return ok
    }

    private nonisolated func closeSpeedFd(_ fd: Int32) {
        cancelLock.withLock { state in
            if state.activeFd == fd {
                state.activeFd = -1
            }
        }
        Darwin.close(fd)
    }

    private nonisolated func isSpeedCancelled() -> Bool {
        cancelLock.withLock { $0.cancelled }
    }

    private nonisolated func finishCancelledIfNeeded() -> Bool {
        guard isSpeedCancelled() else { return false }
        DispatchQueue.main.async {
            self.speedState.running = false
            self.speedState.phaseKey = "st_phase_idle"
        }
        return true
    }

    private nonisolated func executeSpeedTest(targetIp: String) {
        let ip = targetIp

        // 1. RTT Latency
        let fdPing = Self.connectSocket(ip: ip, port: Self.servicePort, timeoutMs: 2500)
        if fdPing < 0 {
            DispatchQueue.main.async {
                self.speedState.running = false
                self.speedState.error = L10n.shared.t("st_no_peer", ip)
            }
            return
        }
        guard registerSpeedFd(fdPing) else {
            _ = finishCancelledIfNeeded()
            return
        }

        var rttSum = 0.0
        var rttCount = 0
        for i in 0..<6 {
            if isSpeedCancelled() { break }
            let t0 = DispatchTime.now().uptimeNanoseconds
            if Self.sendLine(fdPing, "PING"), Self.readLine(fdPing) == "PONG" {
                let dt = Double(DispatchTime.now().uptimeNanoseconds - t0) / 1_000_000.0
                if i > 0 {
                    rttSum += dt
                    rttCount += 1
                }
            }
        }
        closeSpeedFd(fdPing)
        if finishCancelledIfNeeded() { return }

        let avgRtt = rttCount > 0 ? rttSum / Double(rttCount) : 0
        DispatchQueue.main.async {
            self.speedState.rttMs = avgRtt
            self.speedState.phaseKey = "st_phase_down"
            self.speedState.downPercent = 5
        }

        // 2. Download (Windows -> Mac, dedicated 0 -> 100% progress)
        let fdDown = Self.connectSocket(ip: ip, port: Self.servicePort, timeoutMs: 2500)
        guard fdDown >= 0 else {
            DispatchQueue.main.async {
                self.speedState.running = false
                self.speedState.error = L10n.shared.t("st_no_peer", ip)
            }
            return
        }
        guard registerSpeedFd(fdDown) else {
            _ = finishCancelledIfNeeded()
            return
        }

        var bufSize: Int32 = 512 * 1024
        setsockopt(fdDown, SOL_SOCKET, SO_RCVBUF, &bufSize, socklen_t(MemoryLayout<Int32>.size))
        _ = Self.sendLine(fdDown, "DOWN 3000")

        var dlBuf = [UInt8](repeating: 0, count: 128 * 1024)
        var totalDl: Int64 = 0
        var winDl: Int64 = 0
        var dlPeak = 0.0
        let dlStart = DispatchTime.now().uptimeNanoseconds
        var winStart = dlStart

        while true {
            if isSpeedCancelled() { break }
            let n = dlBuf.withUnsafeMutableBytes { ptr in
                Darwin.recv(fdDown, ptr.baseAddress, ptr.count, 0)
            }
            if n <= 0 { break }
            totalDl += Int64(n)
            winDl += Int64(n)
            let now = DispatchTime.now().uptimeNanoseconds
            let winSec = Double(now - winStart) / 1_000_000_000.0
            if winSec >= 0.16 {
                let instMbps = (Double(winDl) * 8.0) / (winSec * 1_000_000.0)
                dlPeak = max(dlPeak, instMbps)
                let totalMs = Double(now - dlStart) / 1_000_000.0
                let pct = 5 + min(92, Int((totalMs / 3000.0) * 92.0))
                let peakSnap = dlPeak
                DispatchQueue.main.async {
                    self.speedState.liveMbps = instMbps
                    self.speedState.downPeak = peakSnap
                    self.speedState.downPercent = pct
                }
                winDl = 0
                winStart = now
            }
        }
        closeSpeedFd(fdDown)
        if finishCancelledIfNeeded() { return }

        let dlSec = max(0.001, Double(DispatchTime.now().uptimeNanoseconds - dlStart) / 1_000_000_000.0)
        let dlAvgFinal = (Double(totalDl) * 8.0) / (dlSec * 1_000_000.0)
        let dlPeakFinal = max(dlPeak, dlAvgFinal)

        DispatchQueue.main.async {
            self.speedState.downAvg = dlAvgFinal
            self.speedState.downPeak = dlPeakFinal
            self.speedState.downPercent = 100
            self.speedState.phaseKey = "st_phase_up"
            self.speedState.upPercent = 5
            self.speedState.liveMbps = 0
        }

        // 3. Upload (Mac -> Windows, dedicated 0 -> 100% progress)
        let fdUp = Self.connectSocket(ip: ip, port: Self.servicePort, timeoutMs: 2500)
        guard fdUp >= 0 else {
            DispatchQueue.main.async {
                self.speedState.running = false
                self.speedState.error = L10n.shared.t("st_no_peer", ip)
            }
            return
        }
        guard registerSpeedFd(fdUp) else {
            _ = finishCancelledIfNeeded()
            return
        }

        setsockopt(fdUp, SOL_SOCKET, SO_SNDBUF, &bufSize, socklen_t(MemoryLayout<Int32>.size))
        _ = Self.sendLine(fdUp, "UP")

        let ulChunk = [UInt8](repeating: 0xA5, count: 128 * 1024)
        var totalUl: Int64 = 0
        var winUl: Int64 = 0
        var ulPeak = 0.0
        let ulStart = DispatchTime.now().uptimeNanoseconds
        winStart = ulStart

        ulChunk.withUnsafeBytes { ptr in
            guard let base = ptr.baseAddress else { return }
            while DispatchTime.now().uptimeNanoseconds - ulStart < 3_000_000_000 {
                if isSpeedCancelled() { break }
                if !Self.sendAll(fdUp, base, ulChunk.count) { break }
                totalUl += Int64(ulChunk.count)
                winUl += Int64(ulChunk.count)
                let now = DispatchTime.now().uptimeNanoseconds
                let winSec = Double(now - winStart) / 1_000_000_000.0
                if winSec >= 0.16 {
                    let instMbps = (Double(winUl) * 8.0) / (winSec * 1_000_000.0)
                    ulPeak = max(ulPeak, instMbps)
                    let totalMs = Double(now - ulStart) / 1_000_000.0
                    let pct = 5 + min(92, Int((totalMs / 3000.0) * 92.0))
                    let peakSnap = ulPeak
                    DispatchQueue.main.async {
                        self.speedState.liveMbps = instMbps
                        self.speedState.upPeak = peakSnap
                        self.speedState.upPercent = pct
                    }
                    winUl = 0
                    winStart = now
                }
            }
        }
        Darwin.shutdown(fdUp, SHUT_WR)
        let doneReply = isSpeedCancelled() ? "" : (Self.readLine(fdUp) ?? "")
        closeSpeedFd(fdUp)
        if finishCancelledIfNeeded() { return }

        let ulSec = max(0.001, Double(DispatchTime.now().uptimeNanoseconds - ulStart) / 1_000_000_000.0)
        var ulAvgFinal = (Double(totalUl) * 8.0) / (ulSec * 1_000_000.0)
        if doneReply.hasPrefix("DONE ") {
            let parts = doneReply.split(separator: " ")
            if parts.count >= 3, let rxBytes = Double(parts[1]), let ms = Double(parts[2]), ms > 0 {
                ulAvgFinal = (rxBytes * 8.0) / ((ms / 1000.0) * 1_000_000.0)
            }
        }
        let ulPeakFinal = max(ulPeak, ulAvgFinal)
        let ulAvgCap = ulAvgFinal
        let finalBest = max(dlAvgFinal, ulAvgFinal)

        DispatchQueue.main.async {
            self.speedState.upAvg = ulAvgCap
            self.speedState.upPeak = ulPeakFinal
            self.speedState.upPercent = 100
            self.speedState.liveMbps = finalBest
            self.speedState.phaseKey = "st_phase_done"
            self.speedState.hasCompletedOnce = true
            self.speedState.running = false
            self.appendLog(String(format: "[BENCH] RTT=%.2fms Down=%.1fMbps Up=%.1fMbps", avgRtt, dlAvgFinal, ulAvgCap))
        }
    }
}
