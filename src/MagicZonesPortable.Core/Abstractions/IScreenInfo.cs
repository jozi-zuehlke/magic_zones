namespace MagicZonesPortable.Core.Abstractions;

/// <summary>
/// Abstraction for querying screen/monitor information.
/// </summary>
public interface IScreenInfo
{
    /// <summary>
    /// Gets the working area of the primary monitor (excludes taskbar).
    /// </summary>
    Rectangle GetPrimaryWorkingArea();
}
