using System.Diagnostics;
using System.Globalization;

namespace EngineDjPlaylistSync;

public sealed class MissingFilesDialog : Form
{
    private readonly string _dbPath;
    private readonly string _musicFolder;
    private readonly ComboBox _scopeComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _refreshPlaylistsButton = new() { Text = LocalizationManager.Text("Button.RefreshPlaylists") };
    private readonly Button _deleteButton = new() { Text = LocalizationManager.Text("Button.DeleteCheckedMissing"), Enabled = false };
    private readonly TextBox _repairSearchFolderTextBox = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right };
    private readonly Button _browseRepairFolderButton = new() { Text = LocalizationManager.Text("Button.Browse") };
    private readonly Button _repairButton = new() { Text = LocalizationManager.Text("Button.LocateCheckedFiles"), Enabled = false };
    private readonly CheckBox _headerSelectCheckBox = new()
    {
        ThreeState = true,
        Size = new Size(16, 16),
        BackColor = Color.Transparent,
        TabStop = false
    };
    private bool _suppressCellEvents;
    private readonly Label _scopeHelpLabel = new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Text = LocalizationManager.Text("Label.MissingHelp"),
        Padding = new Padding(6),
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true,
        RowHeadersVisible = false
    };
    private readonly TextBox _logTextBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private string _lastStatusText = string.Empty;
    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Value = 0 };
    private readonly Label _progressLabel = new() { AutoSize = true, Text = LocalizationManager.Text("Status.ProgressIdle"), Anchor = AnchorStyles.Left };
    private readonly System.Windows.Forms.Timer _progressTimer = new() { Interval = 100 };

    private readonly object _progressLock = new();
    private TrackScanProgress? _latestProgress;
    private int _lastRenderedProgressCurrent = -1;
    private bool _busy;
    private readonly bool _darkMode;

    public MissingFilesDialog(string dbPath, string musicFolder, bool darkMode = false)
    {
        _dbPath = dbPath;
        _musicFolder = musicFolder;
        _darkMode = darkMode;

        Text = LocalizationManager.Text("Form.MissingFilesTitle");
        Icon = EmbeddedAssets.LoadAppIcon() ?? Icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(1100, 760);
        Size = new Size(1180, 800);
        StartPosition = FormStartPosition.CenterParent;
        DpiScalingService.ConfigureForm(this, darkMode: _darkMode, module: "Missing Files");

        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 8,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));

        main.Controls.Add(new Label { Text = LocalizationManager.Text("Label.Scope"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        main.Controls.Add(_scopeComboBox, 1, 0);
        main.Controls.Add(_refreshPlaylistsButton, 2, 0);

        main.SetColumnSpan(_scopeHelpLabel, 3);
        main.Controls.Add(_scopeHelpLabel, 0, 1);

        var searchPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Padding = new Padding(0)
        };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
        searchPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        searchPanel.Controls.Add(new Label
        {
            Text = LocalizationManager.Text("Label.SearchFolderDrive"),
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 0);
        _repairSearchFolderTextBox.Dock = DockStyle.Fill;
        _repairSearchFolderTextBox.Margin = new Padding(0, 5, 8, 5);
        _browseRepairFolderButton.Dock = DockStyle.Fill;
        _browseRepairFolderButton.Margin = new Padding(0, 3, 8, 3);
        _repairButton.Dock = DockStyle.Fill;
        _repairButton.Margin = new Padding(0, 3, 0, 3);
        searchPanel.Controls.Add(_repairSearchFolderTextBox, 1, 0);
        searchPanel.Controls.Add(_browseRepairFolderButton, 2, 0);
        searchPanel.Controls.Add(_repairButton, 3, 0);
        main.SetColumnSpan(searchPanel, 3);
        main.Controls.Add(searchPanel, 0, 2);

        var progressPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, GrowStyle = TableLayoutPanelGrowStyle.AddRows };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        progressPanel.Controls.Add(_progressLabel, 0, 0);
        progressPanel.Controls.Add(_progressBar, 1, 0);
        main.SetColumnSpan(progressPanel, 3);
        main.Controls.Add(progressPanel, 0, 3);

        ConfigureGrid();
        main.SetColumnSpan(_grid, 3);
        main.Controls.Add(_grid, 0, 4);

        var deletePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = false };
        _deleteButton.Margin = new Padding(0, 4, 0, 0);
        deletePanel.Controls.Add(_deleteButton);
        main.SetColumnSpan(deletePanel, 3);
        main.Controls.Add(deletePanel, 0, 5);

        main.SetColumnSpan(_logTextBox, 3);
        main.Controls.Add(_logTextBox, 0, 6);

        Controls.Add(main);

        _refreshPlaylistsButton.Click += async (_, _) => { LoadPlaylistScopeItems(); await CheckMissingFilesAsync(); };
        _deleteButton.Click += async (_, _) => await DeleteCheckedMissingTracksAsync();
        _repairButton.Click += async (_, _) => await LocateCheckedMissingTracksAsync();
        _browseRepairFolderButton.Click += (_, _) => BrowseRepairSearchFolder();
        _progressTimer.Tick += (_, _) => FlushLatestProgress();

        _headerSelectCheckBox.AutoCheck = false;
        _headerSelectCheckBox.Click += (_, _) => ToggleAllCheckedFromHeader();
        _grid.Controls.Add(_headerSelectCheckBox);
        _grid.ColumnWidthChanged += (_, _) => PositionHeaderCheckBox();
        _grid.Scroll += (_, _) => PositionHeaderCheckBox();
        _grid.SizeChanged += (_, _) => PositionHeaderCheckBox();
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (TryGetDeleteColumnIndex(out var deleteColumnIndex) && e.ColumnIndex == deleteColumnIndex)
                ToggleAllCheckedFromHeader();
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (_suppressCellEvents) return;
            if (TryGetDeleteColumnIndex(out var deleteColumnIndex) && e.ColumnIndex == deleteColumnIndex)
                UpdateHeaderCheckBoxState();
        };

        Load += async (_, _) => { LoadPlaylistScopeItems(); await CheckMissingFilesAsync(); };
        ThemeManager.Apply(this, _darkMode);
        PerformLayout();
        StyleActionButtons();
        ApplyHeaderCheckBoxTheme();
        PositionHeaderCheckBox();
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Clear();
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Delete", HeaderText = "", Width = 54, FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TrackId", HeaderText = LocalizationManager.Text("Grid.TrackId"), ReadOnly = true, Width = 80, FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = LocalizationManager.Text("Grid.Title"), ReadOnly = true, FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Artist", HeaderText = LocalizationManager.Text("Grid.Artist"), ReadOnly = true, FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StoredPath", HeaderText = LocalizationManager.Text("Grid.StoredDbPath"), ReadOnly = true, FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CheckedPath", HeaderText = LocalizationManager.Text("Grid.CheckedDiskPath"), ReadOnly = true, FillWeight = 28 });
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        ThemeManager.StyleGrid(_grid, _darkMode);
    }

    private void LoadPlaylistScopeItems()
    {
        try
        {
            _scopeComboBox.Items.Clear();
            using var sync = new EngineDjPlaylistSync(_dbPath);
            var collectionName = sync.GetImportCollectionName(_musicFolder);
            var playlists = sync.ListImportedCollectionPlaylists(_musicFolder);

            _scopeComboBox.Items.Add(MissingFileScopeItem.EntireCollection(collectionName));
            foreach (var playlist in playlists)
                _scopeComboBox.Items.Add(MissingFileScopeItem.Playlist(playlist));

            _scopeComboBox.SelectedIndex = 0;

            if (playlists.Count == 0)
                Log("Root collection not found yet. Run Scan Now first, or use this checker after the collection has been created.");
            else
                Log($"Loaded {playlists.Count} playlist scope(s) under root collection '{collectionName}'. The first option checks all Track rows in the selected m.db.");
        }
        catch (Exception ex)
        {
            Log("ERROR loading playlists: " + ex.Message);
            MessageBox.Show(this, ex.Message, LocalizationManager.Text("Dialog.LoadPlaylistsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task CheckMissingFilesAsync()
    {
        if (_scopeComboBox.SelectedItem is not MissingFileScopeItem scope)
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingScopeMessage"), LocalizationManager.Text("Dialog.MissingScopeTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ClearGrid();
        SetBusy(true, LocalizationManager.Text("Status.CheckingMissing"));
        try
        {
            ResetProgress();
            _progressTimer.Start();
            var result = await Task.Run(() =>
            {
                using var sync = new EngineDjPlaylistSync(_dbPath);
                return sync.FindMissingTrackFilesForImportedCollectionScope(_musicFolder, scope.PlaylistId, scope.IsEntireCollection, LogFromWorker, StoreLatestProgress);
            });

            FlushLatestProgress(force: true);
            PopulateGrid(result.MissingFiles);
            Log($"Check complete. Scope: {scope}. Tracks checked: {result.TracksChecked}. Missing files: {result.MissingFiles.Count}.");

            if (result.MissingFiles.Count > 0)
            {
                var reportPath = WriteMissingFilesReport(result);
                Log("Missing file report saved: " + reportPath);
                Log("Tick rows to delete them, or choose a search folder/drive and click Locate Checked Files to repair database paths.");
            }
            else
            {
                Log("No missing files found for the selected scope.");
            }

            SetStatus(LocalizationManager.Text("Status.CheckComplete"));
            if (result.TracksChecked == 0)
                SetProgressIdle(LocalizationManager.Text("Status.ProgressNoTracksChecked"));
            else
            {
                SetProgressComplete(result.TracksChecked);
                SetStatus(LocalizationManager.Format("Status.CheckCompleteDetails", result.TracksChecked, result.MissingFiles.Count));
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _progressTimer.Stop();
            SetBusy(false);
        }
    }


    private void BrowseRepairSearchFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = LocalizationManager.Text("Dialog.SelectRepairFolder"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (Directory.Exists(_repairSearchFolderTextBox.Text))
            dialog.SelectedPath = _repairSearchFolderTextBox.Text;
        else if (Directory.Exists(_musicFolder))
            dialog.SelectedPath = _musicFolder;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _repairSearchFolderTextBox.Text = dialog.SelectedPath;
    }

    private async Task LocateCheckedMissingTracksAsync()
    {
        var checkedItems = GetCheckedRows().ToList();
        if (checkedItems.Count == 0)
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.TickTracksLocate"), LocalizationManager.Text("Dialog.NoTracksSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var searchRoot = _repairSearchFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.MissingSearchFolderMessage"), LocalizationManager.Text("Dialog.MissingSearchFolderTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!EnsureEngineDjIsClosed()) return;

        var confirm = MessageBox.Show(
            this,
            LocalizationManager.Format("Dialog.LocateMissingConfirm", searchRoot, checkedItems.Count),
            LocalizationManager.Text("Dialog.LocateMissingTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        SetBusy(true, LocalizationManager.Text("Status.LocatingMissing"));
        try
        {
            BackupDatabase(_dbPath);
            ResetProgress();
            _progressTimer.Start();

            var repairResult = await Task.Run(() =>
            {
                using var sync = new EngineDjPlaylistSync(_dbPath);
                return sync.RepairMissingTracksByFileName(checkedItems, searchRoot, LogFromWorker, StoreLatestProgress);
            });

            FlushLatestProgress(force: true);
            RemoveDeletedRows(repairResult.UpdatedTrackIds);

            Log($"Locate complete. Updated: {repairResult.Updated}. Not found: {repairResult.NotFound}. Ambiguous skipped: {repairResult.Ambiguous}. Files scanned: {repairResult.FilesScanned}.");
            foreach (var item in repairResult.Messages)
                Log(item);

            SetStatus(LocalizationManager.Format("Status.LocateCompleteDetails", repairResult.Updated, repairResult.NotFound, repairResult.Ambiguous));
            if (checkedItems.Count == 0)
                SetProgressIdle(LocalizationManager.Text("Status.ProgressNoTracksSelected"));
            else
                SetProgressComplete(checkedItems.Count);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            _progressTimer.Stop();
            SetBusy(false);
        }
    }

    private async Task DeleteCheckedMissingTracksAsync()
    {
        var checkedItems = GetCheckedRows().ToList();
        if (checkedItems.Count == 0)
        {
            MessageBox.Show(this, LocalizationManager.Text("Dialog.TickTracksDelete"), LocalizationManager.Text("Dialog.NoTracksSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!EnsureEngineDjIsClosed()) return;

        var confirm = MessageBox.Show(
            this,
            LocalizationManager.Format("Dialog.DeleteMissingConfirm", checkedItems.Count),
            LocalizationManager.Text("Dialog.DeleteMissingTitle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        SetBusy(true, LocalizationManager.Text("Status.DeletingMissing"));
        try
        {
            BackupDatabase(_dbPath);
            var trackIds = checkedItems.Select(i => i.TrackId).ToList();
            var deleteResult = await Task.Run(() =>
            {
                using var sync = new EngineDjPlaylistSync(_dbPath);
                return sync.DeleteTracksFromDatabase(trackIds, LogFromWorker);
            });

            RemoveDeletedRows(trackIds);
            Log($"Deleted missing tracks from database. Tracks deleted: {deleteResult.TracksDeleted}. Playlist entries deleted: {deleteResult.PlaylistEntitiesDeleted}. Prepare-list entries deleted: {deleteResult.PreparelistEntriesDeleted}. Performance rows deleted: {deleteResult.PerformanceDataRowsDeleted}.");
            SetStatus(LocalizationManager.Text("Status.DeletedMissing"));
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

    private void PopulateGrid(IReadOnlyCollection<MissingTrackFile> missingFiles)
    {
        _grid.Rows.Clear();
        foreach (var item in missingFiles)
        {
            var index = _grid.Rows.Add(false, item.TrackId, item.Title, item.Artist, item.StoredPath, item.CheckedPath);
            _grid.Rows[index].Tag = item;
        }

        var hasRows = missingFiles.Count > 0;
        _deleteButton.Enabled = hasRows && !_busy;
        _repairButton.Enabled = hasRows && !_busy;
        UpdateHeaderCheckBoxState();
        PositionHeaderCheckBox();
    }

    private void ClearGrid()
    {
        _grid.Rows.Clear();
        _deleteButton.Enabled = false;
        _repairButton.Enabled = false;
        UpdateHeaderCheckBoxState();
        PositionHeaderCheckBox();
        ResetProgress();
    }

    private void ToggleAllCheckedFromHeader()
    {
        var total = _grid.Rows.Count;
        if (total == 0) return;

        var checkedCount = CountCheckedRows();
        SetAllChecks(checkedCount != total);
    }

    private int CountCheckedRows()
    {
        return _grid.Rows
            .Cast<DataGridViewRow>()
            .Count(row => row.Cells["Delete"].Value is bool value && value);
    }

    private void SetAllChecks(bool isChecked)
    {
        _grid.EndEdit();
        _suppressCellEvents = true;
        try
        {
            foreach (DataGridViewRow row in _grid.Rows)
                row.Cells["Delete"].Value = isChecked;
        }
        finally
        {
            _suppressCellEvents = false;
        }

        _grid.Invalidate();
        UpdateHeaderCheckBoxState();
    }

    private void PositionHeaderCheckBox()
    {
        if (!TryGetDeleteColumnIndex(out var deleteColumnIndex))
        {
            _headerSelectCheckBox.Visible = false;
            return;
        }

        var cellRectangle = _grid.GetCellDisplayRectangle(deleteColumnIndex, -1, true);
        if (cellRectangle.Width <= 0 || cellRectangle.Height <= 0)
        {
            _headerSelectCheckBox.Visible = false;
            return;
        }

        _headerSelectCheckBox.Visible = _grid.Rows.Count > 0;
        _headerSelectCheckBox.Location = new Point(
            cellRectangle.Left + (cellRectangle.Width - _headerSelectCheckBox.Width) / 2,
            cellRectangle.Top + (cellRectangle.Height - _headerSelectCheckBox.Height) / 2);
    }

    private bool TryGetDeleteColumnIndex(out int columnIndex)
    {
        var column = _grid.Columns["Delete"];
        if (column is null)
        {
            columnIndex = -1;
            return false;
        }

        columnIndex = column.Index;
        return true;
    }

    private void UpdateHeaderCheckBoxState()
    {
        var total = _grid.Rows.Count;
        var selected = CountCheckedRows();

        _headerSelectCheckBox.CheckState = total == 0 || selected == 0
            ? CheckState.Unchecked
            : selected == total
                ? CheckState.Checked
                : CheckState.Indeterminate;
        _headerSelectCheckBox.Enabled = total > 0 && !_busy;
        _headerSelectCheckBox.Visible = total > 0;
    }

    private IEnumerable<MissingTrackFile> GetCheckedRows()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var selected = row.Cells["Delete"].Value is bool value && value;
            if (selected && row.Tag is MissingTrackFile item)
                yield return item;
        }
    }

    private void RemoveDeletedRows(IReadOnlyCollection<long> trackIds)
    {
        var toDelete = new HashSet<long>(trackIds);
        for (var i = _grid.Rows.Count - 1; i >= 0; i--)
        {
            if (_grid.Rows[i].Tag is MissingTrackFile item && toDelete.Contains(item.TrackId))
                _grid.Rows.RemoveAt(i);
        }

        var hasRows = _grid.Rows.Count > 0;
        _deleteButton.Enabled = hasRows && !_busy;
        _repairButton.Enabled = hasRows && !_busy;
        UpdateHeaderCheckBoxState();
        PositionHeaderCheckBox();
    }

    private static string WriteMissingFilesReport(MissingTrackFilesResult result)
    {
        var reportDir = Path.Combine(AppContext.BaseDirectory, "EngineDjPlaylistSyncReports");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "missing-files-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv");

        using var writer = new StreamWriter(reportPath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("TrackId,Title,Artist,StoredPath,CheckedPath");
        foreach (var item in result.MissingFiles)
        {
            writer.WriteLine(string.Join(",",
                Csv(item.TrackId.ToString(CultureInfo.InvariantCulture)),
                Csv(item.Title),
                Csv(item.Artist),
                Csv(item.StoredPath),
                Csv(item.CheckedPath)));
        }
        return reportPath;
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void BackupDatabase(string dbPath)
    {
        var backup = dbPath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        File.Copy(dbPath, backup, overwrite: false);
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

    private void ResetProgress()
    {
        _progressTimer.Stop();
        lock (_progressLock)
            _latestProgress = null;

        _lastRenderedProgressCurrent = -1;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        _progressLabel.Text = LocalizationManager.Text("Status.ProgressIdle");
        SetStatus(LocalizationManager.Text("Status.Ready"));
    }

    private void StoreLatestProgress(TrackScanProgress progress)
    {
        lock (_progressLock)
            _latestProgress = progress;
    }

    private void FlushLatestProgress(bool force = false)
    {
        TrackScanProgress? progress;
        lock (_progressLock)
            progress = _latestProgress;

        if (progress is null)
            return;

        if (!force && progress.Current == _lastRenderedProgressCurrent)
            return;

        _lastRenderedProgressCurrent = progress.Current;
        UpdateTrackScanProgress(progress);
    }

    private void UpdateTrackScanProgress(TrackScanProgress progress)
    {
        var total = Math.Max(progress.Total, 1);
        var current = Math.Min(Math.Max(progress.Current, 0), total);
        _progressBar.Maximum = total;
        _progressBar.Value = current;
        _progressLabel.Text = LocalizationManager.Format("Status.ProgressCount", current, total);
        SetStatus(LocalizationManager.Format("Status.CheckingItem", current, total, progress.FileName));
    }

    private void SetProgressComplete(int total)
    {
        var safeTotal = Math.Max(total, 1);
        _progressBar.Maximum = safeTotal;
        _progressBar.Value = safeTotal;
        _progressLabel.Text = LocalizationManager.Format("Status.ProgressCount", total, total);
    }

    private void SetProgressIdle(string message)
    {
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 100;
        _progressBar.Value = 0;
        _progressLabel.Text = message;
    }

    private void StyleActionButtons()
    {
        foreach (var button in new[] { _deleteButton, _repairButton })
        {
            button.ForeColor = Color.White;
            button.UseVisualStyleBackColor = false;
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _busy = busy;
        _scopeComboBox.Enabled = !busy;
        _refreshPlaylistsButton.Enabled = !busy;
        _deleteButton.Enabled = !busy;
        _repairButton.Enabled = !busy;
        StyleActionButtons();
        _browseRepairFolderButton.Enabled = !busy;
        _repairSearchFolderTextBox.Enabled = !busy;
        UpdateHeaderCheckBoxState();
        if (status is not null) SetStatus(status);
    }

    private void SetStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return;

        // The old bottom status box has been removed; write status messages to the log instead.
        // Avoid flooding the log with every per-track progress update, because the progress bar
        // and progress label already show the live scan position.
        if (status.StartsWith(LocalizationManager.Text("Status.CheckingItem").Split('{')[0], StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(status, _lastStatusText, StringComparison.Ordinal))
            return;

        _lastStatusText = status;
        Log(status);
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
        MessageBox.Show(this, ex.Message, LocalizationManager.Text("Form.MissingFilesTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ApplyHeaderCheckBoxTheme()
    {
        _headerSelectCheckBox.BackColor = _darkMode
            ? Color.FromArgb(45, 50, 60)
            : SystemColors.Control;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            return;
        }
        _progressTimer.Stop();
        base.OnFormClosing(e);
    }
}

internal sealed class MissingFileScopeItem
{
    public bool IsEntireCollection { get; init; }
    public long? PlaylistId { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    public static MissingFileScopeItem EntireCollection(string collectionName) => new()
    {
        IsEntireCollection = true,
        DisplayName = LocalizationManager.Text("Scope.EntireCollection")
    };

    public static MissingFileScopeItem Playlist(PlaylistInfo playlist) => new()
    {
        IsEntireCollection = false,
        PlaylistId = playlist.Id,
        DisplayName = playlist.Path
    };

    public override string ToString() => DisplayName;
}
