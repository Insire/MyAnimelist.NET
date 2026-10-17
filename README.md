# MyAnimelist.NET

![MyAnimelist.NET logo: an anime eye with curly braces as the pupil](https://raw.githubusercontent.com/Insire/MyAnimelist.NET/main/assets/Icon/icon.png)

[![NuGet](https://img.shields.io/nuget/v/MyAnimelist.NET.svg)](https://www.nuget.org/packages/MyAnimelist.NET)
[![codecov](https://codecov.io/gh/Insire/MyAnimelist.NET/branch/main/graph/badge.svg)](https://codecov.io/gh/Insire/MyAnimelist.NET)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/Insire/MyAnimelist.NET/blob/main/LICENSE)

A .NET client library for the [MyAnimeList API v2](https://myanimelist.net/apiconfig/references/api/v2).

It lets you search for anime and manga, read rankings and seasonal lists, browse the forums, read and update a user's lists, and sign in through MyAnimeList's OAuth2 (PKCE) login flow. Everything plugs into `Microsoft.Extensions.DependencyInjection` and `IHttpClientFactory`.

- **Target framework:** .NET 10
- **License:** [MIT](https://github.com/Insire/MyAnimelist.NET/blob/main/LICENSE)
- **Status:** early development (0.x). Breaking changes can happen in any 0.x release, see [versioning.md](https://github.com/Insire/MyAnimelist.NET/blob/main/versioning.md).

> [!IMPORTANT]
> This is an unofficial, community-made library. It is not affiliated with, endorsed by, or supported by MyAnimeList. By using the MyAnimeList API through this library you agree to MyAnimeList's [API License and Developer Agreement](https://myanimelist.net/static/apiagreement.html).

## Installation

```sh
dotnet add package MyAnimelist.NET
```

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

## Known limitations

- **Tokens are kept in memory by default.** Users have to sign in again after every restart unless you [store tokens somewhere else](#store-tokens-somewhere-else).
- **Desktop and console apps only.** The login waits for the redirect on a local `HttpListener`, so it doesn't fit server-side web apps, where the redirect goes to your web server instead.
- **Public clients only.** The library sends a Client ID but no client secret.
- **PKCE uses `code_challenge_method=plain`**, because that is the only method MyAnimeList supports.
- **No retry on `401 Unauthorized`.** Tokens are refreshed shortly before they expire. If MyAnimeList revokes a token earlier, requests fail until the user signs in again.
- **User lists have no `fields` parameter.** MyAnimeList's OpenAPI spec doesn't define one for `Users.AnimelistAsync` and `Users.MangalistAsync`, so `list_status` can't be requested and each entry only contains id, title and picture.

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
| `Default` | Clean, update assembly info, run tests with coverage, pack, and push packages (to nuget.org only on Azure Pipelines) |
| `ReleaseNotes` | Write release notes for the commits since the last `v*` tag to `tmp_build/results/release-notes.md` (and a plain-text version for the package) |
| `UpdateOpenApiSpec` | Download the latest OpenAPI spec from MyAnimeList and regenerate `src/AutoGeneratedClient.cs` with NSwag |

Packages are written to `tmp_build/packages`. CI runs on Azure Pipelines, see [azure-pipelines.yml](https://github.com/Insire/MyAnimelist.NET/blob/main/azure-pipelines.yml). Versions come from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (see [version.json](https://github.com/Insire/MyAnimelist.NET/blob/main/version.json) and [versioning.md](https://github.com/Insire/MyAnimelist.NET/blob/main/versioning.md)).

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

The integration tests compare responses with [Verify](https://github.com/VerifyTests/Verify) snapshots (`*.verified.txt`). Lists, rankings and the test account's profile change over time, so those tests only snapshot the shape of the response, not its values. Before you accept a changed snapshot, check that the diff comes from changed data and not from a model that stopped deserializing.

## Contributing

Issues and pull requests are welcome. A few things to know:

- **Don't edit `src/AutoGeneratedClient.cs` by hand.** NSwag generates it from `assets/`. Change the spec or the Liquid templates and regenerate with `./build.ps1 --target UpdateOpenApiSpec`.
- **Fix spec errors in the build, not only in the JSON.** MyAnimeList's published spec doesn't always match the live API. `UpdateOpenApiSpec` re-downloads the spec and then patches it in `build/Tasks/UpdateOpenApiSpecTask.cs`. A fix made only in `assets/MyAnimelist.NET_OpenApiSpec.json` is lost on the next download.
- **Put tests in the right project.** Tests that don't call MyAnimeList go in `MyAnimelist.NET.UnitTests`, which must never use the network.
- **Use [conventional commits](https://www.conventionalcommits.org).** Release notes are generated from commit messages, grouped by type (`feat`, `fix`, `perf`, `docs`, `chore(deps)`, ...). Mark breaking changes with `!` after the type (`feat(auth)!: ...`) or a `BREAKING CHANGE: <what users have to change>` footer. To put a sentence into the release's highlights, add a `Release-Note: <sentence>` footer.
- **Run the unit tests before you open a pull request.** If you can't run the integration tests, say so in the pull request, and the maintainer will run them.

## AI assistance

Parts of this project were written with the help of [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant. That includes code, tests, build scripts and documentation, including this README. The maintainer reviewed and committed every change.

Commits that Claude contributed to carry a `Co-Authored-By: Claude ...` trailer. To list them:

```sh
git log --grep="Co-Authored-By: Claude"
```

## License

MyAnimelist.NET is released under the [MIT License](https://github.com/Insire/MyAnimelist.NET/blob/main/LICENSE).
