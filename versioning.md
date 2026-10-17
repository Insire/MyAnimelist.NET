# Versioning

SoftThorn.MAL.ApiClient follows [Semantic Versioning 2.0](https://semver.org/spec/v2.0.0.html).

| Number | Changes when |
| - | - |
| major | incompatible API changes were made |
| minor | functionality was added in a backwards compatible manner |
| patch | backwards compatible bug fixes were made |

While the major version is `0`, the library is in early development and the public API isn't stable yet. Any `0.x` release can contain breaking changes.

Version numbers are calculated by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) from [version.json](version.json) and the git history:

- `version.json` sets `major.minor`. The patch number is the number of commits since that version was set (the "git height").
- Builds of `main` and `v<major>.<minor>` branches are public releases and get plain versions such as `0.1.12`.
- Builds of all other branches are prereleases and get the commit id as a suffix.
