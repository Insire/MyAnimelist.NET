using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestCollectionOrderer(typeof(LoginFirstTestCollectionOrderer))]

namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    /// <summary>
    /// The collection of <see cref="OAuthFlowTests"/>, which runs before every other collection.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class LoginTestCollection
    {
        public const string Name = "Login";
    }

    /// <summary>
    /// Runs <see cref="LoginTestCollection"/> first and the others in xUnit's default order. When the login fails,
    /// <see cref="OAuthFlowTests"/> is the test that reports it, with the login's log, and the tests after it are skipped.
    /// The tests run one at a time (see Properties/AssemblyInfo.cs), so first means before any other test starts.
    /// </summary>
    public sealed class LoginFirstTestCollectionOrderer : ITestCollectionOrderer
    {
        public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection> testCollections)
            where TTestCollection : notnull, ITestCollection
            => DefaultTestCollectionOrderer.Instance.OrderTestCollections(testCollections)
                .OrderBy(collection => collection.TestCollectionDisplayName == LoginTestCollection.Name ? 0 : 1) // stable, so the default order stays
                .ToArray();
    }
}
