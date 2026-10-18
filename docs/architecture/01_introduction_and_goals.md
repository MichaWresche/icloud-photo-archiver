# 1. Introduction and Goals

## 1.1 Requirements overview

The iCloud Photo Archiver moves old photos and videos from the iCloud Photos libraries of **multiple Apple IDs** to local archive storage (a local drive or a network share), so the iCloud+ storage plan(s) can be downgraded. The accounts are independent of each other; they may each have their own plan or share one.

Core functional requirements:

| # | Requirement |
|---|---|
| F-1 | Manage several Apple ID accounts (login with password + 2FA, session renewal) from one UI. |
| F-2 | List each personal library (including Hidden) and select all items older than a configurable cutoff (global default, per-account override). |
| F-3 | Download items **oldest first** in original quality: original file, Live Photo video, edited version. |
| F-4 | Store them in the archive storage in a dated folder structure with a fixed naming scheme. |
| F-5 | Verify each file (size, SHA-256 read-back, decodability) and record the result in a journal. |
| F-6 | Write an XMP sidecar with metadata that would otherwise be lost (albums, favorite, hidden, caption, edit info). |
| F-7 | Create viewable copies (JPEG / H.264 MP4) next to the originals. |
| F-8 | Delete verified items from iCloud in a separate, confirmed step (dry-run, per-account switch). |
| F-9 | Start, pause and resume runs at any time; continue exactly where a run stopped. |
| F-10 | Show storage: iCloud usage per account, each plan's total/used (with per-account breakdown for shared plans), archive storage free/used, archive progress, plan-downgrade suggestion. |
| F-11 | Check all external tools and resources at startup and show their health. |
| F-12 | Expose logs (Loki) and metrics (Prometheus) for a Grafana dashboard. |

Details, scope and limitations: [Product Decisions & Limitations](../product/product-decisions.md).

## 1.2 Quality goals

Ordered by priority:

| Prio | Quality goal | Motivation |
|---|---|---|
| 1 | **Data safety** | Nothing is deleted from iCloud unless a verified, byte-exact copy exists in the archive storage. Irreplaceable personal photos. |
| 2 | **Security** | Several Apple IDs, not all owned by the operator. No stored passwords, encrypted tokens, public repository without secrets, masked identifiers in logs. |
| 3 | **Recoverability / resumability** | Runs take days; crashes, reboots, network or storage drops must never corrupt state or lose progress. |
| 4 | **Maintainability & testability** | The private iCloud API will change. The iCloud client must be isolated, replaceable and covered by tests with recorded responses. |
| 5 | **Portability** | Windows, macOS and Linux/Docker with the same code, no OS-specific dependencies. |
| 6 | **Transparency / observability** | The operator always knows what happened to each item, and why. |

## 1.3 Stakeholders

| Role | Expectations |
|---|---|
| Operator (developer) | Safe, understandable tool; clean .NET code and architecture; runs as a Docker container on the PC/MacBook and on any later host |
| Account owners | Their photos are not lost; little effort (only occasional 2FA codes); recent photos stay on their devices |
| Contributors (public repo) | Readable architecture, documented decisions, reproducible build and tests |
