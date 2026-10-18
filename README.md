# iCloud Photo Archiver

A cross-platform .NET application that archives the oldest photos and videos of one or more iCloud accounts to local storage (a local drive or a network share) in original quality, verifies every file, and only then removes it from iCloud, so the iCloud storage plan(s) can be downgraded.

> [!WARNING]
> This project uses Apple's **unofficial, undocumented** iCloud web APIs. Apple may change or block them at any time without notice. Every Apple ID used needs **"Access iCloud Data on the Web" turned on** and **Advanced Data Protection turned off**. Deleting a photo from iCloud deletes it from **every device** synced to that account. Use at your own risk, and keep a backup of your archive.

## Status

Design phase. No code yet. See the documentation:

| Document | Purpose |
|---|---|
| [Product decisions & limitations](docs/product/product-decisions.md) | Product owner's view: goals, scope, decisions, limitations, milestones |
| [Architecture documentation (arc42)](docs/architecture/README.md) | Technical architecture following the arc42 template |

## Tech at a glance

- Runs as a hardened, non-root Docker container (Alpine) from day one; Docker Desktop on Windows/macOS
- .NET 10 / latest C#, Blazor Web App (interactive server), Onion Architecture
- Own iCloud client (SRP login, 2FA, CloudKit photo queries)
- Plain-text state: an append-only JSON Lines journal per account, stored with the archive
- Magick.NET (images) and FFmpeg (videos) for viewable copies
- Serilog → Loki, Prometheus `/metrics`, Grafana dashboard (compose profile `observability`)
- xUnit, `TreatWarningsAsErrors`, GitHub Actions CI on Windows, macOS and Linux

## License

MIT (added with the first code milestone).
