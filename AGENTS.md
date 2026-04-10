# FancyZones Portable

A portable Windows window zone manager for corporate users who lack admin rights.

## What it does

Users hold Shift while dragging a window; an overlay highlights the nearest zone; releasing the mouse snaps the window to that zone. Zone layouts are defined in a human-readable `zones.json` file next to the executable. A system tray icon allows toggling snapping on/off and reloading the config without restarting.

## Key constraints

- Single `.exe`, no installer, no UAC prompts, no registry writes, no network access
- Runs from any user-writable location (USB stick, Documents folder)
- Framework-dependent .NET 8 executable (requires the .NET 8 runtime on the host machine)
- Targets Windows only
