using System.Text.Json;
using WSLManager.Desktop.Core.Models;

namespace WSLManager.Desktop.Core.Backend;

public static class DistributionSummaryParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<DistributionSummary> Parse(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return [];
        if (root.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<List<DistributionSummary>>(root.GetRawText(), Options) ?? [];
        if (root.ValueKind == JsonValueKind.Object)
        {
            var item = JsonSerializer.Deserialize<DistributionSummary>(root.GetRawText(), Options);
            return item is null ? [] : [item];
        }
        return [];
    }
}
