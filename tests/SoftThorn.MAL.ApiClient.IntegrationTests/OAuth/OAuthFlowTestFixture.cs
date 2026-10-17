using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;
using SoftThorn.MAL.ApiClient.Authentication;
using SoftThorn.MAL.ApiClient.IntegrationTests.OAuth;
using System.Runtime.ExceptionServices;

[assembly: AssemblyFixture(typeof(OAuthFlowTestFixture))]

namespace SoftThorn.MAL.ApiClient.IntegrationTests.OAuth
{
    public sealed class OAuthFlowTestFixture : PageTest, IAsyncLifetime, IBrowserLoginHandler
    {
        private readonly ServiceCollection _services;
        private readonly ServiceProvider _serviceProvider;
        private readonly MalAuthSettings _malAuthSettings;
        private readonly ILogger<OAuthFlowTestFixture> _logger;
        private readonly TestOutputLoggerProvider _testOutput = new();
        private ExceptionDispatchInfo? _loginFailure;
        private string _loginLog = string.Empty;

        /// <summary>
        /// The service, logged in. Skips the test when the login failed, since <see cref="OAuthFlowTests"/> reports that.
        /// </summary>
        public MalApiService GetMalApiService()
        {
            if (_loginFailure is { } failure)
            {
                Assert.Skip($"The login failed ({failure.SourceException.Message}); {nameof(OAuthFlowTests)} reports it with the login's log.");
            }

            return _serviceProvider.GetRequiredService<MalApiService>();
        }

        /// <summary>
        /// Rethrows the exception of a failed login, after writing the login's log to the test's output.
        /// </summary>
        internal void ThrowIfLoginFailed()
        {
            if (_loginFailure is null)
            {
                return;
            }

            TestContext.Current.TestOutputHelper?.WriteLine($"Log of the login:{Environment.NewLine}{_loginLog}");
            _loginFailure.Throw();
        }

        internal AuthTestProgressCollector GetAuthTestProgressCollector()
            => _serviceProvider.GetRequiredService<AuthTestProgressCollector>();

        public OAuthFlowTestFixture()
        {
            // user secrets in every configuration, since the Cake build runs the tests in Release; CI has no secrets file and uses the environment variables, which win
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<OAuthFlowTestFixture>()
                .AddEnvironmentVariables()
                .Build();

            var sectionName = nameof(ClientConfiguration);
            var clientConfiguration = new ClientConfigurationOptions();

            var section = configuration.GetSection(sectionName);
            section.Bind(clientConfiguration);

            sectionName = nameof(MalAuthSettings);
            var malAuthSettings = new MalAuthSettings();

            section = configuration.GetSection(sectionName);
            section.Bind(malAuthSettings);

            _malAuthSettings = malAuthSettings;

            _services = new ServiceCollection();

            // only into the tests' output (and from there into the trx), never into files
            _services.AddLogging(builder => builder
                .AddProvider(_testOutput)
                .AddFilter(IsLogged));

            _services
                .AddSingleton<AuthTestProgressCollector>()
                .AddSingleton<IBrowserLoginHandler>(this)
                .AddTransient<HttpLoggingHandler>()
                .AddHttpClient<MalApiService>(client => client.BaseAddress = new Uri(RegistrationExtensions.API_URI))
                .AddHttpMessageHandler<HttpLoggingHandler>();

            _services.AddMalApiClient(new ClientConfiguration()
            {
                ClientId = clientConfiguration.ClientId!,
                RedirectUrl = clientConfiguration.RedirectUrl!,
            });

            _serviceProvider = _services.BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<OAuthFlowTestFixture>>();
        }

        // IHttpClientFactory's own logging: ClientHandler logs every attempt, so retries show up; LogicalHandler would repeat HttpLoggingHandler.
        // Below Information it logs the (redacted) headers.
        private static bool IsLogged(string? category, LogLevel level)
            => level >= (category?.EndsWith(".LogicalHandler", StringComparison.Ordinal) == true ? LogLevel.Warning : LogLevel.Information);

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();

            var progress = GetAuthTestProgressCollector();
            var sut = GetMalApiService();

            _logger.LogInformation("Logging in to MAL");
            try
            {
                await sut.Login.StartOAuthFlowAsync(progress, TestContext.Current.CancellationToken);
            }
            catch (Exception exception) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                // not thrown here, or every test would fail with it: OAuthFlowTests, which runs first, reports it and the other tests are skipped
                _logger.LogError(exception, "The login failed");
                _loginFailure = ExceptionDispatchInfo.Capture(exception);
                _loginLog = _testOutput.TakePending();
                return;
            }

            _logger.LogInformation("Logged in to MAL");
        }

        public override async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();

            await base.DisposeAsync();
        }

        public async ValueTask Handle(string authUrl)
        {
            await Page.GotoAsync(authUrl);

            await Page.Locator("#loginUserName").FillAsync(_malAuthSettings.UserName);
            await Page.Locator("#login-password").FillAsync(_malAuthSettings.Password);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Allow" }).ClickAsync();
        }
    }
}