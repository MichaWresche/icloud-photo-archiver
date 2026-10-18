# 6. Runtime View

## 6.1 Sign-in with 2FA

```mermaid
sequenceDiagram
    actor Op as Operator
    participant UI as Web UI
    participant Auth as SignIn use case
    participant IC as Infrastructure.ICloud
    participant Apple as Apple auth / setup
    participant SS as Session store
    actor Fam as Account owner's device

    Op->>UI: Sign in (alias, Apple ID, password)
    UI->>Auth: SignIn
    Auth->>IC: SRP init/complete
    IC->>Apple: signin/init (A)
    Apple-->>IC: salt, B, iterations
    IC->>Apple: signin/complete (M1, M2)
    Apple-->>IC: 409 – 2FA required
    Apple-->>Fam: Push 2FA code
    Fam-->>Op: tells code
    Op->>UI: enter code
    UI->>Auth: SubmitTwoFactorCode
    Auth->>IC: verify code, request trust
    IC->>Apple: verify + trust
    Apple-->>IC: session + trust token
    IC->>Apple: accountLogin
    Apple-->>IC: dsid, web service URLs
    Auth->>SS: save encrypted session
    Note over Auth: password is discarded from memory
    UI-->>Op: Account ready
```

When a stored session is still valid, the app reuses it without any prompt. When it expires (during a run or at startup), the account goes to **"re-login needed"** and its run pauses (D-12).

## 6.2 Archive run (one account)

```mermaid
sequenceDiagram
    participant R as ArchiveRunner
    participant UC as RunArchive
    participant J as Journal (archive storage)
    participant L as IPhotoLibrary
    participant FS as Archive file store
    participant V as Media validator
    participant X as Sidecar writer

    R->>UC: start(account)
    UC->>J: replay → current state
    UC->>L: query assets oldest-first until cutoff
    UC->>J: append AssetDiscovered (new ones)
    loop each pending asset, oldest first, 3–4 in parallel
        loop each resource (original, live video, edited)
            UC->>L: get download URL
            UC->>FS: stream to *.partial, SHA-256 on the fly
            UC->>FS: flush, compare size, rename to final name
            UC->>J: append ResourceDownloaded(hash, size, path)
            UC->>FS: read file back, SHA-256
            UC->>V: decode check
            UC->>J: append ResourceVerified
        end
        UC->>X: write .xmp, set file timestamps
        UC->>J: append SidecarWritten
    end
    UC-->>R: summary (verified, failed, needs attention)
```

Steps are **idempotent**: a crash between any two steps is repaired on the next run (§6.5).

## 6.3 Deletion run

```mermaid
sequenceDiagram
    actor Op as Operator
    participant UI as Web UI
    participant P as GetDeletionPlan (query)
    participant RM as DeletionCandidates read model
    participant D as ExecuteDeletion (command)
    participant FS as Archive file store
    participant L as IPhotoLibrary
    participant J as Journal

    Op->>UI: open Deletion (account)
    UI->>P: plan
    P->>RM: verified assets with sidecar
    P-->>UI: N items / X GB, oldest/newest date
    Op->>UI: Dry run
    UI->>D: execute(dryRun = true)
    D-->>UI: report (what would be deleted, gate failures)
    Op->>UI: Confirm (account switch must be ON)
    UI->>D: execute(dryRun = false)
    loop each asset
        D->>J: authoritative asset state (not the read model)
        D->>FS: re-hash every resource file
        alt hash matches journal
            D->>L: delete asset (records/modify)
            D->>J: append AssetDeleted
        else mismatch / missing
            D->>J: append ResourceFailed (gate)
        end
    end
    D-->>UI: summary
```

Deleted items move to iCloud "Recently Deleted" and are purged by Apple after 30 days (D-08).

## 6.4 Creating viewable copies

A separate background step after verification. For each verified original that is HEIC/HEIF/DNG/RAW (→ JPEG) or HEVC/ProRes (→ H.264 MP4), the converter writes to `*.partial`, validates, renames, and appends `CopyCreated`. Failures go to the needs-attention list but never block deletion. If FFmpeg is missing, video copies are skipped and shown as "unavailable".

## 6.5 Crash and interruption recovery

| Interrupted after… | Found on restart | Repair |
|---|---|---|
| Partial download | `*.partial` file, no `ResourceDownloaded` | Delete partial, download again |
| Rename, before journal append | Final file exists, no journal event | Hash the file; if size matches expected, append `ResourceDownloaded` and continue with verification; otherwise re-download |
| `ResourceDownloaded`, before verification | Event exists, no `ResourceVerified` | Run verification |
| iCloud delete call, before `AssetDeleted` | Event missing | Query asset in iCloud: not found / deleted → append `AssetDeleted`; still present → retry |
| Journal line half-written | Last line not valid JSON | Ignore the broken trailing line on replay (it was never acknowledged) and log a warning |

## 6.6 Session expired or rate-limited during a run

The `ICloudErrorMapper` classifies the error. `Transient` → retry with exponential backoff (resilience pipeline). `RateLimited` → pause the account for a growing interval, then continue. `SessionExpired` → pause the account, set "re-login needed", show a banner; after the operator signs in again, the run can be resumed. The runner then continues with the next account if one is queued.
