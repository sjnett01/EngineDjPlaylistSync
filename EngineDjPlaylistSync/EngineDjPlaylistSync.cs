using Microsoft.Data.Sqlite;
using System.Globalization;

namespace EngineDjPlaylistSync;

public sealed class EngineDjPlaylistSync : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;
    private readonly string _databaseUuid;

    public static readonly string[] AudioExtensions = [".mp3", ".flac", ".wav", ".aiff", ".aif", ".m4a"];

    public EngineDjPlaylistSync(string dbPath)
    {
        _dbPath = Path.GetFullPath(dbPath);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        _connection.Open();
        _databaseUuid = ScalarString("SELECT uuid FROM Information LIMIT 1")
            ?? throw new InvalidOperationException("Information.uuid not found in database.");
    }


    public IReadOnlyList<PlaylistInfo> ListImportedCollectionPlaylists(string musicFolder)
    {
        var collectionName = GetFolderNameForCollection(musicFolder);
        var rootId = FindPlaylistId(collectionName);
        if (rootId is null)
            return Array.Empty<PlaylistInfo>();

        var rows = LoadPlaylistRows();
        var byId = rows.ToDictionary(r => r.Id);
        var descendantIds = GetDescendantPlaylistIds(rootId.Value, includeRoot: true);

        return rows
            .Where(r => descendantIds.Contains(r.Id))
            .OrderBy(r => r.Id == rootId.Value ? 0 : 1)
            .ThenBy(r => BuildPath(r, byId), StringComparer.OrdinalIgnoreCase)
            .Select(r => new PlaylistInfo(r.Id, BuildPath(r, byId)))
            .ToList();
    }

    public MissingTrackFilesResult FindMissingTrackFilesForImportedCollectionScope(string musicFolder, long? selectedPlaylistId, bool entireRootCollection, Action<string>? log = null, Action<TrackScanProgress>? progress = null)
    {
        var importFolder = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var collectionName = GetFolderNameForCollection(importFolder);
        var rootId = FindPlaylistId(collectionName);
        if (rootId is null)
            return new MissingTrackFilesResult();

        if (entireRootCollection)
        {
            // Engine DJ's left-side Collection view is the Track table, not just a Playlist tree.
            // Scan every Track row in the selected m.db so missing tracks that are not in any playlist
            // are included too.
            return FindMissingTrackFiles(importFolder, onlyUnderImportFolder: false, log, progress);
        }

        var playlistIds = selectedPlaylistId is null
            ? new HashSet<long> { rootId.Value }
            : new HashSet<long> { selectedPlaylistId.Value };

        return FindMissingTrackFilesForPlaylistIds(importFolder, playlistIds, log, progress);
    }

    public string GetImportCollectionName(string musicFolder)
    {
        var rootFolder = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return ResolveFolderImportTarget(rootFolder, createIfMissing: false).DisplayPath;
    }


    public IReadOnlyList<ImportPreviewTrack> PreviewFolderImport(string musicFolder, Action<TrackScanProgress>? progress = null)
    {
        var rootFolder = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var files = EnumerateAudioFiles(rootFolder, progress).ToList();
        var trackMap = LoadTrackMapByStoredPath();
        var identityIndex = LoadTrackIdentityIndex();
        var result = new List<ImportPreviewTrack>(files.Count);

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            progress?.Invoke(new TrackScanProgress(i + 1, files.Count, "Previewing " + Path.GetFileName(file), 1, 1, "Previewing tracks"));
            var storedPath = ToEngineSyncRelativePath(file);
            var relativeFolder = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(file) ?? rootFolder);
            if (relativeFolder == ".") relativeFolder = string.Empty;

            var match = ResolveExistingTrackMatch(file, storedPath, trackMap, identityIndex);
            result.Add(new ImportPreviewTrack(
                file,
                Path.GetFileName(file),
                relativeFolder,
                storedPath,
                match.Status,
                match.TrackId,
                match.ExistingStoredPath));
        }

        return result;
    }

    public SyncResult ImportSelectedFolderTracksAsImportedCollection(string musicFolder, IReadOnlyCollection<string> selectedFiles, bool generateExperimentalAnalysis = false, bool generateExperimentalKeys = false, ExternalAnalysisOptions? analysisOptions = null, bool useOfficialOfflineAnalyzer = false, bool useManagedInternalAnalyzer = false, bool captureOfficialAnalyzerFrames = false, int concurrentAnalysisTracks = 4, Action<string>? log = null, Action<TrackScanProgress>? progress = null)
    {
        var selected = selectedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path))
            .Where(path => AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return RebuildImportedCollectionFromFiles(musicFolder, selected, generateExperimentalAnalysis, generateExperimentalKeys, analysisOptions ?? ExternalAnalysisOptions.Default, useOfficialOfflineAnalyzer, useManagedInternalAnalyzer, captureOfficialAnalyzerFrames, concurrentAnalysisTracks, log, progress);
    }



    private List<string> EnumerateAudioFiles(string rootFolder, Action<TrackScanProgress>? progress = null)
    {
        var files = new List<string>();
        foreach (var root in Directory.EnumerateDirectories(rootFolder, "*", SearchOption.AllDirectories).Prepend(rootFolder))
        {
            var validFiles = Directory.EnumerateFiles(root)
                .Where(file => AudioExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
            files.AddRange(validFiles);
            progress?.Invoke(new TrackScanProgress(files.Count, 0, Path.GetFileName(root), 1, 1, "Finding files"));
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private SyncResult RebuildImportedCollectionFromFiles(string musicFolder, IReadOnlyCollection<string> selectedFiles, bool generateExperimentalAnalysis, bool generateExperimentalKeys, ExternalAnalysisOptions analysisOptions, bool useOfficialOfflineAnalyzer, bool useManagedInternalAnalyzer, bool captureOfficialAnalyzerFrames, int concurrentAnalysisTracks, Action<string>? log = null, Action<TrackScanProgress>? progress = null)
    {
        // Compatibility rebuild mode based on the working Engine-Sync script:
        // - use the selected m.db for BOTH Track and PlaylistEntity rows
        // - store Track.path relative to the Engine Library folder, without #history#
        // - rebuild the selected root collection's child playlist tree and memberships.
        var rootFolder = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = ResolveFolderImportTarget(rootFolder, createIfMissing: true);
        var selectedSet = selectedFiles
            .Select(path => Path.GetFullPath(path))
            .Where(path => path.StartsWith(rootFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(rootFolder + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootFolder, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var folderStructure = new List<(string Root, List<string> Dirs, List<string> Files)>();
        foreach (var root in Directory.EnumerateDirectories(rootFolder, "*", SearchOption.AllDirectories).Prepend(rootFolder))
        {
            var dirs = Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validFiles = Directory.EnumerateFiles(root)
                .Where(file => selectedSet.Contains(Path.GetFullPath(file)))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();

            folderStructure.Add((root, dirs, validFiles));
        }

        var files = selectedSet.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        var result = new SyncResult
        {
            FilesScanned = files.Count,
            CollectionName = target.DisplayPath
        };

        var stageCount = generateExperimentalAnalysis && files.Count > 0 ? 2 : 1;
        var addTracksStage = stageCount;
        Action<TrackScanProgress>? analysisProgress = progress is null
            ? null
            : p => progress(new TrackScanProgress(p.Current, p.Total, p.FileName, 1, stageCount, useOfficialOfflineAnalyzer && !useManagedInternalAnalyzer ? "Official analysis" : "Managed analysis"));

        IReadOnlyDictionary<string, ExternalAnalysisResult> analysisResults = new Dictionary<string, ExternalAnalysisResult>(StringComparer.OrdinalIgnoreCase);
        concurrentAnalysisTracks = Math.Max(1, Math.Min(concurrentAnalysisTracks, 16));
        if (generateExperimentalAnalysis && files.Count > 0)
        {
            if (!useManagedInternalAnalyzer && useOfficialOfflineAnalyzer && OfficialOfflineAnalyzerBridge.TryFindOfflineAnalyzer(out var offlineAnalyzerPath))
            {
                log?.Invoke("Generating official Engine DJ analysis using: " + offlineAnalyzerPath);
                if (captureOfficialAnalyzerFrames)
                    log?.Invoke("Official analyzer capture/decode mode is enabled. Raw frames and decoded waveform payloads will be saved under %LOCALAPPDATA%\\EngineDjPlaylistSync\\OfficialAnalyzerCaptures.");
                try
                {
                    analysisResults = OfficialOfflineAnalyzerBridge.AnalyzeFilesAsync(files, generateExperimentalKeys, analysisOptions, captureOfficialAnalyzerFrames, concurrentAnalysisTracks, analysisProgress).GetAwaiter().GetResult();
                    log?.Invoke($"Official Engine DJ analysis complete for {analysisResults.Count} file(s).");
                }
                catch (Exception ex)
                {
                    log?.Invoke("Official Engine DJ OfflineAnalyzer failed, falling back to managed experimental analyser: " + ex.Message);
                    analysisResults = ExternalAnalysisEngine.AnalyzeFilesAsync(files, generateExperimentalKeys, analysisOptions, concurrentAnalysisTracks, analysisProgress).GetAwaiter().GetResult();
                    log?.Invoke($"Experimental managed analysis complete for {analysisResults.Count} file(s)." + (generateExperimentalKeys ? " High-confidence keys were written where available." : string.Empty));
                }
            }
            else
            {
                log?.Invoke(useManagedInternalAnalyzer ? "Generating analysis data using internal managed analyser..." : "Generating experimental analysis data using managed C# analyser...");
                analysisResults = ExternalAnalysisEngine.AnalyzeFilesAsync(files, generateExperimentalKeys, analysisOptions, concurrentAnalysisTracks, analysisProgress).GetAwaiter().GetResult();
                log?.Invoke($"Experimental managed analysis complete for {analysisResults.Count} file(s)." + (generateExperimentalKeys ? " High-confidence keys were written where available." : string.Empty));
            }
        }

        Execute("BEGIN IMMEDIATE");
        try
        {
            log?.Invoke("Using Engine-Sync-compatible rebuild mode for update target: " + target.DisplayPath);

            var trackMap = LoadTrackMapByStoredPath();
            var identityIndex = LoadTrackIdentityIndex();
            for (var i = 0; i < files.Count; i++)
            {
                progress?.Invoke(new TrackScanProgress(i + 1, files.Count, "Importing " + Path.GetFileName(files[i]), addTracksStage, stageCount, "Adding tracks"));
                var trackResult = InsertTrackIfMissingEngineSyncStyle(files[i], trackMap, identityIndex, log);
                if (trackResult.WasInserted)
                    result.TracksInserted++;

                if (generateExperimentalAnalysis && analysisResults.TryGetValue(Path.GetFullPath(files[i]), out var analysis))
                    WriteExternalAnalysis(trackResult.TrackId, analysis);
            }

            var rootId = target.PlaylistId;
            DeleteCollectionChildrenAndMemberships(rootId);

            var playlistMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                [rootFolder] = rootId
            };
            var hierarchyMap = new Dictionary<long, long?>
            {
                [rootId] = null
            };
            var tracksPerPlaylist = new Dictionary<long, Dictionary<long, string>>();
            var touchedPlaylistIds = new HashSet<long> { rootId };

            Dictionary<long, string> TrackBucket(long playlistId)
            {
                if (!tracksPerPlaylist.TryGetValue(playlistId, out var bucket))
                {
                    bucket = new Dictionary<long, string>();
                    tracksPerPlaylist[playlistId] = bucket;
                }
                return bucket;
            }

            foreach (var (root, dirs, validFiles) in folderStructure)
            {
                var parentId = playlistMap.TryGetValue(root, out var mappedParent) ? mappedParent : rootId;
                var hasSubdirs = dirs.Count > 0;
                var hasFiles = validFiles.Count > 0;
                var nextFolderId = 0L;
                var targetPlaylistId = parentId;

                foreach (var dirName in dirs)
                {
                    var subfolderPath = Path.Combine(root, dirName);
                    var childId = InsertPlaylistCompatibility(dirName, parentId, nextFolderId);
                    result.PlaylistsCreated++;
                    nextFolderId = childId;
                    playlistMap[subfolderPath] = childId;
                    hierarchyMap[childId] = parentId;
                    touchedPlaylistIds.Add(childId);
                }

                if (hasSubdirs && hasFiles)
                {
                    var currentFolderName = string.Equals(root, rootFolder, StringComparison.OrdinalIgnoreCase)
                        ? "Loose Tracks"
                        : Path.GetFileName(root);
                    var twinName = "[ " + currentFolderName + " ]";
                    var twinId = InsertPlaylistCompatibility(twinName, parentId, nextFolderId);
                    result.PlaylistsCreated++;
                    hierarchyMap[twinId] = parentId;
                    targetPlaylistId = twinId;
                    touchedPlaylistIds.Add(twinId);
                }

                foreach (var file in validFiles)
                {
                    var storedPath = ToEngineSyncRelativePath(file);
                    if (!trackMap.TryGetValue(NormalizeEnginePathForCompare(storedPath), out var trackId))
                        continue;

                    var currentListId = targetPlaylistId;
                    while (true)
                    {
                        TrackBucket(currentListId)[trackId] = file;
                        if (!hierarchyMap.TryGetValue(currentListId, out var parent) || parent is null)
                            break;
                        currentListId = parent.Value;
                    }
                }
            }

            foreach (var kvp in tracksPerPlaylist)
            {
                WritePlaylistEntitiesEngineSyncStyle(kvp.Key, kvp.Value);
                result.PlaylistRowsInserted += kvp.Value.Count;
            }

            foreach (var playlistId in touchedPlaylistIds)
                TouchPlaylistCompatibility(playlistId);

            Execute("COMMIT");
            return result;
        }
        catch
        {
            Execute("ROLLBACK");
            throw;
        }
    }



    private void WriteExternalAnalysis(long trackId, ExternalAnalysisResult analysis)
    {
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
INSERT INTO PerformanceData
(trackId, trackData, overviewWaveFormData, beatData, quickCues, loops, thirdPartySourceId, activeOnLoadLoops)
VALUES
($trackId, $trackData, $overview, $beatData, $quickCues, $loops, NULL, 0)
ON CONFLICT(trackId) DO UPDATE SET
    trackData = excluded.trackData,
    overviewWaveFormData = excluded.overviewWaveFormData,
    beatData = excluded.beatData,
    quickCues = excluded.quickCues,
    loops = excluded.loops,
    activeOnLoadLoops = excluded.activeOnLoadLoops;";
            cmd.Parameters.AddWithValue("$trackId", trackId);
            cmd.Parameters.Add("$trackData", SqliteType.Blob).Value = analysis.TrackDataBlob;
            cmd.Parameters.Add("$overview", SqliteType.Blob).Value = analysis.OverviewWaveformBlob;
            cmd.Parameters.Add("$beatData", SqliteType.Blob).Value = analysis.BeatDataBlob;
            cmd.Parameters.Add("$quickCues", SqliteType.Blob).Value = analysis.QuickCuesBlob;
            cmd.Parameters.Add("$loops", SqliteType.Blob).Value = analysis.LoopsBlob;
            cmd.ExecuteNonQuery();
        }

        Execute(@"
UPDATE Track
SET bpmAnalyzed = $bpm,
    isAnalyzed = 1,
    isAvailable = 1,
    lastEditTime = $editTime
WHERE id = $trackId",
            ("$bpm", analysis.Bpm),
            ("$editTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            ("$trackId", trackId));

        if (analysis.EngineKey >= 0)
        {
            Execute("UPDATE Track SET key = $key WHERE id = $trackId",
                ("$key", analysis.EngineKey),
                ("$trackId", trackId));
        }

        WriteOverviewRgbCache(trackId, analysis.OverviewRgbCacheBlob);
    }

    private void WriteOverviewRgbCache(long trackId, byte[] rgbCacheBlob)
    {
        var database2Folder = Path.GetDirectoryName(_dbPath)
            ?? throw new InvalidOperationException("Cannot determine Database2 folder from db path.");
        var folder = Path.Combine(database2Folder, "OverviewData", _databaseUuid);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, trackId.ToString(CultureInfo.InvariantCulture) + ".rgb"), rgbCacheBlob);
    }

    private Dictionary<string, long> LoadTrackMapByStoredPath()
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, path FROM Track WHERE path IS NOT NULL AND TRIM(path) <> ''";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            var path = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var key = NormalizeEnginePathForCompare(path);
            if (!map.ContainsKey(key))
                map[key] = id;
        }
        return map;
    }

    private Dictionary<string, List<TrackIdentityRow>> LoadTrackIdentityIndex()
    {
        var index = new Dictionary<string, List<TrackIdentityRow>>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT id, path, filename, COALESCE(fileBytes, 0), COALESCE(length, 0), title, artist
FROM Track
WHERE filename IS NOT NULL AND TRIM(filename) <> ''";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new TrackIdentityRow(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                reader.IsDBNull(6) ? string.Empty : reader.GetString(6));

            foreach (var key in BuildTrackIdentityKeys(row.FileName, row.FileBytes, row.LengthSeconds, row.Title, row.Artist))
            {
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<TrackIdentityRow>();
                    index[key] = list;
                }
                list.Add(row);
            }
        }
        return index;
    }

    private ExistingTrackMatch ResolveExistingTrackMatch(string fullPath, string storedPath, IReadOnlyDictionary<string, long> trackMap, IReadOnlyDictionary<string, List<TrackIdentityRow>> identityIndex)
    {
        var key = NormalizeEnginePathForCompare(storedPath);
        if (trackMap.TryGetValue(key, out var existingId))
            return new ExistingTrackMatch(existingId, ImportPreviewStatus.AlreadyInDatabase, storedPath);

        var match = FindUniqueIdentityMatch(fullPath, identityIndex);
        return match is null
            ? new ExistingTrackMatch(0, ImportPreviewStatus.NewTrack, string.Empty)
            : new ExistingTrackMatch(match.TrackId, ImportPreviewStatus.RelocatedExisting, match.StoredPath);
    }

    private TrackIdentityRow? FindUniqueIdentityMatch(string fullPath, IReadOnlyDictionary<string, List<TrackIdentityRow>> identityIndex)
    {
        var info = new FileInfo(fullPath);
        var metadata = ReadMetadata(fullPath);
        foreach (var key in BuildTrackIdentityKeys(Path.GetFileName(fullPath), info.Exists ? info.Length : 0, metadata.DurationSeconds, metadata.Title ?? string.Empty, metadata.Artist ?? string.Empty))
        {
            if (!identityIndex.TryGetValue(key, out var matches))
                continue;

            var uniqueMatches = matches
                .GroupBy(row => row.TrackId)
                .Select(group => group.First())
                .Take(2)
                .ToList();
            if (uniqueMatches.Count == 1)
                return uniqueMatches[0];
        }
        return null;
    }

    private static IEnumerable<string> BuildTrackIdentityKeys(string fileName, long fileBytes, int lengthSeconds, string? title, string? artist)
    {
        var normalizedFileName = NormalizeIdentityText(fileName);
        if (string.IsNullOrWhiteSpace(normalizedFileName))
            yield break;

        if (fileBytes > 0 && lengthSeconds > 0)
            yield return "name+bytes+length:" + normalizedFileName + ":" + fileBytes.ToString(CultureInfo.InvariantCulture) + ":" + lengthSeconds.ToString(CultureInfo.InvariantCulture);

        if (fileBytes > 0)
            yield return "name+bytes:" + normalizedFileName + ":" + fileBytes.ToString(CultureInfo.InvariantCulture);

        var normalizedTitle = NormalizeIdentityText(title ?? string.Empty);
        var normalizedArtist = NormalizeIdentityText(artist ?? string.Empty);
        if (fileBytes > 0 && !string.IsNullOrWhiteSpace(normalizedTitle))
            yield return "title+artist+bytes:" + normalizedTitle + ":" + normalizedArtist + ":" + fileBytes.ToString(CultureInfo.InvariantCulture);
    }

    private static string NormalizeIdentityText(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private TrackInsertResult InsertTrackIfMissingEngineSyncStyle(string fullPath, IDictionary<string, long> trackMap, Dictionary<string, List<TrackIdentityRow>> identityIndex, Action<string>? log)
    {
        var storedPath = ToEngineSyncRelativePath(fullPath);
        var key = NormalizeEnginePathForCompare(storedPath);
        if (trackMap.TryGetValue(key, out var existingId))
            return new TrackInsertResult(existingId, false, new TrackPlaylistReference(existingId, _databaseUuid));

        var relocatedMatch = FindUniqueIdentityMatch(fullPath, identityIndex);
        if (relocatedMatch is not null)
        {
            Execute("UPDATE Track SET path = $path, filename = $filename, isAvailable = 1, lastEditTime = $editTime WHERE id = $trackId",
                ("$path", storedPath),
                ("$filename", Path.GetFileName(fullPath)),
                ("$editTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ("$trackId", relocatedMatch.TrackId));
            trackMap[key] = relocatedMatch.TrackId;
            log?.Invoke($"Relocated existing track: {Path.GetFileName(fullPath)} -> {storedPath}");
            return new TrackInsertResult(relocatedMatch.TrackId, false, new TrackPlaylistReference(relocatedMatch.TrackId, _databaseUuid));
        }

        var fileInfo = new FileInfo(fullPath);
        var metadata = ReadMetadata(fullPath);
        var dateAdded = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Track
(path, filename, title, artist, album, length, bpm, year, fileBytes, bitrate, fileType, dateAdded, isAnalyzed, isAvailable)
VALUES
($path, $filename, $title, $artist, $album, $length, $bpm, $year, $fileBytes, $bitrate, $fileType, $dateAdded, 0, 1);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$path", storedPath);
        cmd.Parameters.AddWithValue("$filename", Path.GetFileName(fullPath));
        cmd.Parameters.AddWithValue("$title", metadata.Title ?? Path.GetFileName(fullPath));
        cmd.Parameters.AddWithValue("$artist", metadata.Artist ?? "Unknown");
        cmd.Parameters.AddWithValue("$album", metadata.Album ?? string.Empty);
        cmd.Parameters.AddWithValue("$length", metadata.DurationSeconds);
        cmd.Parameters.AddWithValue("$bpm", metadata.Bpm);
        cmd.Parameters.AddWithValue("$year", metadata.Year);
        cmd.Parameters.AddWithValue("$fileBytes", fileInfo.Length);
        cmd.Parameters.AddWithValue("$bitrate", metadata.Bitrate);
        cmd.Parameters.AddWithValue("$fileType", Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant());
        cmd.Parameters.AddWithValue("$dateAdded", dateAdded);

        var newId = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        trackMap[key] = newId;
        foreach (var identityKey in BuildTrackIdentityKeys(Path.GetFileName(fullPath), fileInfo.Length, metadata.DurationSeconds, metadata.Title ?? string.Empty, metadata.Artist ?? string.Empty))
        {
            if (!identityIndex.TryGetValue(identityKey, out var list))
            {
                list = new List<TrackIdentityRow>();
                identityIndex[identityKey] = list;
            }
            list.Add(new TrackIdentityRow(newId, storedPath, Path.GetFileName(fullPath), fileInfo.Length, metadata.DurationSeconds, metadata.Title ?? string.Empty, metadata.Artist ?? string.Empty));
        }
        return new TrackInsertResult(newId, true, new TrackPlaylistReference(newId, _databaseUuid));
    }

    private ImportTarget ResolveFolderImportTarget(string rootFolder, bool createIfMissing)
    {
        var folderPath = Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var suffixes = BuildFolderPlaylistPathSuffixes(folderPath);

        // Prefer the most specific existing Engine DJ playlist path.
        // Example: selecting C:\Users\you\Music\Folder 2 will update
        // Music/Folder 2 if that playlist path already exists, instead of
        // creating a new root collection called Folder 2.
        foreach (var parts in suffixes.OrderByDescending(p => p.Length))
        {
            var path = string.Join("/", parts);
            var id = FindPlaylistId(path);
            if (id is not null)
            {
                if (createIfMissing)
                {
                    ForcePlaylistCompatibilityFlags(id.Value);
                    EnsurePlaylistSiblingChain(GetPlaylistParentId(id.Value));
                }
                return new ImportTarget(id.Value, path, false);
            }
        }

        var fallbackName = GetFolderNameForCollection(folderPath);
        if (!createIfMissing)
            return new ImportTarget(0, fallbackName, false);

        var rootId = EnsureRootCollectionForCompatibilityRebuild(fallbackName);
        return new ImportTarget(rootId, fallbackName, true);
    }

    private static List<string[]> BuildFolderPlaylistPathSuffixes(string folderPath)
    {
        var names = new List<string>();
        var dir = new DirectoryInfo(folderPath);
        while (dir is not null)
        {
            if (!string.IsNullOrWhiteSpace(dir.Name))
                names.Insert(0, dir.Name);
            dir = dir.Parent;
        }

        var suffixes = new List<string[]>();
        for (var start = 0; start < names.Count; start++)
        {
            var suffix = names.Skip(start).ToArray();
            if (suffix.Length > 0)
                suffixes.Add(suffix);
        }
        return suffixes;
    }

    private long GetPlaylistParentId(long playlistId)
    {
        return ScalarLong("SELECT parentListId FROM Playlist WHERE id = $id LIMIT 1", ("$id", playlistId)) ?? 0;
    }

    private long EnsureRootCollectionForCompatibilityRebuild(string collectionName)
    {
        var existing = ScalarLong("SELECT id FROM Playlist WHERE title = $title AND parentListId = 0 LIMIT 1", ("$title", collectionName));
        if (existing is not null)
        {
            ForcePlaylistCompatibilityFlags(existing.Value);
            EnsurePlaylistSiblingChain(0);
            return existing.Value;
        }

        var tailId = ScalarLong("SELECT id FROM Playlist WHERE parentListId = 0 AND nextListId = 0 ORDER BY id DESC LIMIT 1");
        var initialNextListId = tailId is null ? 0 : GetUnusedTemporaryNextListId(0);
        var rootId = InsertPlaylistCompatibility(collectionName, 0, initialNextListId);
        if (tailId is not null && tailId.Value != rootId)
        {
            Execute("UPDATE Playlist SET nextListId = $rootId WHERE id = $tailId", ("$rootId", rootId), ("$tailId", tailId.Value));
            Execute("UPDATE Playlist SET nextListId = 0 WHERE id = $rootId", ("$rootId", rootId));
        }
        EnsurePlaylistSiblingChain(0);
        return rootId;
    }

    private void DeleteCollectionChildrenAndMemberships(long rootId)
    {
        var descendants = GetDescendantPlaylistIds(rootId, includeRoot: false).ToList();
        foreach (var playlistId in descendants)
            Execute("DELETE FROM PlaylistEntity WHERE listId = $listId", ("$listId", playlistId));
        foreach (var playlistId in descendants)
            Execute("DELETE FROM Playlist WHERE id = $id", ("$id", playlistId));
        Execute("DELETE FROM PlaylistEntity WHERE listId = $listId", ("$listId", rootId));
    }

    private long InsertPlaylistCompatibility(string title, long parentListId, long nextListId)
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Playlist (title, parentListId, isPersisted, nextListId, lastEditTime, isExplicitlyExported)
VALUES ($title, $parent, 1, $nextListId, $editTime, 1);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$parent", parentListId);
        cmd.Parameters.AddWithValue("$nextListId", nextListId);
        cmd.Parameters.AddWithValue("$editTime", now);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private void WritePlaylistEntitiesEngineSyncStyle(long playlistId, Dictionary<long, string> tracks)
    {
        var nextEntityId = 0L;
        foreach (var item in tracks.OrderByDescending(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase))
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO PlaylistEntity (listId, trackId, databaseUuid, nextEntityId, membershipReference)
VALUES ($listId, $trackId, $databaseUuid, $nextEntityId, 0);
SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$listId", playlistId);
            cmd.Parameters.AddWithValue("$trackId", item.Key);
            cmd.Parameters.AddWithValue("$databaseUuid", _databaseUuid);
            cmd.Parameters.AddWithValue("$nextEntityId", nextEntityId);
            nextEntityId = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    private void TouchPlaylistCompatibility(long playlistId)
    {
        Execute(@"
UPDATE Playlist
SET isPersisted = 1,
    isExplicitlyExported = 1,
    lastEditTime = $editTime
WHERE id = $id",
            ("$editTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            ("$id", playlistId));
    }

    private void ForcePlaylistCompatibilityFlags(long playlistId)
    {
        Execute("UPDATE Playlist SET isPersisted = 1, isExplicitlyExported = 1 WHERE id = $id", ("$id", playlistId));
    }

    private string ToEngineSyncRelativePath(string fullPath)
    {
        var database2Folder = Path.GetDirectoryName(_dbPath)
            ?? throw new InvalidOperationException("Cannot determine Database2 folder from db path.");
        var engineLibraryFolder = Path.GetDirectoryName(database2Folder)
            ?? throw new InvalidOperationException("Cannot determine Engine Library folder from db path.");
        return Path.GetRelativePath(engineLibraryFolder, Path.GetFullPath(fullPath)).Replace('\\', '/');
    }




    private HashSet<long> GetDescendantPlaylistIds(long rootPlaylistId, bool includeRoot)
    {
        var rows = LoadPlaylistRows();
        var childrenByParent = rows
            .GroupBy(r => r.ParentListId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToList());

        var result = new HashSet<long>();
        var stack = new Stack<long>();
        stack.Push(rootPlaylistId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if ((includeRoot || current != rootPlaylistId) && !result.Add(current))
                continue;

            if (childrenByParent.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                    stack.Push(child);
            }
        }

        return result;
    }

    private MissingTrackFilesResult FindMissingTrackFilesForPlaylistIds(string importFolder, IReadOnlySet<long> playlistIds, Action<string>? log, Action<TrackScanProgress>? progress)
    {
        var result = new MissingTrackFilesResult();
        var seenTrackIds = new HashSet<long>();
        var tracks = new List<TrackFileRow>();

        foreach (var playlistId in playlistIds.OrderBy(id => id))
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
SELECT DISTINCT t.id, t.path, t.title, t.artist
FROM Track t
INNER JOIN PlaylistEntity pe ON pe.trackId = t.id
WHERE pe.listId = $listId
  AND t.path IS NOT NULL
  AND TRIM(t.path) <> ''
ORDER BY t.path";
            cmd.Parameters.AddWithValue("$listId", playlistId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var trackId = reader.GetInt64(0);
                if (!seenTrackIds.Add(trackId))
                    continue;

                var storedPath = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var artist = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                tracks.Add(new TrackFileRow(trackId, storedPath, title, artist));
            }
        }

        for (var i = 0; i < tracks.Count; i++)
        {
            var row = tracks[i];
            progress?.Invoke(new TrackScanProgress(i + 1, tracks.Count, GetTrackDisplayName(row.StoredPath, row.Title)));
            AddMissingFileResultIfMissing(result, row.TrackId, row.StoredPath, row.Title, row.Artist, importFolder, log);
        }

        return result;
    }

    private static string GetTrackDisplayName(string storedPath, string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
            return title;

        var physicalPath = StripEngineHistorySuffix(storedPath);
        var fileName = Path.GetFileName(physicalPath.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(fileName) ? storedPath : fileName;
    }

    private void AddMissingFileResultIfMissing(MissingTrackFilesResult result, long trackId, string storedPath, string title, string artist, string importFolder, Action<string>? log)
    {
        var candidates = GetCandidateAbsolutePaths(storedPath, importFolder).ToList();
        result.TracksChecked++;

        if (candidates.Any(File.Exists))
            return;

        result.MissingFiles.Add(new MissingTrackFile(
            trackId,
            storedPath,
            title,
            artist,
            candidates.FirstOrDefault() ?? StripEngineHistorySuffix(storedPath)));

        if (result.MissingFiles.Count % 100 == 0)
            log?.Invoke($"Missing file check: found {result.MissingFiles.Count} missing files so far...");
    }


    public MissingTrackFilesResult FindMissingTrackFiles(string musicFolder, bool onlyUnderImportFolder, Action<string>? log = null, Action<TrackScanProgress>? progress = null)
    {
        var importFolder = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var result = new MissingTrackFilesResult();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT id, path, title, artist
FROM Track
WHERE path IS NOT NULL AND TRIM(path) <> ''
ORDER BY path";

        var tracks = new List<TrackFileRow>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var trackId = reader.GetInt64(0);
            var storedPath = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var artist = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

            var candidates = GetCandidateAbsolutePaths(storedPath, importFolder).ToList();
            if (onlyUnderImportFolder && !LooksLikeTrackBelongsToImportFolder(storedPath, candidates, importFolder))
                continue;

            tracks.Add(new TrackFileRow(trackId, storedPath, title, artist));
        }

        for (var i = 0; i < tracks.Count; i++)
        {
            var row = tracks[i];
            progress?.Invoke(new TrackScanProgress(i + 1, tracks.Count, GetTrackDisplayName(row.StoredPath, row.Title)));
            AddMissingFileResultIfMissing(result, row.TrackId, row.StoredPath, row.Title, row.Artist, importFolder, log);
        }

        return result;
    }


    public RepairMissingTracksResult RepairMissingTracksByFileName(IReadOnlyCollection<MissingTrackFile> missingTracks, string searchRoot, Action<string>? log = null, Action<TrackScanProgress>? progress = null)
    {
        var result = new RepairMissingTracksResult();
        var items = missingTracks
            .Where(item => item.TrackId > 0)
            .GroupBy(item => item.TrackId)
            .Select(group => group.First())
            .ToList();

        if (items.Count == 0)
            return result;

        var root = Path.GetFullPath(searchRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Search folder does not exist: " + root);

        var wantedNames = items
            .Select(GetExpectedFileNameForMissingTrack)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (wantedNames.Count == 0)
            throw new InvalidOperationException("The selected missing tracks do not contain usable filenames to search for.");

        log?.Invoke($"Indexing audio files under {root}...");
        var matchesByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateAudioFilesSafe(root))
        {
            result.FilesScanned++;
            var name = Path.GetFileName(file);
            if (!wantedNames.Contains(name))
                continue;

            if (!matchesByName.TryGetValue(name, out var list))
            {
                list = new List<string>();
                matchesByName[name] = list;
            }
            list.Add(file);
        }

        log?.Invoke($"Search complete. Audio files scanned: {result.FilesScanned}. Matching filenames found: {matchesByName.Values.Sum(list => list.Count)}.");

        Execute("BEGIN IMMEDIATE");
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var expectedName = GetExpectedFileNameForMissingTrack(item);
                progress?.Invoke(new TrackScanProgress(i + 1, items.Count, expectedName));

                if (string.IsNullOrWhiteSpace(expectedName) || !matchesByName.TryGetValue(expectedName, out var matches) || matches.Count == 0)
                {
                    result.NotFound++;
                    result.Messages.Add("Not found: " + GetTrackDisplayName(item.StoredPath, item.Title));
                    continue;
                }

                var distinctMatches = matches.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                if (distinctMatches.Count > 1)
                {
                    result.Ambiguous++;
                    result.Messages.Add($"Ambiguous: {expectedName} matched {distinctMatches.Count} files. Skipped to avoid updating to the wrong file.");
                    continue;
                }

                var foundPath = distinctMatches[0];
                var storedPath = ToStoredPathForExistingFile(foundPath);
                var fileInfo = new FileInfo(foundPath);
                Execute(@"
UPDATE Track
SET path = $path,
    filename = $filename,
    fileBytes = $fileBytes,
    fileType = $fileType,
    isAvailable = 1,
    lastEditTime = $editTime
WHERE id = $trackId",
                    ("$path", storedPath),
                    ("$filename", Path.GetFileName(foundPath)),
                    ("$fileBytes", fileInfo.Length),
                    ("$fileType", Path.GetExtension(foundPath).TrimStart('.').ToLowerInvariant()),
                    ("$editTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                    ("$trackId", item.TrackId));

                result.Updated++;
                result.UpdatedTrackIds.Add(item.TrackId);
                result.Messages.Add($"Updated: {expectedName} -> {storedPath}");
            }

            Execute("COMMIT");
            return result;
        }
        catch
        {
            Execute("ROLLBACK");
            throw;
        }
    }

    private static string GetExpectedFileNameForMissingTrack(MissingTrackFile item)
    {
        var stored = StripEngineHistorySuffix(item.StoredPath).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var name = Path.GetFileName(stored);
        return string.IsNullOrWhiteSpace(name) ? item.Title : name;
    }

    private string ToStoredPathForExistingFile(string fullPath)
    {
        var full = Path.GetFullPath(fullPath);
        var engineLibraryFolder = GetEngineLibraryFolder();
        if (IsPathUnderFolder(full, engineLibraryFolder))
            return Path.GetRelativePath(engineLibraryFolder, full).Replace('\\', '/');

        return full.Replace('\\', '/');
    }

    private static IEnumerable<string> EnumerateAudioFilesSafe(string rootFolder)
    {
        var stack = new Stack<string>();
        stack.Push(rootFolder);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current).ToList();
            }
            catch
            {
                files = Array.Empty<string>();
            }

            foreach (var file in files)
            {
                if (AudioExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    yield return file;
            }

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToList();
            }
            catch
            {
                directories = Array.Empty<string>();
            }

            foreach (var directory in directories)
                stack.Push(directory);
        }
    }

    public DeleteTracksResult DeleteTracksFromDatabase(IEnumerable<long> trackIds, Action<string>? log = null)
    {
        var ids = trackIds.Distinct().ToList();
        var result = new DeleteTracksResult();
        if (ids.Count == 0) return result;

        Execute("BEGIN IMMEDIATE");
        try
        {
            foreach (var trackId in ids)
            {
                var title = ScalarString("SELECT COALESCE(NULLIF(title, ''), filename, path) FROM Track WHERE id = $trackId LIMIT 1", ("$trackId", trackId));
                if (title is null)
                    continue;

                result.PlaylistEntitiesDeleted += DeletePlaylistEntitiesForTrack(trackId);
                result.PreparelistEntriesDeleted += ExecuteCount("DELETE FROM PreparelistEntity WHERE trackId = $trackId", ("$trackId", trackId));
                result.PerformanceDataRowsDeleted += ExecuteCount("DELETE FROM PerformanceData WHERE trackId = $trackId", ("$trackId", trackId));
                result.TracksDeleted += ExecuteCount("DELETE FROM Track WHERE id = $trackId", ("$trackId", trackId));
                log?.Invoke("Deleted missing track from database: " + title);
            }

            Execute("COMMIT");
            return result;
        }
        catch
        {
            Execute("ROLLBACK");
            throw;
        }
    }

    private int DeletePlaylistEntitiesForTrack(long trackId)
    {
        var reference = GetPlaylistTrackReference(trackId);
        var rows = new List<(long Id, long ListId, long NextEntityId)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
SELECT id, listId, nextEntityId
FROM PlaylistEntity
WHERE trackId = $trackId
   OR (trackId = $referenceTrackId AND databaseUuid = $referenceDatabaseUuid)
ORDER BY id";
            cmd.Parameters.AddWithValue("$trackId", trackId);
            cmd.Parameters.AddWithValue("$referenceTrackId", reference.TrackId);
            cmd.Parameters.AddWithValue("$referenceDatabaseUuid", reference.DatabaseUuid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                    reader.IsDBNull(2) ? 0 : reader.GetInt64(2)));
            }
        }

        foreach (var row in rows)
        {
            Execute("UPDATE PlaylistEntity SET nextEntityId = $nextEntityId WHERE listId = $listId AND nextEntityId = $entityId",
                ("$nextEntityId", row.NextEntityId),
                ("$listId", row.ListId),
                ("$entityId", row.Id));
        }

        var deleted = 0;
        foreach (var row in rows)
        {
            deleted += ExecuteCount("DELETE FROM PlaylistEntity WHERE id = $id", ("$id", row.Id));
        }

        return deleted;
    }


    public long? FindPlaylistId(string playlistPath)
    {
        var parts = SplitPlaylistPath(playlistPath);
        if (parts.Length == 0) return null;

        long parent = 0;
        long? current = null;
        foreach (var part in parts)
        {
            current = ScalarLong("SELECT id FROM Playlist WHERE title = $title AND parentListId = $parent LIMIT 1",
                ("$title", part), ("$parent", parent));
            if (current is null) return null;
            parent = current.Value;
        }
        return current;
    }


    private List<PlaylistRow> LoadPlaylistRows()
    {
        var rows = new List<PlaylistRow>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, parentListId FROM Playlist ORDER BY parentListId, title";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new PlaylistRow(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt64(2)));
        }
        return rows;
    }


    private bool EnsurePlaylistSiblingChain(long parentListId)
    {
        var siblings = LoadPlaylistSiblingRows(parentListId);
        if (siblings.Count <= 1)
            return false;

        var desiredOrder = BuildPlaylistSiblingOrder(siblings);
        var changed = false;
        for (var i = 0; i < desiredOrder.Count; i++)
        {
            var expectedNext = i + 1 < desiredOrder.Count ? desiredOrder[i + 1].Id : 0;
            if (desiredOrder[i].NextListId != expectedNext)
            {
                changed = true;
                break;
            }
        }

        if (!changed)
            return false;

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        // The Playlist table has a unique constraint on (parentListId, nextListId),
        // so move all siblings onto unique temporary negative values before writing
        // the final linked-list values.
        for (var i = 0; i < desiredOrder.Count; i++)
        {
            var tempNext = GetUnusedTemporaryNextListId(parentListId);
            Execute("UPDATE Playlist SET nextListId = $nextListId WHERE id = $id",
                ("$nextListId", tempNext), ("$id", desiredOrder[i].Id));
        }

        for (var i = 0; i < desiredOrder.Count; i++)
        {
            var nextId = i + 1 < desiredOrder.Count ? desiredOrder[i + 1].Id : 0;
            Execute("UPDATE Playlist SET nextListId = $nextListId, lastEditTime = $editTime WHERE id = $id",
                ("$nextListId", nextId), ("$editTime", now), ("$id", desiredOrder[i].Id));
        }

        return true;
    }

    private List<PlaylistSiblingRow> LoadPlaylistSiblingRows(long parentListId)
    {
        var rows = new List<PlaylistSiblingRow>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, nextListId FROM Playlist WHERE parentListId = $parent ORDER BY id";
        cmd.Parameters.AddWithValue("$parent", parentListId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new PlaylistSiblingRow(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? 0 : reader.GetInt64(2)));
        }
        return rows;
    }

    private static List<PlaylistSiblingRow> BuildPlaylistSiblingOrder(List<PlaylistSiblingRow> siblings)
    {
        var byId = siblings.ToDictionary(r => r.Id);
        var referenced = siblings
            .Where(r => r.NextListId != 0 && byId.ContainsKey(r.NextListId))
            .Select(r => r.NextListId)
            .ToHashSet();

        var heads = siblings
            .Where(r => !referenced.Contains(r.Id))
            .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();

        if (heads.Count == 0)
            heads.Add(siblings.OrderBy(r => r.Id).First());

        var ordered = new List<PlaylistSiblingRow>();
        var visited = new HashSet<long>();

        foreach (var head in heads)
            FollowPlaylistChain(head, byId, visited, ordered);

        foreach (var orphan in siblings
            .Where(r => !visited.Contains(r.Id))
            .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id))
        {
            FollowPlaylistChain(orphan, byId, visited, ordered);
        }

        return ordered;
    }

    private static void FollowPlaylistChain(
        PlaylistSiblingRow start,
        IReadOnlyDictionary<long, PlaylistSiblingRow> byId,
        ISet<long> visited,
        ICollection<PlaylistSiblingRow> ordered)
    {
        var current = start;
        while (visited.Add(current.Id))
        {
            ordered.Add(current);
            if (current.NextListId == 0 || !byId.TryGetValue(current.NextListId, out var next))
                break;
            current = next;
        }
    }



    private long GetUnusedTemporaryNextListId(long parentListId)
    {
        var candidate = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (ScalarLong("SELECT id FROM Playlist WHERE parentListId = $parent AND nextListId = $nextListId LIMIT 1",
            ("$parent", parentListId), ("$nextListId", candidate)) is not null)
        {
            candidate--;
        }
        return candidate;
    }






    private TrackPlaylistReference GetPlaylistTrackReference(long localTrackId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
SELECT
    COALESCE(NULLIF(originTrackId, 0), id) AS referenceTrackId,
    COALESCE(NULLIF(originDatabaseUuid, ''), $databaseUuid) AS referenceDatabaseUuid
FROM Track
WHERE id = $id
LIMIT 1";
        cmd.Parameters.AddWithValue("$id", localTrackId);
        cmd.Parameters.AddWithValue("$databaseUuid", _databaseUuid);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return new TrackPlaylistReference(localTrackId, _databaseUuid);

        var referenceTrackId = reader.IsDBNull(0) ? localTrackId : reader.GetInt64(0);
        var referenceDatabaseUuid = reader.IsDBNull(1) ? _databaseUuid : reader.GetString(1);
        if (string.IsNullOrWhiteSpace(referenceDatabaseUuid))
            referenceDatabaseUuid = _databaseUuid;

        return new TrackPlaylistReference(referenceTrackId, referenceDatabaseUuid);
    }



    private string GetEngineLibraryFolder()
    {
        var database2Folder = Path.GetDirectoryName(_dbPath)
            ?? throw new InvalidOperationException("Cannot determine Database2 folder from db path.");
        return Path.GetDirectoryName(database2Folder)
            ?? throw new InvalidOperationException("Cannot determine Engine Library folder from db path.");
    }

    private IEnumerable<string> GetCandidateAbsolutePaths(string storedPath, string importFolder)
    {
        var normalized = (storedPath ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        void Add(string candidate)
        {
            try
            {
                var full = Path.GetFullPath(candidate);
                if (seen.Add(full))
                    candidates.Add(full);
            }
            catch
            {
                // Ignore malformed candidate paths and keep checking other candidates.
            }
        }

        if (Path.IsPathRooted(normalized))
        {
            Add(normalized);
        }
        else
        {
            Add(Path.Combine(GetEngineLibraryFolder(), normalized));

            var importParent = Path.GetDirectoryName(importFolder);
            var collectionName = Path.GetFileName(importFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(importParent) &&
                !string.IsNullOrWhiteSpace(collectionName) &&
                (normalized.Equals(collectionName, StringComparison.OrdinalIgnoreCase) ||
                 normalized.StartsWith(collectionName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            {
                Add(Path.Combine(importParent, normalized));
            }
        }

        foreach (var candidate in candidates)
            yield return candidate;
    }

    private bool LooksLikeTrackBelongsToImportFolder(string storedPath, IReadOnlyCollection<string> candidatePaths, string importFolder)
    {
        var normalizedImportFolder = Path.GetFullPath(importFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (candidatePaths.Any(candidate => IsPathUnderFolder(candidate, normalizedImportFolder)))
            return true;

        try
        {
            var importEnginePath = ToAbsoluteEnginePath(normalizedImportFolder).TrimEnd('/');
            var normalizedStoredPath = (storedPath ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
            if (normalizedStoredPath.Equals(importEnginePath, StringComparison.OrdinalIgnoreCase) ||
                normalizedStoredPath.StartsWith(importEnginePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // Fall through to folder-name matching.
        }

        var collectionName = Path.GetFileName(normalizedImportFolder);
        var storedForward = (storedPath ?? string.Empty).Trim().Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(collectionName) &&
               (storedForward.Equals(collectionName, StringComparison.OrdinalIgnoreCase) ||
                storedForward.StartsWith(collectionName + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPathUnderFolder(string path, string folder)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(fullFolder, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(fullFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(fullFolder + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string StripEngineHistorySuffix(string? storedPath)
    {
        var value = (storedPath ?? string.Empty).Trim();
        var markerIndex = value.IndexOf("#history#", StringComparison.OrdinalIgnoreCase);
        return markerIndex >= 0 ? value[..markerIndex] : value;
    }


    private static string ToAbsoluteEnginePath(string fullPath)
    {
        return Path.GetFullPath(fullPath).Replace('\\', '/');
    }


    private static string NormalizeEnginePathForCompare(string path)
    {
        return StripEngineHistorySuffix(path).Replace('\\', '/').ToLowerInvariant();
    }

    private static string GetFolderNameForCollection(string musicFolder)
    {
        var full = Path.GetFullPath(musicFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(full);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("The import folder must have a folder name.", nameof(musicFolder));
        return name;
    }


    private static TrackMetadata ReadMetadata(string fullPath)
    {
        try
        {
            using var audio = TagLib.File.Create(fullPath);
            var title = string.IsNullOrWhiteSpace(audio.Tag.Title) ? null : audio.Tag.Title;
            var artist = audio.Tag.Performers.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            var album = string.IsNullOrWhiteSpace(audio.Tag.Album) ? null : audio.Tag.Album;
            return new TrackMetadata(
                title,
                artist,
                album,
                checked((int)Math.Round(audio.Properties.Duration.TotalSeconds)),
                0,
                checked((int)audio.Tag.Year),
                checked((int)audio.Properties.AudioBitrate * 1000));
        }
        catch
        {
            return new TrackMetadata(Path.GetFileNameWithoutExtension(fullPath), "Unknown", string.Empty, 0, 0, 0, 0);
        }
    }

    private static string[] SplitPlaylistPath(string playlistPath)
    {
        return playlistPath.Split(['/', '\\', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private string? ScalarString(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        return cmd.ExecuteScalar() as string;
    }

    private long? ScalarLong(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        var value = cmd.ExecuteScalar();
        if (value is null || value == DBNull.Value) return null;
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private int ExecuteCount(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        return cmd.ExecuteNonQuery();
    }

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Name, p.Value);
        cmd.ExecuteNonQuery();
    }

    private static string BuildPath(PlaylistRow row, IReadOnlyDictionary<long, PlaylistRow> byId)
    {
        var parts = new Stack<string>();
        var current = row;
        var guard = 0;
        while (guard++ < 100)
        {
            if (!string.IsNullOrWhiteSpace(current.Title)) parts.Push(current.Title);
            if (current.ParentListId == 0 || !byId.TryGetValue(current.ParentListId, out var parent)) break;
            current = parent;
        }
        return string.Join("/", parts);
    }



    public void Dispose() => _connection.Dispose();
}

public sealed record PlaylistInfo(long Id, string Path)
{
    public override string ToString() => Path;
}

internal sealed record PlaylistRow(long Id, string Title, long ParentListId);
internal sealed record ImportTarget(long PlaylistId, string DisplayPath, bool WasCreated);
internal sealed record TrackMetadata(string? Title, string? Artist, string? Album, int DurationSeconds, int Bpm, int Year, int Bitrate);
internal sealed record TrackInsertResult(long TrackId, bool WasInserted, TrackPlaylistReference Reference);

public sealed record MissingTrackFile(long TrackId, string StoredPath, string Title, string Artist, string CheckedPath);

public sealed class MissingTrackFilesResult
{
    public int TracksChecked { get; set; }
    public List<MissingTrackFile> MissingFiles { get; } = new();
}

public sealed class RepairMissingTracksResult
{
    public int Updated { get; set; }
    public int NotFound { get; set; }
    public int Ambiguous { get; set; }
    public int FilesScanned { get; set; }
    public List<long> UpdatedTrackIds { get; } = new();
    public List<string> Messages { get; } = new();
}

public sealed class DeleteTracksResult
{
    public int TracksDeleted { get; set; }
    public int PlaylistEntitiesDeleted { get; set; }
    public int PreparelistEntriesDeleted { get; set; }
    public int PerformanceDataRowsDeleted { get; set; }
}

public sealed class SyncResult
{
    public string CollectionName { get; set; } = string.Empty;
    public int FilesScanned { get; set; }
    public int TracksInserted { get; set; }
    public int PlaylistRowsInserted { get; set; }
    public int PlaylistReferencesRepaired { get; set; }
    public int PlaylistEntityChainsRepaired { get; set; }
    public int AlreadyInPlaylist { get; set; }
    public int PlaylistsCreated { get; set; }
    public int PlaylistChainsRepaired { get; set; }
}

public sealed record ImportPreviewTrack(string FullPath, string FileName, string RelativeFolder, string StoredPath, ImportPreviewStatus Status, long ExistingTrackId, string ExistingStoredPath)
{
    public bool AlreadyInDatabase => Status != ImportPreviewStatus.NewTrack;
    public string StatusText => LocalizationManager.ImportPreviewStatusText(Status);
}

public enum ImportPreviewStatus
{
    NewTrack = 0,
    AlreadyInDatabase = 1,
    RelocatedExisting = 2
}

public sealed record TrackScanProgress(int Current, int Total, string FileName, int Stage = 1, int StageCount = 1, string StageName = "Working");
internal sealed record TrackFileRow(long TrackId, string StoredPath, string Title, string Artist);
internal sealed record TrackIdentityRow(long TrackId, string StoredPath, string FileName, long FileBytes, int LengthSeconds, string Title, string Artist);
internal sealed record ExistingTrackMatch(long TrackId, ImportPreviewStatus Status, string ExistingStoredPath);
internal sealed record PlaylistSiblingRow(long Id, string Title, long NextListId);
internal sealed record TrackPlaylistReference(long TrackId, string DatabaseUuid);
