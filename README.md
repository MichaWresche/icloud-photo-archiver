# iCloud Photo Archiver

A cross-platform .NET application that archives the oldest photos and videos of one or more iCloud accounts to local storage (a local drive or a network share) in original quality, verifies every file, and only then removes it from iCloud, so the iCloud storage plan(s) can be downgraded.

> [!WARNING]
> This project uses Apple's **unofficial, undocumented** iCloud web APIs. Apple may change or block them at any time without notice. Every Apple ID used needs **"Access iCloud Data on the Web" turned on** and **Advanced Data Protection turned off**. Deleting a photo from iCloud deletes it from **every device** synced to that account. Use at your own risk, and keep a backup of your archive.

## Status

Milestone M0 (walking skeleton) in progress. Planning and progress: [project board](https://github.com/users/MichaWresche/projects/1) and [issues](https://github.com/MichaWresche/icloud-photo-archiver/issues).

| Document | Purpose |
|---|---|
| [Product decisions & limitations](docs/product/product-decisions.md) | Product owner's view: goals, scope, decisions, limitations, milestones |
| [Architecture documentation (arc42)](docs/architecture/README.md) | Technical architecture following the arc42 template |

## Getting started

The app is meant to run as a Docker container:

```sh
cp deploy/.env.example deploy/.env    # choose the archive folder or share
docker compose -f deploy/compose.yaml up
```

Then open http://127.0.0.1:8080. The container setup is added with [#10](https://github.com/MichaWresche/icloud-photo-archiver/issues/10).

### Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build PhotoArchiver.slnx
dotnet test --solution PhotoArchiver.slnx
dotnet run --project src/PhotoArchiver.Web
```

The solution follows the Onion layout described in [§5 of the architecture documentation](docs/architecture/05_building_block_view.md). `PhotoArchiver.Architecture.Tests` fails the build when a project references or uses a layer it must not depend on.

## Tech at a glance

- Runs as a hardened, non-root Docker container (Alpine) from day one; Docker Desktop on Windows/macOS
- .NET 10 / latest C#, Blazor Web App (interactive server), Onion Architecture
- Own iCloud client (SRP login, 2FA, CloudKit photo queries)
- Plain-text state: an append-only JSON Lines journal per account, stored with the archive
- Magick.NET (images) and FFmpeg (videos) for viewable copies
- Serilog → Loki, Prometheus `/metrics`, Grafana dashboard (compose profile `observability`)
- xUnit, `TreatWarningsAsErrors`, GitHub Actions CI on Windows, macOS and Linux

## License

[MIT](LICENSE)
