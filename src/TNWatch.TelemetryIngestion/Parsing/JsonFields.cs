using System.Globalization;
using System.Text.Json;

namespace TNWatch.TelemetryIngestion.Parsing;

/// <summary>Tolerant accessors for JSON fields that may be missing, null, numbers or numeric strings.</summary>
internal static class JsonFields
{
    public static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            }
        }

        value = default;
        return false;
    }

    public static double? Number(JsonElement obj, string name)
    {
        if (!TryGet(obj, name, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new FormatException($"Field '{name}' is not a number."),
        };
    }

    /// <summary>Returns the first present field among <paramref name="candidates"/> scaled by its factor.</summary>
    public static double? FirstNumber(JsonElement obj, params (string Name, double Factor)[] candidates)
    {
        foreach (var (name, factor) in candidates)
        {
            var value = Number(obj, name);
            if (value is not null)
            {
                return value.Value * factor;
            }
        }

        return null;
    }

    public static string? String(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGet(obj, name, out var v))
            {
                var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s.Trim();
                }
            }
        }

        return null;
    }
}
