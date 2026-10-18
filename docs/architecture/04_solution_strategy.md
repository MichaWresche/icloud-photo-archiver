# 4. Solution Strategy

| Quality goal / constraint | Strategy | Details |
|---|---|---|
| Data safety | **Verify-then-delete gate** with four layers; deletion as a separate, confirmed phase; journal as single source of truth | §6.2, §6.3, §8.3, ADR-007, ADR-008 |
| Data safety | **Append-only event journal** in the archive storage; atomic file writes (temp file + rename) | §8.2, ADR-005 |
| Security | No stored passwords; session tokens encrypted with ASP.NET Core Data Protection, key ring encrypted with a key derived from a master password; localhost binding; masked identifiers in telemetry; master-password login with step-up for deletion; hardened non-root container; untrusted-input handling; hash-chained journal | §8.1, §8.11, §8.12, ADR-009, ADR-019, ADR-020, ADR-021 |
| Resumability | State machine per asset, rebuilt by replaying the journal; every step idempotent | §5.3, §6.5 |
| Maintainability | **Light CQRS** on the journal: commands append events, queries read synchronously updated in-memory projections; no mediator, no bus | §5.2, ADR-017 |
| Maintainability | **Onion Architecture**; iCloud behind ports (`IICloudAuthenticator`, `IPhotoLibrary`); recorded HTTP fixtures for tests; architecture tests | §5, ADR-004 |
| Portability | .NET 10 only, Blazor Web App, Magick.NET with bundled natives, FFmpeg as optional process; **Docker container as the runtime from M0** (Docker Desktop on Windows/macOS); archive storage is any file system path (local drive or network share) | ADR-003, ADR-010, ADR-022 |
| Biggest risk: private API | **Connectivity first** (M1): the production `Infrastructure.ICloud` project is built first and run against a real account via `tools/ICloudProbe` | ADR-013 |
| Transparency | Health page + `/health`, needs-attention list, Serilog → Loki, Prometheus metrics, Grafana dashboard | §8.6, ADR-011 |
| Apple rate limits | Accounts processed one after another; 3–4 parallel downloads; resilience pipeline with backoff; pause on 429/503 or expired session | §8.5, ADR-012 |

## 4.1 Technology decisions

| Area | Choice |
|---|---|
| Runtime | .NET 10, C# latest |
| UI | Blazor Web App, interactive server render mode |
| HTTP | `HttpClient` via `IHttpClientFactory`, `Microsoft.Extensions.Http.Resilience` |
| Cookies/session | Own cookie container per account, serialized into the encrypted session store |
| SRP-6a | Own implementation on `System.Numerics.BigInteger` + `System.Security.Cryptography` (PBKDF2, SHA-256) |
| JSON | `System.Text.Json` with source generators |
| Images | Magick.NET (Q16, bundled natives, HEIC via libheif) |
| Videos | FFmpeg (external) via FFMpegCore |
| Hashing | SHA-256 (`IncrementalHash`) while streaming |
| Encryption at rest | ASP.NET Core Data Protection + custom `IXmlEncryptor` (AES-GCM, key from master password via PBKDF2-SHA256 with high iteration count) |
| Logging | Serilog: console, rolling file, Loki sink |
| Metrics | `prometheus-net.AspNetCore` (`/metrics`) |
| Tests | xUnit, bUnit (Blazor components), NetArchTest (layer rules), recorded HTTP fixtures |
| CI | GitHub Actions matrix: `windows-latest`, `macos-latest`, `ubuntu-latest` |

## 4.2 Approach to the work

Milestones M0–M7 (see product decisions §9). Deletion (M5) is deliberately built late, after the archive pipeline and its verification have been used in practice.
