namespace WSLManager.Desktop.Core.Models;

public sealed class DistributionSummary
{
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsProtected { get; set; }

    public string LogoKey
    {
        get
        {
            var tokens = System.Text.RegularExpressions.Regex.Matches(Name ?? string.Empty, "[A-Za-z0-9]+")
                .Select(match => match.Value.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
            bool Has(params string[] names) => names.Any(tokens.Contains);

            if (Has("docker")) return "docker";
            if (Has("debian")) return "debian";
            if (Has("ubuntu")) return "ubuntu";
            if (Has("kali")) return "kalilinux";
            if (Has("arch", "archlinux")) return "archlinux";
            if (Has("fedora")) return "fedora";
            if (Has("opensuse", "suse", "sles")) return "opensuse";
            if (Has("alpine")) return "alpinelinux";
            if (Has("rocky", "rockylinux")) return "rockylinux";
            if (Has("alma", "almalinux")) return "almalinux";
            if (Has("centos")) return "centos";
            if (Has("redhat", "rhel")) return "redhat";
            if (Has("gentoo")) return "gentoo";
            if (Has("void", "voidlinux")) return "voidlinux";
            if (Has("manjaro")) return "manjaro";
            if (Has("elementary")) return "elementary";
            if (Has("tails")) return "tails";
            if (Has("parrot")) return "parrotsecurity";
            return "linux";
        }
    }

    public string Summary => $"WSL {Version} · {State}";
    public string Flags => string.Join(" · ", new[] { IsDefault ? "Default" : null, IsProtected ? "System-managed" : null }.Where(value => value is not null));
}
