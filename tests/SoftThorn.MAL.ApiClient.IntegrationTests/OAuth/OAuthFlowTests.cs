namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class OAuthFlowTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task AuthFlow_Should_Work_And_Report_Expected_Progress()
        {
            // Arrange
            var progress = fixture.GetAuthTestProgressCollector();
            var sut = fixture.GetMalApiService();

            // Act
            await sut.Login.StartOAuthFlowAsync(progress, TestContext.Current.CancellationToken);

            // Assert
            await Verify(progress.AllProgress());
        }
    }
}