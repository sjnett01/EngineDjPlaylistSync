using System.Diagnostics;

namespace EngineDjPlaylistSync;

public sealed class PlaylistSyncDialog : Form
{
    private const int FastCopyBufferSize = 4 * 1024 * 1024;
    private const int CopyProgressReportIntervalMs = 250;

    private readonly string _sourceDbPath;
    private readonly string _sourceMusicFolder;
    private readonly bool _darkMode;

    private readonly TreeView _playlistTree = new()
    {
        Dock = DockStyle.Fill,
        CheckBoxes = true,
        HideSelection = false
    };

    private readonly ComboBox _destinationDriveComboBox = new()
    {
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly Button _refreshDrivesButton = new() { Text = LocalizationManager.Text("Button.RefreshDrives") };
    private readonly Button _refreshButton = new() { Text = LocalizationManager.Text("Button.RefreshPlaylists") };
    private readonly Button _previewButton = new() { Text = LocalizationManager.Text("Button.PreviewSync") };
    private readonly Button _copyFilesButton = new() { Text = LocalizationManager.Text("Button.CopyFilesToDrive"), Enabled = false };
    private readonly Button _updateDatabaseButton = new() { Text = LocalizationManager.Text("Button.UpdateDestinationDatabase"), Enabled = false };
    private readonly Button _syncSelectedButton = new() { Text = LocalizationManager.Text("Button.SyncSelectedPlaylists"), Enabled = false };
    private readonly Label _concurrentCopiesLabel = new()
    {
        Text = LocalizationManager.Text("Label.ConcurrentCopies"),
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly NumericUpDown _concurrentCopiesNumericUpDown = new()
    {
        Minimum = 1,
        Maximum = 8,
        Value = 4,
        Width = 64
    };
    private readonly Label _copySpeedLabel = new()
    {
        Text = LocalizationManager.Format("Label.CopySpeed", "0.0 MB/s"),
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft
    };
    private readonly Button _closeButton = new() { Text = LocalizationManager.Text("Button.Close"), DialogResult = DialogResult.Cancel };
    private readonly Label _helpLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = LocalizationManager.Text("Label.PlaylistSyncHelp"),
        Padding = new Padding(6),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Label _summaryLabel = new() { AutoSize = false, Dock = DockStyle.Fill, Text = LocalizationManager.Text("Status.PlaylistSyncReady") };
    private readonly DataGridView _previewGrid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true,
        RowHeadersVisible = false,
        ReadOnly = true
    };
    private readonly TextBox _logTextBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    private bool _updatingChecks;
    private bool _previewHasCopyableFiles;
    private bool _previewCanUpdateDatabase;

    public PlaylistSyncDialog(string sourceDbPath, string sourceMusicFolder, bool darkMode)
    {
        _sourceDbPath = sourceDbPath;
        _sourceMusicFolder = sourceMusicFolder;
        _darkMode = darkMode;

        Text = LocalizationManager.Text("Form.PlaylistSyncTitle");
        Icon = EmbeddedAssets.LoadAppIcon() ?? Icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1600, 980);
        StartPosition = FormStartPosition.CenterParent;
        DpiScalingService.ConfigureForm(this, darkMode: _darkMode, module: "Playlist Sync");

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 8,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        main.SetColumnSpan(_helpLabel, 3);
        main.Controls.Add(_helpLabel, 0, 0);

        main.Controls.Add(new Label { Text = LocalizationManager.Text("Label.DestinationDrive"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _destinationDriveComboBox.Dock = DockStyle.Fill;
        _destinationDriveComboBox.Margin = new Padding(0, 5, 8, 5);
        _refreshDrivesButton.Dock = DockStyle.Fill;
        _refreshDrivesButton.Margin = new Padding(0, 3, 0, 3);
        main.Controls.Add(_destinationDriveComboBox, 1, 1);
        main.Controls.Add(_refreshDrivesButton, 2, 1);

        var actionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false };
        _refreshButton.Margin = new Padding(0, 5, 8, 0);
        _previewButton.Margin = new Padding(0, 5, 8, 0);
        _copyFilesButton.Margin = new Padding(0, 5, 8, 0);
        _updateDatabaseButton.Margin = new Padding(0, 5, 16, 0);
        _syncSelectedButton.Margin = new Padding(0, 5, 16, 0);
        _concurrentCopiesLabel.Margin = new Padding(8, 9, 6, 0);
        _concurrentCopiesNumericUpDown.Margin = new Padding(0, 5, 18, 0);
        _copySpeedLabel.Margin = new Padding(8, 9, 0, 0);
        actionRow.Controls.Add(_refreshButton);
        actionRow.Controls.Add(_previewButton);
        actionRow.Controls.Add(_syncSelectedButton);
        actionRow.Controls.Add(_concurrentCopiesLabel);
        actionRow.Controls.Add(_concurrentCopiesNumericUpDown);
        actionRow.Controls.Add(_copySpeedLabel);
        main.SetColumnSpan(actionRow, 3);
        main.Controls.Add(actionRow, 0, 2);

        var playlistGroup = new GroupBox { Text = LocalizationManager.Text("Group.Playlists"), Dock = DockStyle.Fill };
        playlistGroup.Controls.Add(_playlistTree);
        main.SetColumnSpan(playlistGroup, 3);
        main.Controls.Add(playlistGroup, 0, 3);

        main.SetColumnSpan(_summaryLabel, 3);
        main.Controls.Add(_summaryLabel, 0, 4);

        ConfigurePreviewGrid();
        main.SetColumnSpan(_previewGrid, 3);
        main.Controls.Add(_previewGrid, 0, 5);

        main.SetColumnSpan(_logTextBox, 3);
        main.Controls.Add(_logTextBox, 0, 6);

        var closeRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        closeRow.Controls.Add(_closeButton);
        main.SetColumnSpan(closeRow, 3);
        main.Controls.Add(closeRow, 0, 7);

        Controls.Add(main);

        _refreshDrivesButton.Click += (_, _) => LoadRemovableDrives();
        _refreshButton.Click += (_, _) => LoadPlaylists();
        _previewButton.Click += async (_, _) => await PreviewSyncAsync();
        _copyFilesButton.Click += async (_, _) => await CopyMissingFilesToDriveAsync();
        _updateDatabaseButton.Click += async (_, _) => await UpdateDestinationDatabaseAsync();
        _syncSelectedButton.Click += async (_, _) => await SyncSelectedPlaylistsAsync();
        _destinationDriveComboBox.SelectedIndexChanged += (_, _) => ClearPreviewResults(LocalizationManager.Text("Status.PlaylistSyncReady"));
        _playlistTree.AfterCheck += PlaylistTreeAfterCheck;
        Load += (_, _) =>
        {
            LoadPlaylists();
            LoadRemovableDrives();
        };
        Shown += (_, _) => ExpandInitialWindowForPlaylistSync();

        AcceptButton = _previewButton;
        CancelButton = _closeButton;

        ThemeManager.Apply(this, _darkMode);
        PerformLayout();
    }

    private void ExpandInitialWindowForPlaylistSync()
    {
        if (WindowState != FormWindowState.Normal)
        {
            return;
        }

        var workingArea = Owner is not null
            ? Screen.FromControl(Owner).WorkingArea
            : Screen.FromControl(this).WorkingArea;

        var margin = ScaleLogical(48);
        var maxWidth = Math.Max(MinimumSize.Width, workingArea.Width - margin);
        var maxHeight = Math.Max(MinimumSize.Height, workingArea.Height - margin);

        var targetWidth = Math.Min(maxWidth, Math.Max(Width, (int)Math.Round(workingArea.Width * 0.90)));
        var targetHeight = Math.Min(maxHeight, Math.Max(Height, (int)Math.Round(workingArea.Height * 0.90)));

        Size = new Size(targetWidth, targetHeight);

        var left = workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2);
        var top = workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2);
        Location = new Point(left, top);
    }

    private int ScaleLogical(int value)
    {
        try
        {
            return Math.Max(value, (int)Math.Round(value * DeviceDpi / 96.0));
        }
        catch
        {
            return value;
        }
    }

    private void ConfigurePreviewGrid()
    {
        _previewGrid.Columns.Clear();
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Playlist", HeaderText = LocalizationManager.Text("Grid.Playlist"), ReadOnly = true, FillWeight = 20 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = LocalizationManager.Text("Grid.Title"), ReadOnly = true, FillWeight = 18 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Artist", HeaderText = LocalizationManager.Text("Grid.Artist"), ReadOnly = true, FillWeight = 14 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StoredPath", HeaderText = LocalizationManager.Text("Grid.StoredEnginePath"), ReadOnly = true, FillWeight = 24 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SourcePath", HeaderText = LocalizationManager.Text("Grid.SourceFile"), ReadOnly = true, FillWeight = 26 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DestinationPath", HeaderText = LocalizationManager.Text("Grid.DestinationFile"), ReadOnly = true, FillWeight = 26 });
        _previewGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = LocalizationManager.Text("Grid.Status"), ReadOnly = true, FillWeight = 14 });
    }

    private void LoadPlaylists()
    {
        try
        {
            SetBusy(true);
            _playlistTree.Nodes.Clear();
            ClearPreviewResults(LocalizationManager.Text("Status.PlaylistSyncReady"));

            using var sync = new EngineDjPlaylistSync(_sourceDbPath);
            var playlists = sync.ListAllPlaylists().ToList();
            foreach (var playlist in playlists)
                AddPlaylistNode(playlist);

            _playlistTree.ExpandAll();
            Log(LocalizationManager.Format("Log.PlaylistsLoaded", playlists.Count));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void AddPlaylistNode(PlaylistInfo playlist)
    {
        var parts = playlist.Path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;

        TreeNodeCollection nodes = _playlistTree.Nodes;
        TreeNode? current = null;
        var pathSoFar = string.Empty;
        foreach (var part in parts)
        {
            pathSoFar = string.IsNullOrEmpty(pathSoFar) ? part : pathSoFar + "/" + part;
            var existing = nodes.Cast<TreeNode>().FirstOrDefault(n => string.Equals(n.Text, part, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new TreeNode(part) { Tag = new PlaylistTreeNodeData(null, pathSoFar) };
                nodes.Add(existing);
            }
            current = existing;
            nodes = existing.Nodes;
        }

        if (current is not null)
            current.Tag = new PlaylistTreeNodeData(playlist, playlist.Path);
    }

    private void PlaylistTreeAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_updatingChecks || e.Node is null)
            return;

        try
        {
            _updatingChecks = true;
            SetChildChecked(e.Node, e.Node.Checked);
            UpdateParentCheckedState(e.Node.Parent);
        }
        finally
        {
            _updatingChecks = false;
        }
    }

    private static void SetChildChecked(TreeNode node, bool isChecked)
    {
        foreach (TreeNode child in node.Nodes)
        {
            child.Checked = isChecked;
            SetChildChecked(child, isChecked);
        }
    }

    private static void UpdateParentCheckedState(TreeNode? parent)
    {
        while (parent is not null)
        {
            var children = parent.Nodes.Cast<TreeNode>().ToList();
            parent.Checked = children.Count > 0 && children.All(child => child.Checked);
            parent = parent.Parent;
        }
    }

    private IReadOnlyList<long> GetSelectedPlaylistIds()
    {
        var result = new List<long>();
        foreach (TreeNode node in _playlistTree.Nodes)
            AddSelectedPlaylistIds(node, result);
        return result.Distinct().ToList();
    }

    private static void AddSelectedPlaylistIds(TreeNode node, List<long> result)
    {
        if (node.Checked && node.Tag is PlaylistTreeNodeData { Playlist: not null } data)
            result.Add(data.Playlist.Id);

        foreach (TreeNode child in node.Nodes)
            AddSelectedPlaylistIds(child, result);
    }

    private async Task PreviewSyncAsync()
    {
        try
        {
            var selectedIds = GetSelectedPlaylistIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.NoPlaylistsSelectedMessage"), LocalizationManager.Text("Dialog.NoPlaylistsSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var destination = GetSelectedDestinationFolder();
            if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingDestinationMessage"), LocalizationManager.Text("Dialog.MissingDestinationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true, LocalizationManager.Text("Status.BuildingPlaylistSyncPreview"));
            ClearPreviewResults(LocalizationManager.Text("Status.BuildingPlaylistSyncPreview"));
            Log(LocalizationManager.Format("Log.BuildingPlaylistSyncPreview", selectedIds.Count));

            PlaylistSyncPreview preview;
            using (var sync = new EngineDjPlaylistSync(_sourceDbPath))
            {
                preview = await Task.Run(() => sync.PreviewPlaylistDriveSync(selectedIds, _sourceMusicFolder, destination));
            }

            foreach (var track in preview.Tracks)
            {
                var status = GetPreviewStatusText(track);
                var rowIndex = _previewGrid.Rows.Add(track.PlaylistPath, track.Title, track.Artist, track.StoredPath, track.SourcePath, track.DestinationPath, status);
                _previewGrid.Rows[rowIndex].Tag = track;
            }

            _previewHasCopyableFiles = preview.FilesToCopy > 0;
            _previewCanUpdateDatabase = preview.Tracks.Count > 0;
            _copyFilesButton.Enabled = _previewHasCopyableFiles;
            _updateDatabaseButton.Enabled = _previewCanUpdateDatabase;
            _syncSelectedButton.Enabled = _previewCanUpdateDatabase;

            var dbStatus = preview.DestinationDatabaseExists
                ? LocalizationManager.Text("PlaylistSync.DatabaseFound")
                : LocalizationManager.Text("PlaylistSync.DatabaseNotFound");

            _summaryLabel.Text = LocalizationManager.Format(
                "Status.PlaylistSyncPreviewSummary",
                preview.SelectedPlaylistCount,
                preview.PlaylistRows,
                preview.UniqueTracks,
                preview.FilesToCopy,
                preview.FilesAlreadyOnDestination,
                preview.SourceFilesMissing,
                dbStatus,
                preview.DestinationDatabasePath);

            Log(_summaryLabel.Text);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _refreshDrivesButton.Enabled = !busy;
        _refreshButton.Enabled = !busy;
        _previewButton.Enabled = !busy;
        _copyFilesButton.Enabled = !busy && _previewHasCopyableFiles;
        _updateDatabaseButton.Enabled = !busy && _previewCanUpdateDatabase;
        _syncSelectedButton.Enabled = !busy && _previewCanUpdateDatabase;
        _concurrentCopiesNumericUpDown.Enabled = !busy;
        _playlistTree.Enabled = !busy;
        _destinationDriveComboBox.Enabled = !busy && _destinationDriveComboBox.Items.Count > 0;
        if (status is not null)
            _summaryLabel.Text = status;
    }

    private void ClearPreviewResults(string? status = null)
    {
        _previewGrid.Rows.Clear();
        _previewHasCopyableFiles = false;
        _previewCanUpdateDatabase = false;
        _copyFilesButton.Enabled = false;
        _updateDatabaseButton.Enabled = false;
        _syncSelectedButton.Enabled = false;
        SetCopySpeed(0);
        if (status is not null)
            _summaryLabel.Text = status;
    }

    private static string GetPreviewStatusText(PlaylistSyncTrackPreview track)
    {
        if (!track.SourceExists)
            return LocalizationManager.Text("PlaylistSync.Status.SourceMissing");

        return track.DestinationExists
            ? LocalizationManager.Text("PlaylistSync.Status.AlreadyOnDrive")
            : LocalizationManager.Text("PlaylistSync.Status.WouldCopy");
    }

    private async Task SyncSelectedPlaylistsAsync()
    {
        try
        {
            var selectedIds = GetSelectedPlaylistIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.NoPlaylistsSelectedMessage"), LocalizationManager.Text("Dialog.NoPlaylistsSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var destination = GetSelectedDestinationFolder();
            if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingDestinationMessage"), LocalizationManager.Text("Dialog.MissingDestinationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_previewGrid.Rows.Count == 0 || !_previewCanUpdateDatabase)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.RunPreviewBeforeSyncMessage"), LocalizationManager.Text("Dialog.RunPreviewBeforeSyncTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var copyItems = GetCopyablePreviewTracks().ToList();
            var requiredBytes = GetRequiredCopyBytes(copyItems);
            if (copyItems.Count > 0 && !HasEnoughDestinationFreeSpace(destination, requiredBytes, out var freeBytes))
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.NotEnoughSpaceMessage", FormatBytes(requiredBytes), FormatBytes(freeBytes)), LocalizationManager.Text("Dialog.NotEnoughSpaceTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var destinationDatabasePath = EngineDjPlaylistSync.GetDestinationDatabasePath(destination);
            var destinationDatabaseExists = File.Exists(destinationDatabasePath);
            var filesStillToCopy = CountPreviewRowsStillNeedingDestinationFile();
            var databaseAction = destinationDatabaseExists
                ? LocalizationManager.Text("PlaylistSync.DatabaseWillUpdate")
                : LocalizationManager.Text("PlaylistSync.DatabaseWillCreate");

            var confirm = MessageBox.Show(
                this,
                LocalizationManager.Format(
                    "Dialog.SyncSelectedPlaylistsConfirm",
                    selectedIds.Count,
                    copyItems.Count,
                    FormatBytes(requiredBytes),
                    filesStillToCopy,
                    databaseAction,
                    destinationDatabasePath),
                LocalizationManager.Text("Dialog.SyncSelectedPlaylistsTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK)
                return;

            if (!EnsureEngineDjIsClosed())
                return;

            SetBusy(true, LocalizationManager.Text("Status.SyncSelectedPlaylistsStarting"));
            SetCopySpeed(0);

            PlaylistSyncCopyResult? copyResult = null;
            if (copyItems.Count > 0)
            {
                var maxParallelCopies = (int)_concurrentCopiesNumericUpDown.Value;
                SetBusy(true, LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", 0, copyItems.Count));
                Log(LocalizationManager.Format("Log.CopyingPlaylistSyncFiles", copyItems.Count, maxParallelCopies));

                var copyProgress = new Progress<PlaylistSyncCopyProgress>(p =>
                {
                    _summaryLabel.Text = string.IsNullOrWhiteSpace(p.Message)
                        ? LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", p.Current, p.Total)
                        : p.Message;

                    if (p.SpeedBytesPerSecond >= 0)
                        SetCopySpeed(p.SpeedBytesPerSecond);

                    if (p.WriteToLog && !string.IsNullOrWhiteSpace(p.Message))
                        Log(p.Message);
                });

                copyResult = await Task.Run(() => CopyFiles(copyItems, maxParallelCopies, copyProgress));
                MarkCopiedRows(copyResult.CopiedDestinationPaths);
                RecalculateCopyButtonState();

                var copySummary = LocalizationManager.Format("Status.CopyFilesCompleteForSync", copyResult.Copied, copyResult.Skipped, copyResult.Failed);
                _summaryLabel.Text = copySummary;
                Log(copySummary);
                if (copyResult.Elapsed > TimeSpan.Zero && copyResult.CopiedBytes > 0)
                {
                    var finalSpeed = copyResult.CopiedBytes / Math.Max(0.001, copyResult.Elapsed.TotalSeconds);
                    SetCopySpeed(finalSpeed);
                    Log(LocalizationManager.Format("Log.CopyFilesPerformance", FormatBytes(copyResult.CopiedBytes), copyResult.Elapsed.TotalSeconds, FormatMegabytesPerSecond(finalSpeed)));
                }
            }
            else
            {
                Log(LocalizationManager.Text("Log.NoPlaylistSyncFilesToCopy"));
            }

            SetBusy(true, LocalizationManager.Text("Status.UpdatingPlaylistSyncDatabase"));
            Log(LocalizationManager.Format("Log.UpdatingPlaylistSyncDatabase", selectedIds.Count, destinationDatabasePath));

            IProgress<PlaylistDatabaseSyncProgress> databaseProgress = new Progress<PlaylistDatabaseSyncProgress>(p =>
            {
                _summaryLabel.Text = LocalizationManager.Format("Status.UpdatingPlaylistSyncDatabaseProgress", p.Current, p.Total, p.PlaylistPath);
            });
            IProgress<string> logProgress = new Progress<string>(Log);

            var databaseResult = await Task.Run(() =>
            {
                using var sync = new EngineDjPlaylistSync(_sourceDbPath);
                return sync.SyncSelectedPlaylistsToDestination(
                    selectedIds,
                    _sourceMusicFolder,
                    destination,
                    message => logProgress.Report(message),
                    p => databaseProgress.Report(p));
            });

            var copied = copyResult?.Copied ?? 0;
            var skipped = copyResult?.Skipped ?? 0;
            var failed = copyResult?.Failed ?? 0;
            _summaryLabel.Text = LocalizationManager.Format(
                "Status.SyncSelectedPlaylistsComplete",
                copied,
                skipped,
                failed,
                databaseResult.PlaylistRowsWritten,
                databaseResult.PlaylistsCreated,
                databaseResult.TracksInserted,
                databaseResult.TracksAlreadyInDatabase,
                databaseResult.PlaylistRowsSkippedMissingFiles);
            Log(_summaryLabel.Text);

            if (databaseResult.DestinationDatabaseCreated)
                Log(LocalizationManager.Format("Log.DestinationDatabaseCreated", databaseResult.DestinationDatabasePath));
            if (!string.IsNullOrWhiteSpace(databaseResult.BackupPath))
                Log(LocalizationManager.Format("Log.DestinationDatabaseBackup", databaseResult.BackupPath));
            Log(LocalizationManager.Format(
                "Log.PlaylistSyncAnalysisCopied",
                databaseResult.AnalysisRowsCopied,
                databaseResult.TrackAnalysisFieldsCopied,
                databaseResult.OverviewDataFilesCopied,
                databaseResult.AnalysisRowsMissing,
                databaseResult.AnalysisCopyFailures));

            var hasWarnings = failed > 0 || databaseResult.PlaylistRowsSkippedMissingFiles > 0 || databaseResult.AnalysisCopyFailures > 0;
            var completionMessage = hasWarnings
                ? LocalizationManager.Format("Dialog.SyncSelectedPlaylistsCompletedWithWarnings", copied, skipped, failed, databaseResult.PlaylistRowsWritten, databaseResult.PlaylistRowsSkippedMissingFiles, databaseResult.AnalysisCopyFailures)
                : LocalizationManager.Format("Dialog.SyncSelectedPlaylistsCompleted", copied, skipped, databaseResult.PlaylistRowsWritten, destinationDatabasePath);

            MessageBox.Show(
                this,
                completionMessage,
                LocalizationManager.Text("Dialog.SyncSelectedPlaylistsTitle"),
                MessageBoxButtons.OK,
                hasWarnings ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }


    private async Task UpdateDestinationDatabaseAsync()
    {
        try
        {
            var selectedIds = GetSelectedPlaylistIds();
            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.NoPlaylistsSelectedMessage"), LocalizationManager.Text("Dialog.NoPlaylistsSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var destination = GetSelectedDestinationFolder();
            if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingDestinationMessage"), LocalizationManager.Text("Dialog.MissingDestinationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var destinationDatabasePath = EngineDjPlaylistSync.GetDestinationDatabasePath(destination);
            var destinationDatabaseExists = File.Exists(destinationDatabasePath);

            var filesStillToCopy = CountPreviewRowsStillNeedingDestinationFile();
            var confirmMessage = destinationDatabaseExists
                ? LocalizationManager.Format("Dialog.UpdateDatabaseConfirm", selectedIds.Count, destinationDatabasePath, filesStillToCopy)
                : LocalizationManager.Format("Dialog.CreateDatabaseConfirm", selectedIds.Count, destinationDatabasePath, filesStillToCopy);
            var confirm = MessageBox.Show(
                this,
                confirmMessage,
                LocalizationManager.Text("Dialog.UpdateDatabaseTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.OK)
                return;

            if (!EnsureEngineDjIsClosed())
                return;

            SetBusy(true, LocalizationManager.Text("Status.UpdatingPlaylistSyncDatabase"));
            Log(LocalizationManager.Format("Log.UpdatingPlaylistSyncDatabase", selectedIds.Count, destinationDatabasePath));

            IProgress<PlaylistDatabaseSyncProgress> progress = new Progress<PlaylistDatabaseSyncProgress>(p =>
            {
                _summaryLabel.Text = LocalizationManager.Format("Status.UpdatingPlaylistSyncDatabaseProgress", p.Current, p.Total, p.PlaylistPath);
            });
            IProgress<string> logProgress = new Progress<string>(Log);

            var result = await Task.Run(() =>
            {
                using var sync = new EngineDjPlaylistSync(_sourceDbPath);
                return sync.SyncSelectedPlaylistsToDestination(
                    selectedIds,
                    _sourceMusicFolder,
                    destination,
                    message => logProgress.Report(message),
                    p => progress.Report(p));
            });

            _summaryLabel.Text = LocalizationManager.Format(
                "Status.UpdateDatabaseComplete",
                result.SelectedPlaylists,
                result.PlaylistRowsWritten,
                result.PlaylistsCreated,
                result.TracksInserted,
                result.TracksAlreadyInDatabase,
                result.PlaylistRowsSkippedMissingFiles);
            Log(_summaryLabel.Text);
            if (result.DestinationDatabaseCreated)
                Log(LocalizationManager.Format("Log.DestinationDatabaseCreated", result.DestinationDatabasePath));
            if (!string.IsNullOrWhiteSpace(result.BackupPath))
                Log(LocalizationManager.Format("Log.DestinationDatabaseBackup", result.BackupPath));
            Log(LocalizationManager.Format(
                "Log.PlaylistSyncAnalysisCopied",
                result.AnalysisRowsCopied,
                result.TrackAnalysisFieldsCopied,
                result.OverviewDataFilesCopied,
                result.AnalysisRowsMissing,
                result.AnalysisCopyFailures));

            if (result.DestinationDatabaseCreated)
            {
                if (result.PlaylistRowsSkippedMissingFiles > 0)
                {
                    MessageBox.Show(this, LocalizationManager.Format("Dialog.CreateDatabaseCompletedWithMissing", result.PlaylistRowsWritten, result.PlaylistRowsSkippedMissingFiles, result.DestinationDatabasePath), LocalizationManager.Text("Dialog.UpdateDatabaseTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(this, LocalizationManager.Format("Dialog.CreateDatabaseCompleted", result.PlaylistRowsWritten, result.DestinationDatabasePath), LocalizationManager.Text("Dialog.UpdateDatabaseTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (result.PlaylistRowsSkippedMissingFiles > 0)
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.UpdateDatabaseCompletedWithMissing", result.PlaylistRowsWritten, result.PlaylistRowsSkippedMissingFiles, result.BackupPath), LocalizationManager.Text("Dialog.UpdateDatabaseTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.UpdateDatabaseCompleted", result.PlaylistRowsWritten, result.BackupPath), LocalizationManager.Text("Dialog.UpdateDatabaseTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private int CountPreviewRowsStillNeedingDestinationFile()
    {
        var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _previewGrid.Rows)
        {
            if (row.Tag is not PlaylistSyncTrackPreview track)
                continue;

            if (!track.SourceExists || track.DestinationExists || string.IsNullOrWhiteSpace(track.DestinationPath))
                continue;

            try
            {
                var fullPath = Path.GetFullPath(track.DestinationPath);
                if (!File.Exists(fullPath))
                    destinations.Add(fullPath);
            }
            catch
            {
                destinations.Add(track.DestinationPath);
            }
        }

        return destinations.Count;
    }

    private async Task CopyMissingFilesToDriveAsync()
    {
        try
        {
            var destination = GetSelectedDestinationFolder();
            if (string.IsNullOrWhiteSpace(destination) || !Directory.Exists(destination))
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingDestinationMessage"), LocalizationManager.Text("Dialog.MissingDestinationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var copyItems = GetCopyablePreviewTracks().ToList();
            if (copyItems.Count == 0)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.NoFilesToCopyMessage"), LocalizationManager.Text("Dialog.NoFilesToCopyTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                _previewHasCopyableFiles = false;
                _copyFilesButton.Enabled = false;
                return;
            }

            var requiredBytes = GetRequiredCopyBytes(copyItems);
            if (!HasEnoughDestinationFreeSpace(destination, requiredBytes, out var freeBytes))
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.NotEnoughSpaceMessage", FormatBytes(requiredBytes), FormatBytes(freeBytes)), LocalizationManager.Text("Dialog.NotEnoughSpaceTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var maxParallelCopies = (int)_concurrentCopiesNumericUpDown.Value;
            var confirm = MessageBox.Show(
                this,
                LocalizationManager.Format("Dialog.CopyFilesConfirm", copyItems.Count, FormatBytes(requiredBytes), destination),
                LocalizationManager.Text("Dialog.CopyFilesTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK)
                return;

            SetBusy(true, LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", 0, copyItems.Count));
            SetCopySpeed(0);
            Log(LocalizationManager.Format("Log.CopyingPlaylistSyncFiles", copyItems.Count, maxParallelCopies));

            var progress = new Progress<PlaylistSyncCopyProgress>(p =>
            {
                _summaryLabel.Text = string.IsNullOrWhiteSpace(p.Message)
                    ? LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", p.Current, p.Total)
                    : p.Message;

                if (p.SpeedBytesPerSecond >= 0)
                    SetCopySpeed(p.SpeedBytesPerSecond);

                if (p.WriteToLog && !string.IsNullOrWhiteSpace(p.Message))
                    Log(p.Message);
            });

            var result = await Task.Run(() => CopyFiles(copyItems, maxParallelCopies, progress));
            MarkCopiedRows(result.CopiedDestinationPaths);
            RecalculateCopyButtonState();

            _summaryLabel.Text = LocalizationManager.Format("Status.CopyFilesComplete", result.Copied, result.Skipped, result.Failed);
            Log(_summaryLabel.Text);
            if (result.Elapsed > TimeSpan.Zero && result.CopiedBytes > 0)
            {
                var finalSpeed = result.CopiedBytes / Math.Max(0.001, result.Elapsed.TotalSeconds);
                SetCopySpeed(finalSpeed);
                Log(LocalizationManager.Format("Log.CopyFilesPerformance", FormatBytes(result.CopiedBytes), result.Elapsed.TotalSeconds, FormatMegabytesPerSecond(finalSpeed)));
            }

            if (result.Failed > 0)
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.CopyFilesCompletedWithErrors", result.Copied, result.Skipped, result.Failed), LocalizationManager.Text("Dialog.CopyFilesTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, LocalizationManager.Format("Dialog.CopyFilesCompleted", result.Copied, result.Skipped), LocalizationManager.Text("Dialog.CopyFilesTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private IEnumerable<PlaylistSyncTrackPreview> GetCopyablePreviewTracks()
    {
        var seenDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _previewGrid.Rows)
        {
            if (row.Tag is not PlaylistSyncTrackPreview track)
                continue;

            if (!track.SourceExists || track.DestinationExists)
                continue;

            if (string.IsNullOrWhiteSpace(track.SourcePath) || string.IsNullOrWhiteSpace(track.DestinationPath))
                continue;

            if (!File.Exists(track.SourcePath) || File.Exists(track.DestinationPath))
                continue;

            if (seenDestinations.Add(Path.GetFullPath(track.DestinationPath)))
                yield return track;
        }
    }

    private static long GetRequiredCopyBytes(IReadOnlyList<PlaylistSyncTrackPreview> copyItems)
    {
        long total = 0;
        foreach (var item in copyItems)
        {
            try
            {
                total += new FileInfo(item.SourcePath).Length;
            }
            catch
            {
                // The copy pass will report the file-level failure if it cannot be read.
            }
        }

        return total;
    }

    private static bool HasEnoughDestinationFreeSpace(string destinationFolder, long requiredBytes, out long freeBytes)
    {
        freeBytes = 0;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(destinationFolder));
            if (string.IsNullOrWhiteSpace(root))
                return true;

            var drive = new DriveInfo(root);
            freeBytes = drive.AvailableFreeSpace;
            return requiredBytes <= 0 || freeBytes > requiredBytes;
        }
        catch
        {
            return true;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
            return "0 B";

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static PlaylistSyncCopyResult CopyFiles(IReadOnlyList<PlaylistSyncTrackPreview> copyItems, int maxParallelCopies, IProgress<PlaylistSyncCopyProgress> progress)
    {
        var result = new PlaylistSyncCopyResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var completed = 0;
        long liveCopiedBytes = 0;
        var total = copyItems.Count;
        var resultLock = new object();
        var actualParallelCopies = Math.Max(1, Math.Min(maxParallelCopies, Math.Min(8, Math.Max(1, total))));

        progress.Report(new PlaylistSyncCopyProgress(0, total, LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", 0, total), SpeedBytesPerSecond: 0));

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = actualParallelCopies };
        Parallel.ForEach(copyItems.Select((item, index) => new PlaylistSyncCopyWorkItem(item, index + 1)), parallelOptions, workItem =>
        {
            var item = workItem.Track;

            try
            {
                if (File.Exists(item.DestinationPath))
                {
                    lock (resultLock)
                    {
                        result.Skipped++;
                        result.CopiedDestinationPaths.Add(Path.GetFullPath(item.DestinationPath));
                    }
                    return;
                }

                var destinationDirectory = Path.GetDirectoryName(item.DestinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                var sourceLength = GetFileLength(item.SourcePath);
                CopyFileWithVisibleProgress(
                    item.SourcePath,
                    item.DestinationPath,
                    total,
                    progress,
                    () => Volatile.Read(ref completed),
                    stopwatch,
                    () => Volatile.Read(ref liveCopiedBytes),
                    bytesCopied => Interlocked.Add(ref liveCopiedBytes, bytesCopied));

                lock (resultLock)
                {
                    result.Copied++;
                    result.CopiedBytes += sourceLength;
                    result.CopiedDestinationPaths.Add(Path.GetFullPath(item.DestinationPath));
                }
            }
            catch (Exception ex)
            {
                TryDeletePartialCopy(item.DestinationPath);
                lock (resultLock)
                {
                    result.Failed++;
                }

                progress.Report(new PlaylistSyncCopyProgress(
                    Volatile.Read(ref completed),
                    total,
                    LocalizationManager.Format("Log.CopyFileFailed", item.SourcePath, ex.Message),
                    true,
                    GetCopySpeedBytesPerSecond(Volatile.Read(ref liveCopiedBytes), stopwatch)));
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                progress.Report(new PlaylistSyncCopyProgress(
                    done,
                    total,
                    LocalizationManager.Format("Status.CopyingPlaylistSyncFiles", done, total),
                    SpeedBytesPerSecond: GetCopySpeedBytesPerSecond(Volatile.Read(ref liveCopiedBytes), stopwatch)));
            }
        });

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        progress.Report(new PlaylistSyncCopyProgress(total, total, string.Empty, SpeedBytesPerSecond: GetCopySpeedBytesPerSecond(Volatile.Read(ref liveCopiedBytes), stopwatch)));
        return result;
    }

    private static void CopyFileWithVisibleProgress(
        string sourcePath,
        string destinationPath,
        int totalFiles,
        IProgress<PlaylistSyncCopyProgress> progress,
        Func<int> getCompletedCount,
        System.Diagnostics.Stopwatch copyStopwatch,
        Func<long> getLiveCopiedBytes,
        Action<long> addLiveCopiedBytes)
    {
        var tempPath = destinationPath + ".copying";
        var sourceLength = GetFileLength(sourcePath);
        var displayName = Path.GetFileName(sourcePath);
        var copiedBytes = 0L;
        var lastProgressReport = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            progress.Report(new PlaylistSyncCopyProgress(
                getCompletedCount(),
                totalFiles,
                FormatCurrentFileProgress(getCompletedCount(), totalFiles, displayName, 0, sourceLength),
                SpeedBytesPerSecond: GetCopySpeedBytesPerSecond(getLiveCopiedBytes(), copyStopwatch)));

            var buffer = new byte[FastCopyBufferSize];
            using (var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FastCopyBufferSize,
                FileOptions.SequentialScan))
            using (var destination = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FastCopyBufferSize,
                FileOptions.SequentialScan))
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, read);
                    copiedBytes += read;
                    addLiveCopiedBytes(read);

                    if (lastProgressReport.ElapsedMilliseconds >= CopyProgressReportIntervalMs || copiedBytes >= sourceLength)
                    {
                        progress.Report(new PlaylistSyncCopyProgress(
                            getCompletedCount(),
                            totalFiles,
                            FormatCurrentFileProgress(getCompletedCount(), totalFiles, displayName, copiedBytes, sourceLength),
                            SpeedBytesPerSecond: GetCopySpeedBytesPerSecond(getLiveCopiedBytes(), copyStopwatch)));
                        lastProgressReport.Restart();
                    }
                }

                destination.Flush();
            }

            try
            {
                File.SetLastWriteTimeUtc(tempPath, File.GetLastWriteTimeUtc(sourcePath));
            }
            catch
            {
                // Timestamp preservation is best-effort only; the copied audio file is still valid.
            }

            File.Move(tempPath, destinationPath);
            progress.Report(new PlaylistSyncCopyProgress(
                getCompletedCount(),
                totalFiles,
                FormatCurrentFileProgress(getCompletedCount(), totalFiles, displayName, sourceLength, sourceLength),
                SpeedBytesPerSecond: GetCopySpeedBytesPerSecond(getLiveCopiedBytes(), copyStopwatch)));
        }
        catch
        {
            TryDeletePartialCopy(tempPath);
            throw;
        }
    }

    private static string FormatCurrentFileProgress(int completedFiles, int totalFiles, string fileName, long copiedBytes, long totalBytes)
    {
        var percent = totalBytes > 0
            ? Math.Min(100d, Math.Max(0d, copiedBytes * 100d / totalBytes))
            : 0d;

        return LocalizationManager.Format("Status.CopyingCurrentPlaylistSyncFile", completedFiles, totalFiles, fileName, percent, FormatBytes(copiedBytes), FormatBytes(totalBytes));
    }

    private void SetCopySpeed(double bytesPerSecond)
    {
        _copySpeedLabel.Text = LocalizationManager.Format("Label.CopySpeed", FormatMegabytesPerSecond(bytesPerSecond));
    }

    private static double GetCopySpeedBytesPerSecond(long copiedBytes, System.Diagnostics.Stopwatch stopwatch)
    {
        if (copiedBytes <= 0 || stopwatch.Elapsed.TotalSeconds <= 0)
            return 0;

        return copiedBytes / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
    }

    private static string FormatMegabytesPerSecond(double bytesPerSecond)
    {
        if (double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond) || bytesPerSecond <= 0)
            return "0.0 MB/s";

        return $"{bytesPerSecond / 1024d / 1024d:0.0} MB/s";
    }

    private static long GetFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static void TryDeletePartialCopy(string destinationPath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(destinationPath) && File.Exists(destinationPath))
                File.Delete(destinationPath);
        }
        catch
        {
            // Leave the file-level error in the log and avoid masking the original failure.
        }
    }

    private void MarkCopiedRows(IReadOnlySet<string> copiedDestinationPaths)
    {
        foreach (DataGridViewRow row in _previewGrid.Rows)
        {
            if (row.Tag is not PlaylistSyncTrackPreview track)
                continue;

            var destination = string.IsNullOrWhiteSpace(track.DestinationPath)
                ? string.Empty
                : Path.GetFullPath(track.DestinationPath);
            if (!copiedDestinationPaths.Contains(destination))
                continue;

            var updated = track with { DestinationExists = true };
            row.Tag = updated;
            row.Cells["Status"].Value = LocalizationManager.Text("PlaylistSync.Status.Copied");
        }
    }

    private void RecalculateCopyButtonState()
    {
        _previewHasCopyableFiles = GetCopyablePreviewTracks().Any();
        _copyFilesButton.Enabled = _previewHasCopyableFiles;
    }

    private void LoadRemovableDrives()
    {
        var previousSelection = GetSelectedDestinationFolder();
        var drives = GetReadyRemovableDrives().ToList();

        _destinationDriveComboBox.BeginUpdate();
        try
        {
            _destinationDriveComboBox.Items.Clear();
            foreach (var drive in drives)
                _destinationDriveComboBox.Items.Add(drive);

            if (drives.Count > 0)
            {
                var selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(previousSelection))
                {
                    for (var i = 0; i < drives.Count; i++)
                    {
                        if (string.Equals(drives[i].RootPath, previousSelection, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = i;
                            break;
                        }
                    }
                }

                _destinationDriveComboBox.SelectedIndex = selectedIndex;
                _destinationDriveComboBox.Enabled = true;
                _summaryLabel.Text = LocalizationManager.Text("Status.PlaylistSyncReady");
            }
            else
            {
                _destinationDriveComboBox.SelectedIndex = -1;
                _destinationDriveComboBox.Enabled = false;
                _summaryLabel.Text = LocalizationManager.Text("Status.NoRemovableDrives");
            }
        }
        finally
        {
            _destinationDriveComboBox.EndUpdate();
        }

        Log(LocalizationManager.Format("Log.RemovableDrivesLoaded", drives.Count));
    }

    private string GetSelectedDestinationFolder()
    {
        return _destinationDriveComboBox.SelectedItem is RemovableDriveItem item
            ? item.RootPath
            : string.Empty;
    }

    private static IReadOnlyList<RemovableDriveItem> GetReadyRemovableDrives()
    {
        var result = new List<RemovableDriveItem>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Removable || !drive.IsReady)
                    continue;

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? LocalizationManager.Text("PlaylistSync.RemovableDrive")
                    : drive.VolumeLabel.Trim();
                var root = drive.RootDirectory.FullName;
                var capacity = FormatDriveCapacity(drive.TotalSize);
                var free = FormatDriveCapacity(drive.AvailableFreeSpace);
                var displayName = LocalizationManager.Format("PlaylistSync.DriveDisplay", root, label, free, capacity);
                result.Add(new RemovableDriveItem(root, displayName));
            }
            catch
            {
                // Ignore drives that disappear or deny access while the list is being refreshed.
            }
        }

        return result;
    }

    private static string FormatDriveCapacity(long bytes)
    {
        if (bytes <= 0)
            return "0 GB";

        var gb = bytes / 1024d / 1024d / 1024d;
        return gb >= 10
            ? $"{gb:0} GB"
            : $"{gb:0.0} GB";
    }

    private sealed record RemovableDriveItem(string RootPath, string DisplayName)
    {
        public override string ToString() => DisplayName;
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

    private void Log(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void ShowError(Exception ex)
    {
        Log("ERROR: " + ex.Message);
        MessageBox.Show(this, ex.Message, LocalizationManager.Text("Form.PlaylistSyncTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private sealed record PlaylistSyncCopyWorkItem(PlaylistSyncTrackPreview Track, int Index);

    private sealed record PlaylistSyncCopyProgress(int Current, int Total, string Message, bool WriteToLog = false, double SpeedBytesPerSecond = -1);

    private sealed class PlaylistSyncCopyResult
    {
        public int Copied { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public long CopiedBytes { get; set; }
        public TimeSpan Elapsed { get; set; }
        public HashSet<string> CopiedDestinationPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record PlaylistTreeNodeData(PlaylistInfo? Playlist, string Path);
}
