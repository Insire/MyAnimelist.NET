using System.Reflection;
using System.Text.RegularExpressions;

namespace MyAnimelist.NET.IntegrationTests.Snapshots
{
    internal static class SnapshotSettings
    {
        // the rank, score and member counters change every day, the rest of a finished anime/manga does not
        private static readonly HashSet<string> _volatileMembers =
        [
            nameof(WorkForList.Mean),
            nameof(WorkForList.Rank),
            nameof(WorkForList.Popularity),
            nameof(WorkForList.Num_list_users),
            nameof(WorkForList.Num_scoring_users),
        ];

        // every member of these types keeps changing (statistics.status counters, the recommended works and their counts)
        private static readonly HashSet<Type> _volatileTypes =
        [
            typeof(Status),
            typeof(AnimeRecommendationAggregationEdgeBase),
            typeof(MangaRecommendationAggregationEdgeBase),
        ];

        // MAL's CDN serves the same picture as .jpg or .webp, changing between requests
        private static readonly Regex _imageExtension = new(@"(?<path>cdn\.myanimelist\.net/\S+?)\.(jpg|jpeg|png|webp)\b", RegexOptions.Compiled);

        /// <summary>
        /// Snapshot the actual values of a response for an entity that doesn't change anymore,
        /// with the counters that keep moving scrubbed.
        /// </summary>
        public static VerifySettings ForStableEntity()
        {
            var settings = new VerifySettings();
            settings.ScrubMembers(IsVolatile);
            settings.AddExtraSettings(serializer => serializer.Converters.Add(new ImageUriConverter()));

            return settings;
        }

        // line scrubbers don't reach Uri values, so normalize them while they are written
        private sealed class ImageUriConverter : WriteOnlyJsonConverter<Uri>
        {
            public override void Write(VerifyJsonWriter writer, Uri value)
                => writer.WriteValue(_imageExtension.Replace(value.ToString(), "${path}.{image}"));
        }

        // unmapped fields always stay visible, they are what these snapshots are meant to catch
        private static bool IsVolatile(MemberInfo member)
            => member.Name != nameof(Status.AdditionalProperties)
            && (_volatileMembers.Contains(member.Name) || (member.DeclaringType is not null && _volatileTypes.Contains(member.DeclaringType)));
    }
}
