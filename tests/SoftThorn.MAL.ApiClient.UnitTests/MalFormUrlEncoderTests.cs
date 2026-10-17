using System.Text.Json;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalFormUrlEncoderTests
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        [Fact]
        public void Encode_Should_Write_Numbers_Booleans_Strings_And_Arrays_As_Form_Values()
        {
            // MAL takes the tags comma-separated
            var body = new AnimeListStatusPatch { Status = AnimeWatchStatus.Watching, Score = 7, IsRewatching = false, Tags = ["a", "b"] };

            var fields = MalFormUrlEncoder.Encode(body, Options);

            Assert.Equal(
                [new("status", "watching"), new("is_rewatching", "false"), new("score", "7"), new("tags", "a,b")],
                fields);
        }

        [Fact]
        public void Encode_Should_Leave_Out_Unset_Fields()
        {
            var fields = MalFormUrlEncoder.Encode(new MangaListStatusPatch { Score = 9 }, Options);

            Assert.Equal([new("score", "9")], fields);
        }

        [Fact]
        public void Encode_Should_Keep_Empty_Strings()
        {
            // an empty date is how MAL removes it, so it must not be left out like an unset one
            var fields = MalFormUrlEncoder.Encode(new AnimeListStatusPatch { StartDate = "2017-10", FinishDate = "" }, Options);

            Assert.Equal([new("start_date", "2017-10"), new("finish_date", "")], fields);
        }

        [Fact]
        public async Task Encode_Should_Produce_Url_Encoded_Content()
        {
            // the enum member is sent as MAL's value (JsonStringEnumMemberName), not as its C# name
            var body = new AnimeListStatusPatch { Status = AnimeWatchStatus.PlanToWatch, Comments = "a & b" };

            using var content = new FormUrlEncodedContent(MalFormUrlEncoder.Encode(body, Options));

            Assert.Equal("status=plan_to_watch&comments=a+%26+b", await content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Encode_Should_Return_No_Fields_For_Null_Body()
        {
            Assert.Empty(MalFormUrlEncoder.Encode<AnimeListStatusPatch>(null, Options));
        }
    }
}
