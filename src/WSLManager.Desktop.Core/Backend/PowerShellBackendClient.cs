using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WSLManager.Desktop.Core.Backend;

public sealed class PowerShellBackendClient
{
    private readonly IProcessRunner _processRunner;
    private readonly string _backendScript;
    private readonly string _powerShellExecutable;
    private readonly Func<int> _logRetentionDays;

    public PowerShellBackendClient(
        string? backendScript = null,
        string? powerShellExecutable = null,
        IProcessRunner? processRunner = null,
        Func<int>? logRetentionDays = null)
    {
        _backendScript = backendScript ?? BackendLocator.FindScript();
        _powerShellExecutable = powerShellExecutable ?? PowerShellLocator.FindExecutable();
        _processRunner = processRunner ?? new ProcessRunner();
        _logRetentionDays = logRetentionDays ?? (() => 90);
    }

    public async Task<JsonDocument> InvokeAsync(BackendRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_backendScript))
            throw new FileNotFoundException("The WSL Manager PowerShell backend was not found.", _backendScript);

        var startInfo = new ProcessStartInfo
        {
            FileName = _powerShellExecutable,
            WorkingDirectory = Path.GetDirectoryName(_backendScript)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(_backendScript);
        foreach (var argument in BackendArgumentBuilder.Build(request))
            startInfo.ArgumentList.Add(argument);

        startInfo.Environment["WSL_MANAGER_LOG_RETENTION_DAYS"] =
            Math.Clamp(_logRetentionDays(), 1, 3650).ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["WSL_MANAGER_UTF8_OUTPUT"] = "1";

        var result = await _processRunner.RunAsync(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? "The PowerShell backend reported an operation failure."
                : Limit(result.StandardError.Trim(), 2400);
            throw new BackendInvocationException(detail, result.ExitCode, detail);
        }

        try
        {
            return JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            throw new BackendInvocationException(
                $"The PowerShell backend returned invalid JSON ({ex.Message}). Verify the PowerShell host encoding and backend output.",
                result.ExitCode,
                Limit(result.StandardError, 1200));
        }
    }

    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "…";
}

public static class BackendLocator
{
    public static string FindScript(string? startDirectory = null)
    {
        var current = new DirectoryInfo(startDirectory ?? AppContext.BaseDirectory);
        while (current is not null)
        {
            var bundled = Path.Combine(current.FullName, "backend", "WSL-Manager.ps1");
            if (File.Exists(bundled))
                return bundled;

            var repository = Path.Combine(current.FullName, "WSL-Manager.ps1");
            if (File.Exists(repository) && Directory.Exists(Path.Combine(current.FullName, "src", "Core")))
                return repository;
            current = current.Parent;
        }
        throw new FileNotFoundException("Could not locate WSL-Manager.ps1. Build the desktop project with its backend content files or run it from a repository checkout.");
    }
}

public static class PowerShellLocator
{
    public static string FindExecutable()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), "pwsh.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        var windowsPowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell))
            return windowsPowerShell;

        throw new FileNotFoundException("PowerShell 7 (pwsh.exe) or Windows PowerShell 5.1 (powershell.exe) is required.");
    }
}
