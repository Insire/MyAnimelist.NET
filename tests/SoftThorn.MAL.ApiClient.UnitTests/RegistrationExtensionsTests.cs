using Microsoft.Extensions.DependencyInjection;
using SoftThorn.MAL.ApiClient.Authentication;

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
        public void AddMalApiClient_Should_Accept_Valid_Configuration(string redirectUrl)
        {
            var services = new ServiceCollection();

            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = redirectUrl });
        }

        [Fact]
        public void AddMalApiClient_Should_Reject_Https_Redirect_Url_And_Explain_Why()
        {
            // HttpListener can't answer https without a certificate bound to the port, so the login would hang after the sign-in
            var services = new ServiceCollection();

            var exception = Assert.Throws<ArgumentException>(() => services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "https://localhost:5001/callback/" }));

            Assert.Equal("clientConfiguration", exception.ParamName);
            Assert.Contains("certificate", exception.Message);
            Assert.Contains("http://localhost:5000/callback/", exception.Message);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task IsSignedInAsync_Should_Report_The_Cached_Token_Without_Opening_The_Login_Page(bool hasToken)
        {
            var authCache = new AuthCache();
            if (hasToken)
            {
                await authCache.SaveAsync(new() { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600, TokenType = "Bearer", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }, TestContext.Current.CancellationToken);
            }

            var services = new ServiceCollection();
            services.AddSingleton<IAuthCache>(authCache);
            services.AddSingleton<IBrowserLoginHandler, FailingBrowserLoginHandler>();
            services.AddMalApiClient(new() { ClientId = "client-id", RedirectUrl = "http://localhost:5000/" });

            using var provider = services.BuildServiceProvider();

            Assert.Equal(hasToken, await provider.GetRequiredService<IMalApiService>().Login.IsSignedInAsync(TestContext.Current.CancellationToken));
        }

        private sealed class FailingBrowserLoginHandler : IBrowserLoginHandler
        {
            public ValueTask HandleAsync(string authUrl, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("IsSignedInAsync must not open the login page.");
        }
    }
}
