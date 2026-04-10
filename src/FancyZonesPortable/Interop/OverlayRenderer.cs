using FancyZonesPortable.App;
using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Logging;
using CoreRectangle = FancyZonesPortable.Core.Abstractions.Rectangle;

namespace FancyZonesPortable.Interop;

/// <summary>
/// Implements <see cref="IOverlayRenderer"/> using the <see cref="ZoneOverlay"/> form.
/// </summary>
internal class OverlayRenderer : IOverlayRenderer
{
    private readonly ILogger? _logger;
    private ZoneOverlay? _overlay;
    private string? _activeZoneId;
    private bool _isVisible;

    public OverlayRenderer(ILogger? logger = null)
    {
        _logger = logger;
    }

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
        _logger?.Debug("Overlay colors updated from settings.");
    }

    public void Show(IReadOnlyList<ZoneRenderInfo> zones, CoreRectangle workingArea, string? activeZoneId)
    {
        EnsureOverlay();
        _logger?.Debug(
            $"Starting overlay rendering for {zones.Count} zones in bounds " +
            $"({workingArea.X}, {workingArea.Y}, {workingArea.Width}, {workingArea.Height}) with active zone '{activeZoneId ?? "<none>"}'.");
        using (_overlay!.DeferRender())
        {
            _overlay.SetOverlayBounds(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height);
            _overlay.SetZones(zones);
            _overlay.SetActiveZone(activeZoneId);
            _overlay.ShowTopMost();
        }

        _activeZoneId = activeZoneId;
        _isVisible = true;
    }

    public void SetActiveZone(string? zoneId)
    {
        if (string.Equals(_activeZoneId, zoneId, StringComparison.Ordinal))
            return;

        _logger?.Debug($"Overlay active zone changed: '{_activeZoneId ?? "<none>"}' -> '{zoneId ?? "<none>"}'.");
        _activeZoneId = zoneId;

        _overlay?.SetActiveZone(zoneId);
    }

    public void Hide()
    {
        if (_isVisible)
        {
            _logger?.Debug("Stopping overlay rendering.");
        }

        _overlay?.Hide();
        _isVisible = false;
    }

    private void EnsureOverlay()
    {
        if (_overlay == null || _overlay.IsDisposed)
        {
            _overlay = new ZoneOverlay();
            _logger?.Debug("Created overlay window instance.");
        }
    }
}
