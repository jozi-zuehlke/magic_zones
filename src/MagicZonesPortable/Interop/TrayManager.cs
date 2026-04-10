using MagicZonesPortable.Core.Abstractions;

namespace MagicZonesPortable.Interop;

/// <summary>
/// Implements <see cref="ITrayManager"/> using WinForms NotifyIcon.
/// </summary>
internal class TrayManager : ITrayManager
{
    private const int MaxTooltipLength = 63;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    public event EventHandler? DoubleClicked;

    public TrayManager()
    {
        _contextMenu = new ContextMenuStrip();
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _contextMenu
        };
        _notifyIcon.DoubleClick += (_, e) => DoubleClicked?.Invoke(this, e);
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void ShowBalloon(string title, string message, int timeoutMs = 3000)
    {
        _notifyIcon.ShowBalloonTip(timeoutMs, title, message, ToolTipIcon.Info);
    }

    public void SetTooltip(string text)
    {
        _notifyIcon.Text = text.Length > MaxTooltipLength
            ? text[..MaxTooltipLength]
            : text;
    }

    public void AddMenuItem(string text, Action onClick)
    {
        _contextMenu.Items.Add(new ToolStripMenuItem(text, null, (_, _) => onClick()));
    }

    public void SetIcon(Icon icon)
    {
        _notifyIcon.Icon = icon;
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        GC.SuppressFinalize(this);
    }
}
