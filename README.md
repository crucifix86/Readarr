# Readarr (crucifix86 revival fork)

[![Docker Build & Release](https://github.com/crucifix86/Readarr/actions/workflows/docker-build.yml/badge.svg?branch=develop)](https://github.com/crucifix86/Readarr/actions/workflows/docker-build.yml)
[![Readarr CI](https://github.com/crucifix86/Readarr/actions/workflows/readarr-ci.yml/badge.svg?branch=develop)](https://github.com/crucifix86/Readarr/actions/workflows/readarr-ci.yml)

> ⚠️ **Early beta — expect rough edges.**
> This is an actively-evolving revival of abandoned Readarr. Working metadata, working search, working indexers — but not polished. Don't expect production stability, don't file bugs expecting same-day fixes, and **keep a backup of your library DB.** A dedicated metadata server is in the works; until it ships, this fork points at the community `api.bookinfo.pro` instance by default.

Readarr is an ebook and audiobook collection manager for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new books from your favorite authors and will grab, sort, and rename them.

Note that only one type of a given book is supported per instance. If you want both an audiobook and ebook of a given book you will need multiple instances.

## About this fork

The original Readarr project was retired by the Servarr team in mid-2025 after its metadata provider went down. This fork builds on the excellent work of [@Faustvii](https://github.com/Faustvii/Readarr) (perf + reliability fixes, Docker/CI wiring, OpenTelemetry) and [@ricetim](https://github.com/ricetim/readarr-rresurrected) (native Bibliotik + MyAnonamouse indexers, AllowedLanguages filter, UI niceties), with additional work on top.

Not affiliated with, endorsed by, or related to the Servarr team.

## Quick start (Docker)

```bash
docker run -d --name readarr \
  --restart unless-stopped \
  -p 8787:8787 \
  -e TZ=America/Los_Angeles \
  --user 1000:1000 \
  -v ~/readarr-config:/config \
  -v /path/to/books:/books \
  -v /path/to/downloads:/data \
  ghcr.io/crucifix86/readarr:latest
```

Then open http://localhost:8787. The metadata URL is pre-wired to `api.bookinfo.pro`; override it in **Settings → General → Development → Metadata Source** if you run your own.

On Unraid, use `--user 99:100` (nobody:users).

## What this fork adds on top of upstream

Runtime modernization:
- Targets **.NET 8** (upstream was .NET 6, long EOL)
- GitHub Actions updated to Node 24
- Docker base images refreshed (`dotnet/aspnet:8.0-alpine`)
- Container defaults to `ASPNETCORE_ENVIRONMENT=Production` so modern indexers that use 302 redirects (nzbgeek, nzbfinder) work without manual env tweaks

Import engine:
- **Book Match Threshold** slider in *Settings → Media Management* (advanced) — tune how strict the identification gate is
- **Skip Book Matching** toggle — bypass the match-quality gate entirely when you want to force imports and curate manually
- Defensive null-checks around the upgrade pipeline so a single bad match no longer crashes the whole `DownloadedBooksScan` with a `NullReferenceException`
- Edition fallback when skip-matching imports a book without a monitored edition

Indexers (via ricetim):
- Native **Bibliotik** (cookie-based auth, all known auth-mode bugs fixed)
- Native **MyAnonamouse** with language field parsing

Download decisions:
- **Prefer Larger Files** config to pick the bigger release when quality ties
- **AllowedLanguages** filter on quality profiles (migration 043), with UI and manual-grab column

UI / navigation:
- Search-Series button on the author details page

API correctness (the biggest invisible fix):
- `[FromBody]` added to every POST/PUT handler that takes a resource. ASP.NET Core 8 is stricter about inferring body binding from `IApiBehaviorMetadata` on generic base classes; without this fix, **every `/api/v1/config/*` PUT silently fails** on .NET 8 — the auth setup modal can't save, media management won't persist, host config goes nowhere. Symptoms look like the UI doing nothing when you click Save.

## Metadata

Defaults to `https://api.bookinfo.pro` (community-hosted [rreading-glasses](https://github.com/blampe/rreading-glasses)). Change it in *Settings → General → Development → Metadata Source* to point at your own instance.

## Core classic features (inherited from upstream)

* Watch for better-quality editions (e.g. PDF → AZW3) and auto-upgrade
* Scan existing libraries for missing books
* Failed-download retry to a different release
* Manual search with per-release decision explanations
* Quality profiles, metadata profiles, naming templates
* Calibre integration (requires Calibre Content Server)
* Native download client support: SABnzbd, NZBGet, QBittorrent, Deluge, rTorrent, Transmission, uTorrent, and more

## Known edges / tradeoffs

- **Omnibus files don't import** (upstream limitation, fix planned — see Roadmap). When one `.epub`/`.mobi` contains two books in one file (e.g. "Dinosaur Planet AND Dinosaur Planet Survivors", or "The Ship Who Searched; Partnership"), Readarr can only map a file to a single book entity and rejects the import with *"Couldn't find similar book"*. Co-author names in filenames are unrelated — single-author collaborations import fine. Current workarounds: Calibre-split into two files, Manual Import to one of the two books (the other stays "missing"), or re-search for a single-book release.
- **Skip Book Matching** accepts every import by design. If it picks a wrong target for a sloppily-named file, you'll end up with the file in the wrong author/book path — manual cleanup. Leave it off and raise the threshold to 0.35-0.50 if you want a middle ground.
- **Duplicate rejection**: Readarr won't re-import a file whose size matches something already in the library. If you re-download a book, clear the existing file first.
- **Live metadata tests** are marked `[Explicit]` and skip in CI. Run them manually when validating metadata-source changes.
- **"Failed to import N files" alongside a successful grab** is cosmetic. NZB packages typically ship `.par2`, `.nfo`, cover `.jpg` etc. beside the actual `.epub`/`.mobi`/`.azw3`. Readarr walks every file in the completed folder; only the book is importable, the rest get counted as "failed" but that's just the count of ignored extras — the book itself lands fine.

## Roadmap

Known gaps we plan to fix (not-yet-started):

- **Omnibus/multi-book file support** — let a single file satisfy multiple book entities. Candidate approaches: detect `AND` / `;` / `&` patterns in parsed titles and try each side against the book DB, or add a "multi-book file" flag that binds one `BookFile` row to several books. Needs design before coding.
- Dedicated self-hosted metadata server (user-owned alternative to `api.bookinfo.pro`)

## Contributing / building locally

See upstream Readarr docs for the general structure. The bits unique to this fork:
- `Dockerfile` — multi-stage build, Alpine + .NET 8. Final image is `aspnet:8.0-alpine`.
- `.github/workflows/docker-build.yml` — builds and pushes `:latest`, `:develop`, `:sha-<shortsha>` on every push to `develop`. Semantic-release tags produce versioned images too.
- `build.sh --all` works locally once you have .NET 8 SDK + yarn.

## License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)

Derivative work of [Readarr](https://github.com/Readarr/Readarr) (GPLv3).
