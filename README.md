# MyAnimelist.NET

A .NET client library for the [MyAnimeList API v2](https://myanimelist.net/apiconfig/references/api/v2).

It lets you search for anime and manga, read rankings and seasonal lists, browse the forums, read and update a user's lists, and sign in through MyAnimeList's OAuth2 (PKCE) login flow. Everything plugs into `Microsoft.Extensions.DependencyInjection` and `IHttpClientFactory`.

- **Target framework:** .NET 10
- **License:** [MIT](LICENSE)
- **Status:** early development (0.x). The package isn't published on NuGet yet, so [build it from source](#building-from-source).

## Features

- **Typed API clients** generated from the official MyAnimeList OpenAPI specification
- **OAuth2 login** with PKCE: a browser window opens, the user signs in, and the library catches the redirect on a local `HttpListener`
- **Automatic authorization:** once signed in, every request gets the `Bearer` token
- **Replaceable pieces:** swap in your own token storage (`IAuthCache`) or your own way of opening the login page (`IBrowserLoginHandler`)
- **Progress reporting** during login through `IProgress<AuthProgress>`, so you can show progress in a UI

## Getting started

### 1. Register a MyAnimeList API client

1. Sign in to MyAnimeList and open [API settings](https://myanimelist.net/apiconfig).
2. Create a new client. Choose the app type that fits your application (for desktop apps, usually *other*).
3. Set the **App Redirect URL** to a local address, for example `http://localhost:5000/callback/`.
4. Write down the **Client ID**.

### 2. Register the services

```csharp
using Microsoft.Extensions.DependencyInjection;
using MyAnimelist.NET;
using MyAnimelist.NET.Authentication;

var services = new ServiceCollection();

services.AddMyAnimelist(new ClientConfiguration
{
    ClientId    = "<your client id>",
    RedirectUrl = "http://localhost:5000/callback/", // must match the URL registered with MyAnimeList
});

var provider = services.BuildServiceProvider();
var mal = provider.GetRequiredService<MyAnimelistService>();
```

> [!NOTE]
> `HttpListener` uses `RedirectUrl` as its listening prefix, so the URL must end with `/`.

### 3. Sign in

```csharp
var progress = new Progress<AuthProgress>(p => Console.WriteLine($"{p.Progress:P0} {p.Message}"));

var signedIn = await mal.Login.StartOAuthFlowAsync(progress);
```

The default `BrowserLoginHandler` opens the login page in the user's default browser. After the user approves access, the token is stored in the `IAuthCache`. Expired access tokens are refreshed automatically with the refresh token before the next request. If a valid token is already cached, or an expired one can be refreshed, `StartOAuthFlowAsync` returns `true` without opening the browser. It returns `false` if the user denies access or the token exchange fails.

### 4. Call the API

```csharp
// Search for anime
var results = await mal.Catalog.AnimeGetAsync("frieren", limit: 5, fields: "title,mean");

foreach (var entry in results.Data ?? [])
{
    Console.WriteLine(entry.Node.Title);
}

// Top-ranked anime
var ranking = await mal.Anime.RankingAsync(ranking_type: "all", limit: 10);

// A user's anime list
var list = await mal.Users.AnimelistAsync("@me", status: "watching");
```

## API overview

`MyAnimelistService` groups the endpoints into these clients:

| Property | Client | What it covers |
| --- | --- | --- |
| `Login` | `LoginClient` | OAuth2 sign-in (`StartOAuthFlowAsync`) |
| `Catalog` | `Client` | Anime search and details (`AnimeGetAsync`), manga search and details (`MangaGetAsync`), user profile (`UsersAsync`) |
| `Anime` | `AnimeClient` | Anime ranking, seasonal anime, suggestions, and updating your anime list status |
| `Manga` | `MangaClient` | Manga ranking and updating your manga list status |
| `Forum` | `ForumClient` | Forum boards, topics, and topic details |
| `Users` | `UsersClient` | A user's anime list and manga list |

Most methods take an optional `fields` parameter, a comma-separated list of the fields you want returned (for example `"title,mean,genres"`). See the [MyAnimeList API reference](https://myanimelist.net/apiconfig/references/api/v2) for the available fields and values.

When a request fails, the client throws an `ApiException`.

## Customization

`AddMyAnimelist` registers the default implementations with `TryAdd`, so anything you register first wins.

### Store tokens somewhere else

The default `AuthCache` keeps tokens in memory, so they are gone when the app restarts. To keep users signed in, implement `IAuthCache`, for example with a file or the OS credential store. Store the whole `AuthResponse`, including `ExpiresAt`; without it the library can't tell when to refresh the token.

```csharp
public sealed class FileAuthCache : IAuthCache
{
    public Task<AuthResponse?> Get(CancellationToken cancellationToken = default) { /* load */ }
    public Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default) { /* save */ }
}

services.AddSingleton<IAuthCache, FileAuthCache>();
services.AddMyAnimelist(config);
```

### Open the login page differently

Implement `IBrowserLoginHandler` to show the login page in an embedded web view, or to automate it. The test project does this with Playwright.

```csharp
services.AddSingleton<IBrowserLoginHandler, MyWebViewLoginHandler>();
services.AddMyAnimelist(config);
```

## Repository layout

```text
src/            The library
  Authentication/   OAuth flow, token cache, and the handler that adds the Bearer token
  AutoGeneratedClient.cs   API clients generated by NSwag (do not edit by hand)
  CustomizedModels.cs      Hand-written list and paging models
assets/         OpenAPI spec, NSwag config, and Liquid templates used for code generation
build/          Cake Frosting build project
tests/
  MyAnimelist.NET.UnitTests/          Tests with no network access
  MyAnimelist.NET.IntegrationTests/   Tests against the live MyAnimeList API
```

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build MyAnimelist.NET.slnx
```

The full build pipeline (clean, version stamping, tests, coverage report, packing, publishing) runs through [Cake Frosting](https://cakebuild.net/docs/running-builds/runners/cake-frosting):

```sh
./build.ps1                          # Windows / PowerShell
./build.sh                           # Linux / macOS
./build.ps1 --target UpdateOpenApiSpec
```

| Target | What it does |
| --- | --- |
| `Default` | Clean, update assembly info, run tests with coverage, pack, and push packages |
| `UpdateOpenApiSpec` | Download the latest OpenAPI spec from MyAnimeList and regenerate `src/AutoGeneratedClient.cs` with NSwag |

Packages are written to `tmp_build/packages`. Versions come from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (see [version.json](version.json) and [versioning.md](versioning.md)).

## Running the tests

The tests are split into two projects:

| Project | What it tests | Needs |
| --- | --- | --- |
| `tests/MyAnimelist.NET.UnitTests` | Library logic, with no network access | Nothing |
| `tests/MyAnimelist.NET.IntegrationTests` | Signing in to a real MyAnimeList account and calling the live API. Playwright fills in the login form automatically. | Credentials and Playwright browsers (see below) |

Both use [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro). `global.json` turns it on for `dotnet test`.

### Unit tests

```sh
dotnet test --project tests/MyAnimelist.NET.UnitTests/MyAnimelist.NET.UnitTests.csproj
```

### Integration tests

1. Install the Playwright browsers once (after building the project):

   ```sh
   pwsh tests/MyAnimelist.NET.IntegrationTests/bin/Debug/net10.0/playwright.ps1 install
   ```

2. Add your credentials as [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (Debug builds) or as environment variables:

   ```sh
   cd tests/MyAnimelist.NET.IntegrationTests
   dotnet user-secrets set "ClientConfiguration:ClientId"    "<client id>"
   dotnet user-secrets set "ClientConfiguration:RedirectUrl" "http://localhost:5000/callback/"
   dotnet user-secrets set "MalAuthSettings:UserName"        "<mal user name>"
   dotnet user-secrets set "MalAuthSettings:Password"        "<mal password>"
   ```

   As environment variables, use `__` as the separator, for example `ClientConfiguration__ClientId`.

3. Run the tests:

   ```sh
   dotnet test --project tests/MyAnimelist.NET.IntegrationTests/MyAnimelist.NET.IntegrationTests.csproj
   ```

To run both projects, use `dotnet test --solution MyAnimelist.NET.slnx` from the repository root. The solution has to be named because `Build.slnx` sits next to it. Add `--coverage` to collect code coverage.

> [!WARNING]
> Use a dedicated test account. The tests sign in with these credentials and approve API access for your client.

## License

MyAnimelist.NET is released under the [MIT License](LICENSE).
