using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EngineDjPlaylistSync;

public static class OfficialOfflineAnalyzerBridge
{
    private const int AnalyzerTimeoutMs = 45_000;

    public static bool TryFindOfflineAnalyzer(out string analyzerPath)
    {
        var candidates = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrWhiteSpace(programFiles))
            candidates.Add(Path.Combine(programFiles, "Engine DJ", "OfflineAnalyzer.exe"));
        if (!string.IsNullOrWhiteSpace(programFilesX86))
            candidates.Add(Path.Combine(programFilesX86, "Engine DJ", "OfflineAnalyzer.exe"));

        candidates.Add(@"C:\Program Files\Engine DJ\OfflineAnalyzer.exe");
        candidates.Add(@"C:\Program Files (x86)\Engine DJ\OfflineAnalyzer.exe");

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                analyzerPath = candidate;
                return true;
            }
        }

        analyzerPath = string.Empty;
        return false;
    }

    public static async Task<IReadOnlyDictionary<string, ExternalAnalysisResult>> AnalyzeFilesAsync(
        IReadOnlyCollection<string> files,
        bool includeKeyDetection,
        ExternalAnalysisOptions? options = null,
        bool captureOfficialAnalyzerFrames = false,
        int maxConcurrentTracks = 4,
        Action<TrackScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= ExternalAnalysisOptions.Default;
        if (!TryFindOfflineAnalyzer(out var analyzerPath))
            throw new InvalidOperationException("Engine DJ OfflineAnalyzer.exe was not found on this computer.");

        var uniqueFiles = files
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new ConcurrentDictionary<string, ExternalAnalysisResult>(StringComparer.OrdinalIgnoreCase);
        var completed = 0;

        // Engine DJ itself launches several OfflineAnalyzer.exe workers connected to
        // the same local control port.  The first bridge build processed one file at
        // a time, which worked but was much slower than the official application.
        // This version runs several official analyzer worker processes in parallel.
        // Each worker still gets an isolated localhost listener/port so the protocol
        // remains simple and jobs cannot be cross-wired between tracks.
        var maxDegree = captureOfficialAnalyzerFrames ? 1 : Math.Max(1, Math.Min(maxConcurrentTracks, 16));

        await Parallel.ForEachAsync(uniqueFiles, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegree,
            CancellationToken = cancellationToken
        }, async (file, token) =>
        {
            token.ThrowIfCancellationRequested();
            var current = Math.Min(Volatile.Read(ref completed) + 1, uniqueFiles.Count);
            progress?.Invoke(new TrackScanProgress(current, uniqueFiles.Count, "Official Engine DJ analysing " + Path.GetFileName(file)));
            var analysis = await AnalyzeFileAsync(analyzerPath, file, includeKeyDetection, options, captureOfficialAnalyzerFrames, token).ConfigureAwait(false);
            results[file] = analysis;
            var done = Interlocked.Increment(ref completed);
            progress?.Invoke(new TrackScanProgress(done, uniqueFiles.Count, "Official Engine DJ analysed " + Path.GetFileName(file)));
        }).ConfigureAwait(false);

        return new Dictionary<string, ExternalAnalysisResult>(results, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<ExternalAnalysisResult> AnalyzeFileAsync(
        string analyzerPath,
        string filePath,
        bool includeKeyDetection,
        ExternalAnalysisOptions options,
        bool captureOfficialAnalyzerFrames,
        CancellationToken cancellationToken)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var process = StartAnalyzerProcess(analyzerPath, port);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(AnalyzerTimeoutMs);
            using var client = await listener.AcceptTcpClientAsync(timeout.Token).ConfigureAwait(false);
            client.NoDelay = true;
            await using var stream = client.GetStream();

            var captureDirectory = TryCreateCaptureDirectory(filePath, captureOfficialAnalyzerFrames);
            var request = BuildRequest(filePath, options, includeKeyDetection);
            if (captureDirectory is not null)
            {
                await File.WriteAllBytesAsync(Path.Combine(captureDirectory, "request.bin"), request, timeout.Token).ConfigureAwait(false);
                await WriteRequestCaptureInfoAsync(captureDirectory, filePath, analyzerPath, port, options, includeKeyDetection, request, timeout.Token).ConfigureAwait(false);
            }

            await stream.WriteAsync(request, timeout.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

            var response = await ReadAnalysisResponseAsync(stream, filePath, includeKeyDetection, captureDirectory, timeout.Token).ConfigureAwait(false);
            TryStopProcess(process);
            return response;
        }
        catch
        {
            TryStopProcess(process);
            throw;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Process StartAnalyzerProcess(string analyzerPath, int port)
    {
        var psi = new ProcessStartInfo
        {
            FileName = analyzerPath,
            Arguments = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            WorkingDirectory = Path.GetDirectoryName(analyzerPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Engine DJ OfflineAnalyzer.exe.");
    }

    private static byte[] BuildRequest(string filePath, ExternalAnalysisOptions options, bool includeKeyDetection)
    {
        var analyzerPath = ToAnalyzerPath(filePath);
        var pathBytes = System.Text.Encoding.UTF8.GetBytes(analyzerPath);
        using var payload = new MemoryStream();

        WriteInt32Little(payload, ClampBpm(options.MinBpm));
        WriteInt32Little(payload, ClampBpm(options.MaxBpm));

        // Captured Engine DJ request flags after the BPM range. Keep these byte-for-byte
        // compatible with the observed desktop app request.
        payload.WriteByte(1);
        payload.WriteByte(1);
        payload.WriteByte(0);
        payload.WriteByte(1);
        payload.WriteByte(0);
        payload.Write(pathBytes, 0, pathBytes.Length);

        var payloadBytes = payload.ToArray();
        using var request = new MemoryStream();
        WriteInt32Little(request, 0);
        WriteInt32Little(request, 1);
        WriteInt32Little(request, payloadBytes.Length);
        WriteInt32Little(request, 0);

        // The fifth frame field is not arbitrary: in the captured Engine DJ
        // request it exactly equals CRC32(payload). OfflineAnalyzer rejects the
        // job when this field does not match, which causes it to hold/close the
        // connection without returning analysis frames.
        WriteUInt32Little(request, Crc32(payloadBytes));

        // The sixth field appears to be a small job/session id. It is echoed only
        // for correlation, so any positive value is acceptable. Keep it simple and
        // stable rather than using a large random value.
        WriteInt32Little(request, Random.Shared.Next(1, 65535));
        request.Write(payloadBytes, 0, payloadBytes.Length);
        return request.ToArray();
    }

    private static int ClampBpm(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
        return Math.Max(1, Math.Min(300, (int)Math.Round(value, MidpointRounding.AwayFromZero)));
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (var value in data)
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    private static void WriteUInt32Little(Stream stream, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        stream.Write(b);
    }

    private static string ToAnalyzerPath(string filePath)
    {
        var full = Path.GetFullPath(filePath).Replace('\\', '/');
        if (full.Length >= 2 && full[1] == ':')
        {
            var drive = char.ToUpperInvariant(full[0]);
            return "FTE" + drive + full[1..];
        }
        return full;
    }

    private static async Task WriteRequestCaptureInfoAsync(
        string captureDirectory,
        string filePath,
        string analyzerPath,
        int port,
        ExternalAnalysisOptions options,
        bool includeKeyDetection,
        byte[] request,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = new
            {
                capturedAtUtc = DateTimeOffset.UtcNow,
                sourcePath = filePath,
                analyzerPath,
                port,
                includeKeyDetection,
                bpmRange = new { min = options.MinBpm, max = options.MaxBpm },
                requestLength = request.Length,
                requestSha256 = Sha256Hex(request),
                note = "Official OfflineAnalyzer request and decoded response payload capture. Use this folder for waveform reverse-engineering; the app import does not depend on these debug files."
            };
            await File.WriteAllTextAsync(
                Path.Combine(captureDirectory, "request.json"),
                JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Diagnostic capture only.
        }
    }

    private static string? TryCreateCaptureDirectory(string filePath, bool captureOfficialAnalyzerFrames)
    {
        if (!captureOfficialAnalyzerFrames)
            return null;

        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EngineDjPlaylistSync",
                "OfficialAnalyzerCaptures",
                DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture) + "-" + SanitizeFileName(Path.GetFileNameWithoutExtension(filePath)));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "source.txt"), filePath);
            return root;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "track" : result;
    }

    private static async Task<ExternalAnalysisResult> ReadAnalysisResponseAsync(NetworkStream stream, string filePath, bool includeKeyDetection, string? captureDirectory, CancellationToken cancellationToken)
    {
        byte[]? lastTrackData = null;
        byte[]? lastBeatData = null;
        double bpm = 0;
        var loadedWaveform = new WaveformAccumulator();
        var browserWaveform = new WaveformAccumulator();
        var frameIndex = 0;
        var frames = new List<AnalyzerFrameCapture>();

        var completedNormally = false;
        var endReason = "stream ended before type 15 completion frame";

        while (true)
        {
            byte[] header;
            try
            {
                header = await ReadExactAsync(stream, 24, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                endReason = "connection closed before type 15 completion frame";
                break;
            }
            catch (OperationCanceledException) when (frames.Count > 0)
            {
                endReason = "timeout/cancellation after one or more frames; finalising captured partial payloads";
                break;
            }

            var zero = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
            var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));
            var zero2 = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12, 4));
            var field4 = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16, 4));
            var field5 = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20, 4));

            if (zero != 0 || zero2 != 0 || length < 0 || length > 50_000_000)
                throw new InvalidDataException("Unexpected response frame from Engine DJ OfflineAnalyzer.exe.");

            var payload = await ReadExactAsync(stream, length, cancellationToken).ConfigureAwait(false);
            if (captureDirectory is not null)
            {
                var thisFrameIndex = frameIndex++;
                var payloadName = string.Format(System.Globalization.CultureInfo.InvariantCulture, "frame_{0:0000}_type_{1}_payload.bin", thisFrameIndex, type);
                var fullFrameName = string.Format(System.Globalization.CultureInfo.InvariantCulture, "frame_{0:0000}_type_{1}_fullframe.bin", thisFrameIndex, type);
                await File.WriteAllBytesAsync(Path.Combine(captureDirectory, payloadName), payload, cancellationToken).ConfigureAwait(false);
                var fullFrame = new byte[header.Length + payload.Length];
                Buffer.BlockCopy(header, 0, fullFrame, 0, header.Length);
                Buffer.BlockCopy(payload, 0, fullFrame, header.Length, payload.Length);
                await File.WriteAllBytesAsync(Path.Combine(captureDirectory, fullFrameName), fullFrame, cancellationToken).ConfigureAwait(false);
                frames.Add(new AnalyzerFrameCapture(
                    thisFrameIndex,
                    type,
                    length,
                    field4,
                    field5,
                    payloadName,
                    fullFrameName,
                    Sha256Hex(payload),
                    Sha256Hex(fullFrame)));
            }

            switch (type)
            {
                case 6:
                    lastBeatData = payload;
                    break;
                case 7:
                    lastTrackData = payload;
                    break;
                case 8:
                    loadedWaveform.Add(payload);
                    break;
                case 9:
                    browserWaveform.Add(payload);
                    break;
                case 15:
                    if (payload.Length >= 8)
                        bpm = BitConverter.ToDouble(payload, 0);
                    completedNormally = true;
                    endReason = "type 15 completion frame";
                    if (captureDirectory is not null)
                        await WriteDecodedCaptureOutputsAsync(captureDirectory, filePath, bpm, lastTrackData, lastBeatData, loadedWaveform, browserWaveform, frames, true, endReason, cancellationToken).ConfigureAwait(false);
                    return BuildResult(filePath, bpm, includeKeyDetection, lastTrackData, lastBeatData, loadedWaveform, browserWaveform);
            }
        }

        if (captureDirectory is not null)
            await WriteDecodedCaptureOutputsAsync(captureDirectory, filePath, bpm, lastTrackData, lastBeatData, loadedWaveform, browserWaveform, frames, completedNormally, endReason, CancellationToken.None).ConfigureAwait(false);

        return BuildResult(filePath, bpm, includeKeyDetection, lastTrackData, lastBeatData, loadedWaveform, browserWaveform);
    }

    private static ExternalAnalysisResult BuildResult(
        string filePath,
        double bpm,
        bool includeKeyDetection,
        byte[]? trackDataPayload,
        byte[]? beatDataPayload,
        WaveformAccumulator loadedWaveform,
        WaveformAccumulator browserWaveform)
    {
        if (trackDataPayload is null || beatDataPayload is null)
            throw new InvalidDataException("Engine DJ OfflineAnalyzer.exe did not return required analysis data.");

        var sampleRate = ReadDoubleBig(trackDataPayload, 0);
        var sampleCount = ReadInt64Big(trackDataPayload, 8);
        var engineKey = includeKeyDetection && trackDataPayload.Length >= 28 ? BinaryPrimitives.ReadInt32BigEndian(trackDataPayload.AsSpan(24, 4)) : -1;
        if (!includeKeyDetection && trackDataPayload.Length >= 28)
        {
            var editableTrackData = trackDataPayload.ToArray();
            BinaryPrimitives.WriteInt32BigEndian(editableTrackData.AsSpan(24, 4), -1);
            trackDataPayload = editableTrackData;
        }

        return new ExternalAnalysisResult(
            Path.GetFullPath(filePath),
            (int)Math.Round(sampleRate, MidpointRounding.AwayFromZero),
            sampleCount,
            bpm,
            engineKey,
            ZBlob(trackDataPayload),
            ZBlob(loadedWaveform.BuildPayload()),
            ZBlob(beatDataPayload),
            PackEmptyQuickCues(),
            PackEmptyLoops(),
            ZBlob(browserWaveform.BuildPayload()));
    }


    private static async Task WriteDecodedCaptureOutputsAsync(
        string captureDirectory,
        string filePath,
        double bpm,
        byte[]? trackDataPayload,
        byte[]? beatDataPayload,
        WaveformAccumulator loadedWaveform,
        WaveformAccumulator browserWaveform,
        IReadOnlyList<AnalyzerFrameCapture> frames,
        bool completedNormally,
        string endReason,
        CancellationToken cancellationToken)
    {
        try
        {
            var decodedDirectory = Path.Combine(captureDirectory, "decoded");
            Directory.CreateDirectory(decodedDirectory);

            var summary = new StringBuilder();
            summary.AppendLine("Engine DJ OfflineAnalyzer decoded capture");
            summary.AppendLine("========================================");
            summary.AppendLine("Source: " + filePath);
            summary.AppendLine("Completed normally: " + completedNormally.ToString());
            summary.AppendLine("End reason: " + endReason);
            summary.AppendLine("BPM: " + bpm.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture));

            if (trackDataPayload is not null)
            {
                await WritePayloadAndHashAsync(decodedDirectory, "trackData_payload.bin", trackDataPayload, summary, cancellationToken).ConfigureAwait(false);
                await WritePayloadAndHashAsync(decodedDirectory, "trackData_engine_zblob.bin", ZBlob(trackDataPayload), summary, cancellationToken).ConfigureAwait(false);
                await WriteHexPreviewAsync(decodedDirectory, "trackData_payload.hex.txt", trackDataPayload, cancellationToken).ConfigureAwait(false);
                if (trackDataPayload.Length >= 28)
                {
                    var sampleRate = ReadDoubleBig(trackDataPayload, 0);
                    var sampleCount = ReadInt64Big(trackDataPayload, 8);
                    var key = BinaryPrimitives.ReadInt32BigEndian(trackDataPayload.AsSpan(24, 4));
                    summary.AppendLine("trackData sampleRate: " + sampleRate.ToString("0.############", System.Globalization.CultureInfo.InvariantCulture));
                    summary.AppendLine("trackData sampleCount: " + sampleCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    summary.AppendLine("trackData Engine key value: " + key.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            if (beatDataPayload is not null)
            {
                await WritePayloadAndHashAsync(decodedDirectory, "beatData_payload.bin", beatDataPayload, summary, cancellationToken).ConfigureAwait(false);
                await WritePayloadAndHashAsync(decodedDirectory, "beatData_engine_zblob.bin", ZBlob(beatDataPayload), summary, cancellationToken).ConfigureAwait(false);
                await WriteHexPreviewAsync(decodedDirectory, "beatData_payload.hex.txt", beatDataPayload, cancellationToken).ConfigureAwait(false);
            }

            if (loadedWaveform.HasData)
            {
                var loadedPayload = loadedWaveform.BuildPayload();
                await WritePayloadAndHashAsync(decodedDirectory, "overviewWaveFormData_payload.bin", loadedPayload, summary, cancellationToken).ConfigureAwait(false);
                await WritePayloadAndHashAsync(decodedDirectory, "overviewWaveFormData_engine_zblob.bin", ZBlob(loadedPayload), summary, cancellationToken).ConfigureAwait(false);
                await WriteHexPreviewAsync(decodedDirectory, "overviewWaveFormData_payload.hex.txt", loadedPayload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                summary.AppendLine("overviewWaveFormData_payload.bin: not available");
            }

            if (browserWaveform.HasData)
            {
                var browserPayload = browserWaveform.BuildPayload();
                await WritePayloadAndHashAsync(decodedDirectory, "overview_rgb_cache.rgb", browserPayload, summary, cancellationToken).ConfigureAwait(false);
                await WritePayloadAndHashAsync(decodedDirectory, "overview_rgb_cache_engine_zblob.bin", ZBlob(browserPayload), summary, cancellationToken).ConfigureAwait(false);
                await WriteHexPreviewAsync(decodedDirectory, "overview_rgb_cache.hex.txt", browserPayload, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                summary.AppendLine("overview_rgb_cache.rgb: not available");
            }

            await WriteFrameManifestAsync(captureDirectory, decodedDirectory, frames, summary, completedNormally, endReason, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(decodedDirectory, "summary.txt"), summary.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Capture output is for diagnostics only. Never fail an import because
            // the optional decoded capture files could not be written.
        }
    }

    private static async Task WriteFrameManifestAsync(
        string captureDirectory,
        string decodedDirectory,
        IReadOnlyList<AnalyzerFrameCapture> frames,
        StringBuilder summary,
        bool completedNormally,
        string endReason,
        CancellationToken cancellationToken)
    {
        var manifest = new
        {
            completedNormally,
            endReason,
            frameCount = frames.Count,
            typeCounts = frames
                .GroupBy(frame => frame.Type)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), group => group.Count()),
            frames = frames.Select(frame => new
            {
                index = frame.Index,
                type = frame.Type,
                length = frame.Length,
                headerField4 = frame.HeaderField4,
                headerField5 = frame.HeaderField5,
                payloadFile = frame.PayloadFile,
                fullFrameFile = frame.FullFrameFile,
                payloadSha256 = frame.PayloadSha256,
                fullFrameSha256 = frame.FullFrameSha256
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(captureDirectory, "frame_manifest.json"), json, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(decodedDirectory, "frame_manifest.json"), json, cancellationToken).ConfigureAwait(false);
        summary.AppendLine("Frames captured: " + frames.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var group in frames.GroupBy(frame => frame.Type).OrderBy(group => group.Key))
            summary.AppendLine("Frame type " + group.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task WriteHexPreviewAsync(string directory, string fileName, byte[] payload, CancellationToken cancellationToken)
    {
        const int maxBytes = 4096;
        var length = Math.Min(payload.Length, maxBytes);
        var builder = new StringBuilder();
        builder.AppendLine("Length: " + payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes");
        builder.AppendLine("Showing first " + length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes");
        for (var offset = 0; offset < length; offset += 16)
        {
            var count = Math.Min(16, length - offset);
            builder.Append(offset.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append("  ");
            for (var i = 0; i < count; i++)
            {
                if (i == 8) builder.Append(' ');
                builder.Append(payload[offset + i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                builder.Append(' ');
            }
            builder.AppendLine();
        }
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WritePayloadAndHashAsync(
        string directory,
        string fileName,
        byte[] payload,
        StringBuilder summary,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, payload, cancellationToken).ConfigureAwait(false);
        var hash = Sha256Hex(payload);
        summary.AppendLine(fileName + ": " + payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes, sha256=" + hash);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                throw new EndOfStreamException("Engine DJ OfflineAnalyzer.exe closed the connection before the analysis response was complete.");
            offset += read;
        }
        return buffer;
    }

    private sealed record AnalyzerFrameCapture(
        int Index,
        uint Type,
        int Length,
        uint HeaderField4,
        uint HeaderField5,
        string PayloadFile,
        string FullFrameFile,
        string PayloadSha256,
        string FullFrameSha256);

    private static string Sha256Hex(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    private sealed class WaveformAccumulator
    {
        private byte[]? _firstHeader;
        private byte[]? _lastPayload;
        private readonly MemoryStream _entries = new();
        private int _entryCount;

        public bool HasData => _firstHeader is not null && _lastPayload is not null && _entryCount > 0;

        public void Add(byte[] payload)
        {
            if (payload.Length < 27)
                return;

            _firstHeader ??= payload.Take(24).ToArray();
            _lastPayload = payload;
            var count = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(4, 4));
            if (count <= 0)
                return;

            var bytesToCopy = Math.Min(count * 3, payload.Length - 24);
            _entries.Write(payload, 24, bytesToCopy);
            _entryCount += bytesToCopy / 3;
        }

        public byte[] BuildPayload()
        {
            if (_firstHeader is null || _lastPayload is null || _entryCount == 0)
                throw new InvalidDataException("Engine DJ OfflineAnalyzer.exe did not return waveform data.");

            using var output = new MemoryStream();
            output.Write(_firstHeader, 0, 4);
            Span<byte> countBytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(countBytes, _entryCount);
            output.Write(countBytes);
            output.Write(_firstHeader, 8, 16);
            _entries.Position = 0;
            _entries.CopyTo(output);
            output.Write(_lastPayload, _lastPayload.Length - 3, 3);
            return output.ToArray();
        }
    }

    private static void TryStopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                if (!process.WaitForExit(750))
                    process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static double ReadDoubleBig(byte[] data, int offset) => BitConverter.Int64BitsToDouble(ReadInt64Big(data, offset));

    private static long ReadInt64Big(byte[] data, int offset)
    {
        if (data.Length < offset + 8) return 0;
        return BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset, 8));
    }

    private static byte[] ZBlob(byte[] payload)
    {
        using var output = new MemoryStream();
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, payload.Length);
        output.Write(len);
        using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            z.Write(payload, 0, payload.Length);
        return output.ToArray();
    }

    private static byte[] PackEmptyQuickCues()
    {
        using var ms = new MemoryStream();
        WriteInt64Big(ms, 8);
        for (var i = 0; i < 8; i++)
        {
            ms.WriteByte(0);
            WriteDoubleBig(ms, -1.0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
            ms.WriteByte(0);
        }
        WriteDoubleBig(ms, -1.0);
        ms.WriteByte(0);
        WriteDoubleBig(ms, -1.0);
        return ZBlob(ms.ToArray());
    }

    private static byte[] PackEmptyLoops()
    {
        using var ms = new MemoryStream();
        WriteInt64Little(ms, 8);
        for (var i = 0; i < 8; i++)
        {
            ms.WriteByte(0);
            WriteDoubleLittle(ms, -1.0);
            WriteDoubleLittle(ms, -1.0);
            for (var j = 0; j < 6; j++) ms.WriteByte(0);
        }
        return ms.ToArray();
    }

    private static void WriteDoubleBig(Stream stream, double value) => WriteInt64Big(stream, BitConverter.DoubleToInt64Bits(value));
    private static void WriteDoubleLittle(Stream stream, double value) => WriteInt64Little(stream, BitConverter.DoubleToInt64Bits(value));

    private static void WriteInt64Big(Stream stream, long value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, value);
        stream.Write(b);
    }

    private static void WriteInt64Little(Stream stream, long value)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(b, value);
        stream.Write(b);
    }

    private static void WriteInt32Little(Stream stream, int value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(b, value);
        stream.Write(b);
    }
}
