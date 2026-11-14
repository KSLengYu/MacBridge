using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MacBridgeUI;

public sealed record SpeedTestProgress(
    string PhaseKey,
    int DownPercent,
    int UpPercent,
    double LiveMbps,
    double? RttMs,
    double? DownAvgMbps,
    double? DownPeakMbps,
    double? UpAvgMbps,
    double? UpPeakMbps);

public sealed record SpeedTestResult(
    bool Success,
    string ErrorMessage,
    double RttMs,
    double DownAvgMbps,
    double DownPeakMbps,
    double UpAvgMbps,
    double UpPeakMbps);

/// <summary>
/// Built-in zero-disk TCP benchmark server and client over port 19876 for MacBridgeUI <-> MacBridge.app.
/// </summary>
public sealed class SpeedTestService : IDisposable
{
    public const int Port = 19876;
    private const int ChunkBytes = 128 * 1024;

    private TcpListener? _listener;
    private readonly CancellationTokenSource _serverCts = new();
    private readonly Func<(string UsbSpeed, int UsbMbps, bool BridgeUp)> _infoProvider;

    public SpeedTestService(Func<(string UsbSpeed, int UsbMbps, bool BridgeUp)> infoProvider)
    {
        _infoProvider = infoProvider;
    }

    public void StartServer()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            _ = AcceptLoopAsync(_serverCts.Token);
        }
        catch
        {
            // Non-fatal if another instance holds port 19876
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is not null)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                try { await Task.Delay(200, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using var clientCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        clientCts.CancelAfter(TimeSpan.FromSeconds(15));
        CancellationToken token = clientCts.Token;

        try
        {
            using (client)
            {
                client.NoDelay = true;
                client.SendBufferSize = 512 * 1024;
                client.ReceiveBufferSize = 512 * 1024;
                using NetworkStream stream = client.GetStream();

                var line = await ReadLineAsciiAsync(stream, token);
                if (string.IsNullOrWhiteSpace(line)) return;

                if (line == "INFO")
                {
                    var info = _infoProvider();
                    string json = JsonSerializer.Serialize(new
                    {
                        role = "windows",
                        usbSpeed = info.UsbSpeed,
                        usbMbps = info.UsbMbps,
                        bridgeUp = info.BridgeUp
                    }) + "\n";
                    byte[] payload = Encoding.UTF8.GetBytes(json);
                    await stream.WriteAsync(payload, token);
                    return;
                }

                if (line == "PING")
                {
                    byte[] pong = "PONG\n"u8.ToArray();
                    await stream.WriteAsync(pong, token);
                    while (!token.IsCancellationRequested)
                    {
                        var next = await ReadLineAsciiAsync(stream, token);
                        if (next != "PING") break;
                        await stream.WriteAsync(pong, token);
                    }
                    return;
                }

                if (line.StartsWith("DOWN", StringComparison.Ordinal))
                {
                    int durationMs = 3000;
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int parsed))
                        durationMs = Math.Clamp(parsed, 500, 10000);

                    var buf = new byte[ChunkBytes];
                    for (int i = 0; i < buf.Length; i += 64) buf[i] = 0x5A;

                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < durationMs && !token.IsCancellationRequested)
                    {
                        await stream.WriteAsync(buf, token);
                    }
                    try { client.Client.Shutdown(SocketShutdown.Send); } catch { }
                    return;
                }

                if (line == "UP")
                {
                    var buf = new byte[ChunkBytes];
                    long total = 0;
                    var sw = Stopwatch.StartNew();
                    while (!token.IsCancellationRequested)
                    {
                        int n = await stream.ReadAsync(buf, token);
                        if (n <= 0) break;
                        total += n;
                    }
                    sw.Stop();
                    long elapsedMs = Math.Max(1, sw.ElapsedMilliseconds);
                    byte[] reply = Encoding.ASCII.GetBytes($"DONE {total} {elapsedMs}\n");
                    await stream.WriteAsync(reply, token);
                }
            }
        }
        catch
        {
            // Ignore per-client disconnects / timeouts cleanly
        }
    }

    private static async Task<string?> ReadLineAsciiAsync(NetworkStream stream, CancellationToken ct)
    {
        var sb = new StringBuilder(64);
        var b = new byte[1];
        while (sb.Length < 256)
        {
            int n = await stream.ReadAsync(b, ct);
            if (n <= 0) return sb.Length > 0 ? sb.ToString().Trim() : null;
            char ch = (char)b[0];
            if (ch == '\n') break;
            if (ch != '\r') sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    public static async Task<double?> ProbeLatencyAsync(string peerIp, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient { NoDelay = true };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(1200);
            var sw = Stopwatch.StartNew();
            await client.ConnectAsync(peerIp, Port, timeoutCts.Token);
            using var stream = client.GetStream();
            byte[] ping = "PING\n"u8.ToArray();
            sw.Restart();
            await stream.WriteAsync(ping, timeoutCts.Token);
            string? resp = await ReadLineAsciiAsync(stream, timeoutCts.Token);
            sw.Stop();
            return resp == "PONG" ? sw.Elapsed.TotalMilliseconds : null;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<SpeedTestResult> RunBenchmarkAsync(
        string peerIp,
        IProgress<SpeedTestProgress> progress,
        CancellationToken ct)
    {
        try
        {
            // 1. RTT Latency (5 samples)
            progress.Report(new SpeedTestProgress("st_phase_ping", 0, 0, 0, null, null, null, null, null));
            double rttSum = 0;
            int rttOk = 0;
            using (var pingClient = new TcpClient { NoDelay = true })
            {
                using var connCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connCts.CancelAfter(2500);
                await pingClient.ConnectAsync(peerIp, Port, connCts.Token);
                using var stream = pingClient.GetStream();
                byte[] ping = "PING\n"u8.ToArray();
                var sw = new Stopwatch();
                for (int i = 0; i < 6; i++)
                {
                    sw.Restart();
                    await stream.WriteAsync(ping, connCts.Token);
                    string? reply = await ReadLineAsciiAsync(stream, connCts.Token);
                    sw.Stop();
                    if (reply == "PONG" && i > 0) // skip cold first sample
                    {
                        rttSum += sw.Elapsed.TotalMilliseconds;
                        rttOk++;
                    }
                }
            }

            double avgRtt = rttOk > 0 ? rttSum / rttOk : 0;

            // 2. Download (Peer -> Local, own 0% -> 100% progress)
            progress.Report(new SpeedTestProgress("st_phase_down", 5, 0, 0, avgRtt, null, null, null, null));
            double downAvg = 0, downPeak = 0;
            using (var dlClient = new TcpClient { NoDelay = true, ReceiveBufferSize = 512 * 1024 })
            {
                using var dlCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                dlCts.CancelAfter(8000);
                await dlClient.ConnectAsync(peerIp, Port, dlCts.Token);
                using var stream = dlClient.GetStream();
                await stream.WriteAsync("DOWN 3000\n"u8.ToArray(), dlCts.Token);

                var buf = new byte[ChunkBytes];
                long totalBytes = 0, windowBytes = 0;
                var totalSw = Stopwatch.StartNew();
                var winSw = Stopwatch.StartNew();

                while (!dlCts.IsCancellationRequested)
                {
                    int n = await stream.ReadAsync(buf, dlCts.Token);
                    if (n <= 0) break;
                    totalBytes += n;
                    windowBytes += n;

                    if (winSw.ElapsedMilliseconds >= 160)
                    {
                        double instMbps = (windowBytes * 8.0) / (winSw.Elapsed.TotalSeconds * 1_000_000.0);
                        downPeak = Math.Max(downPeak, instMbps);
                        int pct = 5 + (int)Math.Min(92, (totalSw.ElapsedMilliseconds / 3000.0) * 92);
                        progress.Report(new SpeedTestProgress("st_phase_down", pct, 0, instMbps, avgRtt, null, downPeak, null, null));
                        windowBytes = 0;
                        winSw.Restart();
                    }
                }
                totalSw.Stop();
                downAvg = totalSw.Elapsed.TotalSeconds > 0
                    ? (totalBytes * 8.0) / (totalSw.Elapsed.TotalSeconds * 1_000_000.0)
                    : 0;
                downPeak = Math.Max(downPeak, downAvg);
            }

            // 3. Upload (Local -> Peer, own 0% -> 100% progress)
            progress.Report(new SpeedTestProgress("st_phase_up", 100, 5, 0, avgRtt, downAvg, downPeak, null, null));
            double upAvg = 0, upPeak = 0;
            using (var ulClient = new TcpClient { NoDelay = true, SendBufferSize = 512 * 1024 })
            {
                using var ulCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                ulCts.CancelAfter(8000);
                await ulClient.ConnectAsync(peerIp, Port, ulCts.Token);
                using var stream = ulClient.GetStream();
                await stream.WriteAsync("UP\n"u8.ToArray(), ulCts.Token);

                var buf = new byte[ChunkBytes];
                for (int i = 0; i < buf.Length; i += 64) buf[i] = 0xA5;

                long totalBytes = 0, windowBytes = 0;
                var totalSw = Stopwatch.StartNew();
                var winSw = Stopwatch.StartNew();

                while (totalSw.ElapsedMilliseconds < 3000 && !ulCts.IsCancellationRequested)
                {
                    await stream.WriteAsync(buf, ulCts.Token);
                    totalBytes += buf.Length;
                    windowBytes += buf.Length;

                    if (winSw.ElapsedMilliseconds >= 160)
                    {
                        double instMbps = (windowBytes * 8.0) / (winSw.Elapsed.TotalSeconds * 1_000_000.0);
                        upPeak = Math.Max(upPeak, instMbps);
                        int pct = 5 + (int)Math.Min(92, (totalSw.ElapsedMilliseconds / 3000.0) * 92);
                        progress.Report(new SpeedTestProgress("st_phase_up", 100, pct, instMbps, avgRtt, downAvg, downPeak, null, upPeak));
                        windowBytes = 0;
                        winSw.Restart();
                    }
                }
                ulClient.Client.Shutdown(SocketShutdown.Send);
                string? doneReply = await ReadLineAsciiAsync(stream, ulCts.Token);
                totalSw.Stop();

                if (!string.IsNullOrEmpty(doneReply) && doneReply.StartsWith("DONE ", StringComparison.Ordinal))
                {
                    var parts = doneReply.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3 && long.TryParse(parts[1], out long rxBytes) && long.TryParse(parts[2], out long ms) && ms > 0)
                    {
                        upAvg = (rxBytes * 8.0) / ((ms / 1000.0) * 1_000_000.0);
                    }
                }
                if (upAvg <= 0 && totalSw.Elapsed.TotalSeconds > 0)
                {
                    upAvg = (totalBytes * 8.0) / (totalSw.Elapsed.TotalSeconds * 1_000_000.0);
                }
                upPeak = Math.Max(upPeak, upAvg);
            }

            progress.Report(new SpeedTestProgress("st_phase_done", 100, 100, Math.Max(downAvg, upAvg), avgRtt, downAvg, downPeak, upAvg, upPeak));
            return new SpeedTestResult(true, string.Empty, avgRtt, downAvg, downPeak, upAvg, upPeak);
        }
        catch (OperationCanceledException)
        {
            return new SpeedTestResult(false, "Cancelled", 0, 0, 0, 0, 0);
        }
        catch (Exception ex)
        {
            return new SpeedTestResult(false, ex.Message, 0, 0, 0, 0, 0);
        }
    }

    public void Dispose()
    {
        _serverCts.Cancel();
        try { _listener?.Stop(); } catch { }
        _serverCts.Dispose();
    }
}
