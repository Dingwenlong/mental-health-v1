using System.Collections.ObjectModel;
using System.Text.Json;
using MentalHealth.Application.Abstractions;

namespace MentalHealth.Infrastructure.Content;

public sealed class JsonUiCopyCatalog : IUiCopyCatalog
{
    private readonly IReadOnlyDictionary<string, string> values;

    public JsonUiCopyCatalog(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("UI copy catalog must be a JSON object.");
        }

        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    $"UI copy value '{property.Name}' must be a string.");
            }

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    $"UI copy value '{property.Name}' cannot be empty.");
            }

            if (!loaded.TryAdd(property.Name, value))
            {
                throw new InvalidDataException(
                    $"Duplicate UI copy key '{property.Name}'.");
            }
        }

        values = new ReadOnlyDictionary<string, string>(loaded);
    }

    public string Get(string key) => values.TryGetValue(key, out var value)
        ? value
        : throw new KeyNotFoundException($"UI copy key '{key}' was not found.");
}
