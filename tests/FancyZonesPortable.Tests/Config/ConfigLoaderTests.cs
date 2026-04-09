using System.Text.Json;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Logging;
using NSubstitute;
using Xunit;

namespace FancyZonesPortable.Tests.Config;

/// <summary>
/// Tests for <see cref="ConfigLoader"/>: file discovery, JSON parsing,
/// default generation, and error handling.
/// </summary>
public class ConfigLoaderTests
{
    private readonly IFileSystem _fs = Substitute.For<IFileSystem>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ConfigLoader _loader;

    private const string ExeDir = "/app";
    private const string AppDataDir = "/appdata";
    private const string ExeConfigPath = "/app/zones.json";
    private const string AppDataConfigPath = "/appdata/FancyZonesPortable/zones.json";

    public ConfigLoaderTests()
    {
        _fs.GetExecutableDirectory().Returns(ExeDir);
        _fs.GetAppDataPath().Returns(AppDataDir);
        _fs.DirectoryExists(Arg.Any<string>()).Returns(true);
        _loader = new ConfigLoader(_fs, _logger);
    }

    private static string MakeValidJson() => JsonSerializer.Serialize(new ZonesConfig
    {
        Version = 1,
        Settings = new SettingsConfig(),
        Monitors =
        [
            new MonitorConfig
            {
                Id = "mon1",
                MatchBy = "primary",
                CoordinateUnit = "percent",
                Zones =
                [
                    new ZoneDefinition { Id = "z1", Name = "Left", Priority = 1, X = 0, Y = 0, Width = 0.5, Height = 1.0 },
                    new ZoneDefinition { Id = "z2", Name = "Right", Priority = 1, X = 0.5, Y = 0, Width = 0.5, Height = 1.0 },
                ]
            }
        ]
    }, new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true });

    #region DiscoverConfigPath

    [Fact]
    public void DiscoverConfigPath_ExeDirHasConfig_ReturnsExePath()
    {
        _fs.FileExists(ExeConfigPath).Returns(true);

        var result = _loader.DiscoverConfigPath();

        Assert.Equal(ExeConfigPath, result);
    }

    [Fact]
    public void DiscoverConfigPath_AppDataHasConfig_ReturnsAppDataPath()
    {
        _fs.FileExists(ExeConfigPath).Returns(false);
        _fs.FileExists(AppDataConfigPath).Returns(true);

        var result = _loader.DiscoverConfigPath();

        Assert.Equal(AppDataConfigPath, result);
    }

    [Fact]
    public void DiscoverConfigPath_NoConfigAnywhere_ReturnsNull()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        var result = _loader.DiscoverConfigPath();

        Assert.Null(result);
    }

    [Fact]
    public void DiscoverConfigPath_BothExist_PrefersExeDir()
    {
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.FileExists(AppDataConfigPath).Returns(true);

        var result = _loader.DiscoverConfigPath();

        Assert.Equal(ExeConfigPath, result);
    }

    #endregion

    #region Load

    [Fact]
    public void Load_ExeDirConfigExists_ParsesCorrectly()
    {
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.ReadAllText(ExeConfigPath).Returns(MakeValidJson());

        var result = _loader.Load();

        Assert.Equal(ExeConfigPath, result.FilePath);
        Assert.False(result.IsNewDefault);
        Assert.Single(result.Config.Monitors);
        Assert.Equal("mon1", result.Config.Monitors[0].Id);
    }

    [Fact]
    public void Load_ValidJson_ReturnsPopulatedConfig()
    {
        var json = MakeValidJson();
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.ReadAllText(ExeConfigPath).Returns(json);

        var result = _loader.Load();

        Assert.Equal(1, result.Config.Version);
        Assert.Equal("Shift", result.Config.Settings.ActivationModifier);
        Assert.Equal("#0078D4", result.Config.Settings.HighlightActiveZoneColor);
        Assert.Equal(2, result.Config.Monitors[0].Zones.Count);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaultWithError()
    {
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.ReadAllText(ExeConfigPath).Returns("{ not valid json!!!");

        var result = _loader.Load();

        Assert.NotNull(result.Config);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors, e => e.Contains("parse", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ExeConfigPath, result.FilePath);
        Assert.False(result.IsNewDefault);
    }

    [Fact]
    public void Load_NullDeserialization_ReturnsDefault()
    {
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.ReadAllText(ExeConfigPath).Returns("null");

        var result = _loader.Load();

        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.Version);
        Assert.NotEmpty(result.Config.Monitors);
    }

    [Fact]
    public void Load_NoConfigFile_GeneratesDefaultAndWritesToDisk()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        var result = _loader.Load();

        Assert.True(result.IsNewDefault);
        Assert.Equal(ExeConfigPath, result.FilePath);
        _fs.Received(1).WriteAllText(ExeConfigPath, Arg.Any<string>());
    }

    [Fact]
    public void Load_NoConfigFile_DefaultHasExpectedZones()
    {
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        var result = _loader.Load();

        Assert.True(result.IsNewDefault);
        Assert.NotEmpty(result.Config.Monitors);
        Assert.True(result.Config.Monitors[0].Zones.Count >= 2,
            "Default config should have at least 2 zones.");
    }

    [Fact]
    public void Load_ValidConfig_RunsValidation()
    {
        // Config with an invalid activationModifier to trigger a validation error
        var badConfig = new ZonesConfig
        {
            Version = 1,
            Settings = new SettingsConfig { ActivationModifier = "InvalidMod" },
            Monitors =
            [
                new MonitorConfig
                {
                    Id = "m1", MatchBy = "primary", CoordinateUnit = "percent",
                    Zones = [new ZoneDefinition { Id = "z1", Name = "Full", Priority = 1, X = 0, Y = 0, Width = 1, Height = 1 }]
                }
            ]
        };
        var json = JsonSerializer.Serialize(badConfig, new JsonSerializerOptions { WriteIndented = true });
        _fs.FileExists(ExeConfigPath).Returns(true);
        _fs.ReadAllText(ExeConfigPath).Returns(json);

        var result = _loader.Load();

        Assert.NotEmpty(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors, e => e.Contains("activationModifier"));
        // Config is still returned (caller decides)
        Assert.Equal("InvalidMod", result.Config.Settings.ActivationModifier);
    }

    #endregion

    #region Save

    [Fact]
    public void Save_WritesValidJson()
    {
        var config = ConfigLoader.CreateDefault();
        var path = "/app/zones.json";

        _loader.Save(config, path);

        _fs.Received(1).WriteAllText(path, Arg.Is<string>(json =>
            json.Contains("\"version\"") && json.Contains("\"monitors\"")));
    }

    #endregion

    #region CreateDefault

    [Fact]
    public void CreateDefault_HasValidStructure()
    {
        var config = ConfigLoader.CreateDefault();
        var validator = new ConfigValidator();

        var errors = validator.Validate(config);

        Assert.Empty(errors);
    }

    #endregion
}
