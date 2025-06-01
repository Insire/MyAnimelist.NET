using MyAnimelist.NET.Tests.OAuth;

namespace MyAnimelist.NET.Tests
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class OAuthFlowTests
    {
        [Fact]
        public async Task AuthFlow_Should_Work_And_Report_Expected_Progress()
        {
            // Arrange
            var progress = new AuthTestProgressCollector();
            using var fixture = new OAuthFlowTestFixture();
            var sut = fixture.GetMyAnimelistService();

            // Act
            await sut.Login.StartOAuthFlowAsync(progress);

            // Assert
            await Verify(progress.AllProgress());
        }
    }
}