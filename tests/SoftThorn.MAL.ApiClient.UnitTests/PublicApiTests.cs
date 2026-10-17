using System.CodeDom.Compiler;
using PublicApiGenerator;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class PublicApiTests
    {
        [Fact]
        public Task PublicApi_Should_Not_Change_Unnoticed()
        {
            var options = new ApiGeneratorOptions
            {
                // version, copyright year and InternalsVisibleTo aren't API, and the version attributes only exist in a Cake build
                IncludeAssemblyAttributes = false,
                // carries NSwag's version, which would change the snapshot on every NSwag update
                ExcludeAttributes = [typeof(GeneratedCodeAttribute).FullName!],
            };

            var publicApi = typeof(IMalApiService).Assembly.GeneratePublicApi(options);

            return Verify(publicApi);
        }
    }
}
