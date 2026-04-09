using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Interop;

namespace FancyZonesPortable.App;

/// <summary>
/// Transparent, always-on-top Form used for rendering zone highlights during drag.
/// Uses WS_EX_LAYERED with UpdateLayeredWindow for true per-pixel alpha transparency,
/// and WS_EX_TRANSPARENT to allow click-through.
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
        StartPosition = FormStartPosition.Manual;
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
    /// Shows the overlay and re-asserts HWND_TOPMOST z-order.
    /// </summary>
    public void ShowTopMost()
    {
        TopMost = true;
        Show();
        BringToFront();

        const nint HWND_TOPMOST = -1;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;

        NativeMethods.SetWindowPos(
            Handle,
            HWND_TOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        UpdateOverlayBitmap();
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
        UpdateOverlayBitmap();
    }

    public void SetActiveZone(string? zoneId)
    {
        _activeZoneId = zoneId;
        UpdateOverlayBitmap();
    }

    /// <summary>
    /// Renders all zones to a 32-bit ARGB bitmap and applies it via UpdateLayeredWindow
    /// for true per-pixel alpha transparency.
    /// </summary>
    private void UpdateOverlayBitmap()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0)
            return;

        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.FromArgb(0, 0, 0, 0));

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

                // Draw zone name label centered in the rectangle
                if (!string.IsNullOrEmpty(zone.Name))
                {
                    using var font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
                    using var textBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
                    var textSize = g.MeasureString(zone.Name, font);
                    float textX = rect.X + (rect.Width - textSize.Width) / 2f;
                    float textY = rect.Y + (rect.Height - textSize.Height) / 2f;
                    g.DrawString(zone.Name, font, textBrush, textX, textY);
                }
            }
        }

        ApplyLayeredBitmap(bitmap);
    }

    /// <summary>
    /// Applies a 32-bit ARGB bitmap to this layered window via UpdateLayeredWindow.
    /// </summary>
    private void ApplyLayeredBitmap(Bitmap bitmap)
    {
        nint screenDc = IntPtr.Zero;
        nint memDc = IntPtr.Zero;
        nint hBitmap = IntPtr.Zero;
        nint oldBitmap = IntPtr.Zero;

        try
        {
            screenDc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

            var blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            var ptDst = new NativeMethods.POINT { X = Left, Y = Top };
            var size = new NativeMethods.SIZE { cx = Width, cy = Height };
            var ptSrc = new NativeMethods.POINT { X = 0, Y = 0 };

            NativeMethods.UpdateLayeredWindow(
                Handle, IntPtr.Zero, ref ptDst, ref size,
                memDc, ref ptSrc, 0, ref blend, NativeMethods.ULW_ALPHA);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero && memDc != IntPtr.Zero)
                NativeMethods.SelectObject(memDc, oldBitmap);
            if (hBitmap != IntPtr.Zero)
                NativeMethods.DeleteObject(hBitmap);
            if (memDc != IntPtr.Zero)
                NativeMethods.DeleteDC(memDc);
            if (screenDc != IntPtr.Zero)
                NativeMethods.DeleteDC(screenDc);
        }
    }
}
