# 8. Cross-cutting Concepts

## 8.1 Security

| Topic | Concept |
|---|---|
| Apple ID passwords | **Never stored.** Held in memory only for the SRP handshake, then discarded. The password is never logged and never sent anywhere except Apple's SRP endpoint (and SRP doesn't send it in plain text). |
| Session tokens | Stored per account in `sessions/<alias>.enc`, protected with ASP.NET Core Data Protection (`IDataProtector` with purpose `PhotoArchiver.Sessions.v1`). |
| Key ring | Data Protection keys are encrypted with a custom `IXmlEncryptor`: AES-256-GCM, key derived from the **master password** via PBKDF2-SHA256 (≥ 600 000 iterations, random salt). Cross-platform; no DPAPI/Keychain. |
| App login | Unlocking with the master password **is the login** (ADR-019). At startup the app is locked; every page and SignalR hub requires an authenticated cookie session (ASP.NET Core cookie authentication, `HttpOnly`, `Secure`, `SameSite=Strict`, sliding expiration, e.g. 30 min idle). Failed attempts are rate-limited with increasing delays. Locking the app (manually or on timeout) signs out and drops the decrypted key material from memory; running archive jobs continue. |
| Step-up for deletion | Executing a real deletion requires **re-entering the master password** in the confirmation dialog, in addition to the per-account switch and the plan confirmation. |
| Privileged execution | The app **refuses to start** when running privileged (`Environment.IsPrivilegedProcess`: root, sudo, or elevated Administrator), locally and in the container. |
| Network exposure | In the container Kestrel listens on `8080` (UI) and the compose file publishes it on `127.0.0.1` only (in development mode on the host, Kestrel binds to `localhost`). LAN access only through a reverse proxy with TLS; the app then enforces HTTPS redirection and HSTS (forwarded headers from the known proxy only). |
| Web hardening | Antiforgery on; no CORS; SignalR origin check; security headers: `Content-Security-Policy` (self only, with what Blazor Server needs), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy` (everything off). |
| Internal endpoints | `/metrics` and the detailed health output are served on a **separate internal port** (`9091`), never published by the container; the UI port only exposes a minimal liveness check (`/healthz` → `200 OK`, no details). |
| Outbound traffic | Only Apple's auth, setup, CloudKit and content hosts, plus Loki if configured. TLS certificate validation is **never disabled** (enforced by a test that no custom `ServerCertificateCustomValidationCallback` exists). Optional: an egress allow-list at the network level. |
| Personal data in telemetry | UI, logs, metrics and traces use the **account alias** (e.g. "Account 1" or a chosen nickname). Apple IDs are masked (`m***@g***.com`). File names are logged at Debug level only. |
| Public repository | No settings, tokens, journals, logs or fixtures with real data are committed. Recorded HTTP fixtures are **sanitized** (IDs, names, URLs, tokens replaced) before being committed. Secret scanning + push protection. |
| Dependencies | Central package management, Dependabot, `dotnet list package --vulnerable` in CI. |
| Infrastructure | Container, host, CI and observability hardening: §8.11. Untrusted file names and media: §8.12. Journal integrity: §8.2. |

## 8.2 Persistence: plain-text journal

There is no database (TC-4). Each account has its own folder in the archive storage:

```
<ArchiveRoot>/<AccountAlias>/
├─ .archive/
│  ├─ journal.jsonl          append-only event log
│  ├─ snapshot.json          compacted state at a journal position
│  └─ lock                   prevents two app instances on the same account
├─ 2019/
│  └─ 2019-07/ …            media, copies, sidecars
└─ …
```

Journal line example (one JSON object per line, UTF-8, `\n`):

```json
{"seq":1042,"ts":"2026-10-18T19:02:11.483Z","type":"ResourceVerified","assetId":"AeX…","resource":"Original","path":"2019/2019-07/2019-07-14_153012_IMG_1234.HEIC","size":2483921,"sha256":"9f2c…","prevHash":"c41e…"}
```

Rules:

- **Append-only.** Events are never edited or removed; a correction is a new event.
- Each append is written and **flushed** (`FileStream.Flush(flushToDisk: true)`) before the next step starts.
- `seq` is strictly increasing; replay ignores a broken trailing line (§6.5).
- A **snapshot** (current state of all assets + last `seq`) is written every N events or at run end, via `snapshot.json.tmp` → atomic rename. Replay = load snapshot + apply later events.
- The journal is the **audit trail** of every deletion and is never compacted away; snapshots only speed up loading.
- Every journal line carries a schema `v` once the format changes; readers accept older versions.
- **Hash chain (ADR-021):** each line contains `prevHash`, the SHA-256 of the previous line's exact bytes (the first line uses a fixed genesis value). The snapshot stores the hash of the last line it covers. Replay verifies the chain; a broken chain (edited, removed or inserted lines, silent disk corruption) **locks the account for deletions** and raises a needs-attention alert until the operator has checked it. Archiving may continue (it only adds data). Optional stronger variant: an HMAC per line with a key from the Data Protection key ring, which also detects someone rewriting the whole chain.

Local settings (`settings.json`) are written the same way: temp file + atomic rename.

## 8.3 Verification

| Layer | Check | When |
|---|---|---|
| 1 | Byte count equals the size reported by iCloud | After the download stream ends |
| 2 | SHA-256 computed while downloading equals SHA-256 of the file **read back from the archive storage** | After the rename |
| 3 | File decodes (Magick.NET for images, ffprobe for videos) | After read-back |
| 4 | `ResourceVerified` event is in the journal | Before the asset may enter a deletion plan |
| Gate | All resources verified, sidecar written, files still present with the same SHA-256 | Immediately before each iCloud delete call |

Apple's own `fileChecksum` is recorded in the journal for reference but not used as a verification criterion, because its algorithm is not documented.

## 8.4 File naming and layout

Folder: `<ArchiveRoot>/<AccountAlias>/<YYYY>/<YYYY-MM>/` using the **effective date** (capture date, fallback added date), in the capture time zone if known, otherwise UTC.

| File | Pattern |
|---|---|
| Original still/video | `YYYY-MM-DD_HHmmss_<OriginalName>.<ext>` |
| Live Photo video part | `YYYY-MM-DD_HHmmss_<OriginalName>.MOV` |
| Edited version | `YYYY-MM-DD_HHmmss_<OriginalName>_edited.<ext>` |
| Viewable copy (photo) | `YYYY-MM-DD_HHmmss_<OriginalName>.jpg` (from the edited version if present, otherwise from the original) |
| Viewable copy (Live video) | `YYYY-MM-DD_HHmmss_<OriginalName>_live.mp4` |
| Viewable copy (video) | `YYYY-MM-DD_HHmmss_<OriginalName>.mp4` |
| Sidecar | `<original file name>.xmp` |

- Files that are already JPEG / H.264 MP4 get no copy.
- If two assets produce the same name, a short asset-ID suffix is added: `…_IMG_1234~a1b2c3.HEIC`.
- Names are sanitized for NTFS/APFS/ext4 and SMB shares (reserved characters, trailing dots/spaces, length). File names from iCloud are untrusted input; see §8.12 for path containment.
- The original file's last-modified time is set to the capture date.

## 8.5 Error handling and resilience

| Error class | Handling |
|---|---|
| `Transient` (timeouts, 5xx, connection reset) | `Microsoft.Extensions.Http.Resilience` pipeline: retry with exponential backoff + jitter, circuit breaker per host |
| `RateLimited` (429, 503 with throttling) | Pause the account, increasing wait time, then resume automatically |
| `SessionExpired` (401/421, CloudKit auth errors) | Pause, "re-login needed" banner |
| Per-asset failure (size/hash mismatch, decode error, missing URL) | `ResourceFailed` event, attempt counter; after 3 attempts → `NeedsAttention` |
| Archive storage unavailable / disk full | Stop the run for all accounts, health check turns red; nothing is deleted while the archive storage is unavailable |
| Unexpected exception | Logged with context, run stops cleanly, journal stays consistent |

Use cases return results (`Result<T, Error>`-style) for expected failures; exceptions are reserved for programming errors and infrastructure faults.

## 8.6 Logging, metrics and health

**Logging (Serilog):** structured logs with `Account` (alias), `AssetId`, `RunId` properties. Sinks: console, rolling file (local app data), **Loki** (`Serilog.Sinks.Grafana.Loki`, labels `app`, `account`, `level`). A live tail of the current run is shown in the UI (in-memory sink).

**Metrics (`/metrics` on the internal port `9091`, prometheus-net):**

| Metric | Type | Labels |
|---|---|---|
| `photoarchiver_assets_total` | counter | `account`, `event` (discovered, downloaded, verified, failed, deleted) |
| `photoarchiver_bytes_archived_total` | counter | `account` |
| `photoarchiver_bytes_deleted_total` | counter | `account` |
| `photoarchiver_download_duration_seconds` | histogram | `account` |
| `photoarchiver_run_active` | gauge | `account` |
| `photoarchiver_needs_attention` | gauge | `account` |
| `photoarchiver_relogin_required` | gauge | `account` |
| `photoarchiver_icloud_storage_bytes` | gauge | `scope` (plan_total, plan_used, account_used), `account` |
| `photoarchiver_archive_storage_bytes` | gauge | `kind` (free, used_archive) |
| `photoarchiver_tool_available` | gauge | `tool` |

**Grafana dashboard** (pre-built, provisioned): storage panel (iCloud plan vs archive storage), archive progress per account, throughput, failures/needs attention, re-login required, deletions over time, log panel (Loki).

**Health:** each startup check (`IToolCheck`) is an ASP.NET Core health check: FFmpeg present + version, Magick.NET natives + HEIC support, archive path reachable + writable + free space above threshold, key ring unlocked, local app data writable. Shown on the (authenticated) health page and at `/health` on the internal port; the public UI port only offers `/healthz` without details. A failed check disables only the features that need it (D-17). An additional check reports `Running privileged` as fatal (§8.1).

## 8.7 Configuration

`settings.json` (local, never in the repo), bound via the Options pattern with validation at startup (`ValidateOnStart`):

```json
{
  "ArchiveRoot": "/archive",
  "Retention": { "DefaultMonths": 12 },
  "Accounts": [
    { "Alias": "Account 1", "AppleId": "…", "RetentionMonths": null, "DeletionEnabled": false }
  ],
  "Download": { "Parallelism": 3, "MaxAttempts": 3 },
  "Conversion": { "FfmpegPath": null, "JpegQuality": 95, "VideoCrf": 18 },
  "Storage": { "MinFreeBytes": 53687091200 },
  "Plans": [ { "Name": "200 GB", "Bytes": 214748364800, "MonthlyPrice": 2.99, "Currency": "EUR" } ],
  "Observability": { "LokiUrl": null }
}
```

In the container, `settings.json` lives in `/data`; `ArchiveRoot` is always `/archive` (the host path or share is chosen in `deploy/.env`). Environment variables (`PHOTOARCHIVER__…`) override values; secrets are never passed this way (§8.11).

## 8.8 Concurrency

- One run per account at a time (journal lock file + in-process guard).
- Accounts run one after another (ADR-012); inside an account, a bounded `Channel<T>` with 3–4 workers.
- Journal appends are serialized through a single writer per account.
- Each step accepts a `CancellationToken`; pause = cancel + resume from journal.

## 8.9 Testing

| Level | Scope | Tooling |
|---|---|---|
| Unit | Domain (state machine, gate, retention, naming), application use cases with fake ports | xUnit |
| iCloud client | SRP math against known test vectors; request/response handling against **recorded, sanitized fixtures** served by a fake `HttpMessageHandler` | xUnit |
| Storage | Journal append/replay/snapshot/corruption recovery, atomic writes, on a temp directory | xUnit |
| Media | Validation and conversion with small sample files (HEIC, MOV, JPEG) | xUnit |
| UI | Key components (2FA dialog, deletion confirmation) | bUnit |
| Architecture | Onion dependency rules | NetArchTest |
| Security | Path traversal and malicious file names, hash-chain tampering, unauthenticated access to pages/hubs, step-up on deletion, security headers, no TLS validation bypass, refusal to run privileged | xUnit, `WebApplicationFactory` |
| Container | Image runs as non-root, health checks pass inside the image, Trivy scan | CI (`docker.yml`) |
| Manual | Real account | `tools/ICloudProbe` (never in CI) |

Tests that need FFmpeg are skipped with a clear reason when it is not installed (CI installs it on all three OSes).

## 8.10 Localization

All UI texts in `.resx` resources (`IStringLocalizer`). English is the default culture; German resources can be added without code changes. Dates and sizes are formatted by the browser/user culture.

## 8.11 Infrastructure and container hardening

Baseline (ADR-020). Each rule is enforced by the Dockerfile, the compose files or CI, not only by documentation.

### Container image

| Rule | Implementation |
|---|---|
| **Never run as root** | Packages are installed in the build/runtime stage as root, then the Dockerfile ends with `USER $APP_UID` (the non-root `app` user of the .NET images). The app additionally refuses to start when privileged (§8.1). |
| Minimal content | Alpine runtime image with only `ffmpeg`, `icu-libs`, `tzdata` (ADR-018); no SDK, no shell tools added, no build artifacts. |
| Unprivileged port | Kestrel listens on `8080` (UI) and `9091` (internal); no `NET_BIND_SERVICE` needed. |
| Pinned and scanned | Base images pinned by digest; Trivy scan fails the build on HIGH/CRITICAL vulnerabilities with an available fix; SBOM and build provenance attached; OCI labels (`org.opencontainers.image.*`). If published to GHCR: signed with cosign. |

### Container runtime (compose)

```yaml
services:
  archiver:
    image: icloud-photo-archiver:<version>@sha256:<digest>
    user: "${APP_UID}:${APP_GID}"
    read_only: true
    tmpfs: [ /tmp:size=256m,noexec,nosuid ]
    cap_drop: [ ALL ]
    security_opt: [ "no-new-privileges:true" ]
    pids_limit: 256
    mem_limit: 2g
    cpus: 2
    ports: [ "127.0.0.1:8080:8080" ]        # 9091 is never published
    volumes:
      - archiver-data:/data                 # settings, key ring, sessions, logs (0700, app user)
      - ${ARCHIVE_SOURCE}:/archive          # bind mount of a local drive OR the CIFS volume below

volumes:
  archiver-data:
  archive-share:                            # only when the archive storage is an SMB share
    driver: local
    driver_opts:
      type: cifs
      device: "//${NAS_HOST}/${NAS_SHARE}"
      o: "vers=3.1.1,seal,uid=${APP_UID},gid=${APP_GID},file_mode=0640,dir_mode=0750,username=${NAS_USER},password=${NAS_PASSWORD}"
```

Forbidden (checked in review, documented in the compose file): `privileged: true`, adding capabilities, mounting the Docker socket, `network_mode: host`, disabling seccomp/AppArmor (`unconfined`), publishing `9091`.

### Host and archive storage

| Rule | Implementation |
|---|---|
| File permissions instead of root | The host folder for `/archive` is owned by `APP_UID:APP_GID` (or shared through a group via `group_add`). **Running as root or `chmod 777` is never an acceptable fix** for permission errors. |
| Network shares are mounted by the Docker engine or the host | As a Docker `cifs` volume (mounted by the engine) or a host mount, **never inside the container** (would need `CAP_SYS_ADMIN`). SMB 3.1.1 with encryption (`seal`); SMBv1 disabled. |
| `/archive` must be a real mount | Startup check blocks runs if `/archive` is not a mount point or does not contain the archive marker file after initialization (R-19). |
| Least privilege on the NAS | A **dedicated NAS account** with read/write access to the archive folder only. |
| Rootless runtime | Recommended where available: rootless Docker or Podman. |

### Secrets

| Secret | Handling |
|---|---|
| Master password | **Only typed into the UI.** Never an environment variable, compose entry, command-line argument or file in the image. |
| Key ring, sessions | In `/data` (container) or the local app data folder; directory mode `0700` for the app user; key ring encrypted with the master password-derived key. |
| Grafana admin password, NAS account credentials | In `deploy/.env`, excluded by `.gitignore` and readable only by the operator (`chmod 600` / restricted ACL); only `.env.example` is committed. CIFS credentials are visible to host users via `docker volume inspect` (R-18), hence the dedicated, minimal NAS account. |

### Observability stack

- Only **Grafana** is published, on `127.0.0.1:3000`. Loki and Prometheus are reachable only on the internal `observability` network.
- Grafana: admin password from `.env`, anonymous access and sign-up disabled.
- All three images pinned by digest, run as their non-root users, read-only root file systems where supported, `cap_drop: [ALL]`, `no-new-privileges`.
- Loki has a retention period (e.g. 30 days); logs contain no Apple IDs (§8.1).

### CI / supply chain (public repository)

- All GitHub Actions **pinned to commit SHAs**, updated by Dependabot.
- Every workflow declares minimal `permissions:` (default `contents: read`); write permissions only per job where required.
- `pull_request_target` is never used; workflows triggered by fork PRs get no secrets.
- `main` is protected: pull requests with green CI required, no force pushes.
- Secret scanning with push protection, Dependabot alerts and security updates enabled.

## 8.12 Safe handling of untrusted input

File names, metadata and media content come from iCloud and are treated as **untrusted** (a photo may have been received from someone else and be malformed on purpose).

| Threat | Countermeasure |
|---|---|
| Path traversal via file names (`../../x`, absolute paths, device names like `CON`) | `ArchivePath` sanitizes every name component, then resolves the full path and **verifies it is inside the archive root** (`Path.GetFullPath` + prefix check with directory separator). Violations fail the asset (never written). Covered by tests with malicious names. |
| Command injection into FFmpeg | FFmpeg/ffprobe started only with `ProcessStartInfo.ArgumentList` (no shell, no concatenated command string); file paths passed as separate arguments, prefixed so they can never be parsed as options. |
| FFmpeg exploits / resource exhaustion | `-nostdin`, `-protocol_whitelist file`, `-xerror`; hard timeout per call; process killed on cancellation; runs as the non-root app user inside the hardened container. |
| ImageMagick exploits (e.g. ImageTragick-style coders) | Restrictive Magick.NET security policy at startup: only HEIC/HEIF, JPEG, PNG, TIFF, DNG allowed; MVG, MSL, URL, HTTP(S), PS/EPS/PDF, TEXT and other coders disabled; limits for memory, disk, width, height and area. |
| Oversized or endless processing | Each validation/conversion runs with a `CancellationToken` and a time limit; failures go to the needs-attention list. |
| Malicious metadata in XMP output | Values XML-encoded by the XML writer, never built with string concatenation. |
