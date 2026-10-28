using System.Text.Json;

namespace WSLManager.Desktop.Core.Settings;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed record DesktopSettings
{
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public int LogRetentionDays { get; init; } = 90;
}

public sealed class DesktopSettingsStore
{
    private readonly string _path;

    public DesktopSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WSLManager", "desktop-settings.json");
    }

    public string PathName => _path;

    public DesktopSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) return new DesktopSettings();
            return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(_path)) ?? new DesktopSettings();
        }
        catch (JsonException) { return new DesktopSettings(); }
        catch (IOException) { return new DesktopSettings(); }
        catch (UnauthorizedAccessException) { return new DesktopSettings(); }
    }

    public void Save(DesktopSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var retention = Math.Clamp(settings.LogRetentionDays, 1, 3650);
        var normalized = settings with { LogRetentionDays = retention };
        var directory = System.IO.Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(_path))
            File.Move(temporary, _path, overwrite: true);
        else
            File.Move(temporary, _path);
    }
}

public static class ConfirmationPolicy
{
    public static bool RequiresConfirmation(string action) => action.ToLowerInvariant() switch
    {
        "backup" or "restore" or "move" or "clone" or "terminate" or "convert" or "resize" or "setdefault" => true,
        _ => false
    };

    public static string Describe(string action, string? distro, string? destination = null) => action.ToLowerInvariant() switch
    {
        "backup" => $"Export {distro} to {destination}. This may affect consistency-sensitive workloads; WSL Manager will not stop the distribution automatically.",
        "restore" => $"Register a new distribution named {distro} from the selected backup at {destination}. Existing distributions and directories will not be overwritten. Imported distributions may start as root; set a default Linux user if needed.",
        "move" => $"Move {distro} to {destination} using native WSL support. The distribution may be unavailable during the move.",
        "clone" => $"Create a separate clone ({distro}) at {destination}. The source distribution will be preserved.",
        "terminate" => $"Stop the selected distribution {distro}. Other WSL distributions will remain running.",
        "convert" => $"Convert {distro} to the selected WSL generation. This can take time and changes the distribution's storage format.",
        "resize" => $"Expand virtual disk capacity for {distro}. This does not reduce Linux filesystem usage or shrink the VHDX file.",
        "setdefault" => $"Make {distro} the default WSL distribution.",
        _ => $"Run {action} for {distro}."
    };
}

public static class ProtectedDistributionPolicy
{
    private static readonly HashSet<string> RestrictedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Terminate", "SetDefault", "Convert", "Unregister", "Move", "Clone", "Resize"
    };

    public static bool IsAllowed(string action, bool isProtected) => !isProtected || !RestrictedActions.Contains(action);
}
