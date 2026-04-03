using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Core.Tests;

public class PackageVersionTests
{
    [Theory]
    [InlineData("17.0.16+8", 17, 0, 16, null, "8")]
    [InlineData("2026.1.0", 2026, 1, 0, null, null)]
    [InlineData("1.0.0-beta1", 1, 0, 0, "beta1", null)]
    [InlineData("0.1.0", 0, 1, 0, null, null)]
    [InlineData("3.2.1-rc.1+build.42", 3, 2, 1, "rc.1", "build.42")]
    public void Parse_ValidVersionStrings_ReturnsCorrectComponents(
        string input, int major, int minor, int patch, string? preRelease, string? buildMetadata)
    {
        var version = PackageVersion.Parse(input);

        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(preRelease, version.PreRelease);
        Assert.Equal(buildMetadata, version.BuildMetadata);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    public void Parse_InvalidVersionStrings_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => PackageVersion.Parse(input));
    }

    [Fact]
    public void TryParse_NullInput_ReturnsFalse()
    {
        Assert.False(PackageVersion.TryParse(null, out _));
    }

    [Fact]
    public void Equality_SameVersions_AreEqual()
    {
        var a = PackageVersion.Parse("2026.1.0");
        var b = PackageVersion.Parse("2026.1.0");

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equality_DifferentVersions_AreNotEqual()
    {
        var a = PackageVersion.Parse("2026.1.0");
        var b = PackageVersion.Parse("2026.1.1");

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void GreaterThan_HigherMajor_ReturnsTrue()
    {
        var a = PackageVersion.Parse("2.0.0");
        var b = PackageVersion.Parse("1.0.0");

        Assert.True(a > b);
        Assert.False(b > a);
    }

    [Fact]
    public void LessThan_LowerMinor_ReturnsTrue()
    {
        var a = PackageVersion.Parse("1.0.0");
        var b = PackageVersion.Parse("1.1.0");

        Assert.True(a < b);
        Assert.False(b < a);
    }

    [Fact]
    public void LessThan_EqualVersions_ReturnsFalse()
    {
        // This is the WPILib bug test: < on equal versions must return false
        var a = PackageVersion.Parse("1.0.0");
        var b = PackageVersion.Parse("1.0.0");

        Assert.False(a < b);
        Assert.False(b < a);
    }

    [Fact]
    public void GreaterThanOrEqual_EqualVersions_ReturnsTrue()
    {
        var a = PackageVersion.Parse("1.0.0");
        var b = PackageVersion.Parse("1.0.0");

        Assert.True(a >= b);
        Assert.True(b >= a);
    }

    [Fact]
    public void LessThanOrEqual_EqualVersions_ReturnsTrue()
    {
        var a = PackageVersion.Parse("1.0.0");
        var b = PackageVersion.Parse("1.0.0");

        Assert.True(a <= b);
        Assert.True(b <= a);
    }

    [Fact]
    public void Comparison_PreReleaseIsLessThanRelease()
    {
        var preRelease = PackageVersion.Parse("1.0.0-beta1");
        var release = PackageVersion.Parse("1.0.0");

        Assert.True(preRelease < release);
        Assert.True(release > preRelease);
    }

    [Fact]
    public void Comparison_PreReleaseOrdering()
    {
        var alpha = PackageVersion.Parse("1.0.0-alpha");
        var beta = PackageVersion.Parse("1.0.0-beta");

        Assert.True(alpha < beta);
    }

    [Theory]
    [InlineData("1.5.0", ">=1.0.0", true)]
    [InlineData("0.9.0", ">=1.0.0", false)]
    [InlineData("1.0.0", ">=1.0.0", true)]
    [InlineData("1.9.9", "<2.0.0", true)]
    [InlineData("2.0.0", "<2.0.0", false)]
    [InlineData("2.0.1", "<2.0.0", false)]
    [InlineData("1.5.0", ">=1.0.0, <2.0.0", true)]
    [InlineData("0.5.0", ">=1.0.0, <2.0.0", false)]
    [InlineData("2.5.0", ">=1.0.0, <2.0.0", false)]
    public void SatisfiesRange_ReturnsExpectedResult(string version, string range, bool expected)
    {
        var v = PackageVersion.Parse(version);
        Assert.Equal(expected, v.SatisfiesRange(range));
    }

    [Fact]
    public void ToString_RoundTrips()
    {
        var original = "1.0.0-beta1+build.42";
        var version = PackageVersion.Parse(original);
        Assert.Equal(original, version.ToString());
    }

    [Fact]
    public void BuildMetadata_IgnoredInComparison()
    {
        var a = PackageVersion.Parse("1.0.0+build1");
        var b = PackageVersion.Parse("1.0.0+build2");

        // Build metadata is stored but versions with same major.minor.patch
        // and no pre-release should compare as equal
        Assert.Equal(0, a.CompareTo(b));
    }
}
