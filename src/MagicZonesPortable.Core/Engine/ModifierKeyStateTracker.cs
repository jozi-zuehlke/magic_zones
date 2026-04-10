namespace MagicZonesPortable.Core.Engine;

/// <summary>
/// Tracks pressed/released transitions for a modifier key and suppresses
/// repeated key-down events while the key remains physically held.
/// </summary>
public sealed class ModifierKeyStateTracker
{
    private bool _isHeld;

    /// <summary>
    /// Attempts to transition from "not held" to "held".
    /// Returns true only for the first press edge.
    /// </summary>
    public bool TryPress()
    {
        if (_isHeld)
            return false;

        _isHeld = true;
        return true;
    }

    /// <summary>
    /// Attempts to transition from "held" to "not held".
    /// Returns true only for the first release edge.
    /// </summary>
    public bool TryRelease()
    {
        if (!_isHeld)
            return false;

        _isHeld = false;
        return true;
    }

    /// <summary>
    /// Synchronizes state with a polled physical key state.
    /// </summary>
    public void Sync(bool isHeld)
    {
        _isHeld = isHeld;
    }

    /// <summary>
    /// Clears tracked state to "not held".
    /// </summary>
    public void Reset()
    {
        _isHeld = false;
    }
}
