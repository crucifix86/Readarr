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
  -p 8787:8787 \
  -v ~/readarr-config:/config \
  ghcr.io/crucifix86/readarr:latest
```

Then open http://localhost:8787. The metadata URL is pre-wired to `api.bookinfo.pro`; override it in Settings → General → Development → Metadata Source if you run your own.

## Major Features Include

* Can watch for better quality of the ebooks and audiobooks you have and do an automatic upgrade. *e.g. from PDF to AZW3*
* Automatically detects new books
* Can scan your existing library and download any missing books
* Automatic failed download handling will try another release if one fails
* Manual search so you can pick any release or to see why a release was not downloaded automatically
* Advanced customization for profiles, such that Readarr will always download the copy you want
* Fully configurable book renaming
* SABnzbd, NZBGet, QBittorrent, Deluge, rTorrent, Transmission, uTorrent, and other download clients are supported and integrated
* Full integration with Calibre (add to library, conversion) (Requires Calibre Content Server)
* And a beautiful UI

## License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
