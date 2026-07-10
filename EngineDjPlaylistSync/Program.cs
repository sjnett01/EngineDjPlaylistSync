namespace EngineDjPlaylistSync;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var settings = AppSettingsStore.Load();
        LocalizationManager.ApplyLanguage(settings.Language);

        ApplicationConfiguration.Initialize();
        DpiScalingService.Install();
        Application.Run(new MainForm(args));
    }
}
