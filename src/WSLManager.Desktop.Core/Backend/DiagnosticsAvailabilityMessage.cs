using System.Text.Json;

namespace WSLManager.Desktop.Core.Backend;

public static class DiagnosticsAvailabilityMessage
{
    public static string From(JsonElement diagnostics)
    {
        if (!diagnostics.TryGetProperty("Capabilities", out var capabilities) ||
            !capabilities.TryGetProperty("Available", out var available) ||
            available.ValueKind != JsonValueKind.True)
        {
            return "wsl.exe was not found on PATH. Install or repair WSL, then refresh.";
        }

        if (diagnostics.TryGetProperty("Distributions", out var distributions) &&
            distributions.ValueKind == JsonValueKind.Array && distributions.GetArrayLength() == 0)
        {
            return "WSL is available, but no distributions are registered.";
        }

        return "Diagnostics refreshed. Values not exposed by WSL are omitted or reported unavailable.";
    }
}
