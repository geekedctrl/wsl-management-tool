using System.Globalization;
using System.Text.Json;

namespace WSLManager.Desktop.Core.Backend;

public sealed record DistributionDetailRow(string Label, string Value);

public static class DistributionDetailRows
{
    public static IReadOnlyList<DistributionDetailRow> Create(JsonElement root)
    {
        var rows = new List<DistributionDetailRow>();
        if (root.ValueKind == JsonValueKind.Object)
            AddObjectRows(root, null, rows);
        return rows;
    }

    private static void AddObjectRows(JsonElement element, string? prefix, List<DistributionDetailRow> rows)
    {
        foreach (var property in element.EnumerateObject())
        {
            var label = Humanize(property.Name);
            var path = prefix is null ? label : $"{prefix} · {label}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                AddObjectRows(property.Value, path, rows);
                continue;
            }

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "N/A",
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                JsonValueKind.String => property.Value.GetString() is { Length: > 0 } text ? text : "N/A",
                JsonValueKind.Number => FormatNumber(path, property.Value),
                JsonValueKind.Array => string.Join(", ", property.Value.EnumerateArray().Select(FormatScalar)),
                _ => "N/A"
            };
            rows.Add(new DistributionDetailRow(path, value));
        }
    }

    private static string FormatScalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "N/A",
        JsonValueKind.String => value.GetString() ?? "N/A",
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Number => value.GetRawText(),
        _ => "N/A"
    };

    private static string FormatNumber(string label, JsonElement value)
    {
        if (!label.EndsWith("Bytes", StringComparison.OrdinalIgnoreCase) || !value.TryGetDecimal(out var bytes))
            return value.GetRawText();
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1) { bytes /= 1024; index++; }
        return $"{bytes.ToString("0.##", CultureInfo.CurrentCulture)} {units[index]}";
    }

    private static string Humanize(string value)
    {
        var spaced = System.Text.RegularExpressions.Regex.Replace(value, "(?<=[a-z0-9])(?=[A-Z])", " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.Replace('_', ' '));
    }
}
