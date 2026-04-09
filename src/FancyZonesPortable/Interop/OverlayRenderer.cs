using FancyZonesPortable.App;
using FancyZonesPortable.Core.Abstractions;
using CoreRectangle = FancyZonesPortable.Core.Abstractions.Rectangle;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IOverlayRenderer"/> using the <see cref="ZoneOverlay"/> form.
/// </summary>
internal class OverlayRenderer : IOverlayRenderer
{
    private ZoneOverlay? _overlay;

    /// <summary>
    /// Gets the HWND of the overlay form, or 0 if it hasn't been created yet.
    /// </summary>
    public nint Handle => _overlay?.IsDisposed == false ? _overlay.Handle : 0;

    /// <summary>
    /// Configures overlay colors from settings. Must be called before Show.
    /// </summary>
    public void SetColors(Color activeColor, double activeOpacity, Color inactiveColor, double inactiveOpacity)
    {
        EnsureOverlay();
        _overlay!.SetColors(activeColor, activeOpacity, inactiveColor, inactiveOpacity);
    }

    public void Show(IReadOnlyList<ZoneRenderInfo> zones, CoreRectangle workingArea, string? activeZoneId)
    {
        EnsureOverlay();
        _overlay!.SetOverlayBounds(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height);
        _overlay.SetZones(zones);
        _overlay.SetActiveZone(activeZoneId);
        _overlay.ShowTopMost();
    }

    public void SetActiveZone(string? zoneId)
    {
        _overlay?.SetActiveZone(zoneId);
    }

    public void Hide()
    {
        _overlay?.Hide();
    }

    private void EnsureOverlay()
    {
        if (_overlay == null || _overlay.IsDisposed)
            _overlay = new ZoneOverlay();
    }
}
