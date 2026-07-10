using System.Diagnostics;
using System.Globalization;

namespace EngineDjPlaylistSync;

public sealed class MainForm : Form
{
    private readonly TextBox _dbPathTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _musicFolderTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly TextBox _collectionTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true };
    private readonly PictureBox _logoPictureBox = new() { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
    private readonly Label _dbPathLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _musicFolderLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _collectionLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _collectionAutoLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Label _languageLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(24, 6, 4, 0) };
    private readonly ComboBox _languageComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly Label _titleLabel = new()
    {
        Text = LocalizationManager.Text("App.Title"),
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold)
    };
    private readonly Button _browseDbButton = new() { Text = LocalizationManager.Text("Button.Browse"), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = false };
    private readonly Button _browseMusicButton = new() { Text = LocalizationManager.Text("Button.Browse"), Anchor = AnchorStyles.Left | AnchorStyles.Right, AutoSize = false };
    private readonly Button _scanButton = new() { Text = LocalizationManager.Text("Button.Preview") };
    private readonly Button _syncPlaylistsButton = new() { Text = LocalizationManager.Text("Button.SyncPlaylists"), Width = 190, AutoSize = false };
    private readonly Button _missingFilesButton = new() { Text = LocalizationManager.Text("Button.MissingFiles"), Width = 150, AutoSize = false };
    private readonly TextBox _logTextBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = LocalizationManager.Text("Status.Ready") };
    private readonly ProgressBar _scanProgressBar = new() { Minimum = 0, Maximum = 100, Value = 0, Dock = DockStyle.Fill };
    private readonly Label _scanProgressLabel = new() { AutoSize = true, Text = LocalizationManager.Text("Status.ProgressIdle"), Anchor = AnchorStyles.Left };
    private readonly Label _scanCurrentFileLabel = new() { AutoSize = false, Text = LocalizationManager.Text("Status.Ready"), Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly System.Windows.Forms.Timer _scanProgressTimer = new() { Interval = 150 };
    private readonly object _scanProgressLock = new();
    private TrackScanProgress? _latestScanProgress;
    private int _lastRenderedScanProgressCurrent = -1;
    private int _lastRenderedScanProgressStage = -1;
    private readonly CheckBox _darkModeCheckBox = new() { Text = LocalizationManager.Text("CheckBox.DarkMode"), AutoSize = true };
    private readonly Label _playlistExplanationLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = LocalizationManager.Text("Label.PlaylistExplanation"),
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(6)
    };

    private string? _lastBackupDbPath;
    private bool _loadingSettings;

    public MainForm(string[]? args = null)
    {
        Text = LocalizationManager.Text("App.Title");
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(1080, 860);
        Size = new Size(1180, 900);
        StartPosition = FormStartPosition.CenterScreen;
        DpiScalingService.ConfigureForm(this, darkMode: _darkModeCheckBox.Checked, module: "Main");

        Icon = EmbeddedAssets.LoadAppIcon() ?? Icon;
        _logoPictureBox.Image = EmbeddedAssets.LoadAppLogo();

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 11
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var appHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 0, 0, 8) };
        appHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        appHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        appHeader.Controls.Add(_logoPictureBox, 0, 0);
        appHeader.Controls.Add(_titleLabel, 1, 0);
        main.SetColumnSpan(appHeader, 3);
        main.Controls.Add(appHeader, 0, 0);

        main.Controls.Add(_dbPathLabel, 0, 1);
        main.Controls.Add(_dbPathTextBox, 1, 1);
        main.Controls.Add(_browseDbButton, 2, 1);

        main.Controls.Add(_musicFolderLabel, 0, 2);
        main.Controls.Add(_musicFolderTextBox, 1, 2);
        main.Controls.Add(_browseMusicButton, 2, 2);

        main.Controls.Add(_collectionLabel, 0, 3);
        main.Controls.Add(_collectionTextBox, 1, 3);
        main.Controls.Add(_collectionAutoLabel, 2, 3);

        main.SetColumnSpan(_playlistExplanationLabel, 3);
        main.Controls.Add(_playlistExplanationLabel, 0, 4);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false };
        actions.Controls.Add(_scanButton);
        actions.Controls.Add(_syncPlaylistsButton);
        main.SetColumnSpan(actions, 3);
        main.Controls.Add(actions, 0, 5);

        var scanProgressPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 4, 0, 2)
        };
        scanProgressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scanProgressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        scanProgressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        scanProgressPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scanProgressPanel.SetColumnSpan(_scanProgressBar, 2);
        scanProgressPanel.Controls.Add(_scanProgressBar, 0, 0);
        scanProgressPanel.SetColumnSpan(_scanCurrentFileLabel, 2);
        scanProgressPanel.Controls.Add(_scanCurrentFileLabel, 0, 1);
        main.SetColumnSpan(scanProgressPanel, 3);
        main.Controls.Add(scanProgressPanel, 0, 6);

        var missingFilePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false };
        missingFilePanel.Controls.Add(_missingFilesButton);
        main.SetColumnSpan(missingFilePanel, 3);
        main.Controls.Add(missingFilePanel, 0, 7);

        var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false };
        optionsPanel.Controls.Add(_darkModeCheckBox);
        optionsPanel.Controls.Add(_languageLabel);
        optionsPanel.Controls.Add(_languageComboBox);
        main.SetColumnSpan(optionsPanel, 3);
        main.Controls.Add(optionsPanel, 0, 8);

        main.SetColumnSpan(_logTextBox, 3);
        main.Controls.Add(_logTextBox, 0, 9);
        main.SetColumnSpan(_statusLabel, 3);
        main.Controls.Add(_statusLabel, 0, 10);

        Controls.Add(main);

        PopulateLanguageComboBox();
        ApplyLocalization();

        _browseDbButton.Click += (_, _) => BrowseForDatabase();
        _browseMusicButton.Click += (_, _) => BrowseForMusicFolder();
        _dbPathTextBox.TextChanged += (_, _) => { if (!_loadingSettings) { SaveSettings(); UpdateCollectionNamePreview(); } };
        _scanButton.Click += async (_, _) => await ScanAsync();
        _syncPlaylistsButton.Click += (_, _) => OpenPlaylistSyncDialog();
        _missingFilesButton.Click += (_, _) => OpenMissingFileManager();
        _darkModeCheckBox.CheckedChanged += (_, _) => { if (!_loadingSettings) { SaveSettings(); ApplyTheme(); } };
        _languageComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingSettings) return;
            var selectedLanguage = GetSelectedLanguageCode();
            LocalizationManager.ApplyLanguage(selectedLanguage);
            SaveSettings();
            PopulateLanguageComboBox(selectedLanguage);
            ApplyLocalization();
        };
        _musicFolderTextBox.TextChanged += (_, _) => UpdateCollectionNamePreview();
        FormClosing += (_, _) => { SaveSettings(); };
        _scanProgressTimer.Tick += (_, _) => FlushLatestScanProgress();
        LoadSettings();
        UpdateCollectionNamePreview();
        ApplyLocalization();
        ApplyTheme();
    }

    private void LoadSettings()
    {
        _loadingSettings = true;
        try
        {
            var settings = AppSettingsStore.Load();
            _dbPathTextBox.Text = settings.DatabasePath ?? string.Empty;
            _musicFolderTextBox.Text = settings.MusicFolder ?? string.Empty;
            _darkModeCheckBox.Checked = settings.DarkMode;
            LocalizationManager.ApplyLanguage(settings.Language);
            PopulateLanguageComboBox(settings.Language ?? LocalizationManager.SystemDefaultLanguage);
        }
        catch (Exception ex)
        {
            Log("Could not load settings: " + ex.Message);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                DatabasePath = _dbPathTextBox.Text.Trim(),
                MusicFolder = _musicFolderTextBox.Text.Trim(),
                DarkMode = _darkModeCheckBox.Checked,
                Language = GetSelectedLanguageCode()
            };
            AppSettingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            Log("Could not save settings: " + ex.Message);
        }
    }

    private void BrowseForDatabase()
    {
        using var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Text("Dialog.SelectDbTitle"),
            Filter = LocalizationManager.Text("Dialog.DbFilter"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _dbPathTextBox.Text = dialog.FileName;
            SaveSettings();
        }
    }

    private void BrowseForMusicFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = LocalizationManager.Text("Dialog.SelectImportFolder") };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _musicFolderTextBox.Text = dialog.SelectedPath;
            UpdateCollectionNamePreview();
            SaveSettings();
        }
    }

    private async Task ScanAsync()
    {
        if (!ValidateInputs(out var primaryDbPath, out var musicFolder)) return;
        if (!EnsureEngineDjIsClosed()) return;

        SetBusy(true, LocalizationManager.Text("Status.ScanningPreview"));
        try
        {
            ResetScanProgress();
            _scanProgressTimer.Start();
            List<ImportPreviewTrack> previewTracks;
            using (var sync = new EngineDjPlaylistSync(primaryDbPath))
            {
                previewTracks = await Task.Run(() => sync.PreviewFolderImport(musicFolder, StoreLatestScanProgress).ToList());
            }

            _scanProgressTimer.Stop();
            FlushLatestScanProgress(force: true);

            if (previewTracks.Count == 0)
            {
                SetScanProgressIdle(LocalizationManager.Text("Status.ScanNoAudio"));
                Log("Preview complete. No supported audio files were found.");
                return;
            }

            SetScanProgressComplete(previewTracks.Count);
            Log($"Preview complete. Found {previewTracks.Count} audio file(s). New to database: {previewTracks.Count(t => t.Status == ImportPreviewStatus.NewTrack)}. Relocated existing: {previewTracks.Count(t => t.Status == ImportPreviewStatus.RelocatedExisting)}. Already in database: {previewTracks.Count(t => t.Status == ImportPreviewStatus.AlreadyInDatabase)}.");

            using var previewDialog = new ImportPreviewDialog(previewTracks, _darkModeCheckBox.Checked);
            if (previewDialog.ShowDialog(this) != DialogResult.OK)
            {
                Log("Import cancelled from preview window.");
                SetStatus(LocalizationManager.Text("Status.Ready"));
                return;
            }

            var selectedFiles = previewDialog.SelectedFiles.ToList();
            var generateAnalysis = previewDialog.GenerateExperimentalAnalysis;
            var generateKeys = previewDialog.GenerateExperimentalKeyDetection;
            var analysisOptions = previewDialog.AnalysisOptions;
            var useOfficialOfflineAnalyzer = previewDialog.UseOfficialOfflineAnalyzer;
            var useManagedInternalAnalyzer = previewDialog.UseManagedInternalAnalyzer;
            var captureOfficialAnalyzerFrames = previewDialog.CaptureOfficialAnalyzerFrames;
            var concurrentAnalysisTracks = previewDialog.ConcurrentAnalysisTracks;
            if (selectedFiles.Count == 0)
            {
                Log("No files selected for import.");
                SetStatus(LocalizationManager.Text("Status.Ready"));
                return;
            }

            if (!EnsureEngineDjIsClosed()) return;

            SetBusy(true, LocalizationManager.Text("Status.ImportingSelected"));
            ResetScanProgress();
            _scanProgressTimer.Start();
            Log($"Importing {selectedFiles.Count} selected file(s) into: {primaryDbPath}");
            if (generateAnalysis)
            {
                Log($"Analysis is enabled. BPM range: {analysisOptions.MinBpm:0}-{analysisOptions.MaxBpm:0}. Key notation: {analysisOptions.KeyNotation}. Concurrent tracks: {concurrentAnalysisTracks}.");
                if (useOfficialOfflineAnalyzer)
                {
                    Log("Official Engine DJ OfflineAnalyzer mode is enabled.");
                    if (captureOfficialAnalyzerFrames)
                        Log("Official analyzer capture is enabled.");
                }
                else if (useManagedInternalAnalyzer)
                    Log("Internal managed analyser mode is enabled; OfflineAnalyzer.exe will not be launched.");
                else
                    Log("Managed experimental analyser mode is enabled.");
                if (generateKeys)
                    Log("Key detection is enabled; low-confidence managed keys will be left blank. Official mode uses Engine DJ analyser output.");
            }
            BackupDatabase(primaryDbPath);

            SyncResult result;
            using (var sync = new EngineDjPlaylistSync(primaryDbPath))
            {
                result = await Task.Run(() => sync.ImportSelectedFolderTracksAsImportedCollection(musicFolder, selectedFiles, generateAnalysis, generateKeys, analysisOptions, useOfficialOfflineAnalyzer, useManagedInternalAnalyzer, captureOfficialAnalyzerFrames, concurrentAnalysisTracks, LogFromWorker, StoreLatestScanProgress));
            }

            FlushLatestScanProgress(force: true);
            if (result.FilesScanned == 0)
                SetScanProgressIdle(LocalizationManager.Text("Status.ImportNoFiles"));
            else
                SetScanProgressComplete(result.FilesScanned);

            Log($"Done. Imported selected files. Collection: {result.CollectionName}. Files imported: {result.FilesScanned}. Playlists created: {result.PlaylistsCreated}. New tracks: {result.TracksInserted}. Playlist entries added: {result.PlaylistRowsInserted}.");
            SetStatus(LocalizationManager.Text("Status.ImportComplete"));
            _scanCurrentFileLabel.Text = LocalizationManager.Format("Status.ImportCompleteDetails", result.FilesScanned, result.TracksInserted, result.PlaylistRowsInserted);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _scanProgressTimer.Stop();
            SetBusy(false);
        }
    }




    private void OpenMissingFileManager()
    {
        if (!ValidateInputs(out var dbPath, out var musicFolder)) return;

        using var dialog = new MissingFilesDialog(dbPath, musicFolder, _darkModeCheckBox.Checked);
        dialog.ShowDialog(this);
    }

    private void OpenPlaylistSyncDialog()
    {
        if (!ValidateInputs(out var dbPath, out var musicFolder)) return;

        using var dialog = new PlaylistSyncDialog(dbPath, musicFolder, _darkModeCheckBox.Checked);
        dialog.ShowDialog(this);
    }


    private bool EnsureEngineDjIsClosed()
    {
        while (IsEngineDjRunning())
        {
            var result = MessageBox.Show(
                this,
                LocalizationManager.Text("Dialog.EngineRunningMessage"),
                LocalizationManager.Text("Dialog.EngineRunningTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK)
            {
                Log("Cancelled because Engine DJ.exe is still running.");
                return false;
            }
        }

        return true;
    }

    private static bool IsEngineDjRunning()
    {
        return Process.GetProcesses()
            .Any(p =>
            {
                try
                {
                    return string.Equals(p.ProcessName, "Engine DJ", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(Path.GetFileName(p.MainModule?.FileName), "Engine DJ.exe", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return string.Equals(p.ProcessName, "Engine DJ", StringComparison.OrdinalIgnoreCase);
                }
            });
    }

    private bool ValidateInputs(out string dbPath, out string musicFolder)
    {
        dbPath = _dbPathTextBox.Text.Trim();
        musicFolder = _musicFolderTextBox.Text.Trim();

        if (!File.Exists(dbPath))
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingDatabaseMessage"), LocalizationManager.Text("Dialog.MissingDatabaseTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!Directory.Exists(musicFolder))
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingImportFolderMessage"), LocalizationManager.Text("Dialog.MissingImportFolderTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void UpdateCollectionNamePreview()
    {
        _collectionTextBox.Text = GetCollectionNamePreview();
    }

    private string GetCollectionNamePreview()
    {
        var folder = _musicFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder)) return string.Empty;

        try
        {
            var trimmed = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var dbPath = _dbPathTextBox.Text.Trim();
            if (File.Exists(dbPath) && Directory.Exists(trimmed))
            {
                using var sync = new EngineDjPlaylistSync(dbPath);
                return sync.GetImportCollectionName(trimmed);
            }

            return Path.GetFileName(trimmed);
        }
        catch
        {
            try
            {
                var trimmed = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return Path.GetFileName(trimmed);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private void BackupDatabase(string dbPath)
    {
        var backup = BackupDatabaseFile(dbPath);
        if (!string.IsNullOrWhiteSpace(backup))
            Log("Backup created: " + backup);
    }

    private string? BackupDatabaseFile(string dbPath)
    {
        if (string.Equals(_lastBackupDbPath, dbPath, StringComparison.OrdinalIgnoreCase))
            return null;

        var backup = dbPath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        File.Copy(dbPath, backup, overwrite: false);
        _lastBackupDbPath = dbPath;
        return backup;
    }

    private void ResetScanProgress()
    {
        _scanProgressTimer.Stop();
        lock (_scanProgressLock)
            _latestScanProgress = null;

        _lastRenderedScanProgressCurrent = -1;
        _lastRenderedScanProgressStage = -1;
        _scanProgressBar.Style = ProgressBarStyle.Blocks;
        _scanProgressBar.MarqueeAnimationSpeed = 0;
        _scanProgressBar.Minimum = 0;
        _scanProgressBar.Maximum = 100;
        _scanProgressBar.Value = 0;
        _scanProgressLabel.Text = string.Empty;
        _scanCurrentFileLabel.Text = LocalizationManager.Text("Status.ScanPreparing");
        SetStatus(LocalizationManager.Text("Status.ScanPreparing"));
    }

    private void StoreLatestScanProgress(TrackScanProgress progress)
    {
        lock (_scanProgressLock)
            _latestScanProgress = progress;
    }

    private void FlushLatestScanProgress(bool force = false)
    {
        TrackScanProgress? progress;
        lock (_scanProgressLock)
            progress = _latestScanProgress;

        if (progress is null)
            return;

        if (!force && progress.Current == _lastRenderedScanProgressCurrent && progress.Stage == _lastRenderedScanProgressStage)
            return;

        _lastRenderedScanProgressCurrent = progress.Current;
        _lastRenderedScanProgressStage = progress.Stage;
        UpdateScanProgress(progress);
    }

    private void UpdateScanProgress(TrackScanProgress progress)
    {
        if (progress.Total <= 0)
        {
            _scanProgressBar.Style = ProgressBarStyle.Marquee;
            _scanProgressBar.MarqueeAnimationSpeed = 30;
            var findingStagePrefix = FormatProgressStage(progress);
            var findingStatus = LocalizationManager.Format("Status.FindingFiles", findingStagePrefix, progress.Current, progress.FileName);
            _scanProgressLabel.Text = string.Empty;
            _scanCurrentFileLabel.Text = findingStatus;
            SetStatus(findingStatus);
            return;
        }

        _scanProgressBar.Style = ProgressBarStyle.Blocks;
        _scanProgressBar.MarqueeAnimationSpeed = 0;
        var total = Math.Max(progress.Total, 1);
        var current = Math.Min(Math.Max(progress.Current, 0), total);
        _scanProgressBar.Maximum = total;
        _scanProgressBar.Value = current;
        var stagePrefix = FormatProgressStage(progress);
        var statusText = LocalizationManager.Format("Status.ProgressItem", stagePrefix, current, total, progress.FileName);
        _scanProgressLabel.Text = string.Empty;
        _scanCurrentFileLabel.Text = statusText;
        SetStatus(statusText);
    }

    private static string FormatProgressStage(TrackScanProgress progress)
    {
        var stageCount = Math.Max(progress.StageCount, 1);
        var stage = Math.Min(Math.Max(progress.Stage, 1), stageCount);
        var stageName = string.IsNullOrWhiteSpace(progress.StageName) ? LocalizationManager.Text("Status.StageWorking") : LocalizationManager.LocalizeProgressStageName(progress.StageName);
        return LocalizationManager.Format("Status.Stage", stage, stageCount, stageName);
    }

    private void SetScanProgressComplete(int total)
    {
        _scanProgressBar.Style = ProgressBarStyle.Blocks;
        _scanProgressBar.MarqueeAnimationSpeed = 0;
        var safeTotal = Math.Max(total, 1);
        _scanProgressBar.Maximum = safeTotal;
        _scanProgressBar.Value = safeTotal;
        var completeStatus = LocalizationManager.Format("Status.Complete", total);
        _scanProgressLabel.Text = string.Empty;
        _scanCurrentFileLabel.Text = completeStatus;
        SetStatus(completeStatus);
    }

    private void SetScanProgressIdle(string message)
    {
        _scanProgressBar.Style = ProgressBarStyle.Blocks;
        _scanProgressBar.MarqueeAnimationSpeed = 0;
        _scanProgressBar.Minimum = 0;
        _scanProgressBar.Maximum = 100;
        _scanProgressBar.Value = 0;
        _scanProgressLabel.Text = string.Empty;
        _scanCurrentFileLabel.Text = message;
        SetStatus(message);
    }


    private void SetBusy(bool busy, string? status = null)
    {
        _browseDbButton.Enabled = !busy;
        _browseMusicButton.Enabled = !busy;
        _scanButton.Enabled = !busy;
        _syncPlaylistsButton.Enabled = !busy;
        _missingFilesButton.Enabled = !busy;
        if (status is not null) SetStatus(status);
    }

    private void SetStatus(string status) => _statusLabel.Text = status;

    private void PopulateLanguageComboBox(string? selectedCode = null)
    {
        selectedCode = string.IsNullOrWhiteSpace(selectedCode) ? LocalizationManager.SystemDefaultLanguage : selectedCode;
        var previousLoadingState = _loadingSettings;
        _loadingSettings = true;
        try
        {
            _languageComboBox.Items.Clear();
            foreach (var item in LocalizationManager.GetLanguageChoices())
                _languageComboBox.Items.Add(item);

            SelectLanguageComboBox(selectedCode);
        }
        finally
        {
            _loadingSettings = previousLoadingState;
        }
    }

    private void SelectLanguageComboBox(string? languageCode)
    {
        languageCode = string.IsNullOrWhiteSpace(languageCode) ? LocalizationManager.SystemDefaultLanguage : languageCode;
        foreach (var item in _languageComboBox.Items.OfType<LanguageChoice>())
        {
            if (string.Equals(item.Code, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                _languageComboBox.SelectedItem = item;
                return;
            }
        }

        if (_languageComboBox.Items.Count > 0)
            _languageComboBox.SelectedIndex = 0;
    }

    private string GetSelectedLanguageCode()
    {
        return _languageComboBox.SelectedItem is LanguageChoice item
            ? item.Code
            : LocalizationManager.SystemDefaultLanguage;
    }

    private void ApplyLocalization()
    {
        Text = LocalizationManager.Text("App.Title");
        _titleLabel.Text = LocalizationManager.Text("App.Title");
        _dbPathLabel.Text = LocalizationManager.Text("Label.PrimaryDb");
        _musicFolderLabel.Text = LocalizationManager.Text("Label.ImportFolder");
        _collectionLabel.Text = LocalizationManager.Text("Label.UpdateTarget");
        _collectionAutoLabel.Text = LocalizationManager.Text("Label.AutoDetected");
        _browseDbButton.Text = LocalizationManager.Text("Button.Browse");
        _browseMusicButton.Text = LocalizationManager.Text("Button.Browse");
        _scanButton.Text = LocalizationManager.Text("Button.Preview");
        _syncPlaylistsButton.Text = LocalizationManager.Text("Button.SyncPlaylists");
        _missingFilesButton.Text = LocalizationManager.Text("Button.MissingFiles");
        _darkModeCheckBox.Text = LocalizationManager.Text("CheckBox.DarkMode");
        _languageLabel.Text = LocalizationManager.Text("Label.Language");
        _playlistExplanationLabel.Text = LocalizationManager.Text("Label.PlaylistExplanation");
        _statusLabel.Text = LocalizationManager.Text("Status.Ready");
        if (string.IsNullOrWhiteSpace(_scanCurrentFileLabel.Text) || _scanCurrentFileLabel.Text.Contains("ready", StringComparison.OrdinalIgnoreCase))
            _scanCurrentFileLabel.Text = LocalizationManager.Text("Status.Ready");
        PerformLayout();
    }

    private void ApplyTheme()
    {
        DpiScalingService.UpdateDarkMode(this, _darkModeCheckBox.Checked);
        ThemeManager.Apply(this, _darkModeCheckBox.Checked);
        var titleScale = DeviceDpi <= 96 ? 1.0F : Math.Clamp((float)Math.Sqrt(DeviceDpi / 96.0), 1.0F, 1.45F);
        _titleLabel.Font = new Font("Segoe UI", Math.Max(8.0F, 16F * 0.75F * titleScale), FontStyle.Bold, GraphicsUnit.Point);
        PerformLayout();
        _playlistExplanationLabel.BackColor = _darkModeCheckBox.Checked ? Color.FromArgb(34, 38, 46) : Color.White;
        _statusLabel.BackColor = _darkModeCheckBox.Checked ? Color.FromArgb(34, 38, 46) : Color.White;
    }

    private void Log(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void LogFromWorker(string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
            BeginInvoke(new Action(() => Log(message)));
        else
            Log(message);
    }

    private void ShowError(Exception ex)
    {
        Log("ERROR: " + ex.Message);
        SetStatus(LocalizationManager.Text("Status.Error"));
        MessageBox.Show(this, ex.Message, LocalizationManager.Text("App.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal sealed class AppSettings
{
    public string? DatabasePath { get; set; }
    public string? MusicFolder { get; set; }
    public bool DarkMode { get; set; }
    public string? Language { get; set; }
}
