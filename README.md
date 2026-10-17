# SoftThorn.MAL.ApiClient

![SoftThorn.MAL.ApiClient icon: an anime eye with curly braces as the pupil](assets/Icon/icon.png)

[![NuGet](https://img.shields.io/nuget/v/SoftThorn.MAL.ApiClient.svg)](https://www.nuget.org/packages/SoftThorn.MAL.ApiClient)
[![codecov](https://codecov.io/gh/Insire/SoftThorn.MAL.ApiClient/branch/main/graph/badge.svg)](https://codecov.io/gh/Insire/SoftThorn.MAL.ApiClient)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/LICENSE)

A .NET client library for the [MyAnimeList API v2](https://myanimelist.net/apiconfig/references/api/v2).

It lets you search for anime and manga, read rankings and seasonal lists, browse the forums, read and update a user's lists, and sign in through MyAnimeList's OAuth2 (PKCE) login flow. Everything plugs into `Microsoft.Extensions.DependencyInjection` and `IHttpClientFactory`.

- **Target framework:** .NET 10
- **License:** [MIT](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/LICENSE)
- **Status:** early development (0.x). Breaking changes can happen in any 0.x release, see [versioning.md](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/versioning.md).

> [!IMPORTANT]
> This is an unofficial, community-made library. It is not affiliated with, endorsed by, or supported by MyAnimeList. "MyAnimeList" and "MAL" are used only to say which API the library talks to. To use the API you need your own Client ID, and with it you accept MyAnimeList's [API License and Developer Agreement](https://myanimelist.net/static/apiagreement.html) for your application, see [Using the MyAnimeList API responsibly](#using-the-myanimelist-api-responsibly).

## Installation

```sh
dotnet add package SoftThorn.MAL.ApiClient
```

## Features

- **Typed API clients** generated from the structure of MyAnimeList's OpenAPI specification
- **Rate-limit friendly:** requests rejected with `429 Too Many Requests` or `503 Service Unavailable` are retried up to 3 times with jittered exponential backoff, honoring `Retry-After`. Reads and deletes that get no answer within 20 seconds are retried the same way.
- **Identifies itself** with a `User-Agent` header naming the library
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

The library doesn't ship a Client ID. Every application registers its own, and by doing so its developer accepts MyAnimeList's [API License and Developer Agreement](https://myanimelist.net/static/apiagreement.html).

### 2. Register the services

```csharp
using Microsoft.Extensions.DependencyInjection;
using SoftThorn.MAL.ApiClient;
using SoftThorn.MAL.ApiClient.Authentication;

var services = new ServiceCollection();

services.AddMalApiClient(new ClientConfiguration
{
    ClientId    = "<your client id>",
    RedirectUrl = "http://localhost:5000/callback/", // must match the URL registered with MyAnimeList
});

var provider = services.BuildServiceProvider();
var mal = provider.GetRequiredService<MalApiService>();
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

`MalApiService` (namespace `SoftThorn.MAL.ApiClient`) groups the endpoints into these clients. The sign-in types (`LoginClient`, `ClientConfiguration`, `IAuthCache`, `AuthResponse`, `IBrowserLoginHandler`, `AuthProgress`) are in `SoftThorn.MAL.ApiClient.Authentication`.

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

`AddMalApiClient` registers the default implementations with `TryAdd`, so anything you register first wins.

### Store tokens somewhere else

The default `AuthCache` keeps tokens in memory, so they are gone when the app restarts. To keep users signed in, implement `IAuthCache`, for example with a file or the OS credential store. Store the whole `AuthResponse`, including `ExpiresAt`; without it the library can't tell when to refresh the token. Store only the tokens: MyAnimeList's agreement doesn't allow storing users' personal information or their passwords (see [below](#using-the-myanimelist-api-responsibly)).

```csharp
using SoftThorn.MAL.ApiClient;
using SoftThorn.MAL.ApiClient.Authentication;

public sealed class FileAuthCache : IAuthCache
{
    public Task<AuthResponse?> Get(CancellationToken cancellationToken = default) { /* load */ }
    public Task SaveAsync(AuthResponse authResponse, CancellationToken cancellationToken = default) { /* save */ }
}

services.AddSingleton<IAuthCache, FileAuthCache>();
services.AddMalApiClient(config);
```

### Open the login page differently

Implement `IBrowserLoginHandler` to show the login page in an embedded web view, or to automate it. The test project does this with Playwright.

```csharp
using SoftThorn.MAL.ApiClient;
using SoftThorn.MAL.ApiClient.Authentication;

services.AddSingleton<IBrowserLoginHandler, MyWebViewLoginHandler>();
services.AddMalApiClient(config);
```

## Using the MyAnimeList API responsibly

Your application, not this library, is bound by MyAnimeList's [API License and Developer Agreement](https://myanimelist.net/static/apiagreement.html). Read it before you ship. The points most likely to matter for apps built on this library (section numbers from the version of 2019-08-08; this is a summary, not legal advice):

- **Don't store personal information** from the API (3(c)), and delete MyAnimeList data within 24 hours when MyAnimeList asks you to (3(e)).
- **Don't ask for, store or keep users' passwords** (4(f)). Sign users in through the OAuth flow, which this library uses.
- **Don't alter MyAnimeList content** such as titles or synopses (3(a)(xii)). Showing it in your own layout is fine.
- **Use MyAnimeList's name and logos only to attribute it as the source** of the content (3(a)(xiii), 17), not in your app's or business's name.
- **Don't hide your app's identity** (3(a)(x)). The library sends `User-Agent: SoftThorn.MAL.ApiClient/<version>`; add your own product token if you like.
- **Don't scrape** MyAnimeList's website for data the API doesn't provide (4(i)).
- **Keep your Client ID to yourself** (3(c)): don't share it or tokens with third parties.
- **Tell MyAnimeList before you make your app available to users** (4(b)). They may ask to review it.
- **Respect rate limits** (6). The library retries `429` and `503` with backoff, but don't work around limits, for example by spreading requests across several Client IDs.

You are also responsible for what your users do with your app (4(d)).

## Known limitations

- **Tokens are kept in memory by default.** Users have to sign in again after every restart unless you [store tokens somewhere else](#store-tokens-somewhere-else).
- **Desktop and console apps only.** The login waits for the redirect on a local `HttpListener`, so it doesn't fit server-side web apps, where the redirect goes to your web server instead.
- **Public clients only.** The library sends a Client ID but no client secret.
- **PKCE uses `code_challenge_method=plain`**, because that is the only method MyAnimeList supports.
- **No retry on `401 Unauthorized`.** Tokens are refreshed shortly before they expire. If MyAnimeList revokes a token earlier, requests fail until the user signs in again.
- **User lists have no `fields` parameter.** MyAnimeList's OpenAPI spec doesn't define one for `Users.AnimelistAsync` and `Users.MangalistAsync`, so `list_status` can't be requested and each entry only contains id, title and picture.
- **No XML documentation on the generated clients.** MyAnimeList's agreement doesn't allow copying its API documentation, so the descriptions are stripped from the spec. See the [MyAnimeList API reference](https://myanimelist.net/apiconfig/references/api/v2) for what each endpoint and field means.

## Repository layout

```text
SoftThorn.MAL.ApiClient.slnx   The library and its tests (Build.slnx holds the Cake build)
src/            The library (SoftThorn.MAL.ApiClient.csproj)
  Authentication/            OAuth flow, token cache, and MalLoginHandler, which adds the Bearer token
  MalApiService.cs           The entry point that groups the API clients
  RegistrationExtensions.cs  AddMalApiClient, the User-Agent and the HttpClient setup
  MalRetryHandler.cs         Retries 429 and 503 responses with jittered backoff
  AutoGeneratedClient.cs     API clients generated by NSwag (do not edit by hand)
  CustomizedModels.cs        Hand-written list and paging models
assets/         OpenAPI spec (SoftThorn.MAL.ApiClient_OpenApiSpec.json), NSwag config, Liquid templates and the package icon
build/          Cake Frosting build project
tests/
  SoftThorn.MAL.ApiClient.UnitTests/          Tests with no network access
  SoftThorn.MAL.ApiClient.IntegrationTests/   Tests against the live MyAnimeList API
```

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build SoftThorn.MAL.ApiClient.slnx
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
| `UpdateOpenApiSpec` | Download the latest OpenAPI spec from MyAnimeList's API reference page, strip its documentation, and regenerate `src/AutoGeneratedClient.cs` with NSwag. Add `--skip-download` to regenerate from the committed spec instead |

Packages are written to `tmp_build/packages`. CI runs on Azure Pipelines, see [azure-pipelines.yml](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/azure-pipelines.yml). Versions come from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (see [version.json](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/version.json) and [versioning.md](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/versioning.md)).

## Running the tests

The tests are split into two projects:

| Project | What it tests | Needs |
| --- | --- | --- |
| `tests/SoftThorn.MAL.ApiClient.UnitTests` | Library logic, with no network access | Nothing |
| `tests/SoftThorn.MAL.ApiClient.IntegrationTests` | Signing in to a real MyAnimeList account and calling the live API. Playwright fills in the login form automatically. | Credentials and Playwright browsers (see below) |

Both use [Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro). `global.json` turns it on for `dotnet test`.

### Unit tests

```sh
dotnet test --project tests/SoftThorn.MAL.ApiClient.UnitTests/SoftThorn.MAL.ApiClient.UnitTests.csproj
```

### Integration tests

1. Install the Playwright browsers once (after building the project):

   ```sh
   pwsh tests/SoftThorn.MAL.ApiClient.IntegrationTests/bin/Debug/net10.0/playwright.ps1 install
   ```

2. Add your credentials as [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (Debug builds) or as environment variables:

   ```sh
   cd tests/SoftThorn.MAL.ApiClient.IntegrationTests
   dotnet user-secrets set "ClientConfiguration:ClientId"    "<client id>"
   dotnet user-secrets set "ClientConfiguration:RedirectUrl" "http://localhost:5000/callback/"
   dotnet user-secrets set "MalAuthSettings:UserName"        "<mal user name>"
   dotnet user-secrets set "MalAuthSettings:Password"        "<mal password>"
   ```

   As environment variables, use `__` as the separator, for example `ClientConfiguration__ClientId`.

3. Run the tests:

   ```sh
   dotnet test --project tests/SoftThorn.MAL.ApiClient.IntegrationTests/SoftThorn.MAL.ApiClient.IntegrationTests.csproj
   ```

To run both projects, use `dotnet test --solution SoftThorn.MAL.ApiClient.slnx` from the repository root. The solution has to be named because `Build.slnx` sits next to it. Add `--coverage` to collect code coverage.

> [!WARNING]
> Use a dedicated test account. The tests sign in with these credentials, approve API access for your client, and change the account's lists: they keep Neon Genesis Evangelion and Berserk on them, and add and remove Trigun and One Piece.

The integration tests compare responses with [Verify](https://github.com/VerifyTests/Verify) snapshots (`*.verified.txt`). Lists, rankings and the test account's profile change over time, so those tests only snapshot the shape of the response, not its values. Before you accept a changed snapshot, check that the diff comes from changed data and not from a model that stopped deserializing.

## Contributing

Issues and pull requests are welcome. A few things to know:

- **Don't edit `src/AutoGeneratedClient.cs` by hand.** NSwag generates it from `assets/`. Change the spec or the Liquid templates and regenerate with `./build.ps1 --target UpdateOpenApiSpec`.
- **Fix spec errors in the build, not only in the JSON.** MyAnimeList's published spec doesn't always match the live API. `UpdateOpenApiSpec` re-downloads the spec and then patches it in `build/Tasks/UpdateOpenApiSpecTask.cs`. A fix made only in `assets/SoftThorn.MAL.ApiClient_OpenApiSpec.json` is lost on the next download.
- **Don't add MyAnimeList's documentation text.** The task strips descriptions, summaries and examples from the spec, because MyAnimeList's agreement doesn't allow copying its API documentation. Write your own wording for docs and comments, and link to the official reference.
- **Download the spec only when you need to.** `UpdateOpenApiSpec` reads the spec out of MyAnimeList's API reference page, so it's run by hand, never in CI. Use `--skip-download` when you only change the fixes or the templates.
- **Put tests in the right project.** Tests that don't call MyAnimeList go in `SoftThorn.MAL.ApiClient.UnitTests`, which must never use the network.
- **Use [conventional commits](https://www.conventionalcommits.org).** Release notes are generated from commit messages, grouped by type (`feat`, `fix`, `perf`, `docs`, `chore(deps)`, ...). Mark breaking changes with `!` after the type (`feat(auth)!: ...`) or a `BREAKING CHANGE: <what users have to change>` footer. To put a sentence into the release's highlights, add a `Release-Note: <sentence>` footer.
- **Run the unit tests before you open a pull request.** If you can't run the integration tests, say so in the pull request, and the maintainer will run them.

## AI assistance

Parts of this project were written with the help of [Claude Code](https://claude.com/claude-code), Anthropic's AI coding assistant. That includes code, tests, build scripts and documentation, including this README. The maintainer reviewed and committed every change.

Commits that Claude contributed to carry a `Co-Authored-By: Claude ...` trailer. To list them:

```sh
git log --grep="Co-Authored-By: Claude"
```

## License

SoftThorn.MAL.ApiClient is released under the [MIT License](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/LICENSE).
