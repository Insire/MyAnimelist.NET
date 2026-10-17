using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SoftThorn.MAL.ApiClient
{
    // The JSON options of the API clients of one service provider (a singleton), so deserialization can log.
    // Response fields with a fixed set of values are strings, so a value MAL adds later can't fail a whole response;
    // instead, a value outside the matching enum is logged as a warning, once per field and value.
    internal sealed partial class MalJsonOptions
    {
        private static readonly IReadOnlyList<string> Seasons = GetJsonNames<Season>();

        private readonly ILogger<MalJsonOptions> _logger;
        private readonly ConcurrentDictionary<(string Field, string Value), bool> _reported = new();

        public MalJsonOptions(ILogger<MalJsonOptions> logger)
        {
            _logger = logger;

            Options = new JsonSerializerOptions
            {
                TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { CheckKnownValues } },
            };
            MalJsonSerializerSettings.Update(Options);
        }

        public JsonSerializerOptions Options { get; }

        private void CheckKnownValues(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type == typeof(AnimeSeason))
            {
                typeInfo.OnDeserialized = value => Check("AnimeSeason.season", ((AnimeSeason)value).Season, Seasons);
            }
        }

        // a page repeats the same value in many entries, so each unknown value is logged only once
        private void Check(string field, string? value, IReadOnlyList<string> knownValues)
        {
            if (value is not null && !knownValues.Contains(value, StringComparer.Ordinal) && _reported.TryAdd((field, value), true))
            {
                LogUnknownValue(value, field, string.Join(", ", knownValues));
            }
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "MyAnimeList sent '{Value}' for {Field}, which isn't one of the known values ({KnownValues}). The value is passed on as it is; a newer version of the library may know it")]
        private partial void LogUnknownValue(string value, string field, string knownValues);

        // the values MAL uses on the wire, so the check can't drift from the enum
        private static string[] GetJsonNames<TEnum>()
            where TEnum : struct, Enum
            => typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name)
                .ToArray();
    }
}
