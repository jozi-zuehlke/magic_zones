using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Engine;
using Xunit;

namespace FancyZonesPortable.Tests.Engine;

/// <summary>
/// Tests for <see cref="ZoneHitTester"/>: point-in-rectangle testing,
/// priority resolution, and overlap handling.
/// </summary>
public class ZoneHitTesterTests
{
    private readonly ZoneHitTester _sut = new();

    private static ResolvedZone MakeZone(string id, int x, int y, int w, int h, int priority = 10) =>
        new()
        {
            Definition = new ZoneDefinition { Id = id, Name = id, Priority = priority, X = x, Y = y, Width = w, Height = h },
            Bounds = new Rectangle(x, y, w, h),
        };

    // --- Basic hit / miss ---

    [Fact]
    public void HitTest_PointInsideSingleZone_ReturnsZone()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 0, 0, 200, 200) };
        var result = _sut.HitTest(zones, 100, 100);
        Assert.NotNull(result);
        Assert.Equal("a", result.Definition.Id);
    }

    [Fact]
    public void HitTest_PointOutsideAllZones_ReturnsNull()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 0, 0, 200, 200) };
        Assert.Null(_sut.HitTest(zones, 500, 500));
    }

    // --- Edge inclusivity ---

    [Fact]
    public void HitTest_PointOnLeftEdge_ReturnsZone()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 50, 50, 100, 100) };
        var result = _sut.HitTest(zones, 50, 75);
        Assert.NotNull(result);
        Assert.Equal("a", result.Definition.Id);
    }

    [Fact]
    public void HitTest_PointOnTopEdge_ReturnsZone()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 50, 50, 100, 100) };
        var result = _sut.HitTest(zones, 75, 50);
        Assert.NotNull(result);
        Assert.Equal("a", result.Definition.Id);
    }

    [Fact]
    public void HitTest_PointOnRightEdge_ReturnsNull()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 50, 50, 100, 100) };
        Assert.Null(_sut.HitTest(zones, 150, 75));
    }

    [Fact]
    public void HitTest_PointOnBottomEdge_ReturnsNull()
    {
        var zones = new List<ResolvedZone> { MakeZone("a", 50, 50, 100, 100) };
        Assert.Null(_sut.HitTest(zones, 75, 150));
    }

    // --- Priority resolution ---

    [Fact]
    public void HitTest_OverlappingZones_LowerPriorityValueWins()
    {
        var zones = new List<ResolvedZone>
        {
            MakeZone("low", 0, 0, 200, 200, priority: 10),
            MakeZone("high", 0, 0, 200, 200, priority: 1),
        };
        var result = _sut.HitTest(zones, 100, 100);
        Assert.NotNull(result);
        Assert.Equal("high", result.Definition.Id);
    }

    [Fact]
    public void HitTest_OverlappingZones_EqualPriority_FirstInArrayWins()
    {
        var zones = new List<ResolvedZone>
        {
            MakeZone("first", 0, 0, 200, 200, priority: 5),
            MakeZone("second", 0, 0, 200, 200, priority: 5),
        };
        var result = _sut.HitTest(zones, 100, 100);
        Assert.NotNull(result);
        Assert.Equal("first", result.Definition.Id);
    }

    [Fact]
    public void HitTest_ThreeOverlappingZones_LowestPriorityValueWins()
    {
        var zones = new List<ResolvedZone>
        {
            MakeZone("p10", 0, 0, 200, 200, priority: 10),
            MakeZone("p1", 0, 0, 200, 200, priority: 1),
            MakeZone("p5", 0, 0, 200, 200, priority: 5),
        };
        var result = _sut.HitTest(zones, 100, 100);
        Assert.NotNull(result);
        Assert.Equal("p1", result.Definition.Id);
    }

    // --- Empty / non-overlapping ---

    [Fact]
    public void HitTest_NoZones_ReturnsNull()
    {
        Assert.Null(_sut.HitTest(new List<ResolvedZone>(), 100, 100));
    }

    [Fact]
    public void HitTest_PointInNonOverlappingZone_ReturnsCorrectZone()
    {
        var zones = new List<ResolvedZone>
        {
            MakeZone("left", 0, 0, 100, 100),
            MakeZone("right", 200, 0, 100, 100),
        };
        var result = _sut.HitTest(zones, 250, 50);
        Assert.NotNull(result);
        Assert.Equal("right", result.Definition.Id);
    }

    // --- PRD §7.5 example zones (1920×1080 working area) ---

    private static List<ResolvedZone> PrdExampleZones() =>
    [
        MakeZone("left-half", 0, 0, 960, 1080, priority: 10),
        MakeZone("right-half", 960, 0, 960, 1080, priority: 10),
        MakeZone("top-right-quarter", 960, 0, 960, 540, priority: 5),
        MakeZone("sidebar", 1440, 0, 480, 1080, priority: 1),
    ];

    [Fact]
    public void HitTest_PRDExample_Sidebar_WinsOverRightHalf()
    {
        var result = _sut.HitTest(PrdExampleZones(), 1800, 500);
        Assert.NotNull(result);
        Assert.Equal("sidebar", result.Definition.Id);
    }

    [Fact]
    public void HitTest_PRDExample_TopRightQuarter_WinsOverRightHalf()
    {
        var result = _sut.HitTest(PrdExampleZones(), 1200, 200);
        Assert.NotNull(result);
        Assert.Equal("top-right-quarter", result.Definition.Id);
    }

    [Fact]
    public void HitTest_PRDExample_LeftHalf_NoOverlap()
    {
        var result = _sut.HitTest(PrdExampleZones(), 400, 540);
        Assert.NotNull(result);
        Assert.Equal("left-half", result.Definition.Id);
    }
}
