using MagicZonesPortable.Core.Abstractions;
using MagicZonesPortable.Core.Config;

namespace MagicZonesPortable.Core.Engine;

/// <summary>
/// Determines which zone (if any) contains a given point, resolving overlaps by priority.
/// </summary>
public class ZoneHitTester
{
    /// <summary>
    /// Tests which zone contains the given point. If multiple zones overlap at the point,
    /// returns the one with the lowest priority value (highest precedence per PRD §7.5).
    /// For equal priorities, the first zone in array order wins.
    /// Returns null if no zone contains the point.
    /// </summary>
    /// <param name="zones">The available zone rectangles in pixel coordinates.</param>
    /// <param name="x">Cursor X position in pixels.</param>
    /// <param name="y">Cursor Y position in pixels.</param>
    /// <returns>The matching resolved zone, or null if no zone matches.</returns>
    public ResolvedZone? HitTest(IReadOnlyList<ResolvedZone> zones, int x, int y)
    {
        ResolvedZone? best = null;
        var bestPriority = int.MaxValue;

        foreach (var zone in zones)
        {
            if (ContainsPoint(zone.Bounds, x, y) && zone.Definition.Priority < bestPriority)
            {
                best = zone;
                bestPriority = zone.Definition.Priority;
            }
        }

        return best;
    }

    private static bool ContainsPoint(Rectangle rect, int x, int y)
    {
        return x >= rect.X && x < rect.X + rect.Width
            && y >= rect.Y && y < rect.Y + rect.Height;
    }
}

/// <summary>
/// A zone definition resolved to pixel coordinates.
/// </summary>
public class ResolvedZone
{
    /// <summary>
    /// The original zone definition.
    /// </summary>
    public required ZoneDefinition Definition { get; init; }

    /// <summary>
    /// The zone bounds in pixel coordinates.
    /// </summary>
    public required Rectangle Bounds { get; init; }
}
