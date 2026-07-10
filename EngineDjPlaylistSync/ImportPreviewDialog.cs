namespace EngineDjPlaylistSync;

public sealed class ImportPreviewDialog : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoGenerateColumns = false,
        MultiSelect = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect
    };

    private readonly Label _summaryLabel = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly CheckBox _analysisCheckBox = new()
    {
        Text = LocalizationManager.Text("CheckBox.Analysis"),
        AutoSize = true,
        Checked = true
    };

    private readonly CheckBox _keyDetectionCheckBox = new()
    {
        Text = LocalizationManager.Text("CheckBox.KeyDetection"),
        AutoSize = true,
        Enabled = true,
        Checked = true
    };

    private readonly CheckBox _officialAnalyzerCheckBox = new()
    {
        Text = LocalizationManager.Text("CheckBox.OfficialAnalyzer"),
        AutoSize = true,
        Enabled = false
    };


    private readonly CheckBox _captureOfficialAnalyzerCheckBox = new()
    {
        Text = LocalizationManager.Text("CheckBox.CaptureOfficialAnalyzer"),
        AutoSize = true,
        Enabled = false
    };

    private readonly ComboBox _keyNotationComboBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };

    private readonly ComboBox _bpmRangeComboBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 95
    };

    private readonly NumericUpDown _concurrentAnalysisTracksNumeric = new()
    {
        Minimum = 1,
        Maximum = 16,
        Value = Math.Max(1, Math.Min(Environment.ProcessorCount - 1, 4)),
        Width = 64
    };
    private readonly CheckBox _headerSelectCheckBox = new()
    {
        ThreeState = true,
        Size = new Size(16, 16),
        BackColor = Color.Transparent,
        TabStop = false
    };
    private bool _suppressCellEvents;
    private readonly Button _importButton = new() { Text = LocalizationManager.Text("Button.ImportCheckedTracks"), DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = LocalizationManager.Text("Button.Cancel"), DialogResult = DialogResult.Cancel };
    private readonly List<ImportPreviewTrack> _tracks;
    private readonly bool _darkMode;

    public IReadOnlyList<string> SelectedFiles => _grid.Rows
        .Cast<DataGridViewRow>()
        .Where(row => row.Tag is ImportPreviewTrack && row.Cells[0].Value is bool value && value)
        .Select(row => ((ImportPreviewTrack)row.Tag!).FullPath)
        .ToList();

    public bool GenerateExperimentalAnalysis => _analysisCheckBox.Checked;
    public bool GenerateExperimentalKeyDetection => _analysisCheckBox.Checked && _keyDetectionCheckBox.Checked;
    public bool UseOfficialOfflineAnalyzer => _analysisCheckBox.Checked && _officialAnalyzerCheckBox.Checked && _officialAnalyzerCheckBox.Enabled;
    public bool UseManagedInternalAnalyzer => _analysisCheckBox.Checked && !UseOfficialOfflineAnalyzer;
    public bool CaptureOfficialAnalyzerFrames => UseOfficialOfflineAnalyzer && _captureOfficialAnalyzerCheckBox.Checked && _captureOfficialAnalyzerCheckBox.Enabled;
    public int ConcurrentAnalysisTracks => _analysisCheckBox.Checked ? (int)_concurrentAnalysisTracksNumeric.Value : 1;
    public ExternalAnalysisOptions AnalysisOptions => new(GetSelectedBpmRange().Min, GetSelectedBpmRange().Max, GetSelectedKeyNotation());

    public ImportPreviewDialog(IReadOnlyList<ImportPreviewTrack> tracks, bool darkMode)
    {
        _tracks = tracks.ToList();
        _darkMode = darkMode;
        Text = LocalizationManager.Text("Form.ImportPreviewTitle");
        Icon = EmbeddedAssets.LoadAppIcon() ?? Icon;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1120, 720);
        Size = new Size(1240, 800);
        DpiScalingService.ConfigureForm(this, darkMode: _darkMode, module: "Import Preview");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 8
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new Label
        {
            Text = LocalizationManager.Text("Label.ImportPreviewHeader"),
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(header, 0, 0);

        BuildGrid();
        root.Controls.Add(_grid, 0, 1);

        var analysisOptionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        analysisOptionsRow.Controls.Add(_analysisCheckBox);
        analysisOptionsRow.Controls.Add(new Label { Text = LocalizationManager.Text("Label.BpmRange"), AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 2, 0, 0) });
        analysisOptionsRow.Controls.Add(_bpmRangeComboBox);
        analysisOptionsRow.Controls.Add(new Label { Text = LocalizationManager.Text("Label.ConcurrentTracks"), AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 2, 0, 0) });
        analysisOptionsRow.Controls.Add(_concurrentAnalysisTracksNumeric);

        var keyOptionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        keyOptionsRow.Controls.Add(_keyDetectionCheckBox);
        keyOptionsRow.Controls.Add(new Label { Text = LocalizationManager.Text("Label.KeyNotation"), AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 2, 0, 0) });
        keyOptionsRow.Controls.Add(_keyNotationComboBox);

        var officialAnalyzerRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        officialAnalyzerRow.Controls.Add(_officialAnalyzerCheckBox);


        var captureAnalyzerRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(24, 4, 0, 0)
        };
        captureAnalyzerRow.Controls.Add(_captureOfficialAnalyzerCheckBox);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        buttons.Controls.Add(_importButton);
        buttons.Controls.Add(_cancelButton);

        _bpmRangeComboBox.Items.AddRange(new object[] { "58-115", "68-135", "78-155", "88-175", "98-195" });
        _bpmRangeComboBox.SelectedItem = "98-195";
        _keyNotationComboBox.Items.AddRange(new object[]
        {
            new ComboBoxChoice<KeyNotation>(KeyNotation.Sharps, LocalizationManager.Text("KeyNotation.Sharps")),
            new ComboBoxChoice<KeyNotation>(KeyNotation.Flats, LocalizationManager.Text("KeyNotation.Flats")),
            new ComboBoxChoice<KeyNotation>(KeyNotation.OpenKey, LocalizationManager.Text("KeyNotation.OpenKey")),
            new ComboBoxChoice<KeyNotation>(KeyNotation.Camelot, LocalizationManager.Text("KeyNotation.Camelot"))
        });
        _keyNotationComboBox.SelectedIndex = 3;
        _bpmRangeComboBox.Enabled = true;
        _concurrentAnalysisTracksNumeric.Enabled = true;
        _keyNotationComboBox.Enabled = true;
        var officialAnalyzerAvailable = OfficialOfflineAnalyzerBridge.TryFindOfflineAnalyzer(out var offlineAnalyzerPath);
        if (officialAnalyzerAvailable)
        {
            _officialAnalyzerCheckBox.Text = LocalizationManager.Text("CheckBox.OfficialAnalyzer");
            _officialAnalyzerCheckBox.Tag = offlineAnalyzerPath;
        }
        else
        {
            _officialAnalyzerCheckBox.Text = LocalizationManager.Text("CheckBox.OfficialAnalyzerNotFound");
        }
        _officialAnalyzerCheckBox.Enabled = _analysisCheckBox.Checked && officialAnalyzerAvailable;
        _captureOfficialAnalyzerCheckBox.Enabled = _analysisCheckBox.Checked && officialAnalyzerAvailable && _officialAnalyzerCheckBox.Checked;

        _analysisCheckBox.CheckedChanged += (_, _) =>
        {
            var enabled = _analysisCheckBox.Checked;
            _bpmRangeComboBox.Enabled = enabled;
            _concurrentAnalysisTracksNumeric.Enabled = enabled;
            _keyDetectionCheckBox.Enabled = enabled;
            _officialAnalyzerCheckBox.Enabled = enabled && officialAnalyzerAvailable;
            _captureOfficialAnalyzerCheckBox.Enabled = enabled && officialAnalyzerAvailable && _officialAnalyzerCheckBox.Checked;
            _keyNotationComboBox.Enabled = enabled && _keyDetectionCheckBox.Checked;
            if (!enabled)
            {
                _officialAnalyzerCheckBox.Checked = false;
                _captureOfficialAnalyzerCheckBox.Checked = false;
            }
        };
        _keyDetectionCheckBox.CheckedChanged += (_, _) =>
        {
            _keyNotationComboBox.Enabled = _analysisCheckBox.Checked && _keyDetectionCheckBox.Checked;
        };
        _officialAnalyzerCheckBox.CheckedChanged += (_, _) =>
        {
            _captureOfficialAnalyzerCheckBox.Enabled = _analysisCheckBox.Checked && _officialAnalyzerCheckBox.Checked && _officialAnalyzerCheckBox.Enabled;
            if (!_captureOfficialAnalyzerCheckBox.Enabled)
                _captureOfficialAnalyzerCheckBox.Checked = false;
        };
        root.Controls.Add(analysisOptionsRow, 0, 2);
        root.Controls.Add(keyOptionsRow, 0, 3);
        root.Controls.Add(officialAnalyzerRow, 0, 4);
        root.Controls.Add(captureAnalyzerRow, 0, 5);
        root.Controls.Add(buttons, 0, 6);

        root.Controls.Add(_summaryLabel, 0, 7);
        Controls.Add(root);

        AcceptButton = _importButton;
        CancelButton = _cancelButton;

        _headerSelectCheckBox.AutoCheck = false;
        _headerSelectCheckBox.Click += (_, _) => ToggleAllCheckedFromHeader();
        _grid.Controls.Add(_headerSelectCheckBox);
        _grid.ColumnWidthChanged += (_, _) => PositionHeaderCheckBox();
        _grid.Scroll += (_, _) => PositionHeaderCheckBox();
        _grid.SizeChanged += (_, _) => PositionHeaderCheckBox();
        _grid.CellValueChanged += (_, e) =>
        {
            if (_suppressCellEvents) return;
            if (e.ColumnIndex == 0 && e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is ImportPreviewTrack)
                UpdateSummary();
        };
        _grid.ColumnHeaderMouseClick += (_, e) =>
        {
            if (e.ColumnIndex == 0)
                ToggleAllCheckedFromHeader();
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _importButton.Click += (_, e) =>
        {
            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show(this, LocalizationManager.Text("Dialog.SelectTrackImport"), LocalizationManager.Text("Dialog.NoTracksSelectedTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        LoadRows();
        ApplyTheme();
        UpdateSummary();
        PositionHeaderCheckBox();
    }

    private void BuildGrid()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "",
            Width = 54,
            DataPropertyName = "Import"
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = LocalizationManager.Text("Grid.Status"),
            Width = 120,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = LocalizationManager.Text("Grid.Filename"),
            Width = 280,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = LocalizationManager.Text("Grid.FolderUnderCollection"),
            Width = 220,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = LocalizationManager.Text("Grid.StoredEnginePath"),
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });
    }

    private void LoadRows()
    {
        _grid.Rows.Clear();

        var newTracks = _tracks
            .Where(t => !t.AlreadyInDatabase)
            .OrderBy(t => t.RelativeFolder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var relocatedTracks = _tracks
            .Where(t => t.Status == ImportPreviewStatus.RelocatedExisting)
            .OrderBy(t => t.RelativeFolder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingTracks = _tracks
            .Where(t => t.Status == ImportPreviewStatus.AlreadyInDatabase)
            .OrderBy(t => t.RelativeFolder, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (newTracks.Count > 0)
        {
            AddSectionRow(LocalizationManager.Format("Preview.Section.New", newTracks.Count));
            foreach (var track in newTracks)
                AddTrackRow(track, isChecked: true);
        }

        if (relocatedTracks.Count > 0)
        {
            AddSectionRow(LocalizationManager.Format("Preview.Section.Relocated", relocatedTracks.Count));
            foreach (var track in relocatedTracks)
                AddTrackRow(track, isChecked: true);
        }

        if (existingTracks.Count > 0)
        {
            AddSectionRow(LocalizationManager.Format("Preview.Section.Existing", existingTracks.Count));
            foreach (var track in existingTracks)
                AddTrackRow(track, isChecked: false);
        }
    }

    private void AddTrackRow(ImportPreviewTrack track, bool isChecked)
    {
        var storedPathDisplay = track.Status == ImportPreviewStatus.RelocatedExisting && !string.IsNullOrWhiteSpace(track.ExistingStoredPath)
            ? track.StoredPath + "  (" + LocalizationManager.Format("Preview.Was", track.ExistingStoredPath) + ")"
            : track.StoredPath;
        var rowIndex = _grid.Rows.Add(isChecked, track.StatusText, track.FileName, string.IsNullOrWhiteSpace(track.RelativeFolder) ? LocalizationManager.Text("Preview.Root") : track.RelativeFolder, storedPathDisplay);
        _grid.Rows[rowIndex].Tag = track;
    }

    private void AddSectionRow(string text)
    {
        var rowIndex = _grid.Rows.Add(false, "", text, "", "");
        var row = _grid.Rows[rowIndex];
        row.Tag = null;
        row.ReadOnly = true;
        row.Cells[0] = new DataGridViewTextBoxCell { Value = "" };
        row.DefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
        row.DefaultCellStyle.BackColor = _darkMode ? Color.FromArgb(48, 54, 66) : Color.FromArgb(235, 238, 244);
        row.DefaultCellStyle.ForeColor = _darkMode ? Color.White : Color.Black;
        row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
        row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
    }

    private void ToggleAllCheckedFromHeader()
    {
        var total = _tracks.Count;
        var checkedCount = CountCheckedRows();
        SetAllChecked(checkedCount != total);
    }

    private int CountCheckedRows()
    {
        return _grid.Rows
            .Cast<DataGridViewRow>()
            .Count(row => row.Tag is ImportPreviewTrack && row.Cells[0].Value is bool value && value);
    }

    private void SetAllChecked(bool isChecked)
    {
        _grid.EndEdit();
        _suppressCellEvents = true;
        try
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is ImportPreviewTrack)
                    row.Cells[0].Value = isChecked;
            }
        }
        finally
        {
            _suppressCellEvents = false;
        }

        _grid.Invalidate();
        UpdateSummary();
    }

    private (double Min, double Max) GetSelectedBpmRange()
    {
        var text = _bpmRangeComboBox.SelectedItem?.ToString() ?? "98-195";
        var parts = text.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && double.TryParse(parts[0], out var min) && double.TryParse(parts[1], out var max))
            return (min, max);
        return (98.0, 195.0);
    }

    private KeyNotation GetSelectedKeyNotation()
    {
        return _keyNotationComboBox.SelectedItem is ComboBoxChoice<KeyNotation> choice
            ? choice.Value
            : KeyNotation.Camelot;
    }

    private void PositionHeaderCheckBox()
    {
        if (_grid.Columns.Count == 0) return;
        var cellRectangle = _grid.GetCellDisplayRectangle(0, -1, true);
        if (cellRectangle.Width <= 0 || cellRectangle.Height <= 0)
        {
            _headerSelectCheckBox.Visible = false;
            return;
        }

        _headerSelectCheckBox.Visible = true;
        _headerSelectCheckBox.Location = new Point(
            cellRectangle.Left + (cellRectangle.Width - _headerSelectCheckBox.Width) / 2,
            cellRectangle.Top + (cellRectangle.Height - _headerSelectCheckBox.Height) / 2);
    }

    private void UpdateSummary()
    {
        _grid.EndEdit();
        var selected = CountCheckedRows();
        var total = _tracks.Count;
        var newTotal = _tracks.Count(t => t.Status == ImportPreviewStatus.NewTrack);
        var relocatedTotal = _tracks.Count(t => t.Status == ImportPreviewStatus.RelocatedExisting);
        var existingTotal = _tracks.Count(t => t.Status == ImportPreviewStatus.AlreadyInDatabase);
        var newTracks = _grid.Rows.Cast<DataGridViewRow>().Count(row => row.Tag is ImportPreviewTrack t && t.Status == ImportPreviewStatus.NewTrack && row.Cells[0].Value is bool value && value);
        var relocatedTracks = _grid.Rows.Cast<DataGridViewRow>().Count(row => row.Tag is ImportPreviewTrack t && t.Status == ImportPreviewStatus.RelocatedExisting && row.Cells[0].Value is bool value && value);
        var existingTracks = selected - newTracks - relocatedTracks;
        _summaryLabel.Text = LocalizationManager.Format("Preview.Summary", selected, total, newTracks, newTotal, relocatedTracks, relocatedTotal, existingTracks, existingTotal);
        UpdateHeaderCheckBox(selected, total);
    }

    private void UpdateHeaderCheckBox(int selected, int total)
    {
        _headerSelectCheckBox.CheckState = selected == 0
            ? CheckState.Unchecked
            : selected == total
                ? CheckState.Checked
                : CheckState.Indeterminate;
    }

    private void ApplyTheme()
    {
        ThemeManager.Apply(this, _darkMode);
        PerformLayout();
        if (_darkMode)
        {
            _grid.BackgroundColor = Color.FromArgb(24, 28, 34);
            _grid.GridColor = Color.FromArgb(60, 66, 78);
            _grid.DefaultCellStyle.BackColor = Color.FromArgb(34, 38, 46);
            _grid.DefaultCellStyle.ForeColor = Color.WhiteSmoke;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(68, 78, 96);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 50, 60);
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.EnableHeadersVisualStyles = false;
            _headerSelectCheckBox.BackColor = _grid.ColumnHeadersDefaultCellStyle.BackColor;
        }
        else
        {
            _headerSelectCheckBox.BackColor = SystemColors.Control;
        }
    }
}
