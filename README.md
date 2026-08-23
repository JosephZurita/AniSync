<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/banner-dark.png" />
  <img src="docs/banner-light.png" width="440" alt="AniSync" />
</picture>

**Sync your Shoko watch status to AniList _and_ MyAnimeList - automatically, to both at once.**

</div>

AniSync is a [Shoko Server](https://shokoanime.com) plugin that watches your episode
progress and keeps your **AniList** and **MyAnimeList** lists in sync. Mark an episode
watched in Shoko (or any Shoko-connected player) and it updates both providers -
progress, status, rewatches, and start/finish dates - with a clean web dashboard to
manage everything.

## Features

- **Dual-sync** to AniList + MyAnimeList from a single watch event.
- **Reviewed bulk sync from Shoko** - preview every watched series on a dedicated page,
  search and choose exactly what to include, then follow live progress.
- **Per-user** - each Shoko user connects their own accounts.
- **Smart matching** - resolves provider IDs from the anime-offline-database (cached per
  watch), with fuzzy title-matching as a fallback.
- **Rewatch detection**, optional progress rollback, and flexible start-date handling.
- **Grouped history** - one row per watch with a badge per provider and episode stills.
- **Modern web UI** - dashboard, settings, and history; light/dark; mobile-friendly.

## Screenshots

| Dashboard | Settings | History |
| --- | --- | --- |
| ![Dashboard](docs/screenshots/dashboard.png) | ![Settings](docs/screenshots/settings.png) | ![History](docs/screenshots/history.png) |

And on mobile:

| Dashboard | Settings | History |
| --- | --- | --- |
| ![Dashboard mobile](docs/screenshots/dashboard-mobile.png) | ![Settings mobile](docs/screenshots/settings-mobile.png) | ![History mobile](docs/screenshots/history-mobile.png) |

## Requirements

- **Shoko Server 6.0 dev** with the plugin API. The exact tested
  `Shoko.Abstractions` version is pinned in [`AniSync.csproj`](AniSync/AniSync.csproj).
- **.NET 10 SDK** and **Node.js** (to build).
- A **MyAnimeList** API app and/or an **AniList** API client (for OAuth - see below).

## Install

1. Download `AniSync.dll` from the latest
   [development release](https://github.com/JosephZurita/AniSync/releases), or build it
   locally using the steps in [Development](#development).
2. Drop the DLL into Shoko's `plugins/` folder.
3. Restart Shoko. The plugin serves its UI at `/anisync`.

## Configuration

1. **Create the provider API apps** (use your own - each needs a redirect/callback URL of
   `https://<your-shoko-host>/anisync/authCallback`):
   - **MyAnimeList** - <https://myanimelist.net/apiconfig>
   - **AniList** - <https://anilist.co/settings/developer>

   Note each app's **Client ID** and **Client Secret**.

2. **Enter the credentials** - open `/anisync` as a Shoko **admin** > **Settings > API
   configuration** > paste the Client IDs/Secrets > **Save**.

3. **Connect your accounts** - on the **Dashboard**, click **Connect** for AniList and/or
   MyAnimeList and authorize. Each Shoko user connects their own accounts.

4. Watch something - progress syncs to every connected provider. To import existing
   progress, use **Sync library** on the dashboard.

## Settings

| Setting | What it does |
| --- | --- |
| Auto-sync | Sync watch status automatically on playback. |
| Sync only on completion | Only update when you finish the last episode. |
| Rewatch detection | Bump the repeat count on genuine rewatches. |
| Allow rollback | Let progress decrease when rewatching earlier episodes. |
| Start date from any episode | Set the start date on the first watched episode, not only episode 1. |
| Fuzzy title matching + threshold | Match titles loosely when no exact ID is found. |
| Sync delay (seconds) | Wait this long between series during a bulk sync. |
| Update NSFW | Include adult titles when syncing. |
| Debug logging | Verbose logs for troubleshooting. |

## How it works

On an episode watch event the plugin resolves the anime's provider IDs **once** (cached for
an hour), then for each connected provider it reads the current list entry, decides the
change (watch / rewatch / no-op), and updates it - writing **one grouped history entry per
watch**, shared across providers via an event id.

Watched/unwatched is read from Shoko's `LastPlayedAt`, not the sticky `IsWatched` flag, so
an unwatch is correctly detected instead of being re-synced.

The **Sync Library** page scans the current Shoko user's episode data and previews the
highest watched normal episode in each series. The user can search, select, or exclude
individual entries before anything is sent. AniSync validates the submitted selection
against the user's current Shoko data, then sends only those series to each connected
provider sequentially. The preview refreshes while the page is open and whenever the
window regains focus; marking an episode unwatched removes it from eligibility because
the current Shoko `LastPlayedAt` value, rather than historical playback count, is used.
It respects **Sync only on completion** and the configured sync delay. Rewatch inference is
disabled for bulk jobs, which makes running the same import again idempotent; series already
up to date remain unchanged.

## Development

Backend is C# / .NET 10; the frontend is React + Vite + TypeScript and is **built into the
plugin's `wwwroot/app`** and embedded in the DLL.

```bash
# 1. Build the web UI (outputs into AniSync/wwwroot/app)
cd client && npm install && npm run build && cd ..

# 2. Build the plugin
dotnet build AniSync/AniSync.csproj -c Release

# 3. Copy the DLL into Shoko's plugins/ folder and restart Shoko
#    AniSync/bin/Release/net10.0/AniSync.dll

# Run the tests
dotnet test AniSync.Tests/AniSync.Tests.csproj
```

### Automated compatibility builds

Dependabot checks the Shoko plugin packages daily. When a new
`Shoko.Abstractions` or `Shoko.BuildTools.Targets` version is published, it opens a
pull request with the exact package update. GitHub Actions then builds the frontend,
runs the full test suite, and uploads the resulting `AniSync.dll` for review.

Every successful push to `master` also publishes a development prerelease containing
the DLL and its SHA-256 checksum. The workflow can be run manually from the Actions
tab when an on-demand compatibility build is needed.

## Tech stack

**Backend:** C# / .NET 10 / Shoko.Abstractions
**Frontend:** React / Vite / TypeScript / TanStack Query & Form / Zustand / Tailwind CSS / shadcn/ui / lucide
