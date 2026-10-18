# 9. Architecture Decisions

Short ADRs. Status of all: **Accepted** (2026-10-18, design interview).

## ADR-001 .NET 10 and latest C#

- **Context:** The operator is a .NET developer; code quality and architecture matter.
- **Decision:** .NET 10, latest C# language version. No Python or Node in the product.
- **Consequences:** No existing iCloud library can be reused (ADR-002).

## ADR-002 Own iCloud client

- **Context:** No official API; no maintained .NET library. Python (`icloudpd`, `pyicloud`) and Node (`icloud-photos-sync`) projects implement the private protocol.
- **Decision:** Implement `Infrastructure.ICloud` in C#: SRP-6a, 2FA, session trust, CloudKit queries, download, delete, storage usage. Reference projects are studied for protocol behaviour; code is not copied.
- **Consequences:** Highest-risk component, must be proven first (ADR-013). Isolated behind ports so protocol changes stay local. Needs recorded fixtures for tests.

## ADR-003 Blazor Web App (interactive server) as UI

- **Alternatives:** ASP.NET Core API + SPA (Angular/React); desktop UI (MAUI/Avalonia).
- **Decision:** Blazor Web App with interactive server rendering.
- **Rationale:** One language and toolchain; platform-independent; runs in a container from day one (ADR-022); live progress via SignalR comes built in.
- **Consequences:** The browser needs a live connection to show progress; runs execute in a hosted service so they continue when the browser is closed.

## ADR-004 Onion Architecture

- **Decision:** Domain → Application (ports) → Infrastructure/Web, with dependencies pointing inwards only; one Infrastructure project per external concern.
- **Consequences:** The iCloud client, file system and media tools can be faked in tests. Enforced by architecture tests.

## ADR-005 Plain-text journal instead of a database

- **Alternatives:** SQLite locally; SQLite in the archive storage (corruption risk on network shares); one JSON file per account rewritten each time; per-file sidecars.
- **Decision:** Per account, an **append-only JSON Lines event journal** with periodic snapshots, stored **in the archive storage** in `.archive/`. Atomic writes via temp file + rename; flush after each append.
- **Rationale:** No database (operator constraint); the archive describes itself and survives loss of the PC; complete audit trail of deletions; appends are cheap and robust.
- **Consequences:** Replay on startup (fast thanks to snapshots); query features are limited to in-memory projections; must handle broken trailing lines; lock file against parallel instances.

## ADR-006 Original plus viewable copy, side by side

- **Alternatives:** Original only; converted copy only.
- **Decision:** Keep the byte-exact original; additionally create a JPEG (≈95) / H.264 MP4 copy for HEIC/RAW/HEVC; place copies next to the originals.
- **Consequences:** Somewhat more archive space; conversion is an independent step that never blocks deletion.

## ADR-007 Four-layer verification and deletion gate

- **Decision:** Size, SHA-256 read-back, decodability, journal entry; re-hash immediately before deletion. Apple's `fileChecksum` is not trusted as a criterion.
- **Consequences:** Every file is read twice from the archive storage (once at verification, once before deletion). Acceptable for data safety.

## ADR-008 Two-phase, confirmed deletion

- **Decision:** Archive and delete are separate phases. Deletion requires: account switch "deletion enabled" (default off), a plan, optional dry-run, explicit operator confirmation. "Recently Deleted" is not touched.
- **Consequences:** Storage savings appear 30 days after deletion. No accidental mass deletion.

## ADR-009 No stored passwords; tokens encrypted with a master password

- **Alternatives:** Store passwords encrypted; DPAPI/Keychain (platform-specific).
- **Decision:** Store only session/trust tokens, protected with ASP.NET Core Data Protection; the key ring is encrypted with a key derived from a master password (custom `IXmlEncryptor`, AES-GCM, PBKDF2).
- **Consequences:** Master password entered at each app start; Apple password + 2FA entered on each session expiry.

## ADR-010 Magick.NET and FFmpeg for media, with startup checks

- **Decision:** Magick.NET (bundled natives, HEIC via libheif) for image validation/conversion; FFmpeg/ffprobe as an external process (via FFMpegCore) for videos. All tools are checked at startup; missing tools disable only dependent features.
- **Consequences:** FFmpeg ships in the Docker image (installed from Alpine packages at build time); it must only be installed manually for the development mode. FFmpeg is not redistributed in the repo (licensing).

## ADR-011 Observability: Serilog → Loki, prometheus-net → Prometheus, Grafana

- **Alternatives:** OpenTelemetry SDK + OTLP to Grafana Alloy.
- **Decision:** Direct integration: Serilog Loki sink for logs, `prometheus-net.AspNetCore` scrape endpoint for metrics. Loki, Prometheus and Grafana as the `observability` profile in `deploy/compose.yaml`, including a provisioned dashboard.
- **Consequences:** Simple setup, no collector. Distributed tracing is not provided (not needed for a single process). The app runs fully without the stack.

## ADR-012 Sequential accounts, bounded parallelism, manual start

- **Decision:** One account at a time; 3–4 parallel downloads per account; start/pause/resume per account; no scheduler yet.
- **Consequences:** Gentle on Apple's rate limits; a full archive of all accounts may take days.

## ADR-013 Connectivity first via the production iCloud project

- **Alternatives:** Throwaway spike; build the whole app first.
- **Decision:** Milestone M1 builds the real `Infrastructure.ICloud` project with tests and a console harness `tools/ICloudProbe` (login, 2FA, list oldest-first, download one item, read storage usage; no deletion).
- **Consequences:** Requires ADP off and web access on for the operator's Apple ID before M1 can run.

## ADR-014 Public GitHub repository, MIT, CI on three OSes

- **Decision:** Public repo `icloud-photo-archiver`, MIT license, GitHub Actions matrix (Windows, macOS, Linux), Dependabot, secret scanning with push protection. No SonarQube.
- **Consequences:** Strict hygiene for personal data and fixtures (§8.1). Portability is proven on every push.

## ADR-015 Failure handling: skip, retry, needs attention, pause

- **Decision:** Per-asset failures are skipped and retried on the next run; after 3 attempts → needs attention. Rate limits and expired sessions pause the account.
- **Consequences:** Long runs aren't blocked by single bad items; a non-verified item can never be deleted.

## ADR-016 Personal library only (for now)

- **Decision:** Only the personal library (including Hidden) is archived. Shared Albums, iCloud Shared Photo Library and Recently Deleted are out of scope.
- **Consequences:** The CloudKit client queries only the primary (private) zone. Shared zones can be added later behind the same ports.

## ADR-017 Light CQRS on the event journal

- **Context:** The journal (ADR-005) is already event-sourced: append-only events, state rebuilt by replay. The UI and metrics need several different views of the same data (progress, deletion plan, needs attention, storage overview).
- **Alternatives:** No separation (use cases read and write the same in-memory state ad hoc); full CQRS with separate read stores, a message bus or mediator library, and read models updated after a delay.
- **Decision:** Light CQRS inside the single process:
  - **Commands** (`RunArchive`, `ExecuteDeletion`, `SignIn`, …) are use-case classes that load the asset state from the journal, check the change against the domain state machine and append events. They return no data, only a result (success or error).
  - **Queries** (`GetAccountProgress`, `GetDeletionPlan`, `GetNeedsAttention`, `GetStorageOverview`, …) are use-case classes that only read and never change state.
  - **Read models** are in-memory projections built on replay at startup and updated **synchronously, in the same step** as each journal append, so they are never out of date.
  - Commands and queries are plain classes resolved via DI; **no mediator library** (MediatR has moved to a commercial license, and it adds indirection without benefit at this size) and no message bus.
  - **The deletion gate never trusts a read model.** A read model decides what *appears* in the deletion plan; `ExecuteDeletion` re-checks the authoritative asset state from the journal and re-hashes the file before every delete.
- **Consequences:** Clear separation of state changes and reads, easy to test (commands: events in → events out; queries: events in → view out). New views can be added without touching the write side. No extra storage, no eventual consistency. Projections cost memory proportional to the number of assets (see TD-3).

## ADR-018 Small Linux base image for the container

- **Context:** The app runs as a Docker container from the first milestone on (ADR-022). A small image means fewer packages, a smaller attack surface, fewer CVE alerts and faster pulls. The image must still contain FFmpeg/ffprobe, support Magick.NET's native libraries, and have full globalization (localization, time zones).
- **Alternatives:**
  - `aspnet:10.0` (Debian): largest, many unneeded packages.
  - `aspnet:10.0-noble-chiseled-extra` (Ubuntu Chiseled, includes ICU and tzdata): very small, no shell, non-root by default; but no package manager, so FFmpeg has to be copied in as a static binary from another stage.
  - `aspnet:10.0-alpine` (musl): small, with a package manager, so FFmpeg comes from the distro.
- **Decision:** **Alpine** (`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`) with only `ffmpeg`, `icu-libs` and `tzdata` added; multi-stage build; non-root `app` user. **Fallback:** Ubuntu Chiseled (`noble-chiseled-extra`) with a static FFmpeg binary, if Magick.NET's musl native libraries prove unsuitable.
- **Consequences:** Publish for `linux-musl-x64` / `linux-musl-arm64`. Magick.NET's musl support (especially HEIC on ARM64) must be verified in M0 (R-13). CI builds the image and runs the startup health checks inside it, so a missing tool fails the build.

## ADR-019 Master-password login and step-up for deletion

- **Context:** In Docker, the app must listen on all interfaces inside the container, so exposure depends only on port publishing. Without a login, one wrong `ports:` line would let anyone on the LAN start a mass deletion. Originally accepted as technical debt (TD-2).
- **Alternatives:** Keep localhost-only and rely on a reverse proxy with authentication; external identity provider (OIDC).
- **Decision:** Unlocking with the master password **is the login**: ASP.NET Core cookie authentication protecting all pages and SignalR hubs, secure cookie flags, sliding idle timeout, rate-limited attempts. Real deletions require **re-entering the master password** (step-up). The compose file publishes the UI on `127.0.0.1` only; LAN access through a TLS reverse proxy.
- **Consequences:** TD-2 is resolved; login and step-up become part of milestone M3. Single operator, so no user management. Background runs continue when the UI session times out.

## ADR-020 Container and infrastructure hardening baseline

- **Context:** The container processes untrusted media with native libraries (FFmpeg, ImageMagick), holds encrypted Apple sessions and can delete photos. The repository is public.
- **Decision:** A mandatory baseline (§8.11), enforced by Dockerfile, compose files and CI:
  - never root (`USER $APP_UID`, explicit `user:`, app refuses to run privileged)
  - `cap_drop: [ALL]`, `no-new-privileges`, read-only root file system, resource limits, default seccomp/AppArmor
  - no `privileged`, no Docker socket, no host network, no network mounts inside the container
  - master password only via UI, never via environment or files
  - internal port for metrics/health details, UI published on localhost only
  - restrictive ImageMagick policy, sandboxed FFmpeg invocation, path containment (§8.12)
  - supply chain: digest-pinned images, Trivy, SBOM, provenance, SHA-pinned actions, minimal workflow permissions; cosign if published
  - observability stack: only Grafana published (localhost), no default credentials
- **Consequences:** Slightly more setup (host folder ownership, `.env` for Grafana). Permission problems must be solved with ownership/groups, never with root or `777`.

## ADR-021 Hash-chained journal

- **Context:** The journal decides what may be deleted. Anyone with write access to the archive storage, or silent disk corruption, could alter it (e.g. add `ResourceVerified` lines).
- **Alternatives:** No integrity protection (the re-hash at deletion time only proves the file matches the journal, not that the journal is genuine); HMAC per line.
- **Decision:** Each line carries `prevHash` (SHA-256 of the previous line). Replay verifies the chain; a broken chain locks deletions for that account until the operator checks it. An HMAC per line (key from the Data Protection key ring) is documented as an optional stronger variant.
- **Consequences:** Cheap, keyless, detects edits, deletions and insertions of single lines and corruption. Does not stop an attacker who rewrites the entire chain (the HMAC variant would). Snapshots store the hash of their last covered line.

## ADR-022 Docker container from the first milestone

- **Context:** Originally the app was to run directly on the operator's PC first and be containerized later (M6).
- **Alternatives:** Native first, Docker later; native only (self-contained publish per OS).
- **Decision:** The container is the **only supported runtime from M0 on**. One `deploy/compose.yaml` holds the app, the iCloud probe (profile `tools`) and the observability stack (profile `observability`). On Windows/macOS it runs with Docker Desktop. Direct `dotnet watch` on the host is allowed for the development inner loop only.
- **Rationale:** One tested runtime on every machine; FFmpeg and Magick.NET natives always present and verified; the security baseline (ADR-020) is active from the first line of code; moving later to an always-on host or NAS needs no changes; Alpine/musl risks (R-13) surface in M0 instead of M6.
- **Consequences:**
  - Docker (Desktop) is a prerequisite for the operator.
  - The archive storage reaches the container as a bind mount (local drive, shared with Docker Desktop) or as a Docker CIFS volume (network share); bind mounts from Windows drives are slower (R-17).
  - Data lives in the named volume `/data` instead of the user's app data folder (except in development mode).
  - The probe runs interactively via `docker compose run --rm probe`.
  - The CI image build, non-root check, in-container health check and Trivy scan are part of M0.
  - Code stays free of OS-specific dependencies; CI keeps testing on Windows, macOS and Linux.
