# 11. Risks and Technical Debt

## 11.1 Risks

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R-1 | **Apple changes or blocks the private API** (auth, CloudKit) | High (over time) | High: app stops working | Isolated `Infrastructure.ICloud`; recorded fixtures; error mapping; the archive remains valid. Watch reference projects for protocol changes. |
| R-2 | **SRP/2FA implementation can't be made to work from .NET** | Medium | Blocking | Milestone M1 first (ADR-013); test vectors; compare with reference implementations. |
| R-3 | **Wrong deletion** caused by a bug | Low | Very high | Domain state machine, deletion gate with re-hash, dry-run, default-off switch, operator confirmation, 30-day Recently Deleted, tests. |
| R-4 | **The archive storage is the only copy** after deletion; disk failure | Medium | Very high | Out of the app's control: operator must back up the archive storage (L-10). Health check warns about low free space. |
| R-5 | Account temporarily locked by Apple due to request volume or repeated logins | Medium | Medium | Bounded parallelism, backoff, reuse sessions, sequential accounts. |
| R-6 | Managed/restricted accounts blocked from web access | Medium | Medium: those accounts can't be archived | Check each account before its first run; document workaround (account restriction settings). |
| R-7 | Atomic rename / flush semantics differ on network shares (SMB/NFS, NAS vendors) | Medium | Medium: journal or file corruption on crash | Read-back verification; tolerate broken trailing line; recovery tests on a local drive and a network share in M2. |
| R-8 | iCloud metadata (capture date, albums) incomplete or inconsistent | Medium | Low | Fallback to added date; record raw values in the journal. |
| R-9 | HEIC/HEVC decoding differences between platforms (Magick.NET builds) | Low | Low | Validation only checks decodability; CI runs media tests on all three OSes. |
| R-10 | Accidental commit of personal data to the public repo | Low | High | `.gitignore`, sanitized fixtures, secret scanning with push protection, review. |
| R-11 | Turning off ADP weakens the accounts' iCloud security | Certain while in use | Medium | Product decision accepted by each account owner (L-02); ADP can be turned back on once archiving is finished. |
| R-12 | Long runs on a laptop get interrupted by sleep | High | Low | Resumable by design; the same container can move to an always-on host. |
| R-13 | Magick.NET native libraries (HEIC) not fully supported on Alpine/musl, especially ARM64 | Medium | Medium: image conversion/validation fails in the container | Verify in M0; health check inside the image in CI; fallback base image Ubuntu Chiseled (ADR-018). |
| R-14 | Exploit in a media parser (FFmpeg, ImageMagick, libheif) triggered by a malformed photo/video | Low | High: code execution in the app's context | Restrictive ImageMagick policy, sandboxed FFmpeg calls with timeouts (§8.12); non-root, capability-free, read-only container (§8.11); Dependabot + Trivy for timely patches. |
| R-15 | Misconfigured deployment exposes the UI or internal endpoints on the LAN | Medium | High: unauthorized deletion or information disclosure | Built-in login + step-up for deletion (ADR-019); UI published on localhost by default; metrics/health details on an unpublished internal port. |
| R-17 | Slow file I/O for bind mounts from Windows drives (Docker Desktop / WSL 2) | Medium | Low: longer runs | Acceptable for archiving (network-bound anyway); alternatively a CIFS volume or a drive inside WSL; measured in M2. |
| R-18 | CIFS volume credentials are stored in `.env` and visible via `docker volume inspect` on the host | Medium | Medium: NAS account credentials exposed to host users | Dedicated NAS account limited to the archive folder; `.env` git-ignored with restrictive file permissions; host access limited to the operator. |
| R-19 | Docker Desktop file sharing or volume misconfiguration makes `/archive` point to an empty or wrong folder | Medium | Medium: archive written to the wrong place; journal not found | Startup check: `/archive` must be a mount (not the container's own file system) and contain the expected marker file once initialized; otherwise runs are blocked. |
| R-16 | Journal tampered with or corrupted on the archive storage | Low | High: wrong deletion decisions | Hash chain (ADR-021) locks deletions on mismatch; dedicated NAS account with minimal rights; re-hash before each delete. |

## 11.2 Technical debt (known, accepted)

| # | Item | Reason / plan |
|---|---|---|
| TD-1 | No scheduler | Manual start is enough for now; the runner is designed so a scheduler can trigger it later. |
| TD-2 | ~~No authentication in the app itself~~ | **Resolved** by ADR-019 (master-password login, step-up for deletion). |
| TD-6 | Journal HMAC not implemented (hash chain only) | Hash chain covers corruption and single-line tampering; HMAC can be added later if the archive storage is shared with untrusted users (ADR-021). |
| TD-3 | Journal projections are in memory only | Fine up to a few hundred thousand items; revisit if memory grows too much. |
| TD-4 | Shared Albums / Shared Library not supported | Out of scope (ADR-016). |
| TD-5 | No re-import into iCloud/Photos | Not needed for the goal. |
