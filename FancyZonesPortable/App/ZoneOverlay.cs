using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FancyZonesPortable.Config;
using FancyZonesPortable.Interop;

namespace FancyZonesPortable.App;

/// <summary>
/// Transparent, click-through, always-on-top overlay that renders zone highlights during Shift+drag.
/// Uses UpdateLayeredWindow with per-pixel alpha for true transparency — zones are
/// semi-transparent and windows behind them remain visible.
/// </summary>
internal sealed class ZoneOverlay : Form
{
    private List<ZoneDefinition> _zones = new();
    private string? _activeZoneId;
    private Color _activeColor = ColorTranslator.FromHtml("#0078D4");
    private double _activeOpacity = 0.5;
    private Color _inactiveColor = ColorTranslator.FromHtml("#888888");
    private double _inactiveOpacity = 0.2;

    public ZoneOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // Force handle creation now so it's ready before any WinEvent callbacks fire.
        _ = Handle;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x00080000  // WS_EX_LAYERED
                        | 0x00000020  // WS_EX_TRANSPARENT (click-through)
                        | 0x00000008  // WS_EX_TOPMOST
                        | 0x08000000  // WS_EX_NOACTIVATE
                        | 0x00000080; // WS_EX_TOOLWINDOW (no taskbar entry)
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void UpdateZones(List<ZoneDefinition> zones, SettingsConfig settings)
    {
        _zones = zones;
        _activeColor = ParseColor(settings.HighlightActiveZoneColor, Color.FromArgb(0, 120, 212));
        _activeOpacity = Math.Clamp(settings.HighlightActiveZoneOpacity, 0.0, 1.0);
        _inactiveColor = ParseColor(settings.HighlightInactiveZoneColor, Color.Gray);
        _inactiveOpacity = Math.Clamp(settings.HighlightInactiveZoneOpacity, 0.0, 1.0);
    }

    public void SetActiveZone(string? zoneId)
    {
        if (_activeZoneId == zoneId) return;
        _activeZoneId = zoneId;
        PaintLayered();
    }

    public void ShowOverlay(Rectangle workingArea)
    {
        Bounds = workingArea;
        if (!Visible)
            Show();
        PaintLayered();
    }

    public void HideOverlay()
    {
        if (Visible)
            Hide();
        _activeZoneId = null;
    }

    /// <summary>
    /// Renders all zones into a 32bpp ARGB bitmap and applies it to the layered
    /// window via UpdateLayeredWindow, giving true per-pixel alpha blending.
    /// </summary>
    private void PaintLayered()
    {
        if (!Visible || Width <= 0 || Height <= 0) return;

        using var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            // Bitmap starts fully transparent (all zeros) — no Clear needed.

            // Draw inactive zones first, active zone last (on top)
            foreach (var zone in _zones)
            {
                if (zone.Id == _activeZoneId) continue;
                DrawZone(g, zone, isActive: false);
            }

            var activeZone = _zones.FirstOrDefault(z => z.Id == _activeZoneId);
            if (activeZone != null)
                DrawZone(g, activeZone, isActive: true);
        }

        ApplyBitmapToLayeredWindow(bmp);
    }

    private void ApplyBitmapToLayeredWindow(Bitmap bmp)
    {
        IntPtr screenDc = IntPtr.Zero;
        IntPtr memDc = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            screenDc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

            var size = new NativeMethods.SIZE { cx = bmp.Width, cy = bmp.Height };
            var pointSrc = new NativeMethods.POINT { X = 0, Y = 0 };
            var pointDst = new NativeMethods.POINT { X = Left, Y = Top };
            var blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            NativeMethods.UpdateLayeredWindow(
                Handle, IntPtr.Zero, ref pointDst, ref size,
                memDc, ref pointSrc, 0, ref blend, NativeMethods.ULW_ALPHA);
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

    private void DrawZone(Graphics g, ZoneDefinition zone, bool isActive)
    {
        var rect = new Rectangle(
            zone.AbsoluteRect.X - Bounds.X,
            zone.AbsoluteRect.Y - Bounds.Y,
            zone.AbsoluteRect.Width,
            zone.AbsoluteRect.Height);

        if (rect.Width <= 0 || rect.Height <= 0) return;

        var color = isActive ? _activeColor : _inactiveColor;
        var opacity = isActive ? _activeOpacity : _inactiveOpacity;
        var fillAlpha = (int)(opacity * 255);
        var borderAlpha = Math.Min(255, fillAlpha + 80);

        using var fillBrush = new SolidBrush(Color.FromArgb(fillAlpha, color));
        using var borderPen = new Pen(Color.FromArgb(borderAlpha, color), 2f);

        g.FillRectangle(fillBrush, rect);
        g.DrawRectangle(borderPen, rect);

        if (!string.IsNullOrEmpty(zone.Name))
        {
            using var font = new Font("Segoe UI", 11f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(Math.Min(255, fillAlpha + 120), Color.White));
            var textSize = g.MeasureString(zone.Name, font);
            var textX = rect.X + (rect.Width - textSize.Width) / 2;
            var textY = rect.Y + (rect.Height - textSize.Height) / 2;
            g.DrawString(zone.Name, font, textBrush, textX, textY);
        }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (hex.StartsWith('#'))
                hex = hex[1..];

            return hex.Length switch
            {
                6 => Color.FromArgb(255,
                    Convert.ToInt32(hex[..2], 16),
                    Convert.ToInt32(hex[2..4], 16),
                    Convert.ToInt32(hex[4..6], 16)),
                8 => Color.FromArgb(
                    Convert.ToInt32(hex[..2], 16),
                    Convert.ToInt32(hex[2..4], 16),
                    Convert.ToInt32(hex[4..6], 16),
                    Convert.ToInt32(hex[6..8], 16)),
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }
}
