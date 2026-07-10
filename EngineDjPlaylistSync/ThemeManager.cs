using System.Drawing;
using System.Windows.Forms;

namespace EngineDjPlaylistSync;

internal static class ThemeManager
{
    private const float GlobalFontSizeMultiplier = 0.75F;

    private static readonly Color LightBack = Color.FromArgb(246, 248, 251);
    private static readonly Color LightPanel = Color.White;
    private static readonly Color LightText = Color.FromArgb(26, 32, 44);
    private static readonly Color LightMuted = Color.FromArgb(74, 85, 104);
    private static readonly Color LightBorder = Color.FromArgb(203, 213, 225);

    private static readonly Color DarkBack = Color.FromArgb(24, 26, 32);
    private static readonly Color DarkPanel = Color.FromArgb(34, 38, 46);
    private static readonly Color DarkText = Color.FromArgb(230, 236, 245);
    private static readonly Color DarkMuted = Color.FromArgb(170, 180, 195);
    private static readonly Color DarkBorder = Color.FromArgb(70, 78, 92);

    private static readonly Color Accent = Color.FromArgb(34, 116, 224);
    private static readonly Color AccentDark = Color.FromArgb(51, 133, 255);

    private static readonly Dictionary<RowStyle, float> OriginalAbsoluteRowHeights = new();
    private static readonly Dictionary<ColumnStyle, float> OriginalAbsoluteColumnWidths = new();

    public static void Apply(Form form, bool darkMode)
    {
        DpiScalingService.UpdateDarkMode(form, darkMode);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Font = UiFont(form, 9F);
        form.BackColor = darkMode ? DarkBack : LightBack;
        form.ForeColor = darkMode ? DarkText : LightText;
        ApplyToControlTree(form, darkMode);
        ScaleFixedLayoutSections(form);
    }

    public static void ApplyToControlTree(Control root, bool darkMode)
    {
        foreach (Control control in root.Controls)
        {
            StyleControl(control, darkMode);
            ApplyToControlTree(control, darkMode);
        }
    }

    public static void StyleControl(Control control, bool darkMode)
    {
        var back = darkMode ? DarkBack : LightBack;
        var panel = darkMode ? DarkPanel : LightPanel;
        var text = darkMode ? DarkText : LightText;
        var muted = darkMode ? DarkMuted : LightMuted;
        var accent = darkMode ? AccentDark : Accent;

        control.Font = UiFont(control, 9F);
        switch (control)
        {
            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 0;
                button.BackColor = accent;
                button.ForeColor = Color.White;
                button.Cursor = Cursors.Hand;
                button.UseVisualStyleBackColor = false;
                EnsureButtonFitsText(button, minimumWidth: 96, horizontalPadding: 30, minimumHeight: 31);
                break;

            case TextBox textBox:
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = darkMode ? Color.FromArgb(42, 47, 56) : Color.White;
                textBox.ForeColor = text;
                textBox.MinimumSize = new Size(0, DpiScale(textBox, 27));
                break;

            case ComboBox comboBox:
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.BackColor = darkMode ? Color.FromArgb(42, 47, 56) : Color.White;
                comboBox.ForeColor = text;
                comboBox.MinimumSize = new Size(0, DpiScale(comboBox, 27));
                break;

            case CheckBox checkBox:
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = text;
                checkBox.MinimumSize = new Size(0, DpiScale(checkBox, 24));
                break;

            case DataGridView grid:
                StyleGrid(grid, darkMode);
                break;

            case ProgressBar:
                break;

            case Label label:
                label.ForeColor = text;
                label.BackColor = label.BorderStyle != BorderStyle.None ? panel : Color.Transparent;
                if (label.AutoSize)
                {
                    label.MinimumSize = new Size(0, DpiScale(label, 24));
                }
                break;

            case TableLayoutPanel table:
                table.BackColor = back;
                table.ForeColor = text;
                table.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
                break;

            case FlowLayoutPanel flow:
                flow.BackColor = Color.Transparent;
                flow.ForeColor = text;
                break;

            case Panel or GroupBox:
                control.BackColor = back;
                control.ForeColor = text;
                break;

            default:
                control.BackColor = back;
                control.ForeColor = text;
                break;
        }

        if (!control.Enabled)
        {
            control.ForeColor = muted;
        }
    }

    public static void StyleGrid(DataGridView grid, bool darkMode)
    {
        var panel = darkMode ? DarkPanel : LightPanel;
        var alt = darkMode ? Color.FromArgb(39, 44, 53) : Color.FromArgb(248, 250, 252);
        var text = darkMode ? DarkText : LightText;
        var header = darkMode ? Color.FromArgb(48, 55, 66) : Color.FromArgb(226, 232, 240);
        var border = darkMode ? DarkBorder : LightBorder;
        var select = darkMode ? Color.FromArgb(44, 95, 154) : Color.FromArgb(207, 226, 255);
        var selectText = darkMode ? Color.White : LightText;
        var headerFont = UiFont(grid, 9F, FontStyle.Bold);
        var cellFont = UiFont(grid, 9F);
        var headerHeight = Math.Max(DpiScale(grid, 28), TextRenderer.MeasureText("Ag", headerFont).Height + DpiScale(grid, 10));
        var rowHeight = Math.Max(DpiScale(grid, 26), TextRenderer.MeasureText("Ag", cellFont).Height + DpiScale(grid, 8));

        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = panel;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = border;
        grid.DefaultCellStyle.BackColor = panel;
        grid.DefaultCellStyle.ForeColor = text;
        grid.DefaultCellStyle.SelectionBackColor = select;
        grid.DefaultCellStyle.SelectionForeColor = selectText;
        grid.AlternatingRowsDefaultCellStyle.BackColor = alt;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = text;
        grid.ColumnHeadersDefaultCellStyle.BackColor = header;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = header;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = text;
        grid.ColumnHeadersDefaultCellStyle.Font = headerFont;
        grid.DefaultCellStyle.Font = cellFont;
        grid.RowTemplate.Height = rowHeight;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = headerHeight;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Height = rowHeight;
        }
    }

    private static Font UiFont(Control control, float size, FontStyle style = FontStyle.Regular)
        => new("Segoe UI", Math.Max(6.0F, size * GlobalFontSizeMultiplier * FontScale(control)), style, GraphicsUnit.Point);

    private static float FontScale(Control control)
    {
        var dpi = Math.Max(96, GetDpi(control));
        if (dpi <= 96)
        {
            return 1.0F;
        }

        var scale = (float)Math.Sqrt(dpi / 96.0);
        return Math.Clamp(scale, 1.0F, 1.45F);
    }

    private static int GetDpi(Control control)
    {
        try
        {
            if (control is Form form && form.IsHandleCreated)
            {
                return form.DeviceDpi;
            }

            var topLevel = control.FindForm();
            if (topLevel != null && topLevel.IsHandleCreated)
            {
                return topLevel.DeviceDpi;
            }
        }
        catch
        {
        }

        return 96;
    }

    private static int DpiScale(Control control, int value)
    {
        var dpi = Math.Max(96, GetDpi(control));
        return Math.Max(value, (int)Math.Round(value * dpi / 96.0));
    }


    private static void EnsureButtonFitsText(Control button, int minimumWidth, int horizontalPadding, int minimumHeight)
    {
        var text = button.Text ?? string.Empty;
        var proposed = new Size(int.MaxValue, int.MaxValue);
        var measured = TextRenderer.MeasureText(text, button.Font, proposed, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var requiredWidth = measured.Width + DpiScale(button, horizontalPadding);
        var requiredHeight = measured.Height + DpiScale(button, 10);

        var width = Math.Max(button.Width, Math.Max(DpiScale(button, minimumWidth), requiredWidth));
        var height = Math.Max(button.Height, Math.Max(DpiScale(button, minimumHeight), requiredHeight));
        button.Width = width;
        button.Height = height;
        button.MinimumSize = new Size(Math.Max(button.MinimumSize.Width, width), Math.Max(button.MinimumSize.Height, height));
    }

    private static void ScaleFixedLayoutSections(Control root)
    {
        var dpi = Math.Max(96, GetDpi(root));
        var factor = dpi / 96.0F;

        foreach (var table in EnumerateControls(root).OfType<TableLayoutPanel>())
        {
            foreach (RowStyle row in table.RowStyles)
            {
                if (row.SizeType != SizeType.Absolute)
                {
                    continue;
                }

                if (!OriginalAbsoluteRowHeights.TryGetValue(row, out var originalHeight))
                {
                    originalHeight = row.Height;
                    OriginalAbsoluteRowHeights[row] = originalHeight;
                }

                row.Height = Math.Max(originalHeight, (float)Math.Round(originalHeight * factor));
            }

            foreach (ColumnStyle column in table.ColumnStyles)
            {
                if (column.SizeType != SizeType.Absolute)
                {
                    continue;
                }

                if (!OriginalAbsoluteColumnWidths.TryGetValue(column, out var originalWidth))
                {
                    originalWidth = column.Width;
                    OriginalAbsoluteColumnWidths[column] = originalWidth;
                }

                column.Width = Math.Max(originalWidth, (float)Math.Round(originalWidth * factor));
            }
        }
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        yield return root;

        foreach (Control child in root.Controls)
        {
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }
}
