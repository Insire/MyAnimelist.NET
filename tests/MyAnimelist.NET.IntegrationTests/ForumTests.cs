using MyAnimelist.NET.IntegrationTests.OAuth;
using MyAnimelist.NET.IntegrationTests.Snapshots;

namespace MyAnimelist.NET.IntegrationTests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class ForumTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task BoardsAsync_Should_Return_Expected_Data()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Forum.BoardsAsync(TestContext.Current.CancellationToken);

            // Assert
            await Verify(response, SnapshotSettings.ForStableEntity());
        }

        [Fact]
        public async Task TopicsAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();

            // Act
            var response = await sut.Forum.TopicsAsync(q: "cowboy bebop", limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!);
            await Verify(ModelShape.Of(response));
        }

        [Fact]
        public async Task TopicAsync_Should_Return_Expected_Structure()
        {
            // Arrange
            var sut = fixture.GetMyAnimelistService();
            var topics = await sut.Forum.TopicsAsync(q: "cowboy bebop", limit: 1, cancellationToken: TestContext.Current.CancellationToken);
            var topicId = Assert.Single(topics.Data!).Id!.Value;

            // Act
            var response = await sut.Forum.TopicAsync(topicId, limit: 5, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEmpty(response.Data!.Posts!);
            await Verify(ModelShape.Of(response));
        }
    }
}
