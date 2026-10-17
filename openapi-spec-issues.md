# Issues in MyAnimeList's OpenAPI spec

SoftThorn.MAL.ApiClient generates its clients and models from the OpenAPI spec embedded in MyAnimeList's [API reference](https://myanimelist.net/apiconfig/references/api/v2). That spec doesn't always match the live API, and in places it is incomplete or not valid OpenAPI 3.0. This page lists everything we found and what the library does about each issue.

The descriptions are our own. MyAnimeList's agreement doesn't allow copying its API documentation, so see the official reference for what each endpoint and field means.

Each issue has an ID (P for paths and parameters, R for responses, S for schemas, A for authentication) and a status:

| Status | Meaning | Issues |
| --- | --- | --- |
| Fixed in the build | `UpdateOpenApiSpec` ([UpdateOpenApiSpecTask.cs](https://github.com/Insire/SoftThorn.MAL.ApiClient/blob/main/build/Tasks/UpdateOpenApiSpecTask.cs)) corrects the spec before NSwag generates the client. The step that does it is named in brackets. | 21 |
| Worked around | The spec stays as it is, and the library handles the difference elsewhere. | 7 |
| Open | The library inherits the problem. | 1 |
| Harmless | Wrong or inconsistent, but it doesn't affect the generated client. | 8 |

"Live" means checked against the API on 2026-10-17, either by the integration tests or by direct requests with a Client ID and no signed-in user.

## Paths and parameters

| ID | Issue | Details | Status |
| --- | --- | --- | --- |
| P1 | Path parameter name doesn't match the path | `GET /users/{user_name}` declares its path parameter as `user_id`. Generators can't match it to the placeholder: NSwag dropped it, and the client called `/v2/users/` (404). | Fixed in the build (`FixPathParameterNames`). |
| P2 | User lists have no `fields` parameter | `GET /users/{user_name}/animelist` and `/mangalist` don't declare `fields`. Live, both accept it, and without it each entry has only the work's id, title and picture, with no `list_status`. | Fixed in the build (`AddUserListFields`). |
| P3 | No `nsfw` parameter | No endpoint declares `nsfw`. Live, it changes results: a search for `kanojo` (`limit=100`) returned 57 anime rated `white` without it, and 57 `white` plus 33 `gray` with `nsfw=true`. Manga search and seasonal anime behave the same way. The rankings include `gray` entries either way. MyAnimeList accepts `true`, `True` and `1`. | Fixed in the build (`FixQueryParameters`). `SearchAnimeAsync`, `SearchMangaAsync` and `GetSeasonalAsync` take `bool? nsfw`. |
| P4 | The search term is marked optional | `q` on `GET /anime` and `GET /manga` isn't marked as required. Live, MyAnimeList answers `400` (`invalid q`) without it, and also for the two-letter term `ab`. A three-letter term works. | Fixed in the build (`FixQueryParameters`). The search methods take `string q`. |
| P5 | Limits without bounds | Only the forum endpoints give `limit` a maximum, and no endpoint gives `limit` or `offset` a minimum. Live, MyAnimeList answers `400` with the parameter's name as the message for `limit=501` on the ranking and seasonal endpoints, `limit=1001` on the user lists, `limit=101` on forum topics (which matches the spec), and `limit=0` or `offset=-1` anywhere. Anime and manga searches accept `limit=101`. | Fixed in the build (`FixQueryParameters`). NSwag doesn't check bounds, so the library still passes any value through, but the parameter documentation names the maximum. |
| P6 | Fixed sets of values typed as plain strings | `ranking_type`, the `sort` parameters of the season, user list and forum topic endpoints, the `status` filter of the user lists, and `status` in the list status PATCH bodies only accept a fixed set of values, but are plain strings. So is the `season` path parameter, although the response schema `AnimeSeason.season` has an enum with the same values. Live, an unknown ranking type gets a `400`, unknown sorts are ignored, and `season=autumn` gets a `404`. | Fixed in the build (`AddEnums`). Response fields stay strings, so a value MyAnimeList adds later can't break deserialization. |
| P7 | Profiles of other users | `GET /users/{user_name}` takes a user name in its path, but MyAnimeList's reference says only `@me` is supported. Not checked live, because the endpoint needs a signed-in user. | Open. `IUsersClient.GetUserAsync` takes any string, and its documentation says to pass `@me`. |
| P8 | Generated-looking operation ids | The operation ids repeat the path (`users_user_id_animelist_get`), name a parameter that doesn't exist (`user_id`), and end in `_put` on the two PATCH operations. | Fixed in the build (`SetOperationIds`). |
| P9 | Request bodies not marked as required | The list status PATCHes don't set `requestBody.required`, although an update without a body makes no sense. | Fixed in the build (`FixListStatusPatches`). `UpdateListStatusAsync` takes a required `listStatus` after the id. |
| P10 | Different field names in request and response | The anime PATCH body names the watched-episode count `num_watched_episodes`, while the list status in responses calls it `num_episodes_watched`. Both are what the live API uses. | Fixed in the build (`SetPropertyNames`). Both C# properties are called `NumEpisodesWatched`, and the JSON names stay MyAnimeList's. |
| P11 | Tags have different types in request and response | `tags` is a string in the PATCH bodies and an array of strings in the list status responses. Live, MyAnimeList splits the string at commas. | Fixed in the build (`FixListStatusPatches`). The PATCH bodies take a list of tags, which the library sends comma-separated, so a tag can't contain a comma. |
| P12 | Path-level parameters on two paths only | Only the `my_list_status` paths declare their id at path level. Every other path declares its parameters per operation. | Harmless. |

## Responses

| ID | Issue | Details | Status |
| --- | --- | --- | --- |
| R1 | Wildcard media type | Every response declares the media type `*/*`. Live, MyAnimeList sends `application/json; charset=UTF-8`. | Harmless. NSwag reads the bodies as JSON anyway. |
| R2 | No error responses | Apart from one `404`, no error status is declared, and neither is the error body. Live, errors carry a small JSON object with an `error` code and usually a `message`, for example `{"message":"invalid q","error":"bad_request"}` (400), `{"message":"","error":"not_found"}` (404) or `{"error":"invalid_token"}` (401). | Worked around. `ApiException.ErrorCode` and `ApiException.ErrorMessage` carry the two fields, and are `null` when the body has none. |
| R3 | Delete documents a 404 that never comes | The `DELETE …/my_list_status` operations declare `404` for a work that isn't on the list. Live, MyAnimeList answers `200`. | Worked around. `ListStatusTests` asserts the live behavior, so a delete of a missing entry succeeds. |
| R4 | Redirect to an error page | Not in the spec: MyAnimeList's edge sometimes answers a request with `307 Temporary Redirect` to `https://api.myanimelist.net/error.json`, a URL that never answers. Once it starts, it redirects every request for about a minute, sometimes several. It looks like throttling: about 60 requests in quick succession first got an HTML error page, and then only redirects for several minutes. | Worked around. The clients don't follow redirects, and `MalRetryHandler` retries redirected GET and DELETE requests. A redirect that outlasts the retries arrives as an `ApiException` with status `307`. |
| R5 | Undeclared season on seasonal anime | Live, `GET /anime/season/{year}/{season}` also returns the season itself (`season` with `year` and `season`) next to `data` and `paging`. The spec uses the same `AnimeList` schema as for search results, without it. | Worked around. The hand-written `AnimeList` has a `Season` property, which is `null` for search results and suggestions. |

## Schemas

| ID | Issue | Details | Status |
| --- | --- | --- | --- |
| S1 | No required properties | No schema lists `required` properties. The spec marks always-present fields with `nullable: false` instead, but in OpenAPI 3.0 that only says a field that is present can't be `null`, not that it is always there. Generated as is, every property was nullable, including ones MyAnimeList always sends, such as every id, title and `node`. | Fixed in the build (`SetRequiredProperties`). The properties MyAnimeList sent in every live response, whatever `fields` asked for, are required and therefore not nullable in C#. Nothing checks them while deserializing, so a field MyAnimeList stops sending can't make a whole response fail. The integration test snapshots show a required field that comes back empty. |
| S2 | A "non-nullable" field that is never sent | `MangaMagazineRelationEdge.role` is marked `nullable: false`, but it was missing from the serializations of every manga checked live. | Worked around. It stays optional (`string?`). |
| S3 | Forum topic data is an object | `ForumTopic.data` is declared as an array. Live, it is a single object. | Fixed in the build (`FixForumTopic`). |
| S4 | Forum poll is an object | `ForumTopicData.poll` is declared as an array. Live, it is a single object. | Fixed in the build (`FixForumTopic`). |
| S5 | Poll flag has another name | `ForumTopicPoll.close` is sent as `closed`. | Fixed in the build (`FixForumTopic`). |
| S6 | Numbers sent as strings | The counts in `AnimeStatistics.status` (`watching`, `completed`, ...) are declared as integers, but MyAnimeList sends them as strings, so deserializing anime details with `statistics` threw. | Worked around. The clients read numbers from strings (`JsonSerializerSettings.cs`). |
| S7 | Partial dates declared as dates | The list status `start_date` and `finish_date` and `User.birthday` have `format: date`, but MyAnimeList sends partial dates there (`2017-10`, `2017`). Generated as `DateTimeOffset`, `2017` threw, `2017-10` became the 1st of the month, and under a Thai culture the year came out 543 years early. `WorkForList.start_date`, which has the same kind of values, has no format. | Fixed in the build (`MakeDatesStrings`). They are strings. |
| S8 | Misspelled field name | The forum post author's avatar is called `forum_avator`. The live API uses the same spelling, so the spec is right about the wire, but the typo would carry over into code. | Fixed in the build (`SetPropertyNames`). The C# property is `ForumPostAuthor.ForumAvatar`, and the JSON name stays `forum_avator`. |
| S9 | `nullable` without a type, and single-part `allOf` | Several references are wrapped in an `allOf` with a part that only says `nullable: true`, sometimes without a `type`, which OpenAPI 3.0 doesn't define (for example `AnimeForList.start_season`, `AnimeForDetails.statistics`). Every forum schema wraps its single object in an `allOf`. NSwag turned these into base classes named `Anonymous`, `Anonymous2`, ... and subclasses such as `Statistics : AnimeStatistics`. | Fixed in the build (`FlattenSingleAllOf`). |
| S10 | Unnamed inline objects | Paging, the list and ranking entries, `broadcast`, `alternative_titles`, `start_season.season`, `statistics.status`, `User.anime_statistics` and both PATCH bodies are inline objects. NSwag named them `Data2`, `Body2`, `Anime_statistics` and so on. | Fixed in the build (`PromoteInlineSchemas`). |
| S11 | Awkward schema names | Some schema names expose MyAnimeList's internals (`AnimeRecommendationAggregationEdgeBase`) or differ by a single letter (`ForumTopicsData` is a topic in a search result, `ForumTopicData` a topic with its posts). | Fixed in the build (`RenameSchemas`). They are `AnimeRecommendation`, `MangaRecommendation`, `ForumTopicList`, `ForumTopicSummary`, `ForumTopicDetails`, `ForumUser` and `ForumPostAuthor`. |
| S12 | Two paging schemas | The lists use an inline `paging` object without `nullable`. The forum uses `ForumTopicPaging`, which has the same two fields and marks them nullable. | Fixed in the build (`UnifyPaging`). Both use `Paging`. |
| S13 | Two author schemas | `ForumTopicsCreatedBy` and `ForumTopicPostCreatedBy` describe the same user reference, and the second adds the avatar. | Harmless. They are `ForumUser` and `ForumPostAuthor` (see S11). |
| S14 | Non-standard extension | `User.anime_statistics` is marked with `x-optional`, which no tool understands. It means the field is only sent when requested, which is true of most fields. | Harmless. |
| S15 | One id with a format | `UserBase.id` has `format: int32`. No other id has a format. | Harmless. |
| S16 | Unused schema | `Date` is declared but never referenced. | Harmless. |
| S17 | Undocumented fields | Live, anime details also return `opening_themes`, `ending_themes` and `videos` when they are requested in `fields`, and the spec doesn't declare them. In `videos`, `created_at` and `updated_at` are Unix timestamps in milliseconds, while every other timestamp in the API is an ISO 8601 string. | Fixed in the build (`AddAnimeDetailsFields`). `AnimeForDetails` has `OpeningThemes`, `EndingThemes` and `Videos`, and `MalFields.AnimeDetails` requests them. The video timestamps are `long`. |

## Authentication

| ID | Issue | Details | Status |
| --- | --- | --- | --- |
| A1 | Wrong OAuth flow | The `main_auth` scheme declares the implicit flow with only an authorization URL. MyAnimeList actually uses the authorization code flow with PKCE (`code_challenge_method=plain` only) and a token endpoint at `https://myanimelist.net/v1/oauth2/token`, and the spec doesn't describe either endpoint. | Worked around. The login is written by hand (`LoginClient`, `TokenClient`), and NSwag doesn't use the security schemes. |
| A2 | Scope without effect | Every operation asks for the scope `write:users`. The library sends no scope at all and still gets full access to the user's lists. | Harmless. |
| A3 | Invalid scopes on the Client ID scheme | Operations list `["-"]` as the scopes of `client_auth`, an API key scheme. OpenAPI 3.0 requires an empty list for schemes other than OAuth2 and OpenID Connect, so validators reject the spec. | Harmless for NSwag. |

## Not checked yet

These came up during the review but haven't been tested against the live API:

| Question | Why it's open |
| --- | --- |
| Do the list status PATCHes accept `start_date` and `finish_date`? | The spec doesn't declare them in the request bodies, so the library can't send them. |
| Does `GET /users/{user_name}` really reject names other than `@me` (see P7)? | The endpoint needs a signed-in user, and the tests only use `@me`. |
| Does `nsfw` change the user lists or the suggestions? | The test account's lists have no entry that isn't rated `white`, and the suggestions need a signed-in user, so the library doesn't offer `nsfw` there. |
