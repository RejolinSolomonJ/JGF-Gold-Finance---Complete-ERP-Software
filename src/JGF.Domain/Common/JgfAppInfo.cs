using System.Reflection;

namespace JGF.Domain.Common;

/// <summary>
/// Provides global application metadata and semantic versioning information.
/// </summary>
public static class JgfAppInfo
{
    public const string AppName = "JGF Jewel Gold Finance";
    public const string TamilTitle = "தமிழ்நாடு நகைக்கடன் நிதி மென்பொருள்";
    public const string CurrentVersionString = "1.2.0";

    public static SemanticVersion CurrentVersion => SemanticVersion.Parse(CurrentVersionString);

    /// <summary>
    /// Gets the assembly informational or file version if available, falling back to CurrentVersion.
    /// </summary>
    public static string GetInstalledVersionString()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion))
        {
            var raw = infoAttr.InformationalVersion.Split('+')[0]; // strip git hash if present
            if (SemanticVersion.TryParse(raw, out _))
            {
                return raw;
            }
        }

        var ver = asm.GetName().Version;
        if (ver != null && (ver.Major > 0 || ver.Minor > 0))
        {
            return $"{ver.Major}.{ver.Minor}.{ver.Build}";
        }

        return CurrentVersionString;
    }
}
