using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots
{
    /// <summary>
    /// Projects a deserialized response onto its structure: every populated member becomes its type name,
    /// collections collapse into the merged shape of their elements and null members are left out.
    /// Unlike the raw values this stays stable while the live data changes, but still shows when a field
    /// stops deserializing (it disappears) or when MAL returns a field our models don't map (it shows up under AdditionalProperties).
    /// A required member (UpdateOpenApiSpecTask.RequiredProperties) that is null shows up as <see cref="MissingRequired"/>.
    /// </summary>
    internal static class ModelShape
    {
        public const string MissingRequired = "MISSING, although required";

        /// <summary>
        /// Members whose presence depends on the test account's lists rather than on the model:
        /// MAL only sends <c>my_list_status</c> for works on the account's list, which the list status tests change.
        /// They are covered by <see cref="ListStatusTests"/> instead.
        /// </summary>
        public static readonly string[] AccountDependent = ["my_list_status"];

        public static object? Of(object? value, string[] ignoredMembers) => Without(Of(value), ignoredMembers);

        private static object? Without(object? shape, string[] ignoredMembers)
        {
            switch (shape)
            {
                case SortedDictionary<string, object?> members:
                    foreach (var name in ignoredMembers)
                    {
                        members.Remove(name);
                    }

                    foreach (var (_, member) in members)
                    {
                        Without(member, ignoredMembers);
                    }

                    break;

                case object[] elements:
                    foreach (var element in elements)
                    {
                        Without(element, ignoredMembers);
                    }

                    break;
            }

            return shape;
        }

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
                            var shape = Of(property.GetValue(value));

                            // required members are non-nullable, but nothing enforces that while deserializing (value types read as 0)
                            if (shape is null && property.IsDefined(typeof(RequiredAttribute)))
                            {
                                shape = MissingRequired;
                            }

                            Add(result, name, shape);
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
