using System.Text.Json;

namespace WSLManager.Desktop.Core.Backend;

public static class DiagnosticsExportService
{
    public static async Task WriteJsonAsync(
        string path,
        string diagnosticsJson,
        string? userProfile = null,
        string? localAppData = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(diagnosticsJson);

        var redacted = DiagnosticsRedactor.Redact(diagnosticsJson, userProfile, localAppData);
        using var parsed = JsonDocument.Parse(redacted);
        var formatted = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(formatted.AsMemory(), cancellationToken).ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
