using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using SoftThorn.MAL.ApiClient.IntegrationTests.Snapshots;

namespace SoftThorn.MAL.ApiClient.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class ForumTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task GetBoardsAsync_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Forum.GetBoardsAsync(TestContext.Current.CancellationToken);

            // Assert
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task GetTopicsAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();

            // Act
            var response = await sut.Forum.GetTopicsAsync(q: "cowboy bebop", limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task GetTopicAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMalApiService();
            var topics = await sut.Forum.GetTopicsAsync(q: "cowboy bebop", limit: 1, cancellationToken: TestContext.Current.CancellationToken);
            var topicId = Assert.Single(topics.Data).Id;

            // Act
            var response = await sut.Forum.GetTopicAsync(topicId, limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!.Posts!);
            await Verify(ModelShape.Of(response));
        }
    }
}
