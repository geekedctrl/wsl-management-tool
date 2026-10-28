using System.Diagnostics;

namespace WSLManager.Desktop.Core.Backend;

public sealed record BackendRequest
{
    public required string Action { get; init; }
    public string? Distro { get; init; }
    public IReadOnlyList<string> Distributions { get; init; } = [];
    public string? Destination { get; init; }
    public string? Source { get; init; }
    public string? Format { get; init; }
    public string? NewName { get; init; }
    public string? Version { get; init; }
    public string? Size { get; init; }
    public bool WhatIf { get; init; }
}

public static class BackendArgumentBuilder
{
    private static readonly HashSet<string> MutatingActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Start", "Terminate", "SetDefault", "Convert", "Backup", "Restore", "Move", "Clone", "Resize"
    };

    public static IReadOnlyList<string> Build(BackendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Action))
            throw new ArgumentException("A backend action is required.", nameof(request));

        var args = new List<string> { "-Action", request.Action, "-OutputFormat", "Json" };
        var distros = request.Distributions.Count > 0
            ? request.Distributions
            : request.Distro is null ? [] : [request.Distro];

        if (distros.Count > 0)
        {
            args.Add("-Distro");
            args.AddRange(distros);
        }
        Add(args, "-Destination", request.Destination);
        Add(args, "-Source", request.Source);
        Add(args, "-Format", request.Format);
        Add(args, "-NewName", request.NewName);
        Add(args, "-Version", request.Version);
        Add(args, "-Size", request.Size);

        if (request.WhatIf)
            args.Add("-WhatIf");
        else if (MutatingActions.Contains(request.Action))
            // The desktop presents its own action-specific confirmation. Suppress
            // PowerShell's console prompt while keeping backend safety validation.
            args.Add("-Confirm:$false");

        return args;
    }

    private static void Add(List<string> args, string name, string? value)
    {
        if (value is null)
            return;
        args.Add(name);
        args.Add(value);
    }
}

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("PowerShell could not be started.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}

public sealed class BackendInvocationException : Exception
{
    public BackendInvocationException(string message, int? exitCode = null, string? standardError = null)
        : base(message)
    {
        ExitCode = exitCode;
        StandardError = standardError;
    }

    public int? ExitCode { get; }
    public string? StandardError { get; }
}
