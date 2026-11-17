using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>Error messages read from the seed file compiled into this assembly, used when no database is available.</summary>
public static class ErrorMessageFallback
{
    private const string ResourceName = "error-messages.json";

    private static readonly Lazy<IReadOnlyDictionary<int, Row>> Rows = new(Load);

    public static string Get(int code, CultureInfo culture)
    {
        if (!Rows.Value.TryGetValue(code, out var row))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Error message {code}");
        }

        return culture.TwoLetterISOLanguageName == "fr" ? row.Fr : row.En;
    }

    private static Dictionary<int, Row> Load()
    {
        using var stream = typeof(ErrorMessageFallback).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        var rows = JsonSerializer.Deserialize<List<Row>>(stream, JsonOptions) ?? [];
        return rows.ToDictionary(row => row.Code);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record Row(int Code, string En, string Fr);
}
