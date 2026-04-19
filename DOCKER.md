# Docker Build Guide

This document explains how to build and use Readarr Docker images.

## Overview

The Dockerfile uses a multi-stage build process that:
1. Builds the .NET backend
2. Builds the React frontend
3. Packages everything together
4. Creates the final runtime image

## Local Build

### Quick Start

Use the provided build script:

```bash
./docker-build.sh
```

This will build with default values:
- Version: 0.5.0.0 (must stay < 10.x.x and revision <= 10000 or `RuntimeInfo.IsProduction` flips false and HttpClient stops following 302 redirects — nzbgeek/nzbfinder break)
- Vendor: crucifix86
- Branch: develop
- Image name: readarr
- Tag: latest

### Custom Build

You can customize the build with various options:

```bash
./docker-build.sh \
  --version "0.4.19.1" \
  --vendor "YourName" \
  --branch "feature-branch" \
  --image-name "my-readarr" \
  --tag "v1.0"
```

### Manual Docker Build

If you prefer to use Docker directly:

```bash
docker build \
  --build-arg VERSION="0.4.19.0" \
  --build-arg VENDOR="Readarr" \
  --build-arg BRANCH="develop" \
  --tag readarr:latest \
  .
```

## Running the Container

```bash
docker run -d \
  --name readarr \
  --restart unless-stopped \
  -p 8787:8787 \
  -e TZ=America/Los_Angeles \
  -e ASPNETCORE_ENVIRONMENT=Production \
  --user 1000:1000 \
  -v /path/to/config:/config \
  -v /path/to/books:/books \
  -v /path/to/downloads:/data \
  ghcr.io/crucifix86/readarr:latest
```

On Unraid, use `--user 99:100` (nobody:users) so the container can write to `/mnt/user/appdata/<name>` without a `readonly database` failure during migration.

`ASPNETCORE_ENVIRONMENT=Production` is baked into the Dockerfile default, but pass it explicitly if you're overriding env via your orchestrator — without it, HttpClient won't follow 302 redirects and nzbgeek / nzbfinder indexers stop working.

Ports / paths used at runtime:
- `8787` — admin UI, API, OPDS, and `/reader` end-user portal (all one process)
- `/config` — SQLite DBs (`readarr.db`, `cache.db`, `logs.db`), `config.xml`, backups. Must be writable by the container's UID.
- `/books` — your book library (read/write — Readarr renames on import)
- `/data` — completed downloads (read/write — Readarr moves files into `/books`)

## CI/CD Integration

The GitHub Actions workflow automatically builds and pushes Docker images to GitHub Container Registry (GHCR) on:
- Push to `develop` branch
- Push to `master` branch
- Pull requests to `develop` branch

### Image Tags

Images are tagged as:
- `ghcr.io/crucifix86/readarr:latest` — most recent push to `develop`
- `ghcr.io/crucifix86/readarr:develop` — alias for the develop tip
- `ghcr.io/crucifix86/readarr:sha-<shortsha>` — every commit
- `ghcr.io/crucifix86/readarr:<semver>` — semantic-release-versioned builds

### Build Process

The CI workflow:
1. Sets version and build metadata
2. Runs tests (unit and integration)
3. Builds Docker image using multi-stage Dockerfile
4. Pushes to GHCR

## Build Arguments

| Argument | Description | Default |
|----------|-------------|---------|
| `VERSION` | Readarr version number | Required |
| `VENDOR` | Package author/vendor | Required |
| `BRANCH` | Git branch name | `develop` |
| `BUILD_CONFIGURATION` | .NET build configuration | `Release` |

## Multi-Stage Build Details

### Stage 1: Backend Builder
- Uses **.NET 8.0 SDK** Alpine image (upstream was .NET 6, long EOL)
- Builds all .NET projects
- Publishes for multiple platforms

### Stage 2: Frontend Builder
- Uses **Node.js 24** Alpine image
- Installs dependencies with Yarn
- Builds the admin React SPA **and** the separate `/reader` end-user webapp (second webpack entry, produces `reader.html` + `Content/reader-*.{js,css}` + copies `pdfjs-dist` worker to `Content/pdf.worker.min.mjs`)

### Stage 3: Package Builder
- Combines backend and frontend builds
- Runs packaging scripts
- Creates final artifacts

### Stage 4: Runtime Image
- Uses Alpine 3.22
- Copies packaged application
- Sets up runtime environment

## Troubleshooting

### Build Issues

1. **Out of memory**: Increase Docker memory limit
2. **Network timeouts**: Check internet connection for package downloads
3. **Permission errors**: Ensure proper file permissions

### Runtime Issues

1. **Port conflicts**: Change the exposed port (8787)
2. **Volume mounts**: Ensure proper paths and permissions
3. **Configuration**: Check `/config` volume mount

## Development

For development builds, you can modify the Dockerfile to:
- Use development configurations
- Include debugging tools
- Enable hot reloading

Example development build:

```bash
docker build \
  --build-arg BUILD_CONFIGURATION=Debug \
  --build-arg BRANCH="dev" \
  --tag readarr:dev \
  .
``` 