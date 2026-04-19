# Changelog

Fork-specific changes on top of upstream Readarr 0.4.19 (retired). This log is additive — see upstream git history for anything before the retirement.

## Unreleased (develop)

### Serving books (new capability)
- **OPDS 1.2 catalog** at `/opds` exposing the library to any OPDS-aware ebook reader (Moon+ Reader, KyBook, KOReader, Aldiko, Calibre Companion). Endpoints:
  - `/opds` — root navigation feed
  - `/opds/authors` — paged author list
  - `/opds/author/{id}` — books by author
  - `/opds/books/recent` — recently added
  - `/opds/search?query=...` — title/author substring search
  - `/opds/search.xml` — OpenSearch description
  - `/opds/download/{bookFileId}` — file download with correct MIME (epub/pdf/mobi/azw/azw3/cbz/cbr/txt)
- Auth: existing API key via `X-Api-Key` header or `?apikey=` query param
- SPA fallback route updated to exclude `/opds/*` so the OPDS controller wins

### Per-user OPDS feeds (Phase 2c)
- `GET /opds/me/favorites` — atom acquisition feed of the authenticated user's starred books.
- `GET /opds/me/reading` — atom acquisition feed of books the user has in-progress (0 < `UserBookProgress.Percent` < 0.99).
- `GET /opds` now advertises "Currently Reading" + "Favorites" nav entries only when the caller is using a per-user ApiKey. Global ApiKey requests see the previous library-only feed, so existing OPDS clients that use the global key don't get dead links.
- When a per-user endpoint is hit with the global ApiKey, it returns a single nav entry explaining to log in with a per-user key instead of a blank feed.

### Admin sidebar
- "Reader" item added to the main admin sidebar (between Calendar and Activity) so admins and the users they provision can discover the `/reader` portal without having to type the URL. New `noRouter` flag on `PageSidebarItem` threads through to the generic `Link` so this one item does a full-page `<a href>` navigation instead of a React Router push (the reader is a separate bundle, not an SPA route here).

### Multi-user model (Phase 2a)
- Migration 044 extends the `Users` table with `Role`, `ApiKey`, `Email`, `CreatedAt` and creates `UserBookProgress`, `UserBookmarks`, `UserFavorites`. Any existing legacy user is seeded as `Admin` with a freshly generated ApiKey on first boot.
- `ApiKeyAuthenticationHandler` now accepts any `User.ApiKey` alongside the global config key; per-user requests attach `ReadarrUserId` + `ReadarrUserRole` claims.
- `UserController` at `/api/v1/user` — admin-gated CRUD + `POST /{id}/regenerateApiKey`.
- `UserMeController` at `/api/v1/user/me/*` — per-user progress, bookmarks, favorites, plus a one-call `/library` endpoint that joins books + first-bookfile + favorites + progress in one trip.
- **Settings → Users** admin page in the main UI (list, add/edit/delete, role select, reveal/regenerate ApiKey).

### Reader portal (Phase 3 + Phase 4)
- Separate **`/reader`** end-user webapp with its own bundle (second webpack entry, `frontend/src/ReaderApp/`). Not built into the admin UI — admins provision users in Settings → Users, then users sign in at `/reader`.
- Login endpoint `POST /reader/api/login` → `{id, username, role, apiKey}`. Client stores the ApiKey in localStorage and uses it for all subsequent `/api/v1/*` calls. 401 clears the session.
- Pages: Library (responsive grid with covers + progress bars + favorite toggle), Currently Reading (books with 0 < progress < 99%), Favorites, Read.
- Mobile-friendly: auto-fit grid, off-canvas sidebar, bottom-stacked nav on narrow screens.
- **Server-side EPUB rendering** (Kavita-style). Backend parses the epub zip directly (`System.IO.Compression.ZipArchive` + HtmlAgilityPack + ExCSS — can't use VersOne.Epub NuGet because the local `EpubTag` namespace is already a fork of it and collides on identical fully-qualified type names), serves a scoped HTML fragment per spine item, proxies embedded images/CSS/fonts through `/api/v1/user/me/epub/{id}/resource`.
  - `GET /info` — title, author, page count, per-page byte sizes (for skip-empty).
  - `GET /chapters` — parsed TOC (EPUB3 nav.xhtml or NCX fallback, suffix-match fallback for Calibre `../` references).
  - `GET /page/{N}` — `<style>...</style><div class="book-content">...</div>` fragment with CSS scoped to `.book-content` (every selector prefixed, `body`/`html` rewritten), `url(...)` and `<img src>` rewritten to the resource proxy, self-closing `<script/>` / `<title/>` pre-escaped so HAP doesn't mangle them, in-book `<a href>` rewritten to `data-epub-href` so the client can intercept.
  - `GET /resource` — streams the named file from the epub with suffix-match fallback.
- Frontend reads pages via `fetch` → `dangerouslySetInnerHTML`. No iframe, no client-side epub library. Keyboard (←/→/PgUp/PgDn/Esc), chapter label + "Chapter X of Y" + page number, progress bar, TOC sidebar, bookmarks.
- **"Skip empty pages" toggle** in the reader toolbar. Flips per-user in localStorage. When on, Next/Prev skip spine items under 2 KB (the pattern Calibre "split" epubs use for chapter-heading fragments), so you don't have to click past 20 empty pages to reach the actual text.
- Inline boot error handler in the reader HTML so a broken bundle shows the actual stack trace in the page instead of a blank screen (diagnostic tooling from the build-out, left in place).

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
