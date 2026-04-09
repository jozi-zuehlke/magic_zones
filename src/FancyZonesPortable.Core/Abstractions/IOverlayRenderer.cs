namespace FancyZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for rendering zone overlay UI (transparent windows showing zone highlights).
/// </summary>
public interface IOverlayRenderer
{
    /// <summary>
    /// Shows the overlay with the specified zone rectangles, sized to cover the given working area.
    /// </summary>
    void Show(IReadOnlyList<ZoneRenderInfo> zones, Rectangle workingArea);

    /// <summary>
    /// Updates the overlay to highlight the specified active zone (by ID), or null for no highlight.
    /// </summary>
    void SetActiveZone(string? zoneId);

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    void Hide();
}

/// <summary>
/// Information needed to render a single zone in the overlay.
/// </summary>
public class ZoneRenderInfo
{
    /// <summary>
    /// The zone identifier.
    /// </summary>
    public required string ZoneId { get; init; }

    /// <summary>
    /// The zone display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The zone rectangle in pixel coordinates.
    /// </summary>
    public required Rectangle Bounds { get; init; }
}
