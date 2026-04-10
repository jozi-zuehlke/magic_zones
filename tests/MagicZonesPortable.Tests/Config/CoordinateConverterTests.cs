using MagicZonesPortable.Core.Abstractions;
using MagicZonesPortable.Core.Config;
using MagicZonesPortable.Core.Engine;
using Xunit;

namespace MagicZonesPortable.Tests.Config;

/// <summary>
/// Tests for <see cref="CoordinateConverter"/>: percent-to-pixel conversion,
/// pixel passthrough, zone clipping, and edge cases.
/// </summary>
public class CoordinateConverterTests
{
    private static readonly Rectangle StandardArea = new(0, 0, 1920, 1080);
    private static readonly Rectangle TaskbarArea = new(0, 40, 1920, 1040);

    private readonly CoordinateConverter _converter = new();

    private static MonitorConfig MakeMonitor(string unit, params ZoneDefinition[] zones)
    {
        return new MonitorConfig
        {
            Id = "test-monitor",
            CoordinateUnit = unit,
            Zones = zones.ToList(),
        };
    }

    private static ZoneDefinition Zone(string id, double x, double y, double w, double h, int priority = 0)
    {
        return new ZoneDefinition { Id = id, Name = id, X = x, Y = y, Width = w, Height = h, Priority = priority };
    }

    [Fact]
    public void Resolve_PercentZones_ConvertsToPixels()
    {
        var monitor = MakeMonitor("percent", Zone("z1", 0.0, 0.0, 0.5, 1.0));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Single(result.Zones);
        Assert.Equal(new Rectangle(0, 0, 960, 1080), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_PercentZones_AddsWorkingAreaOffset()
    {
        var monitor = MakeMonitor("percent", Zone("z1", 0.0, 0.0, 0.5, 1.0));
        var result = _converter.Resolve(monitor, TaskbarArea);

        Assert.Equal(new Rectangle(0, 40, 960, 1040), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_PercentFullScreen_CoversEntireWorkingArea()
    {
        var monitor = MakeMonitor("percent", Zone("z1", 0.0, 0.0, 1.0, 1.0));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(0, 0, 1920, 1080), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_PercentFractional_CorrectRounding()
    {
        // 0.33 * 1920 = 633.6 → truncate to 633
        // 0.25 * 1080 = 270.0 → 270
        // 0.34 * 1920 = 652.8 → truncate to 652
        // 0.5  * 1080 = 540.0 → 540
        var monitor = MakeMonitor("percent", Zone("z1", 0.33, 0.25, 0.34, 0.5));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(633, 270, 652, 540), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_PixelZones_AddsWorkingAreaOffset()
    {
        var monitor = MakeMonitor("pixels", Zone("z1", 100, 200, 400, 300));
        var result = _converter.Resolve(monitor, TaskbarArea);

        Assert.Equal(new Rectangle(100, 240, 400, 300), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_PixelZones_NoOffset_UsedAsIs()
    {
        var monitor = MakeMonitor("pixels", Zone("z1", 100, 200, 400, 300));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(100, 200, 400, 300), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_ZoneExtendsRight_ClippedToMonitorBounds()
    {
        // Zone at x=0.8, width=0.5 → pixel x=1536, w=960 → extends to 2496, past 1920
        // Clipped width = 1920 - 1536 = 384
        var monitor = MakeMonitor("percent", Zone("z1", 0.8, 0.0, 0.5, 1.0));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(1536, 0, 384, 1080), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_ZoneExtendsBottom_ClippedToMonitorBounds()
    {
        // Zone at y=0.8, height=0.5 → pixel y=864, h=540 → extends to 1404, past 1080
        // Clipped height = 1080 - 864 = 216
        var monitor = MakeMonitor("percent", Zone("z1", 0.0, 0.8, 1.0, 0.5));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(0, 864, 1920, 216), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_ZoneExtendsLeft_ClippedToMonitorBounds()
    {
        // Pixel zone at x=-100, width=400 → after offset (0,0): x=-100
        // Clamp x to 0, reduce width by 100 → width=300
        var monitor = MakeMonitor("pixels", Zone("z1", -100, 0, 400, 300));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(0, 0, 300, 300), result.Zones[0].Bounds);
    }

    [Fact]
    public void Resolve_ZoneFullyOutside_ClippedToZeroSize()
    {
        // Pixel zone entirely to the right of the monitor
        var monitor = MakeMonitor("pixels", Zone("z1", 2000, 0, 400, 300));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(0, result.Zones[0].Bounds.Width);
        Assert.Equal(0, result.Zones[0].Bounds.Height);
    }

    [Fact]
    public void Resolve_ZoneWithinBounds_NoClipping()
    {
        var monitor = MakeMonitor("percent", Zone("z1", 0.0, 0.0, 0.5, 0.5));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(new Rectangle(0, 0, 960, 540), result.Zones[0].Bounds);
        Assert.Empty(result.ClippedZoneIds);
    }

    [Fact]
    public void Resolve_ClippedZones_ReportedInResult()
    {
        // Zone extends right → should appear in ClippedZoneIds
        var monitor = MakeMonitor("percent", Zone("clip1", 0.8, 0.0, 0.5, 1.0));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Contains("clip1", result.ClippedZoneIds);
    }

    [Fact]
    public void Resolve_MultipleZones_AllConverted()
    {
        var monitor = MakeMonitor("percent",
            Zone("z1", 0.0, 0.0, 0.33, 1.0),
            Zone("z2", 0.33, 0.0, 0.34, 1.0),
            Zone("z3", 0.67, 0.0, 0.33, 1.0));
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Equal(3, result.Zones.Count);
    }

    [Fact]
    public void Resolve_EmptyZoneList_ReturnsEmpty()
    {
        var monitor = MakeMonitor("percent");
        var result = _converter.Resolve(monitor, StandardArea);

        Assert.Empty(result.Zones);
        Assert.Empty(result.ClippedZoneIds);
    }
}
