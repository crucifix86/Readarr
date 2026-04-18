# Changelog

Fork-specific changes on top of upstream Readarr 0.4.19 (retired). This log is additive — see upstream git history for anything before the retirement.

## Unreleased (develop)

### Runtime
- Target framework bumped `net6.0` → `net8.0` (upstream was EOL)
- Docker base images bumped to `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`
- GitHub Actions upgraded to Node 24 (`@v5` across checkout/setup-node/setup-dotnet/cache)
- Container defaults to `ASPNETCORE_ENVIRONMENT=Production` so HttpClient follows 302 redirects (needed for nzbgeek, nzbfinder)
- `AssemblyVersion` default `0.5.0.0` so `RuntimeInfo.IsProduction=true` (previously `10.0.0.*` which tripped the dev-mode redirect rejection)

### Metadata
- Default metadata URL swapped from dead `api.bookinfo.club` → `api.bookinfo.pro` (blampe's rreading-glasses shared instance)
- User-configurable via **Settings → General → Development → Metadata Source** (already wired in upstream, we just needed a working default)

### Import engine
- **Book Match Threshold** slider in *Settings → Media Management* (advanced) — exposes the previously hardcoded 0.20 rejection gate
- **Skip Book Matching** toggle to bypass the gate entirely for messy metadata
- Defensive null-checks in `UpgradeMediaFileService.UpgradeBookFile` — previously crashed the whole `DownloadedBooksScan` with NRE when an identification returned a null Book or missing `BookFiles` lazy wrapper
- Edition fallback in `ImportApprovedBooks` when skip-matching imports a book without a monitored edition (was throwing `Sequence contains no matching element`)

### API / infrastructure
- Explicit `[FromBody]` on every POST/PUT handler in V1 API (`ConfigController`, `HostConfigController`, `NamingConfigController`, indexers, download clients, profiles, tags, etc). .NET 8 stops inferring body binding from `IApiBehaviorMetadata` on the generic `RestController<T>` base class — without this fix every `/api/v1/config/*` PUT silently binds a default resource, validation fails, UI saves do nothing
- `[ApiController]` attribute added to `RestController<TResource>` base for belt-and-suspenders
- Docker workflow no longer gated on semantic-release — every push to develop produces `:latest`, `:develop`, `:sha-<shortsha>` tags

### Features cherry-picked from upstream forks
- **Bibliotik** native indexer (via ricetim) — cookie-based auth, all known auth bugs fixed
- **MyAnonamouse** native indexer (via ricetim) — includes language field parsing
- **AllowedLanguages** filter on quality profile (via ricetim) — migration 043, decision-engine spec, UI multi-select
- **Prefer Larger Files** download setting (via ricetim)
- **Search Series** button on author detail page (via ricetim)
- Faustvii's reliability + perf work (author refresh null-pointer fix, lazy-load removals, Book endpoint perf, OpenTelemetry option)

### Testing
- `BookInfoProxy` live-API tests marked `[Explicit]` (they hit `api.bookinfo.pro` and depend on external data; were failing CI after Faustvii's `[Ignore(Until)]` date expired)
- `config_properties_should_write_and_read_using_same_key` test updated to handle `double` properties (for `BookMatchThreshold`)
- MyAnonamouse fixture + test synced with ricetim's final state (field rename `name` → `title`)

## Known edges / roadmap

See [README.md](README.md#known-edges--tradeoffs) for current-state limitations and [Roadmap](README.md#roadmap) for planned work (omnibus support, per-author series filter, self-hosted metadata).
