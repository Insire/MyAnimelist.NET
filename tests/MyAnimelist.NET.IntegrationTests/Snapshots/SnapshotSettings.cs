using System.Reflection;

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

        /// <summary>
        /// Snapshot the actual values of a response for an entity that doesn't change anymore,
        /// with the counters that keep moving scrubbed.
        /// </summary>
        public static VerifySettings ForStableEntity()
        {
            var settings = new VerifySettings();
            settings.ScrubMembers(IsVolatile);

            return settings;
        }

        // unmapped fields always stay visible, they are what these snapshots are meant to catch
        private static bool IsVolatile(MemberInfo member)
            => member.Name != nameof(Status.AdditionalProperties)
            && (_volatileMembers.Contains(member.Name) || (member.DeclaringType is not null && _volatileTypes.Contains(member.DeclaringType)));
    }
}
