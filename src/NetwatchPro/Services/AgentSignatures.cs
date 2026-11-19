using System.Text.Json;
using System.Windows;
using NetwatchPro.Models;

namespace NetwatchPro.Services;

/// <summary>One vendor agent signature (data, not code — see agent-signatures.json).</summary>
public sealed class AgentSignature
{
    public string Name { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Type { get; set; } = "Other";
    public string[] Patterns { get; set; } = Array.Empty<string>();

    public ComponentType ComponentType =>
        Enum.TryParse<ComponentType>(Type, true, out var t) ? t : ComponentType.Other;
}

/// <summary>Loads the vendor-neutral agent signature catalogue from bundled resources.</summary>
public static class AgentSignatures
{
    private static List<AgentSignature>? _cache;

    public static IReadOnlyList<AgentSignature> Load()
    {
        if (_cache != null) return _cache;
        var list = new List<AgentSignature>();
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/agent-signatures.json", UriKind.Absolute);
            var sri = Application.GetResourceStream(uri);
            if (sri != null)
            {
                using var stream = sri.Stream;
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("agents", out var arr))
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var sig = new AgentSignature
                        {
                            Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Vendor = el.TryGetProperty("vendor", out var v) ? v.GetString() ?? "" : "",
                            Type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "Other" : "Other",
                        };
                        if (el.TryGetProperty("patterns", out var pats) && pats.ValueKind == JsonValueKind.Array)
                            sig.Patterns = pats.EnumerateArray()
                                .Select(p => p.GetString() ?? "")
                                .Where(s => s.Length > 0)
                                .ToArray();
                        if (sig.Name.Length > 0) list.Add(sig);
                    }
                }
            }
        }
        catch { /* fall back to empty catalogue */ }
        _cache = list;
        return list;
    }
}
