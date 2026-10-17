namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class MalErrorResponseTests
    {
        [Theory]
        [InlineData("""{"message":"invalid q","error":"bad_request"}""", "bad_request", "invalid q")]
        [InlineData("""{"error":"invalid_token"}""", "invalid_token", null)]
        [InlineData("""{"message":"","error":"not_found"}""", "not_found", null)]
        public void Parse_Should_Read_Error_And_Message(string response, string? expectedCode, string? expectedMessage)
        {
            var (code, message) = MalErrorResponse.Parse(response);

            Assert.Equal(expectedCode, code);
            Assert.Equal(expectedMessage, message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<!DOCTYPE HTML><html><body>Access Denied</body></html>")]
        [InlineData("""["not", "an", "object"]""")]
        [InlineData("""{"error":42}""")]
        public void Parse_Should_Return_Nothing_For_Other_Bodies(string? response)
        {
            Assert.Equal((null, null), MalErrorResponse.Parse(response));
        }

        [Fact]
        public void ApiException_Should_Expose_MyAnimeLists_Error()
        {
            var exception = new ApiException("MyAnimeList answered with the error status 400.", 400, """{"message":"limit","error":"bad_request"}""", new Dictionary<string, IEnumerable<string>>(), null);

            Assert.Equal("bad_request", exception.ErrorCode);
            Assert.Equal("limit", exception.ErrorMessage);
        }

        [Fact]
        public void ApiException_Public_Constructor_Should_Match_The_Clients_Exception()
        {
            // consumers throw it from mocks of the client interfaces
            var exception = new ApiException(404, """{"message":"","error":"not_found"}""");

            Assert.Equal(404, exception.StatusCode);
            Assert.Equal("not_found", exception.ErrorCode);
            Assert.Null(exception.ErrorMessage);
            Assert.Empty(exception.Headers);
            Assert.StartsWith("MyAnimeList answered with the error status 404.", exception.Message);
        }
    }
}
