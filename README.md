# GoogleIntegrationService

ASP.NET Core (.NET 10) Web API that works with the **YouTube Data API v3** on behalf of a user.
The service accepts an OAuth access token **from the frontend** and returns the user's liked
videos grouped by channel, as well as full information about a single channel.

Authorization is **not stored on the server** — the app has no OAuth flow of its own and does not
open a browser on startup. Every call to Google is performed with the token supplied by the client.

---

## Tech stack

- **.NET 10**, ASP.NET Core Web API
- **MediatR** — requests/handlers (CQRS-style)
- **Google.Apis.YouTube.v3** — YouTube Data API client
- OpenAPI (`/openapi`) in the Development environment

---

## Architecture

```
web/                      — entry point and controllers
  Program.cs              — DI, CORS, MediatR, routing
  Controllers/YouTubeController.cs

Application/              — contracts and business logic (MediatR)
  TokenReqDTO.cs          — request body carrying the token
  LikedVideoDto.cs        — liked video model
  ChannelDtos.cs          — ChannelInfoDto, ChannelLikesGroupDto, LikedVideoRefDto
  Requests/               — LikedVideosByChannelRequest, ChannelInfoRequest
  Handlers/               — LikedVideosByChannelHandler, ChannelInfoHandler

Infrastructure/Google/    — Google API integration
  YouTubeUserService.cs   — YouTube calls performed with the access token
  GoogleApiRetry.cs       — retry on failure
```

Flow: `Controller → MediatR Request → Handler → IYouTubeUserService → YouTube Data API`.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A user OAuth access token with the scope `https://www.googleapis.com/auth/youtube.readonly`,
  obtained on the frontend (Google Identity Services / OAuth 2.0).

> ⚠️ Without this scope YouTube returns `403` when fetching liked videos.

---

## Configuration

Secrets are kept out of the repo and supplied via a `.env` file at the project root.

1. Copy the template:
   ```bash
   cp .env.example .env
   ```
2. Fill in `.env`:
   ```
   Kestrel__Certificates__Default__Password=devcert
   Google__ApplicationName=GoogleIntegrationService
   ```

`.env` is loaded automatically on startup via [DotNetEnv](https://github.com/tonerdo/dotnet-env)
(`Env.Load()` in `web/Program.cs`), applied to process environment variables **before** the
configuration is built. Keys use the standard ASP.NET Core `__` section separator, so
`Kestrel__Certificates__Default__Password` maps to `Kestrel:Certificates:Default:Password` and
overrides the value from `appsettings.json`.

- `Kestrel:Certificates:Default:Password` — password for the dev HTTPS certificate
  (`certs/devcert.pfx`). **Secret — set only via `.env`.**
- `Google:ApplicationName` — application name passed to the YouTube API. Not sensitive, but
  configurable the same way; falls back to `"GoogleIntegrationService"` if unset.

`.env` is git-ignored — never commit it. `.env.example` documents the required keys with
placeholder values and is safe to commit.

`appsettings.json` still defines the non-secret parts of the config (e.g. the certificate `Path`).

---

## Running

```bash
dotnet run --launch-profile https
```

Addresses (from `Properties/launchSettings.json`):

| Profile | URL |
|---------|-----|
| `https` | `https://localhost:7267` and `http://localhost:5269` |
| `http`  | `http://localhost:5269` |

OpenAPI document in Development: `https://localhost:7267/openapi/v1.json`.
CORS is open for all origins (`AllowAnyOrigin`).

---

## Endpoints

### `POST /api/youtube/liked`

Returns the user's liked videos **grouped by channel**, with each channel's share of all likes.

**Request:**
```json
{ "token": "ya29.A0AfH6..." }
```

**Response** `200 OK`:
```json
[
  {
    "channelName": "Rick Astley",
    "channelId": "UCuAXFkgsw1L7xaCfnd5JJOw",
    "channelDescription": "Official channel...",
    "channelAvatarUrl": "https://yt3.ggpht.com/...",
    "percent": 23.53,
    "videos": [
      { "name": "Never Gonna Give You Up", "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ" }
    ]
  }
]
```

- `percent` — the channel's share of the total number of likes (rounded to 2 decimals);
  results are sorted in descending order.
- `400 Bad Request` if the token is missing.

### `POST /api/youtube/channel/{channelId}`

Returns full information about a channel by its `id`.

**Request:** `POST /api/youtube/channel/UCuAXFkgsw1L7xaCfnd5JJOw`
```json
{ "token": "ya29.A0AfH6..." }
```

**Response** `200 OK`:
```json
{
  "id": "UCuAXFkgsw1L7xaCfnd5JJOw",
  "title": "Rick Astley",
  "description": "...",
  "customUrl": "@rickastleyyt",
  "country": "GB",
  "publishedAt": "2009-10-25T06:57:33+00:00",
  "thumbnailUrl": "https://yt3.ggpht.com/...",
  "subscriberCount": 4000000,
  "videoCount": 120,
  "viewCount": 1500000000,
  "url": "https://www.youtube.com/channel/UCuAXFkgsw1L7xaCfnd5JJOw"
}
```

- `404 Not Found` if the channel does not exist; `400 Bad Request` if the token is missing.

---

## Implementation details

- **Liked videos** are read from the system `LL` playlist (`PlaylistItems.list`), in pages of 50,
  fully paginated while `nextPageToken` is present.
- **Channels** are fetched in a single batch (`Channels.list`, up to 50 `id`s per call), which
  minimizes quota usage.
- **Retry on failure** (`GoogleApiRetry`): up to 3 attempts with a 2-second delay, honoring the
  `CancellationToken`.

### YouTube Data API quota

`PlaylistItems.list` and `Channels.list` each cost **1 unit per call** (50 items per page).
For example — 2085 liked videos across 880 unique channels:

```
videos:   ceil(2085 / 50) = 42 calls  → 42 units
channels: ceil(880 / 50)  = 18 calls  → 18 units
total:                                 ≈ 60 units
```

With the default daily quota of 10,000 units, that is ~160 full requests per day. Retries only
increase the cost when errors occur.

---

## Build

```bash
dotnet build
```
