using System.Reflection;

namespace EngineDjPlaylistSync;

internal static class EmbeddedAssets
{
    private const string IconResourceName = "EngineDjPlaylistSync.Resources.AppIcon.ico";
    private const string LogoResourceName = "EngineDjPlaylistSync.Resources.AppLogo.png";

    public static Icon? LoadAppIcon()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(IconResourceName);
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    public static Image? LoadAppLogo()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName);
            return stream is null ? null : Image.FromStream(stream);
        }
        catch
        {
            return null;
        }
    }
}
