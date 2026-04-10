using MagicZonesPortable.Core.Abstractions;
using MagicZonesPortable.Core.Config;

namespace MagicZonesPortable.Core.Engine;

/// <summary>
/// Result of resolving zone definitions to pixel coordinates.
/// </summary>
/// <param name="Zones">Resolved zones with pixel coordinates.</param>
/// <param name="ClippedZoneIds">IDs of zones that were clipped to fit the working area.</param>
public record ResolveResult(List<ResolvedZone> Zones, List<string> ClippedZoneIds);

/// <summary>
/// Converts zone definitions from percent/pixel coordinates to pixel coordinates
/// based on the monitor's working area dimensions, clipping zones to monitor bounds.
/// </summary>
public class CoordinateConverter
{
    /// <summary>
    /// Converts a list of zone definitions to pixel-coordinate <see cref="ResolvedZone"/> objects,
    /// clipping any zones that extend outside the working area.
    /// </summary>
    /// <param name="monitor">The monitor configuration containing zone definitions.</param>
    /// <param name="workingArea">The monitor's working area in pixels.</param>
    /// <returns>A <see cref="ResolveResult"/> with resolved zones and clipped zone IDs.</returns>
    public ResolveResult Resolve(MonitorConfig monitor, Rectangle workingArea)
    {
        var resolved = new List<ResolvedZone>();
        var clippedIds = new List<string>();
        var isPercent = string.Equals(monitor.CoordinateUnit, "percent", StringComparison.OrdinalIgnoreCase);

        foreach (var zone in monitor.Zones)
        {
            var bounds = isPercent
                ? ConvertPercent(zone, workingArea)
                : ConvertPixel(zone, workingArea);

            var clipped = ClipToWorkingArea(bounds, workingArea);
            if (clipped != bounds)
            {
                clippedIds.Add(zone.Id);
            }

            resolved.Add(new ResolvedZone
            {
                Definition = zone,
                Bounds = clipped,
            });
        }

        return new ResolveResult(resolved, clippedIds);
    }

    private static Rectangle ConvertPercent(ZoneDefinition zone, Rectangle workingArea)
    {
        var x = workingArea.X + (int)(zone.X * workingArea.Width);
        var y = workingArea.Y + (int)(zone.Y * workingArea.Height);
        var width = (int)(zone.Width * workingArea.Width);
        var height = (int)(zone.Height * workingArea.Height);
        return new Rectangle(x, y, width, height);
    }

    private static Rectangle ConvertPixel(ZoneDefinition zone, Rectangle workingArea)
    {
        return new Rectangle(
            workingArea.X + (int)zone.X,
            workingArea.Y + (int)zone.Y,
            (int)zone.Width,
            (int)zone.Height);
    }

    private static Rectangle ClipToWorkingArea(Rectangle bounds, Rectangle workingArea)
    {
        var areaRight = workingArea.X + workingArea.Width;
        var areaBottom = workingArea.Y + workingArea.Height;

        var x = bounds.X;
        var y = bounds.Y;
        var width = bounds.Width;
        var height = bounds.Height;

        // Clip left edge
        if (x < workingArea.X)
        {
            width -= (workingArea.X - x);
            x = workingArea.X;
        }

        // Clip top edge
        if (y < workingArea.Y)
        {
            height -= (workingArea.Y - y);
            y = workingArea.Y;
        }

        // Clip right edge
        if (x + width > areaRight)
        {
            width = areaRight - x;
        }

        // Clip bottom edge
        if (y + height > areaBottom)
        {
            height = areaBottom - y;
        }

        // Clamp to zero if fully outside
        width = Math.Max(width, 0);
        height = Math.Max(height, 0);

        // If either dimension is zero, the zone is degenerate
        if (width == 0 || height == 0)
        {
            width = 0;
            height = 0;
        }

        return new Rectangle(x, y, width, height);
    }
}
