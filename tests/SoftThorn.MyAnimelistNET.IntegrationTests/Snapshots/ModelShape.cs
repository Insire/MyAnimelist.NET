using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoftThorn.MyAnimelistNET.IntegrationTests.Snapshots
{
    /// <summary>
    /// Projects a deserialized response onto its structure: every populated member becomes its type name,
    /// collections collapse into the merged shape of their elements and null members are left out.
    /// Unlike the raw values this stays stable while the live data changes, but still shows when a field
    /// stops deserializing (it disappears) or when MAL returns a field our models don't map (it shows up under AdditionalProperties).
    /// </summary>
    internal static class ModelShape
    {
        public static object? Of(object? value)
        {
            switch (value)
            {
                case null:
                    return null;

                case JsonElement element:
                    return OfJson(element);

                case string:
                    return "string";

                case bool:
                    return "bool";

                case int or long:
                    return "int";

                case float or double or decimal:
                    return "number";

                case DateTimeOffset or DateTime:
                    return "date";

                case Uri:
                    return "uri";

                case Enum:
                    return "enum";

                case IDictionary dictionary:
                    {
                        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                        foreach (DictionaryEntry entry in dictionary)
                        {
                            Add(result, entry.Key.ToString()!, Of(entry.Value));
                        }

                        return result.Count == 0 ? null : result;
                    }

                case IEnumerable enumerable:
                    return OfCollection(enumerable.Cast<object?>().Select(Of));

                default:
                    {
                        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (property.GetIndexParameters().Length > 0)
                            {
                                continue;
                            }

                            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;
                            Add(result, name, Of(property.GetValue(value)));
                        }

                        return result;
                    }
            }
        }

        private static object? OfJson(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var property in element.EnumerateObject())
                        {
                            Add(result, property.Name, OfJson(property.Value));
                        }

                        return result;
                    }

                case JsonValueKind.Array:
                    return OfCollection(element.EnumerateArray().Select(OfJson));

                case JsonValueKind.String:
                    return "string";

                case JsonValueKind.Number:
                    return "number";

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return "bool";

                default:
                    return null;
            }
        }

        private static object[] OfCollection(IEnumerable<object?> shapes)
        {
            object? merged = null;
            foreach (var shape in shapes)
            {
                merged = Merge(merged, shape);
            }

            return merged is null ? [] : [merged];
        }

        private static void Add(SortedDictionary<string, object?> result, string name, object? shape)
        {
            if (shape is not null)
            {
                result[name] = shape;
            }
        }

        private static object? Merge(object? left, object? right)
        {
            if (left is null)
            {
                return right;
            }

            if (right is null)
            {
                return left;
            }

            if (left is SortedDictionary<string, object?> leftObject && right is SortedDictionary<string, object?> rightObject)
            {
                var result = new SortedDictionary<string, object?>(leftObject, StringComparer.Ordinal);
                foreach (var (key, value) in rightObject)
                {
                    result[key] = Merge(result.GetValueOrDefault(key), value);
                }

                return result;
            }

            if (left is object[] leftArray && right is object[] rightArray)
            {
                return OfCollection(leftArray.Concat(rightArray));
            }

            if (left is string leftType && right is string rightType)
            {
                return string.Join('|', leftType.Split('|').Concat(rightType.Split('|')).Distinct().Order(StringComparer.Ordinal));
            }

            return left;
        }
    }
}
