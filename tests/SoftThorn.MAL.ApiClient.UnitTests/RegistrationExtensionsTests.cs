using Microsoft.Extensions.DependencyInjection;

namespace SoftThorn.MAL.ApiClient.UnitTests
{
    [Trait(Traits.CategoryTraitName, Traits.UnitTestTraitValue)]
    public sealed class RegistrationExtensionsTests
    {
        [Theory]
        [InlineData("", "http://localhost:5000/")]
        [InlineData(" ", "http://localhost:5000/")]
        [InlineData("client-id", "http://localhost:5000")]
        [InlineData("client-id", "/callback/")]
        [InlineData("client-id", "ftp://localhost:5000/")]
        [InlineData("client-id", "not a url/")]
        public void AddMalApiClient_Should_Reject_Invalid_Configuration(string clientId, string redirectUrl)
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<ArgumentException>(() => services.AddMalApiClient(new() { ClientId = clientId, RedirectUrl = redirectUrl }));

            Assert.Equal("clientConfiguration", exception.ParamName);
        }

        [Theory]
        [InlineData("http://localhost:5000/")]
        [InlineData("http://127.0.0.1:5000/callback/")]
        [InlineData("https://localhost:5001/")]
        public void AddMalApiClient_Should_Accept_Valid_Configuration(string redirectUrl)
        {
            var services = new ServiceCollection();

            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = redirectUrl });
        }

        [Fact]
        public void AddMalApiClient_Should_Throw_When_Called_Twice()
        {
            // a second call would add every message handler to the HttpClient a second time
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            Assert.Throws<InvalidOperationException>(() => services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" }));
        }

        [Fact]
        public void AddMalApiClient_Should_Share_One_Login_Client()
        {
            // IMalApiService is transient; a shared login client is what makes concurrent logins wait for each other
            var services = new ServiceCollection();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();

            Assert.Same(provider.GetRequiredService<IMalApiService>().Login, provider.GetRequiredService<IMalApiService>().Login);
        }
    }
}
