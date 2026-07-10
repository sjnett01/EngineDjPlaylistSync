using System.Drawing;
using System.Windows.Forms;

namespace EngineDjPlaylistSync;

internal static class DpiScalingService
{
    private sealed class DpiState
    {
        public int Dpi { get; set; }
        public string ScreenDeviceName { get; set; } = string.Empty;
        public bool FirstCheckComplete { get; set; }
        public Action<string>? Log { get; set; }
        public string Module { get; set; } = "Application";
        public bool DarkMode { get; set; }
    }

    private static readonly Dictionary<Form, DpiState> AttachedForms = new();
    private static bool _installed;
    private static Action<string>? _defaultLog;

    public static void Install(Action<string>? log = null)
    {
        _defaultLog = log ?? _defaultLog;

        if (_installed)
        {
            return;
        }

        _installed = true;
        Application.Idle += (_, _) => AttachOpenForms();
    }

    public static void ConfigureForm(Form form, bool darkMode = false, string module = "Application", Action<string>? log = null)
    {
        if (form == null)
        {
            return;
        }

        form.AutoScaleMode = AutoScaleMode.Dpi;
        AttachForm(form, darkMode, module, log);
    }

    public static void UpdateDarkMode(Form form, bool darkMode)
    {
        if (AttachedForms.TryGetValue(form, out var state))
        {
            state.DarkMode = darkMode;
        }
    }

    public static void AttachOpenForms()
    {
        foreach (Form form in Application.OpenForms)
        {
            if (AttachedForms.ContainsKey(form))
            {
                continue;
            }

            AttachForm(form, TryInferDarkMode(form), form.Text, _defaultLog);
        }
    }

    private static void AttachForm(Form form, bool darkMode, string? module, Action<string>? log)
    {
        if (AttachedForms.TryGetValue(form, out var existing))
        {
            existing.DarkMode = darkMode;

            if (log != null)
            {
                existing.Log = log;
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                existing.Module = module!;
            }

            CheckForScreenChange(form, existing, writeLog: false);
            return;
        }

        var state = new DpiState
        {
            Dpi = GetFormDpi(form),
            ScreenDeviceName = GetScreenDeviceName(form),
            Log = log ?? _defaultLog,
            Module = string.IsNullOrWhiteSpace(module) ? "Application" : module!,
            DarkMode = darkMode
        };

        AttachedForms[form] = state;

        form.HandleCreated += (_, _) =>
        {
            state.Dpi = GetFormDpi(form);
            state.ScreenDeviceName = GetScreenDeviceName(form);
            ApplyThemeForDpiChange(form, state.DarkMode);
            EnsureFormFitsCurrentScreen(form);
        };

        form.DpiChanged += (_, _) =>
        {
            var oldDpi = state.Dpi;
            var newDpi = GetFormDpi(form);
            state.Dpi = newDpi;
            state.ScreenDeviceName = GetScreenDeviceName(form);
            EnsureFormFitsCurrentScreen(form);

            if (oldDpi > 0 && oldDpi != newDpi)
            {
                ApplyThemeForDpiChange(form, state.DarkMode);
                WriteLog(state, $"DPI changed for {GetFormName(form)} from {oldDpi} to {newDpi}. Layout adjusted for current monitor.");
            }
        };

        form.Move += (_, _) => CheckForScreenChange(form, state, writeLog: true);
        form.ResizeEnd += (_, _) =>
        {
            CheckForScreenChange(form, state, writeLog: true);
            EnsureFormFitsCurrentScreen(form);
        };
        form.Disposed += (_, _) => AttachedForms.Remove(form);

        EnsureFormFitsCurrentScreen(form);
    }

    private static void CheckForScreenChange(Form form, DpiState state, bool writeLog)
    {
        if (form.IsDisposed)
        {
            return;
        }

        var currentScreen = GetScreenDeviceName(form);
        var currentDpi = GetFormDpi(form);

        if (!state.FirstCheckComplete)
        {
            state.FirstCheckComplete = true;
            state.ScreenDeviceName = currentScreen;
            state.Dpi = currentDpi;
            return;
        }

        if (!string.Equals(state.ScreenDeviceName, currentScreen, StringComparison.OrdinalIgnoreCase) || state.Dpi != currentDpi)
        {
            var oldScreen = state.ScreenDeviceName;
            var oldDpi = state.Dpi;
            state.ScreenDeviceName = currentScreen;
            state.Dpi = currentDpi;
            EnsureFormFitsCurrentScreen(form);

            if (oldDpi != currentDpi)
            {
                ApplyThemeForDpiChange(form, state.DarkMode);
            }

            if (writeLog)
            {
                WriteLog(state, $"{GetFormName(form)} moved from {oldScreen} ({oldDpi} DPI) to {currentScreen} ({currentDpi} DPI). Layout checked for monitor scaling.");
            }
        }
    }

    private static int GetFormDpi(Form form)
    {
        try
        {
            return form.DeviceDpi;
        }
        catch
        {
            return 96;
        }
    }

    private static string GetScreenDeviceName(Form form)
    {
        try
        {
            return Screen.FromControl(form).DeviceName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetFormName(Form form)
    {
        return string.IsNullOrWhiteSpace(form.Text) ? form.GetType().Name : form.Text;
    }

    private static bool TryInferDarkMode(Form form)
    {
        try
        {
            var back = form.BackColor;
            if (back == Color.Empty)
            {
                return false;
            }

            var brightness = (back.R * 0.299) + (back.G * 0.587) + (back.B * 0.114);
            return brightness < 128;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureFormFitsCurrentScreen(Form form)
    {
        if (form.IsDisposed || form.WindowState != FormWindowState.Normal || !form.IsHandleCreated)
        {
            return;
        }

        var workingArea = Screen.FromControl(form).WorkingArea;
        var margin = ScaleLogical(form, 24);
        var maxWidth = Math.Max(640, workingArea.Width - margin);
        var maxHeight = Math.Max(480, workingArea.Height - margin);

        var width = Math.Min(form.Width, maxWidth);
        var height = Math.Min(form.Height, maxHeight);

        if (width != form.Width || height != form.Height)
        {
            form.Size = new Size(width, height);
        }

        var x = form.Left;
        var y = form.Top;

        if (form.Right > workingArea.Right)
        {
            x = workingArea.Right - form.Width;
        }

        if (form.Bottom > workingArea.Bottom)
        {
            y = workingArea.Bottom - form.Height;
        }

        if (x < workingArea.Left)
        {
            x = workingArea.Left;
        }

        if (y < workingArea.Top)
        {
            y = workingArea.Top;
        }

        if (x != form.Left || y != form.Top)
        {
            form.Location = new Point(x, y);
        }
    }

    private static int ScaleLogical(Form form, int value)
    {
        var dpi = GetFormDpi(form);
        return Math.Max(value, (int)Math.Round(value * dpi / 96.0));
    }

    private static void ApplyThemeForDpiChange(Form form, bool darkMode)
    {
        try
        {
            ThemeManager.Apply(form, darkMode);
            form.PerformLayout();
        }
        catch
        {
            // DPI correction should never block monitor movement or resize handling.
        }
    }

    private static void WriteLog(DpiState state, string message)
    {
        var log = state.Log ?? _defaultLog;
        log?.Invoke($"{state.Module}: {message}");
    }
}
