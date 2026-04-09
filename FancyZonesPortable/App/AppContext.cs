using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using FancyZonesPortable.Config;
using FancyZonesPortable.Logging;

namespace FancyZonesPortable.App;

/// <summary>
/// ApplicationContext subclass that owns the NotifyIcon, SnapEngine, HotkeyManager, and ConfigWatcher.
/// Provides the system tray UI and orchestrates all components.
/// </summary>
internal sealed class AppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly ZoneOverlay _overlay;
    private readonly SnapEngine _engine;
    private readonly HotkeyManager _hotkeyManager;
    private readonly ConfigWatcher _configWatcher;

    private string? _configPath;
    private ZonesConfig? _currentConfig;
    private bool _snappingEnabled = true;
    private readonly HiddenMessageWindow _messageWindow;

    public AppContext()
    {
        // Create hidden message window for hotkey and WM_HOTKEY processing
        _messageWindow = new HiddenMessageWindow();
        _messageWindow.HotkeyMessage += OnHotkeyMessage;

        // Create overlay
        _overlay = new ZoneOverlay();

        // Create engine
        _engine = new SnapEngine(_overlay);

        // Create hotkey manager
        _hotkeyManager = new HotkeyManager(_messageWindow.Handle);
        _hotkeyManager.HotkeyPressed += OnToggleSnapping;

        // Create config watcher
        _configWatcher = new ConfigWatcher();
        _configWatcher.ConfigChanged += OnConfigChanged;

        // Build tray icon
        _toggleMenuItem = new ToolStripMenuItem("Disable Snapping", null, OnToggleClick);
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Reload Config", null, OnReloadConfigClick);
        contextMenu.Items.Add("Open Config File", null, OnOpenConfigClick);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("About", null, OnAboutClick);
        contextMenu.Items.Add("Exit", null, OnExitClick);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadEmbeddedIcon("icon_enabled.ico"),
            ContextMenuStrip = contextMenu,
            Text = "FancyZones Portable — Enabled",
            Visible = true
        };

        // Load config
        LoadConfig(showNotification: false);

        // Start engine
        _engine.Start();

        // Register hotkey
        if (_currentConfig != null)
        {
            if (!_hotkeyManager.Register(_currentConfig.Settings.ToggleHotkey))
            {
                ShowNotification("Hotkey Warning",
                    $"Could not register hotkey '{_currentConfig.Settings.ToggleHotkey}'. It may be in use by another application.",
                    ToolTipIcon.Warning);
            }
        }

        // Start config watcher
        if (_configPath != null && (_currentConfig?.Settings.AutoReloadConfig ?? true))
        {
            _configWatcher.Start(_configPath, _currentConfig?.Settings.AutoReloadDebounceMs ?? 500);
        }

        ShowNotification("FancyZones Portable",
            _configPath != null
                ? $"Started with {GetZoneCount()} zones loaded."
                : "No config found — default zones.json generated.",
            ToolTipIcon.Info);

        Logger.Info("AppContext initialized.");
    }

    private void LoadConfig(bool showNotification)
    {
        try
        {
            _configPath = ConfigLoader.FindConfigPath();

            if (_configPath == null)
            {
                // Generate default config
                _configPath = ConfigLoader.GetDefaultConfigPath();
                ConfigLoader.GenerateDefaultConfig(_configPath);
                Logger.Info($"Generated default config at: {_configPath}");
            }

            var (config, warnings) = ConfigLoader.Load(_configPath);

            // Resolve zones against primary monitor working area
            var workingArea = Screen.PrimaryScreen!.WorkingArea;
            var resolveWarnings = ConfigLoader.ResolveZones(config, workingArea);
            warnings.AddRange(resolveWarnings);

            // Get primary monitor zones
            var primaryMonitor = config.Monitors.FirstOrDefault(m => m.MatchBy == "primary");
            var zones = primaryMonitor?.Zones ?? new List<ZoneDefinition>();

            _currentConfig = config;
            _engine.UpdateConfig(zones, config.Settings, workingArea);

            foreach (var warning in warnings)
            {
                Logger.Warn(warning);
            }

            if (showNotification)
            {
                if (warnings.Count > 0)
                {
                    ShowNotification("Config Loaded",
                        $"Loaded {zones.Count} zones with {warnings.Count} warning(s).\n{warnings[0]}",
                        ToolTipIcon.Warning);
                }
                else
                {
                    ShowNotification("Config Loaded",
                        $"Successfully loaded {zones.Count} zones.",
                        ToolTipIcon.Info);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load config", ex);

            if (showNotification || _currentConfig == null)
            {
                ShowNotification("Config Error",
                    $"Failed to load zones.json: {ex.Message}\nPrevious config remains active.",
                    ToolTipIcon.Error);
            }
        }
    }

    private void OnConfigChanged()
    {
        Logger.Info("Auto-reloading config due to file change.");
        LoadConfig(showNotification: true);
    }

    private void OnToggleSnapping()
    {
        _snappingEnabled = !_snappingEnabled;
        _engine.Enabled = _snappingEnabled;
        UpdateTrayState();

        ShowNotification("FancyZones Portable",
            _snappingEnabled ? "Snapping enabled." : "Snapping disabled.",
            ToolTipIcon.Info);
    }

    private void OnToggleClick(object? sender, EventArgs e) => OnToggleSnapping();

    private void OnReloadConfigClick(object? sender, EventArgs e)
    {
        Logger.Info("Manual config reload requested.");
        LoadConfig(showNotification: true);
    }

    private void OnOpenConfigClick(object? sender, EventArgs e)
    {
        if (_configPath == null || !File.Exists(_configPath))
        {
            ShowNotification("Config File", "No config file found.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open config file", ex);
            ShowNotification("Error", $"Could not open config file: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var dotnetVersion = Environment.Version.ToString();
        var configPath = _configPath ?? "(none)";
        var zoneCount = GetZoneCount();

        MessageBox.Show(
            $"FancyZones Portable v{version}\n\n" +
            $".NET Runtime: {dotnetVersion}\n" +
            $"Config File: {configPath}\n" +
            $"Zones Loaded: {zoneCount}\n" +
            $"Snapping: {(_snappingEnabled ? "Enabled" : "Disabled")}",
            "About FancyZones Portable",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        Logger.Info("Exit requested by user.");
        ExitThread();
    }

    private void OnHotkeyMessage(int id)
    {
        _hotkeyManager.HandleWmHotkey(id);
    }

    private void UpdateTrayState()
    {
        _toggleMenuItem.Text = _snappingEnabled ? "Disable Snapping" : "Enable Snapping";
        _trayIcon.Icon = LoadEmbeddedIcon(_snappingEnabled ? "icon_enabled.ico" : "icon_disabled.ico");
        _trayIcon.Text = $"FancyZones Portable — {(_snappingEnabled ? "Enabled" : "Disabled")}";
    }

    private int GetZoneCount()
    {
        return _currentConfig?.Monitors
            .Where(m => m.MatchBy == "primary")
            .SelectMany(m => m.Zones)
            .Count() ?? 0;
    }

    private void ShowNotification(string title, string text, ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(3000);
    }

    private static Icon? LoadEmbeddedIcon(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resourceName);
        return stream != null ? new Icon(stream) : SystemIcons.Application;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _configWatcher.Dispose();
            _hotkeyManager.Dispose();
            _engine.Dispose();
            _overlay.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _messageWindow.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// A hidden NativeWindow that receives WM_HOTKEY messages.
/// </summary>
internal sealed class HiddenMessageWindow : NativeWindow, IDisposable
{
    public event Action<int>? HotkeyMessage;

    public HiddenMessageWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = "FancyZonesPortable_HotkeyWindow",
            Style = 0,
            ExStyle = 0,
            Parent = IntPtr.Zero
        });
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Interop.NativeMethods.WM_HOTKEY)
        {
            HotkeyMessage?.Invoke(m.WParam.ToInt32());
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}
