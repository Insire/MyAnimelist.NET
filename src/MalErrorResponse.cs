using System.Text.Json;

namespace SoftThorn.MAL.ApiClient
{
    // Reads MAL's error bodies for ApiException (see assets/Templates/File.liquid): {"message":"invalid q","error":"bad_request"},
    // or only {"error":"invalid_token"}. MAL's spec doesn't declare them.
    internal static class MalErrorResponse
    {
        public static (string? Code, string? Message) Parse(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return (null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;

                return root.ValueKind == JsonValueKind.Object
                    ? (GetString(root, "error"), GetString(root, "message"))
                    : (null, null);
            }
            catch (JsonException)
            {
                // e.g. the HTML page MAL's edge sends when it throttles
                return (null, null);
            }
        }

        // MAL sends an empty message for some errors ({"message":"","error":"not_found"})
        private static string? GetString(JsonElement element, string name)
            => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
                ? text
                : null;
    }
}
