using MagicZonesPortable.Core.Config;
using MagicZonesPortable.Tests.Helpers;
using Xunit;

namespace MagicZonesPortable.Tests.Config;

/// <summary>
/// Tests for <see cref="ConfigValidator"/>: required fields, value ranges,
/// duplicate IDs, and structural validation.
/// </summary>
public class ConfigValidatorTests
{
    private readonly ConfigValidator _validator = new();

    [Fact]
    public void Validate_ValidConfig_ReturnsNoErrors()
    {
        var config = TestHelpers.CreateValidConfig();
        var errors = _validator.Validate(config);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_VersionZero_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Version = 0;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("version"));
    }

    [Fact]
    public void Validate_InvalidActivationModifier_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.ActivationModifier = "Tab";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("activationModifier"));
    }

    [Theory]
    [InlineData("shift")]
    [InlineData("SHIFT")]
    [InlineData("Shift")]
    [InlineData("ctrl")]
    [InlineData("ALT")]
    public void Validate_ActivationModifier_CaseInsensitive(string modifier)
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.ActivationModifier = modifier;
        var errors = _validator.Validate(config);
        Assert.DoesNotContain(errors, e => e.Contains("activationModifier"));
    }

    [Fact]
    public void Validate_OpacityBelowZero_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.HighlightActiveZoneOpacity = -0.1;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("highlightActiveZoneOpacity"));
    }

    [Fact]
    public void Validate_OpacityAboveOne_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.HighlightActiveZoneOpacity = 1.5;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("highlightActiveZoneOpacity"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Validate_OpacityAtBoundaries_Valid(double opacity)
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.HighlightActiveZoneOpacity = opacity;
        config.Settings.HighlightInactiveZoneOpacity = opacity;
        var errors = _validator.Validate(config);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_NegativeDebounceMs_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.AutoReloadDebounceMs = -1;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("autoReloadDebounceMs"));
    }

    [Fact]
    public void Validate_NoMonitors_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors.Clear();
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("monitor"));
    }

    [Fact]
    public void Validate_EmptyMonitorId_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Id = "";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("Monitor id") || e.Contains("monitor id"));
    }

    [Fact]
    public void Validate_DuplicateMonitorId_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        var dup = TestHelpers.CreatePercentMonitor("primary",
            TestHelpers.CreateZone("z1", "Zone", 0, 0, 0.5, 1.0));
        config.Monitors.Add(dup);
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("Duplicate monitor"));
    }

    [Fact]
    public void Validate_MatchByPrimary_IsValid()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].MatchBy = "primary";
        var errors = _validator.Validate(config);
        Assert.DoesNotContain(errors, e => e.Contains("matchBy"));
    }

    [Fact]
    public void Validate_MatchByDeviceName_MissingDeviceName_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].MatchBy = "deviceName";
        config.Monitors[0].DeviceName = null;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("deviceName"));
    }

    [Fact]
    public void Validate_MatchByIndex_NegativeIndex_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].MatchBy = "index";
        config.Monitors[0].Index = -1;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("index") && e.Contains(">= 0"));
    }

    [Fact]
    public void Validate_InvalidMatchBy_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].MatchBy = "serial";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("matchBy"));
    }

    [Fact]
    public void Validate_InvalidCoordinateUnit_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].CoordinateUnit = "em";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("coordinateUnit"));
    }

    [Fact]
    public void Validate_CoordinateUnitPixels_IsValid()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].CoordinateUnit = "pixels";
        config.Monitors[0].Zones = [
            TestHelpers.CreateZone("z1", "Zone", 0, 0, 100, 200),
        ];
        var errors = _validator.Validate(config);
        Assert.DoesNotContain(errors, e => e.Contains("coordinateUnit"));
    }

    [Fact]
    public void Validate_NoZonesInMonitor_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones.Clear();
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("zone") && e.Contains("required"));
    }

    [Fact]
    public void Validate_EmptyZoneId_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[0].Id = "";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("zone id"));
    }

    [Fact]
    public void Validate_DuplicateZoneId_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[1].Id = config.Monitors[0].Zones[0].Id;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("duplicate zone"));
    }

    [Fact]
    public void Validate_ZoneWidthZero_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[0].Width = 0;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("width"));
    }

    [Fact]
    public void Validate_ZoneHeightNegative_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[0].Height = -1;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("height"));
    }

    [Fact]
    public void Validate_ZonePriorityNegative_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[0].Priority = -1;
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("priority") || e.Contains("Priority"));
    }

    [Fact]
    public void Validate_ZoneNameEmpty_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Monitors[0].Zones[0].Name = "";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("name") || e.Contains("Name"));
    }

    [Fact]
    public void Validate_EmptyToggleHotkey_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.ToggleHotkey = "";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("toggleHotkey"));
    }

    [Fact]
    public void Validate_InvalidLogLevel_ReturnsError()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.LogLevel = "TRACE";
        var errors = _validator.Validate(config);
        Assert.Contains(errors, e => e.Contains("logLevel"));
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("INFO")]
    [InlineData("warn")]
    [InlineData("ERROR")]
    public void Validate_LogLevel_CaseInsensitive(string logLevel)
    {
        var config = TestHelpers.CreateValidConfig();
        config.Settings.LogLevel = logLevel;
        var errors = _validator.Validate(config);
        Assert.DoesNotContain(errors, e => e.Contains("logLevel"));
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var config = TestHelpers.CreateValidConfig();
        config.Version = 0;
        config.Settings.ActivationModifier = "Tab";
        config.Settings.HighlightActiveZoneOpacity = 2.0;
        var errors = _validator.Validate(config);
        Assert.True(errors.Count >= 3, $"Expected at least 3 errors but got {errors.Count}: {string.Join("; ", errors)}");
    }
}
