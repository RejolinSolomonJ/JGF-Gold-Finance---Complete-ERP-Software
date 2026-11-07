using System.Text.RegularExpressions;

namespace JGF.Domain.Common;

/// <summary>
/// Represents a semantic version (SemVer 2.0: Major.Minor.Patch[-PreRelease]).
/// Provides robust comparison operators and parsing methods.
/// </summary>
public record SemanticVersion : IComparable<SemanticVersion>
{
    private static readonly Regex SemVerRegex = new(
        @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<prerelease>[0-9A-Za-z.-]+))?$",
        RegexOptions.Compiled);

    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
    public string? PreRelease { get; init; }
    public string Raw { get; init; }

    public SemanticVersion(int major, int minor, int patch, string? preRelease = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        Raw = string.IsNullOrEmpty(preRelease) ? $"{major}.{minor}.{patch}" : $"{major}.{minor}.{patch}-{preRelease}";
    }

    public static SemanticVersion Parse(string versionString)
    {
        if (TryParse(versionString, out var version))
        {
            return version!;
        }

        throw new FormatException($"Invalid semantic version string: '{versionString}'. Expected format: Major.Minor.Patch (e.g., 1.2.0)");
    }

    public static bool TryParse(string? versionString, out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return false;
        }

        var match = SemVerRegex.Match(versionString.Trim());
        if (!match.Success)
        {
            // Fallback for simple major.minor
            var parts = versionString.Trim().Split('.');
            if (parts.Length == 2 && int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min))
            {
                version = new SemanticVersion(maj, min, 0);
                return true;
            }

            return false;
        }

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        var patch = int.Parse(match.Groups["patch"].Value);
        var prerelease = match.Groups["prerelease"].Success ? match.Groups["prerelease"].Value : null;

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;

        var majorComp = Major.CompareTo(other.Major);
        if (majorComp != 0) return majorComp;

        var minorComp = Minor.CompareTo(other.Minor);
        if (minorComp != 0) return minorComp;

        var patchComp = Patch.CompareTo(other.Patch);
        if (patchComp != 0) return patchComp;

        // SemVer rule: a pre-release version has lower precedence than a normal version
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease)) return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease)) return -1;
        if (string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease)) return 0;

        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => Raw;
}

