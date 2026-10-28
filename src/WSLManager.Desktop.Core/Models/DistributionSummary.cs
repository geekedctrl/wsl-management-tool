namespace WSLManager.Desktop.Core.Models;

public sealed class DistributionSummary
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsProtected { get; set; }

    public string Summary => $"WSL {Version} · {State}";
    public string Flags => string.Join(" · ", new[] { IsDefault ? "Default" : null, IsProtected ? "System-managed" : null }.Where(value => value is not null));
}
