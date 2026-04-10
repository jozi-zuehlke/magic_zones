namespace MagicZonesPortable.Core.Config;

/// <summary>
/// Validates a loaded <see cref="ZonesConfig"/> for correctness: required fields,
/// value ranges, duplicate IDs, and structural integrity.
/// </summary>
public class ConfigValidator
{
    /// <summary>
    /// Validates the given config and returns a list of validation errors.
    /// An empty list means the config is valid.
    /// </summary>
    public List<string> Validate(ZonesConfig config)
    {
        var errors = new List<string>();

        if (config.Version < 1)
        {
            errors.Add("Config version must be >= 1.");
        }

        ValidateSettings(config.Settings, errors);
        ValidateMonitors(config.Monitors, errors);

        return errors;
    }

    private static void ValidateSettings(SettingsConfig settings, List<string> errors)
    {
        var validModifiers = new[] { "Shift", "Ctrl", "Alt" };
        if (!validModifiers.Contains(settings.ActivationModifier, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Invalid activationModifier '{settings.ActivationModifier}'. Must be one of: {string.Join(", ", validModifiers)}.");
        }

        if (string.IsNullOrWhiteSpace(settings.ToggleHotkey))
        {
            errors.Add("toggleHotkey must not be empty.");
        }

        if (SettingsParser.ParseLogLevel(settings.LogLevel) is null)
        {
            errors.Add($"Invalid logLevel '{settings.LogLevel}'. Must be one of: DEBUG, INFO, WARN, ERROR.");
        }

        ValidateOpacity(settings.HighlightActiveZoneOpacity, "highlightActiveZoneOpacity", errors);
        ValidateOpacity(settings.HighlightInactiveZoneOpacity, "highlightInactiveZoneOpacity", errors);

        if (settings.AutoReloadDebounceMs < 0)
        {
            errors.Add("autoReloadDebounceMs must be >= 0.");
        }
    }

    private static void ValidateOpacity(double value, string name, List<string> errors)
    {
        if (value < 0.0 || value > 1.0)
        {
            errors.Add($"{name} must be between 0.0 and 1.0 (got {value}).");
        }
    }

    private static void ValidateMonitors(List<MonitorConfig> monitors, List<string> errors)
    {
        if (monitors.Count == 0)
        {
            errors.Add("At least one monitor configuration is required.");
            return;
        }

        var monitorIds = new HashSet<string>();
        foreach (var monitor in monitors)
        {
            if (string.IsNullOrWhiteSpace(monitor.Id))
            {
                errors.Add("Monitor id must not be empty.");
                continue;
            }

            if (!monitorIds.Add(monitor.Id))
            {
                errors.Add($"Duplicate monitor id '{monitor.Id}'.");
            }

            var validMatchBy = new[] { "primary", "deviceName", "index" };
            if (!validMatchBy.Contains(monitor.MatchBy))
            {
                errors.Add($"Monitor '{monitor.Id}': matchBy must be one of: {string.Join(", ", validMatchBy)}.");
            }

            var validUnits = new[] { "percent", "pixels" };
            if (!validUnits.Contains(monitor.CoordinateUnit))
            {
                errors.Add($"Monitor '{monitor.Id}': coordinateUnit must be one of: {string.Join(", ", validUnits)}.");
            }

            if (monitor.MatchBy == "deviceName" && string.IsNullOrWhiteSpace(monitor.DeviceName))
            {
                errors.Add($"Monitor '{monitor.Id}': deviceName is required when matchBy is 'deviceName'.");
            }

            if (monitor.MatchBy == "index" && monitor.Index < 0)
            {
                errors.Add($"Monitor '{monitor.Id}': index must be >= 0.");
            }

            if (monitor.Zones.Count == 0)
            {
                errors.Add($"Monitor '{monitor.Id}': at least one zone is required.");
            }

            ValidateZones(monitor.Id, monitor.Zones, errors);
        }
    }

    private static void ValidateZones(string monitorId, List<ZoneDefinition> zones, List<string> errors)
    {
        var zoneIds = new HashSet<string>();
        foreach (var zone in zones)
        {
            if (string.IsNullOrWhiteSpace(zone.Id))
            {
                errors.Add($"Monitor '{monitorId}': zone id must not be empty.");
                continue;
            }

            if (!zoneIds.Add(zone.Id))
            {
                errors.Add($"Monitor '{monitorId}': duplicate zone id '{zone.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(zone.Name))
            {
                errors.Add($"Monitor '{monitorId}', zone '{zone.Id}': name must not be empty.");
            }

            if (zone.Priority < 0)
            {
                errors.Add($"Monitor '{monitorId}', zone '{zone.Id}': priority must be >= 0.");
            }

            if (zone.Width <= 0)
            {
                errors.Add($"Monitor '{monitorId}', zone '{zone.Id}': width must be > 0.");
            }

            if (zone.Height <= 0)
            {
                errors.Add($"Monitor '{monitorId}', zone '{zone.Id}': height must be > 0.");
            }
        }
    }
}
