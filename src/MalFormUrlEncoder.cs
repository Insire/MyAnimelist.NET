using System.Globalization;
using System.Text.Json;

namespace SoftThorn.MAL.ApiClient
{
    /// <summary>
    /// Turns a request body model into form fields for the generated clients (see assets/Templates/Client.Class.liquid).
    /// Unset (null) properties are left out, so a PATCH only changes the fields that were set.
    /// </summary>
    internal static class MalFormUrlEncoder
    {
        public static IEnumerable<KeyValuePair<string, string>> Encode<T>(T? body, JsonSerializerOptions options)
        {
            if (body is null)
            {
                return [];
            }

            var json = JsonSerializer.SerializeToElement(body, options);
            var fields = new List<KeyValuePair<string, string>>();

            foreach (var property in json.EnumerateObject())
            {
                var value = ToFormValue(property.Name, property.Value);
                if (value is not null)
                {
                    fields.Add(new(property.Name, value));
                }
            }

            return fields;
        }

        private static string? ToFormValue(string name, JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => string.Join(',', value.EnumerateArray().Select(item => ToFormValue(name, item))),
            _ => throw new NotSupportedException(string.Format(CultureInfo.InvariantCulture, "The form field '{0}' is a {1}, which can't be sent as a form value.", name, value.ValueKind)),
        };
    }
}
