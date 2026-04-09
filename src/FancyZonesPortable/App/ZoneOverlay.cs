using FancyZonesPortable.Core.Abstractions;

namespace FancyZonesPortable.App;

/// <summary>
/// Transparent, always-on-top Form used for rendering zone highlights during drag.
/// Uses WS_EX_LAYERED and WS_EX_TRANSPARENT to allow click-through.
/// </summary>
internal class ZoneOverlay : Form
{
    private const int BorderWidth = 2;

    private Color _activeZoneColor = Color.FromArgb(128, 0, 120, 215);
    private Color _inactiveZoneColor = Color.FromArgb(64, 0, 120, 215);
    private Color _borderColor = Color.FromArgb(200, 0, 120, 215);

    private IReadOnlyList<ZoneRenderInfo> _zones = Array.Empty<ZoneRenderInfo>();
    private string? _activeZoneId;

    public ZoneOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_LAYERED = 0x80000;
            const int WS_EX_TRANSPARENT = 0x20;
            const int WS_EX_TOOLWINDOW = 0x80;
            const int WS_EX_NOACTIVATE = 0x08000000;

            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    /// <summary>
    /// Sets the overlay bounds to cover the specified screen area.
    /// </summary>
    public void SetOverlayBounds(int x, int y, int width, int height)
    {
        Location = new Point(x, y);
        Size = new Size(width, height);
    }

    /// <summary>
    /// Configures the overlay colors and opacities from settings.
    /// </summary>
    public void SetColors(Color activeColor, double activeOpacity, Color inactiveColor, double inactiveOpacity)
    {
        int activeAlpha = (int)Math.Clamp(activeColor.A * activeOpacity, 0, 255);
        int inactiveAlpha = (int)Math.Clamp(inactiveColor.A * inactiveOpacity, 0, 255);

        _activeZoneColor = Color.FromArgb(activeAlpha, activeColor.R, activeColor.G, activeColor.B);
        _inactiveZoneColor = Color.FromArgb(inactiveAlpha, inactiveColor.R, inactiveColor.G, inactiveColor.B);
        _borderColor = Color.FromArgb(
            Math.Min(activeAlpha + 72, 255),
            activeColor.R, activeColor.G, activeColor.B);
    }

    public void SetZones(IReadOnlyList<ZoneRenderInfo> zones)
    {
        _zones = zones;
        Invalidate();
    }

    public void SetActiveZone(string? zoneId)
    {
        _activeZoneId = zoneId;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        using var borderPen = new Pen(_borderColor, BorderWidth);

        foreach (var zone in _zones)
        {
            bool isActive = zone.ZoneId == _activeZoneId;
            var fillColor = isActive ? _activeZoneColor : _inactiveZoneColor;
            var rect = new System.Drawing.Rectangle(
                zone.Bounds.X, zone.Bounds.Y,
                zone.Bounds.Width, zone.Bounds.Height);

            using var brush = new SolidBrush(fillColor);
            g.FillRectangle(brush, rect);
            g.DrawRectangle(borderPen, rect);
        }
    }
}
