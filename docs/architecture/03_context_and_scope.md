# 3. Context and Scope

## 3.1 Business context

```mermaid
flowchart LR
    op([Operator])
    fam([Account owners<br/>trusted Apple devices])
    app[iCloud Photo Archiver]
    icloud[(Apple iCloud<br/>Photos + Account)]
    store[(Archive storage<br/>local drive or network share)]
    obs[Grafana stack<br/>optional]

    op -- "settings, passwords, 2FA codes,<br/>start/pause, confirm deletion" --> app
    app -- "progress, storage overview,<br/>health, needs attention" --> op
    fam -- "2FA code (via operator)" --> op
    app -- "login, list, download, delete,<br/>storage usage" --> icloud
    icloud -- "2FA push" --> fam
    app -- "originals, copies, sidecars, journal" --> store
    app -- "logs, metrics" --> obs
    op -- "dashboards" --> obs
```

| Partner | Input to the system | Output from the system |
|---|---|---|
| Operator | Account aliases, Apple ID, password (not stored), 2FA codes, master password, cutoff settings, run commands, deletion confirmation | UI: progress, storage overview, deletion plan, health, errors |
| Account owners | 2FA codes from their trusted devices, passed on by the operator | Photos older than the cutoff disappear from their devices (L-04) |
| Apple iCloud | Session tokens, asset lists and metadata, file contents, storage usage | Login requests, queries, download requests, delete requests |
| Archive storage | Free space, existing files (for verification) | Originals, viewable copies, XMP sidecars, journals |
| Grafana stack | – | Logs (Loki), metrics (Prometheus scrape) |

## 3.2 Technical context

```mermaid
flowchart LR
    browser[Browser<br/>127.0.0.1:8080]
    subgraph host[archiver container on the Docker host]
        app[Archiver<br/>ASP.NET Core / Blazor]
        ffmpeg[FFmpeg process]
        local[(/data volume<br/>settings.json, sessions.enc,<br/>keys, logs)]
    end
    idmsa[idmsa.apple.com<br/>SRP auth, 2FA]
    setup[setup.icloud.com<br/>account, webservices, storage]
    ck[CloudKit database web service<br/>Photos library]
    cdn[iCloud content CDN<br/>file downloads]
    store[(Archive storage at /archive<br/>bind mount of a local drive<br/>or CIFS volume of a share)]
    loki[Loki]
    prom[Prometheus]

    browser -- "HTTPS + SignalR" --> app
    app -- HTTPS --> idmsa
    app -- HTTPS --> setup
    app -- HTTPS --> ck
    app -- HTTPS --> cdn
    app -- "file I/O" --> store
    app -- "process call" --> ffmpeg
    app -- "file I/O" --> local
    app -- "HTTP push" --> loki
    prom -- "HTTP scrape /metrics" --> app
```

| Channel | Technology | Notes |
|---|---|---|
| Browser ↔ App | HTTP(S), Blazor interactive server (SignalR) | Container port published on `127.0.0.1:8080` only; TLS via reverse proxy for LAN access |
| App → Apple authentication | HTTPS/JSON, SRP-6a, 2FA (trusted device code) | Endpoints to be confirmed in M1 |
| App → iCloud setup service | HTTPS/JSON | Account login, web service URLs, storage usage |
| App → CloudKit | HTTPS/JSON (`records/query`, `records/modify`) | Listing, metadata, delete. Private, undocumented. |
| App → content CDN | HTTPS, streamed download | Download URLs are short-lived; requested shortly before use |
| App → archive storage | File system API (`System.IO`) on `/archive` | Backed by a bind mount (local drive, e.g. `D:\PhotoArchive`) or a Docker CIFS volume (network share) |
| App → FFmpeg | Child process inside the container | Shipped in the image; path configurable for the development mode |
| App → Loki | HTTP push (Serilog Loki sink) over the compose network | Optional (profile `observability`) |
| Prometheus → App | HTTP scrape `archiver:9091/metrics` over the compose network | Optional; port not published |

## 3.3 Out of scope

Shared Albums, iCloud Shared Photo Library, Recently Deleted, re-upload to iCloud, scheduling, multi-user login. See product decisions §5.
