namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    // runs before every other test (LoginFirstTestCollectionOrderer), so a failed login fails here; the other tests are skipped
    [Collection(LoginTestCollection.Name)]
    [Trait(Traits.CategoryTraitName, Traits.IntegrationTestTraitValue)]
    public sealed class OAuthFlowTests(OAuthFlowTestFixture fixture)
    {
        [Fact]
        public async Task AuthFlow_Should_Work_And_Report_Expected_Progress()
        {
            // Arrange
            fixture.ThrowIfLoginFailed(); // the fixture logged in before any test ran
            var progress = fixture.GetAuthTestProgressCollector();
            var sut = fixture.GetMalApiService();

            // Act
            await sut.Login.StartOAuthFlowAsync(progress, TestContext.Current.CancellationToken);

            // Assert
            await Verify(progress.AllProgress());
        }
    }
}