using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WSLManager.Desktop.Core.Backend;

public static class DiagnosticsRedactor
{
    private static readonly Regex SensitiveConfigLine = new(
        @"^(\s*)(kernel(?:commandline)?|command|[^=]*(?:password|token|secret|credential)[^=]*)(\s*=).*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SecretAssignment = new(
        @"(?i)\b([A-Za-z0-9_-]*(?:password|passwd|token|secret|credential)[A-Za-z0-9_-]*)\b(\s*[:=]\s*)(?:""[^""]*""|'[^']*'|[^\s;,]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Redact(string json, string? userProfile = null, string? localAppData = null)
    {
        var root = JsonNode.Parse(json) ?? throw new JsonException("Diagnostics JSON was empty.");
        RedactNode(root, null, userProfile, localAppData);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void RedactNode(JsonNode node, string? propertyName, string? userProfile, string? localAppData)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key.Equals("Computer", StringComparison.OrdinalIgnoreCase))
                {
                    obj[pair.Key] = "[local computer]";
                    continue;
                }

                if (pair.Value is not null)
                    RedactNode(pair.Value, pair.Key, userProfile, localAppData);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
                if (item is not null) RedactNode(item, propertyName, userProfile, localAppData);
            return;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
            return;

        if (propertyName?.Equals("Content", StringComparison.OrdinalIgnoreCase) == true)
            text = string.Join(Environment.NewLine, text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => SensitiveConfigLine.Replace(line, "$1$2$3[redacted]")));

        node.ReplaceWith(JsonValue.Create(RedactText(text, userProfile, localAppData)));
    }

    public static string RedactText(string text, string? userProfile = null, string? localAppData = null)
    {
        text = SecretAssignment.Replace(text, "$1$2[redacted]");
        if (!string.IsNullOrWhiteSpace(localAppData))
            text = text.Replace(localAppData, "%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(userProfile))
            text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
