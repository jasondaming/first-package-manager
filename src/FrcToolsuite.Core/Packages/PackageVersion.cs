namespace FrcToolsuite.Core.Packages;

public sealed class PackageVersion : IComparable<PackageVersion>, IEquatable<PackageVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }

    public PackageVersion(int major, int minor, int patch, string? preRelease = null, string? buildMetadata = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    public static PackageVersion Parse(string version)
    {
        if (!TryParse(version, out var result))
        {
            throw new FormatException($"Invalid version string: '{version}'");
        }
        return result;
    }

    public static bool TryParse(string? version, out PackageVersion result)
    {
        result = null!;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var remaining = version.AsSpan();

        string? buildMetadata = null;
        var plusIndex = remaining.IndexOf('+');
        if (plusIndex >= 0)
        {
            buildMetadata = remaining[(plusIndex + 1)..].ToString();
            remaining = remaining[..plusIndex];
        }

        string? preRelease = null;
        var dashIndex = remaining.IndexOf('-');
        if (dashIndex >= 0)
        {
            preRelease = remaining[(dashIndex + 1)..].ToString();
            remaining = remaining[..dashIndex];
        }

        var parts = remaining.ToString().Split('.');
        if (parts.Length < 1 || parts.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major))
        {
            return false;
        }

        int minor = 0;
        if (parts.Length >= 2 && !int.TryParse(parts[1], out minor))
        {
            return false;
        }

        int patch = 0;
        if (parts.Length >= 3 && !int.TryParse(parts[2], out patch))
        {
            return false;
        }

        result = new PackageVersion(major, minor, patch, preRelease, buildMetadata);
        return true;
    }

    public int CompareTo(PackageVersion? other)
    {
        if (other is null) return 1;

        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;

        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;

        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        if (PreRelease is null && other.PreRelease is null) return 0;
        if (PreRelease is null) return 1;
        if (other.PreRelease is null) return -1;

        return string.Compare(PreRelease, other.PreRelease, StringComparison.Ordinal);
    }

    public bool SatisfiesRange(string rangeExpression)
    {
        var parts = rangeExpression.Split(',', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (!SatisfiesSingleConstraint(part))
            {
                return false;
            }
        }
        return true;
    }

    private bool SatisfiesSingleConstraint(string constraint)
    {
        if (constraint.StartsWith(">="))
        {
            var target = Parse(constraint[2..].Trim());
            return this.CompareTo(target) >= 0;
        }

        if (constraint.StartsWith(">"))
        {
            var target = Parse(constraint[1..].Trim());
            return this.CompareTo(target) > 0;
        }

        if (constraint.StartsWith("<="))
        {
            var target = Parse(constraint[2..].Trim());
            return this.CompareTo(target) <= 0;
        }

        if (constraint.StartsWith("<"))
        {
            var target = Parse(constraint[1..].Trim());
            return target.CompareTo(this) > 0;
        }

        if (constraint.StartsWith("="))
        {
            var target = Parse(constraint[1..].Trim());
            return this.CompareTo(target) == 0;
        }

        var exact = Parse(constraint.Trim());
        return this.CompareTo(exact) == 0;
    }

    public bool Equals(PackageVersion? other)
    {
        if (other is null) return false;
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj) => obj is PackageVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString()
    {
        var version = $"{Major}.{Minor}.{Patch}";
        if (PreRelease is not null) version += $"-{PreRelease}";
        if (BuildMetadata is not null) version += $"+{BuildMetadata}";
        return version;
    }

    public static bool operator ==(PackageVersion? left, PackageVersion? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(PackageVersion? left, PackageVersion? right) => !(left == right);

    public static bool operator <(PackageVersion left, PackageVersion right) => right.CompareTo(left) > 0;

    public static bool operator >(PackageVersion left, PackageVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(PackageVersion left, PackageVersion right) => right.CompareTo(left) >= 0;

    public static bool operator >=(PackageVersion left, PackageVersion right) => left.CompareTo(right) >= 0;
}
