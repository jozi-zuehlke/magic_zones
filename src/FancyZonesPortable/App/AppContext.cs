using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Reflection;
using FancyZonesPortable.Core.Abstractions;
using FancyZonesPortable.Core.Config;
using FancyZonesPortable.Core.Engine;
using FancyZonesPortable.Core.Logging;
using FancyZonesPortable.Interop;

namespace FancyZonesPortable.App;

/// <summary>
/// Main application context. Owns the tray icon, engine, hotkey registration,
/// and config file watcher. Runs as a headless tray application.
/// </summary>
internal class AppContext : ApplicationContext
{
    private const int CursorPollIntervalMs = 16;
    private const int VK_ESCAPE = 0x1B;

    private readonly ILogger _logger;
    private readonly ConfigLoader _configLoader;
    private readonly CoordinateConverter _converter;
    private readonly SnapEngine _engine;
    private readonly INativeInput _nativeInput;
    private readonly WindowFilter _windowFilter;
    private readonly SnapApplier _snapApplier;
    private readonly OverlayRenderer _overlayRenderer;
    private readonly IWinEventHook _winEventHook;
    private readonly IKeyboardHook _keyboardHook;
    private readonly IHotkeyManager _hotkeyManager;
    private readonly IScreenInfo _screenInfo;
    private readonly TrayManager _trayManager;
    private readonly System.Windows.Forms.Timer _cursorTimer;
    private readonly SynchronizationContext _syncContext;

    private IConfigFileWatcher? _configWatcher;
    private ZonesConfig _config;
    private string _configPath;
    private int _activationModifierVk;
    private bool _enabled;

    // Tracking state: we monitor the drag even before the modifier is pressed
    private bool _trackingDrag;
    private nint _trackedWindowHandle;

    public AppContext()
    {
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

        // 1-2. Create Logger and FileSystem
        _logger = new Logger();
        var fileSystem = new PhysicalFileSystem();

        // 3. Load config
        _configLoader = new ConfigLoader(fileSystem, _logger);
        var loadResult = _configLoader.Load();
        _config = loadResult.Config;
        _configPath = loadResult.FilePath ?? string.Empty;
        ApplyConfiguredLogLevel();

        // 6. Create TrayManager early so we can show balloons
        _trayManager = new TrayManager();

        // 4-5. Notify user about config status
        if (loadResult.IsNewDefault)
            _trayManager.ShowBalloon("FancyZones Portable", $"Default zones.json created at {_configPath}");
        if (loadResult.ValidationErrors.Count > 0)
            _trayManager.ShowBalloon("Config Validation", string.Join(Environment.NewLine, loadResult.ValidationErrors));

        // 7. Create CoordinateConverter, resolve zones for primary monitor
        _converter = new CoordinateConverter();
        _screenInfo = new ScreenInfoProvider();

        // Check for clipped zones on initial load
        CheckClippedZones();

        // 8. Create ZoneHitTester, SnapEngine
        var hitTester = new ZoneHitTester();
        _engine = new SnapEngine(hitTester, _converter, _logger);

        // 9-10. Create WindowManager, NativeInput, WindowFilter
        var windowManager = new WindowManager();
        _nativeInput = new NativeInput();
        _windowFilter = new WindowFilter(windowManager, overlayHwnd: 0);

        // 11-12. Create SnapApplier, OverlayRenderer
        _snapApplier = new SnapApplier(windowManager, _logger);
        _overlayRenderer = new OverlayRenderer();

        // Apply overlay colors from config
        ApplyOverlayColors();

        // Pass overlay HWND to the window filter once the renderer is created
        if (_overlayRenderer.Handle != 0)
            _windowFilter.SetOverlayHandle(_overlayRenderer.Handle);

        // 13. Create WinEventHook, subscribe, start
        _winEventHook = new WinEventHook();
        _winEventHook.WindowMoveStarted += OnWindowMoveStarted;
        _winEventHook.WindowMoveEnded += OnWindowMoveEnded;
        _winEventHook.Start();

        // 14. Create KeyboardHook (installed during drag tracking for instant modifier detection)
        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnKeyPressed;
        _keyboardHook.KeyReleased += OnKeyReleased;

        // 15. Parse activation modifier
        _activationModifierVk = SettingsParser.ParseModifierKey(_config.Settings.ActivationModifier) ?? 0x10;

        // 16-17. Register hotkey
        _hotkeyManager = new HotkeyManager();
        RegisterHotkey();
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;

        // 18. Setup tray menu
        _trayManager.AddMenuItem("Enable/Disable Snapping", OnToggleEnabled);
        _trayManager.AddMenuItem("Reload Config", OnReloadConfig);
        _trayManager.AddMenuItem("Open Config File", OnOpenConfigFile);
        _trayManager.AddMenuItem("About", OnAbout);
        _trayManager.AddMenuItem("Exit", OnExit);
        _trayManager.SetTooltip("FancyZones Portable - Enabled");
        _trayManager.Show();

        // 19. Config file watcher
        if (_config.Settings.AutoReloadConfig && !string.IsNullOrEmpty(_configPath))
        {
            _configWatcher = new ConfigFileWatcher();
            _configWatcher.ConfigFileChanged += OnConfigFileChanged;
            _configWatcher.Watch(_configPath, _config.Settings.AutoReloadDebounceMs);
        }

        // 20-21. Enable and create cursor poll timer
        _enabled = true;
        UpdateTrayIcon(true);
        _cursorTimer = new System.Windows.Forms.Timer { Interval = CursorPollIntervalMs };
        _cursorTimer.Tick += OnCursorTimerTick;

        _logger.Info("AppContext initialized.");
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static MonitorConfig GetPrimaryMonitor(ZonesConfig config)
    {
        return config.Monitors.FirstOrDefault(m =>
            string.Equals(m.MatchBy, "primary", StringComparison.OrdinalIgnoreCase))
            ?? config.Monitors.First();
    }

    private void ApplyConfiguredLogLevel()
    {
        var configuredLevel = SettingsParser.ParseLogLevel(_config.Settings.LogLevel);
        if (configuredLevel is null)
        {
            _logger.SetMinimumLevel(LogLevel.Info);
            _logger.Warning($"Invalid logLevel '{_config.Settings.LogLevel}'. Falling back to INFO.");
            return;
        }

        _logger.SetMinimumLevel(configuredLevel.Value);
    }

    private void RegisterHotkey()
    {
        var hotkey = SettingsParser.ParseHotkey(_config.Settings.ToggleHotkey);
        if (hotkey != null)
        {
            if (!_hotkeyManager.Register((int)hotkey.Modifiers, (int)hotkey.VirtualKey))
            {
                _logger.Warning($"Failed to register hotkey '{_config.Settings.ToggleHotkey}'.");
                _trayManager.ShowBalloon("FancyZones Portable",
                    $"Failed to register hotkey '{_config.Settings.ToggleHotkey}'. It may be in use by another application.");
            }
        }
    }

    private IReadOnlyList<ZoneRenderInfo> BuildRenderInfos(List<ResolvedZone> zones)
    {
        return zones.Select(z => new ZoneRenderInfo
        {
            ZoneId = z.Definition.Id,
            Name = z.Definition.Name,
            Bounds = z.Bounds,
        }).ToList();
    }

    private void ApplyOverlayColors()
    {
        var settings = _config.Settings;
        var activeColor = SettingsParser.ParseColor(settings.HighlightActiveZoneColor);
        var inactiveColor = SettingsParser.ParseColor(settings.HighlightInactiveZoneColor);

        if (activeColor != null && inactiveColor != null)
        {
            _overlayRenderer.SetColors(
                Color.FromArgb(activeColor.A, activeColor.R, activeColor.G, activeColor.B),
                settings.HighlightActiveZoneOpacity,
                Color.FromArgb(inactiveColor.A, inactiveColor.R, inactiveColor.G, inactiveColor.B),
                settings.HighlightInactiveZoneOpacity);
        }
    }

    private void CheckClippedZones()
    {
        var workingArea = _screenInfo.GetPrimaryWorkingArea();
        var primaryMonitor = GetPrimaryMonitor(_config);
        var resolveResult = _converter.Resolve(primaryMonitor, workingArea);

        if (resolveResult.ClippedZoneIds.Count > 0)
        {
            var ids = string.Join(", ", resolveResult.ClippedZoneIds);
            _logger.Warning($"Clipped zones: {ids}");
            _trayManager.ShowBalloon("FancyZones Portable",
                $"The following zones were clipped to monitor bounds: {ids}");
        }
    }

    /// <summary>
    /// Creates a simple colored icon to indicate enabled/disabled state.
    /// </summary>
    private static Icon CreateStateIcon(bool enabled)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var color = enabled ? Color.FromArgb(0, 180, 80) : Color.FromArgb(200, 50, 50);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private void UpdateTrayIcon(bool enabled)
    {
        var icon = CreateStateIcon(enabled);
        _trayManager.SetIcon(icon);
    }

    // ── Drag lifecycle ────────────────────────────────────────────

    private void OnWindowMoveStarted(object? sender, WinEventArgs e)
    {
        if (!_enabled)
            return;

        if (!_windowFilter.ShouldSnap(e.WindowHandle))
            return;

        // Enter tracking state for every eligible drag
        _trackingDrag = true;
        _trackedWindowHandle = e.WindowHandle;
        _keyboardHook.Install();
        _cursorTimer.Start();

        // If modifier is already held, activate immediately
        if (_nativeInput.IsKeyPressed(_activationModifierVk))
            ActivateSnapping();
    }

    private void ActivateSnapping()
    {
        if (_engine.State == SnapState.DragActive)
            return;

        var workingArea = _screenInfo.GetPrimaryWorkingArea();
        var primaryMonitor = GetPrimaryMonitor(_config);
        var (cursorX, cursorY) = _nativeInput.GetCursorPos();

        _engine.BeginDrag(_trackedWindowHandle, primaryMonitor, workingArea, cursorX, cursorY);

        var resolveResult = _converter.Resolve(primaryMonitor, workingArea);
        _overlayRenderer.Show(
            BuildRenderInfos(resolveResult.Zones),
            workingArea,
            _engine.ActiveZone?.Definition.Id);

        // Pass overlay HWND to filter after first show (form handle is created on Show)
        if (_overlayRenderer.Handle != 0)
            _windowFilter.SetOverlayHandle(_overlayRenderer.Handle);
    }

    private void OnCursorTimerTick(object? sender, EventArgs e)
    {
        if (!_trackingDrag)
            return;

        bool modifierHeld = _nativeInput.IsKeyPressed(_activationModifierVk);

        if (_engine.State == SnapState.DragActive)
        {
            // Active state: if modifier released, deactivate back to tracking
            if (!modifierHeld)
            {
                CancelDrag();
                return;
            }

            // Normal cursor update while active
            var (cursorX, cursorY) = _nativeInput.GetCursorPos();
            _engine.UpdateCursorPosition(cursorX, cursorY);
            _overlayRenderer.SetActiveZone(_engine.ActiveZone?.Definition.Id);
        }
        else if (modifierHeld)
        {
            // Tracking state: modifier just pressed → activate
            ActivateSnapping();
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.VirtualKeyCode == VK_ESCAPE)
        {
            CancelDrag();
            return;
        }

        // Instant modifier activation via keyboard hook (no polling delay)
        if (_trackingDrag
            && _engine.State != SnapState.DragActive
            && ModifierKeyMapper.IsModifierMatch(e.VirtualKeyCode, _activationModifierVk))
        {
            ActivateSnapping();
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        // Instant modifier deactivation via keyboard hook
        if (_engine.State == SnapState.DragActive
            && ModifierKeyMapper.IsModifierMatch(e.VirtualKeyCode, _activationModifierVk))
        {
            CancelDrag();
        }
    }

    private void OnWindowMoveEnded(object? sender, WinEventArgs e)
    {
        if (_engine.State == SnapState.DragActive)
        {
            var (cursorX, cursorY) = _nativeInput.GetCursorPos();
            _engine.UpdateCursorPosition(cursorX, cursorY);

            // Capture before CommitSnap resets engine state
            var draggedWindow = _engine.DraggedWindow;
            var result = _engine.CommitSnap();

            if (result != null)
                _snapApplier.Apply(draggedWindow, result.Value);

            _overlayRenderer.Hide();
        }

        StopTracking();
    }

    /// <summary>
    /// Cancels the active snap session (overlay + hook) but keeps tracking the drag
    /// so the user can re-press the modifier to reactivate.
    /// The keyboard hook stays installed for instant re-activation.
    /// </summary>
    private void CancelDrag()
    {
        _engine.CancelDrag();
        _overlayRenderer.Hide();
    }

    /// <summary>
    /// Fully stops tracking the current window drag.
    /// </summary>
    private void StopTracking()
    {
        _cursorTimer.Stop();
        _keyboardHook.Uninstall();
        _trackingDrag = false;
        _trackedWindowHandle = 0;
    }

    // ── Tray menu handlers ────────────────────────────────────────

    private void OnToggleEnabled()
    {
        _enabled = !_enabled;
        var status = _enabled ? "Enabled" : "Disabled";
        _trayManager.SetTooltip($"FancyZones Portable - {status}");
        _trayManager.ShowBalloon("FancyZones Portable", $"Snapping {status.ToLowerInvariant()}.");
        UpdateTrayIcon(_enabled);
        _logger.Info($"Snapping {status.ToLowerInvariant()}.");
    }

    private void OnReloadConfig()
    {
        ReloadConfig();
    }

    private void OnOpenConfigFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open config file.", ex);
        }
    }

    private void OnAbout()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        var dotnet = Environment.Version.ToString();
        _trayManager.ShowBalloon("FancyZones Portable",
            $"Version: {version}\n.NET: {dotnet}\nConfig: {_configPath}");
    }

    private void OnExit()
    {
        Application.Exit();
    }

    // ── Hotkey handler ────────────────────────────────────────────

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        OnToggleEnabled();
    }

    // ── Config file watcher ───────────────────────────────────────

    private void OnConfigFileChanged(object? sender, EventArgs e)
    {
        _syncContext.Post(_ => ReloadConfig(), null);
    }

    private void ReloadConfig()
    {
        _logger.Info("Reloading configuration...");
        var loadResult = _configLoader.Load();
        _config = loadResult.Config;
        if (loadResult.FilePath != null)
            _configPath = loadResult.FilePath;
        ApplyConfiguredLogLevel();

        _activationModifierVk = SettingsParser.ParseModifierKey(_config.Settings.ActivationModifier) ?? 0x10;

        _hotkeyManager.Unregister();
        RegisterHotkey();

        // Re-apply overlay colors from updated config
        ApplyOverlayColors();

        // Check for clipped zones after reload
        CheckClippedZones();

        // Recreate config watcher if setting changed
        _configWatcher?.Dispose();
        _configWatcher = null;
        if (_config.Settings.AutoReloadConfig && !string.IsNullOrEmpty(_configPath))
        {
            _configWatcher = new ConfigFileWatcher();
            _configWatcher.ConfigFileChanged += OnConfigFileChanged;
            _configWatcher.Watch(_configPath, _config.Settings.AutoReloadDebounceMs);
        }

        if (loadResult.ValidationErrors.Count > 0)
            _trayManager.ShowBalloon("Config Reload", $"Reloaded with warnings:\n{string.Join(Environment.NewLine, loadResult.ValidationErrors)}");
        else
            _trayManager.ShowBalloon("Config Reload", "Configuration reloaded successfully.");

        _logger.Info("Configuration reloaded.");
    }

    // ── Dispose ───────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cursorTimer.Stop();
            _cursorTimer.Dispose();
            _configWatcher?.Dispose();
            _hotkeyManager.Dispose();
            _keyboardHook.Dispose();
            _winEventHook.Dispose();
            _overlayRenderer.Hide();
            _trayManager.Dispose();
        }

        base.Dispose(disposing);
    }
}
