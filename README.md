# GoogleIntegrationService

ASP.NET Core (.NET 10) Web API that works with the **YouTube Data API v3** on behalf of a user.
The service accepts an OAuth access token **from the frontend** and returns the user's liked
videos grouped by channel, as well as full information about a single channel.

The YouTube endpoints don't store authorization on the server — they just perform calls with the
access token supplied by the client. The app also has its own server-side **Sign in with Google**
flow (`/api/auth/google-login`) for cases where you want the backend to run the OAuth handshake
and issue tokens itself; see [Google OAuth credentials](#google-oauth-credentials) below for how to
set it up.

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
   Authentication__Google__ClientId=your-client-id.apps.googleusercontent.com
   Authentication__Google__ClientSecret=your-client-secret
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
- `Authentication:Google:ClientId` / `Authentication:Google:ClientSecret` — OAuth 2.0 credentials
  for the "Sign in with Google" flow. **Secret — set only via `.env`.** See below for how to obtain
  them.

`.env` is git-ignored — never commit it. `.env.example` documents the required keys with
placeholder values and is safe to commit.

`appsettings.json` still defines the non-secret parts of the config (e.g. the certificate `Path`).

### Google OAuth credentials

`Authentication:Google:ClientId` / `ClientSecret` (used by `Web/Extensions/GoogleAuthExtensions.cs`)
are **OAuth 2.0 client credentials**, not a plain API key — a Google API key (`AIza...`) cannot be
used here; it authenticates plain API calls, not an OAuth sign-in flow.

To get them:

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and select or create a project.
2. Go to **APIs & Services → OAuth consent screen** and configure it (External user type is fine
   for local development; add your own Google account as a test user while the app is unpublished).
3. Go to **APIs & Services → Library** and enable **YouTube Data API v3** (required for the
   `youtube.readonly` scope requested during login).
4. Go to **APIs & Services → Credentials → Create Credentials → OAuth client ID**.
   - Application type: **Web application**.
   - Under **Authorized redirect URIs**, add the callback path that `AddGoogle()` listens on
     (`/signin-google`), for every base URL you'll run the app on, e.g.:
     ```
     https://localhost:7267/signin-google
     http://localhost:5269/signin-google
     ```
5. Copy the generated **Client ID** and **Client secret** into `.env`:
   ```
   Authentication__Google__ClientId=xxxxxxxx.apps.googleusercontent.com
   Authentication__Google__ClientSecret=GOCSPX-xxxxxxxxxxxxxxxxxxxxxxxx
   ```

Then sign in by opening `https://localhost:7267/api/auth/google-login` in a browser.

### Redirect configuration (returnUrl)

There are **two different redirect URIs** involved in the login flow — don't confuse them:

- **Google → this API** (`/signin-google`): configured in Google Cloud Console as described above.
  Fixed, not related to `returnUrl`.
- **This API → your frontend** (`returnUrl`): where the user lands *after* the backend has finished
  the login and minted its own JWT. This one is controlled per-request by the caller and is
  configured below.

`GET /api/auth/google-login` accepts an optional `returnUrl` query parameter — an absolute URL of
the frontend page to send the user back to once login completes, e.g.:
```
https://localhost:7267/api/auth/google-login?returnUrl=http://localhost:4200/checkout
```
`AuthController` carries it through the whole OAuth round trip and, once the JWT is issued,
redirects to `{returnUrl}?token=<jwt>` (or `&token=<jwt>` if `returnUrl` already has a query
string).

To prevent an [Open Redirect](https://owasp.org/www-community/attacks/Unvalidated_Redirects_and_Forwards),
`returnUrl` is only honored if its host is explicitly allow-listed. This is configured in
`appsettings.json`:
```json
"Frontend": {
  "DefaultReturnUrl": "http://localhost:4200/",
  "AllowedReturnHosts": [ "localhost", "127.0.0.1" ]
}
```
- `AllowedReturnHosts` — exact hostnames (no wildcards, no subdomain matching) the API is allowed
  to redirect back to. Add your production frontend domain here before deploying, e.g.
  `"myapp.com"`. `returnUrl` also must use `http`/`https` — any other scheme (e.g. `javascript:`)
  is rejected outright.
- `DefaultReturnUrl` — used whenever `returnUrl` is missing, malformed, or points at a host that
  isn't allow-listed, so the redirect always lands somewhere safe instead of failing or leaking
  the token to an untrusted host.

Both are non-secret and safe to commit in `appsettings.json` (or override per environment with
`appsettings.Production.json` / the `Frontend__AllowedReturnHosts__0` env-var style).

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

### `GET /api/auth/google-login`

Starts the "Sign in with Google" flow. Redirects the browser to Google's consent screen.

**Query params:**
- `returnUrl` *(optional)* — absolute URL of the frontend page to return to after login. Must
  point at a host listed in `Frontend:AllowedReturnHosts` (see
  [Redirect configuration](#redirect-configuration-returnurl)); otherwise the default is used.

**Example:**
```
GET https://localhost:7267/api/auth/google-login?returnUrl=http://localhost:4200/checkout
```

This is a **browser navigation**, not an API call meant to be `fetch`/`XMLHttpRequest`'d — link or
redirect the user's browser to it directly so they can see Google's consent screen.

### `GET /api/auth/google-callback`

Google redirects here automatically after the user grants consent; you don't call this directly.
The handler looks up/creates the `AppUser` (keyed by the Google id), issues the app's own JWT, and
redirects the browser to:
```
{returnUrl}?token=<jwt>
```
(or `BadRequest` if Google authentication failed). The frontend should read `token` from its own
URL's query string once it loads.

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

### `GET /api/youtube/liked/me`

Same result as `POST /api/youtube/liked` above, but takes **no body**. Instead of a Google token
supplied by the caller, it authenticates the request with the app's own JWT (from
`/api/auth/google-callback`), reads the Google id out of it, and looks up the Google access token
stored for that user in the database.

**Headers:**
```
Authorization: Bearer <jwt>
```

**Response:** identical shape to `POST /api/youtube/liked`.

- `401 Unauthorized` — missing/invalid JWT, or it has no Google id claim.
- `404 Not Found` — no stored Google token for this user (never signed in via
  `/api/auth/google-login`).

> ⚠️ The stored access token is **not refreshed**. Google access tokens expire after ~1 hour, and
> this endpoint doesn't yet use the stored refresh token to renew it — an expired token currently
> surfaces as a `500` rather than a clean re-auth error. Sign in again via `/api/auth/google-login`
> to get a fresh one.

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
