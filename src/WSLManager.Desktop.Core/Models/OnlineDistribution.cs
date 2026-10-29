namespace WSLManager.Desktop.Core.Models;

public sealed record OnlineDistribution(string Name, string FriendlyName, bool AlreadyRegistered = false)
{
    public string LogoKey => new DistributionSummary { Name = $"{FriendlyName} {Name}" }.LogoKey;
    public string RegistrationStatus => AlreadyRegistered ? "Already installed · use a new name to add another" : "Available to install";

    public static IReadOnlyList<OnlineDistribution> Search(IEnumerable<OnlineDistribution> catalog, string? query)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (string.IsNullOrWhiteSpace(query)) return catalog.ToArray();

        var search = query.Trim();
        return catalog.Where(item =>
            item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            item.FriendlyName.Contains(search, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }
}
