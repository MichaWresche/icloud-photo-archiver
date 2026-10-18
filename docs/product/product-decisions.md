# Product Decisions & Limitations

Product owner's view of the iCloud Photo Archiver: why it exists, what it does and doesn't do, which decisions were made, and which limitations everybody using it must accept.

Status: agreed during the design interview, 2026-10-18.

## 1. Vision

> Archive old photos and videos of several iCloud accounts to our own storage (a local drive or a network share) in original quality, safely remove them from iCloud, and pay Apple less for storage.

## 2. Problem

- Several Apple IDs are in use. Each has its own iCloud+ plan, or several share one plan (for example through Family Sharing).
- iCloud Photos is the main consumer of that storage, and it grows every year.
- Old media is rarely viewed on the phones, but it is paid for every month.
- Apple offers no supported way to bulk-export the originals and delete them safely.

## 3. Goals and success criteria

| # | Goal | Success criterion |
|---|---|---|
| G1 | Lower iCloud cost | After the first full run + 30 days, the storage plan(s) can be downgraded by at least one tier |
| G2 | No data loss | Not a single item is deleted from iCloud without a verified, byte-exact original in the archive storage |
| G3 | Original quality | Archived originals are byte-identical to what iCloud stores, including Live Photo videos and edited versions |
| G4 | Easy to view | Every archived item can be opened on stock Windows and macOS (via a viewable copy) |
| G5 | Low effort | One person operates multiple accounts from one web UI; a run can be started at any time and resumed after interruption |
| G6 | Transparency | The UI shows iCloud storage (per account and per plan), archive storage usage, what has been archived, and what is ready to delete |

## 4. Users

| Role | Description |
|---|---|
| **Operator** | Runs the app, manages all configured accounts, enters passwords and 2FA codes, confirms deletions |
| **Account owners** | Own the Apple IDs being archived (the operator may be one of them). They don't need to use the app, but they must provide 2FA codes from their trusted devices when a session expires, and they must agree that old photos disappear from their devices |

## 5. Scope

### In scope

- The **personal iCloud Photos library** of each configured Apple ID: all photos and videos, including the Hidden album, Live Photos (still + video), edited versions, RAW/ProRAW, screenshots and screen recordings.
- **Multiple accounts** (any number of Apple IDs). The accounts don't need any relationship to each other: they may each have their own storage plan or share one.
- Downloading **oldest first**, up to a configurable cutoff ("keep the last N months in iCloud").
- Verifying every file, then deleting from iCloud as a separate, confirmed step.
- Viewable copies (JPEG / H.264 MP4) next to the originals.
- Metadata preservation (journal + XMP sidecars).
- Storage dashboard (iCloud usage per account and per plan, archive storage, archive progress, plan-downgrade suggestion).
- Observability via Prometheus / Loki / Grafana (optional).

### Out of scope (for now)

| Item | Reason |
|---|---|
| **Shared Albums** | They don't count against storage and Apple only keeps reduced-quality copies. Maybe in the future. |
| **iCloud Shared Photo Library** | Deleting removes items for all participants; needs its own rules. Maybe in the future. |
| Recently Deleted album | Items there are already on their way out; Apple purges them after 30 days. |
| Emptying "Recently Deleted" | Decided: Apple's 30-day purge is the safety net (see D-08). |
| Restoring/re-uploading archived media to iCloud | Not needed for the goal. Files can be imported manually via Photos if ever required. |
| Scheduled / automatic runs | Manual start only. The design leaves room for a scheduler. |
| Face/People data, Memories, Places index | Not exported by the iCloud web API. |
| Mobile app | The web UI works in a mobile browser. |
| Multi-user login to the app | Single operator. |

## 6. Product decisions

| ID | Decision | Rationale |
|---|---|---|
| D-01 | **Retention window**: everything older than a configurable period (default 12 months) is archived and becomes deletable. Global default, overridable per account. The UI always shows the resulting cutoff date. | Phones keep recent photos; old media (especially videos) holds most of the storage. |
| D-02 | **Date used** for the cutoff and ordering: capture date (EXIF), fallback to the date added to iCloud. | The capture date is what people mean by "old photos". Scans/screenshots without EXIF still get a sensible date. |
| D-03 | **Oldest first**. | Required by the user. Oldest media is the least likely to still be needed on the phone. |
| D-04 | **Keep the byte-exact original *and* a viewable copy** (JPEG quality ~95 for HEIC/RAW, H.264 MP4 for HEVC). Files that are already JPEG / H.264 get no copy. | Conversion loses quality, HDR, depth data and cannot be undone after iCloud deletion. The copy makes browsing easy on any PC. |
| D-05 | **Copies sit next to the originals** with a defined naming scheme (see arc42 §8). | User preference: one folder per month containing everything. |
| D-06 | **Verification gate**: size match, SHA-256 computed during download and compared after reading the file back from the archive storage, file decodes as image/video, journal entry written. Only "Verified" items may be deleted, and the hash is re-checked right before deletion. | No data loss is the top priority (G2). |
| D-07 | **Deletion is a deliberate step**: two phases (archive, then delete), a dry-run mode, and a per-account "deletion enabled" switch that is **off by default**. The operator confirms "N items / X GB" before anything is deleted. | Unofficial API + irreplaceable personal photos. |
| D-08 | **"Recently Deleted" is left to Apple** (purged after 30 days). | 30-day safety net. Storage savings show up with a 30-day delay. |
| D-09 | **No passwords are stored.** Only session/trust tokens are stored, encrypted with a key protected by a master password. When a session expires, the operator enters password + 2FA again. | Security: several Apple IDs, not all owned by the operator. |
| D-10 | **Multiple accounts, one operator.** Any number of Apple IDs is managed from one UI. Accounts are independent of each other (login, settings, journal, archive folder); they may or may not share a storage plan. | One person handles the archiving. Requires account owners to provide 2FA codes from time to time. |
| D-11 | **Accounts run one after another**, 3–4 parallel downloads inside an account; start/pause/resume per account; manual start only. | Stays below Apple's rate limits, keeps behaviour predictable. |
| D-12 | **Failures**: an item that fails is skipped and retried on the next run; after 3 failures it lands on a "needs attention" list. Rate limits or expired sessions **pause** the account instead of failing it. | One bad file must not block hundreds of GB. A skipped item is never deleted. |
| D-13 | **No cross-account de-duplication** on disk. Duplicates are only reported. | Each account's archive stays complete and self-contained. |
| D-14 | **Metadata** that would be lost with deletion (albums, favorite, hidden, title/caption, edit info) is stored in the journal and in an XMP sidecar per item. File timestamps are set to the capture date. | Albums and favorites are curation work that should not be lost. |
| D-15 | **Storage view** shows: iCloud usage per account, each storage plan (total/used, and a per-account breakdown if accounts share a plan), archive storage free/used, per account (in iCloud / archived / ready to delete), and a plan-downgrade suggestion from an editable price table. | G6. Prices differ by country, so the table is editable. |
| D-16 | **Platform independence through Docker**: the app is delivered and run as a Docker container **from the first milestone on**, on Windows and macOS (Docker Desktop) as well as any Linux host or NAS. The code has no OS-specific dependencies, so it also runs directly with `dotnet` for development. Web UI. | User requirement; one tested runtime everywhere; FFmpeg and native libraries ship in the image. |
| D-17 | **Startup checks** for every external tool and resource (FFmpeg, Magick.NET native libs, archive path reachable/writable, free space, key ring). Missing items disable only the affected features and are shown on a health page. | User requirement: verify availability of all tools at startup. |
| D-18 | **UI language English**, localization prepared (resource files) so German can be added cheaply. | |
| D-19 | **Public GitHub repository**, MIT license, CI on all three OSes. No SonarQube. | User decision. |
| D-20 | **Connectivity first**: the first milestone proves login, 2FA, listing and downloading from .NET against a real account before the rest is built. | Biggest risk is the private API. |
| D-21 | **The app has its own login** (the master password), and a real deletion requires entering the master password again. | A misconfigured port must never allow someone else to delete photos. |
| D-22 | **Security baseline for running the app**: never as root/Administrator (the app refuses to start), hardened container, network shares mounted by the host or the Docker engine (never inside the container) with a dedicated low-privilege NAS account, master password only typed into the UI. | Untrusted media is processed by native libraries; the app can delete irreplaceable data. |

## 7. Limitations and risks everyone must accept

### 7.1 Apple-related

| # | Limitation | Consequence |
|---|---|---|
| L-01 | **Unofficial API.** Apple publishes no API for iCloud Photos; the app uses the same private web APIs as iCloud.com. | It can break at any time, until the iCloud client is fixed. Archived files stay safe. |
| L-02 | **Advanced Data Protection must be off** and **"Access iCloud Data on the Web" must be on** for every archived Apple ID. | Photos lose end-to-end encryption on Apple's servers while ADP is off. This is a real security trade-off each account owner should agree to. |
| L-03 | **2FA re-login every ~1–3 months per account**, using a code from *that person's* trusted device. | Runs for an account pause until its owner provides a code. |
| L-04 | **Deleting from iCloud deletes from all devices** of that Apple ID (iPhone, iPad, Mac). | Account owners will no longer see archived photos on their devices. They must be told and agree. |
| L-05 | **Storage is freed only 30 days after deletion** ("Recently Deleted"). | The plan can be downgraded about a month after the first big deletion run. |
| L-06 | **Managed or restricted accounts** (e.g. with Screen Time restrictions) may block iCloud web access. | Must be checked for each account before its first run. |
| L-07 | **Rate limits / account protection.** Too many requests can get an account temporarily blocked. | Conservative parallelism (D-11), backoff, and pausing. Full archive may take days. |
| L-08 | **What the web API doesn't provide is lost** when deleting: People/faces, Memories, search index, some edit history. | Accepted. Albums, favorites, captions are preserved via XMP (D-14). |
| L-09 | **Shared Photo Library items are not archived** (out of scope). | Storage used by items contributed to a Shared Library is not freed. |

### 7.2 Operational

| # | Limitation | Consequence |
|---|---|---|
| L-10 | **The archive storage becomes the only copy** of archived media once iCloud deletes it. | **The operator must back up the archive storage** (e.g. 3-2-1: second disk + offsite/cloud backup). This is outside the app but critical. |
| L-11 | The app runs as a Docker container; Docker (e.g. Docker Desktop on Windows/macOS) is required. | Initially on the operator's PC/MacBook, which must stay on and connected to the archive storage during a run. The same container can move to an always-on Linux host or NAS without changes. |
| L-12 | The archive storage can be a local drive, an external drive or a network share (SMB/NFS), passed into the container as a bind mount or a Docker CIFS volume. | With network shares, network drops pause the run; the journal guarantees nothing is lost or wrongly deleted. |
| L-13 | Viewable copies use lossy compression. | Only for convenience. The original is always kept. |
| L-14 | FFmpeg is an external program. | It ships in the container image. Only in the development mode (`dotnet watch` on the host) it must be installed manually; without it, video conversion is disabled, archiving and deletion still work. |
| L-15 | HEIC/HEVC originals need Microsoft's (free/cheap) HEIF/HEVC extensions to open on Windows. | Use the viewable copies, or install the extensions. |

## 8. Prerequisites before the first run

1. Turn **Advanced Data Protection off** for each Apple ID (Settings → [Name] → iCloud → Advanced Data Protection).
2. Turn **"Access iCloud Data on the Web" on** for each Apple ID (same screen).
3. Check that every account can sign in to iCloud.com.
4. Tell every account owner that photos older than the cutoff will disappear from their devices.
5. Set up a backup for the archive storage.
6. Install Docker (Docker Desktop on Windows/macOS) and share the archive folder with it, if a local drive is used.
7. If a NAS share is used: create a dedicated NAS account with access to the archive folder only, and enable SMB3 signing/encryption.

## 9. Milestones

| # | Milestone | Outcome |
|---|---|---|
| M0 | Repository skeleton **with container** | Solution in Onion layout; hardened Alpine Dockerfiles (app + probe) and `compose.yaml`; app starts in the container and shows the health page (FFmpeg, Magick.NET/HEIC on musl verified); CI (Windows/macOS/Linux tests + image build, non-root check, Trivy, SBOM); MIT license, `.gitignore`, Dependabot, secret scanning |
| M1 | **iCloud connectivity probe** | `Infrastructure.ICloud` + `tools/ICloudProbe`, run in its container (`docker compose run --rm probe`): SRP login, 2FA, session trust, list library oldest-first, download one original to `/archive`, read storage usage. No deletion. |
| M2 | Archive pipeline | Discover → download → verify → journal → XMP, resumable, one account |
| M3 | Web UI | Master-password login, accounts, settings, runs (start/pause/resume), progress, needs-attention list, storage view, health page, security headers |
| M4 | Viewable copies | Magick.NET + FFmpeg conversion step |
| M5 | **Deletion** | Deletion plan, dry-run, per-account switch, confirmation with step-up, gated delete, journal hash-chain check |
| M6 | Observability | Prometheus metrics, Loki logging, Grafana dashboard (compose profile `observability`) |
| M7 | Multiple accounts | Multi-account hardening, first real full run across all accounts |

## 10. Open items

| # | Item | Owner |
|---|---|---|
| O-1 | Turn off ADP / turn on web access on the operator's Apple ID (needed for M1) | Operator |
| O-2 | Same for all other accounts before M7 | Operator + account owners |
| O-3 | Fill the plan price table for the country | Operator |
| O-4 | Decide later whether Shared Albums / Shared Library get supported | Product owner |
