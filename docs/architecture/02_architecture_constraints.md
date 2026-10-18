# 2. Architecture Constraints

## 2.1 Technical constraints

| # | Constraint | Background |
|---|---|---|
| TC-1 | **.NET 10 and the latest C# version** | Operator is a .NET developer. No Python, no Node in the product. |
| TC-2 | **No OS-specific dependencies** (no Windows DPAPI, no macOS Keychain, no WinForms/WPF/MAUI-only APIs) | Must run on Windows, macOS and Linux/Docker. |
| TC-3 | **Web UI** (Blazor Web App) | Platform-independent UI, usable from any browser; required for running in a container. |
| TC-9 | **Docker container is the runtime from the first milestone on** (Linux image, Docker Desktop on Windows/macOS) | Operator decision (ADR-022). Direct `dotnet` execution is for development only. |
| TC-4 | **No database**, not even SQLite. State is kept in plain text files. | Operator decision. |
| TC-5 | **No official Apple API exists** for iCloud Photos. The private iCloud.com web APIs must be used. | Requires ADP off and web access on (L-02). |
| TC-6 | **No maintained .NET iCloud library exists.** The iCloud client is written in-house, using the open-source Python projects `icloudpd`/`pyicloud` and the Node project `icloud-photos-sync` as references for the protocol. | Reference code is read, not copied (license hygiene). |
| TC-7 | External tools allowed: **FFmpeg** (video), **Magick.NET** with bundled native libraries (images). Both must be checked at startup. | Cross-platform conversion requires native codecs. |
| TC-8 | The archive storage is **any file system path**, inside the container always `/archive`: a local or external drive (bind mount) or a network share (Docker CIFS volume or host mount). The app treats all of them the same. | No storage vendor APIs; only `System.IO`. |

## 2.2 Organizational constraints

| # | Constraint |
|---|---|
| OC-1 | **Public GitHub repository**, MIT license. No secrets, account names or personal data may ever be committed. |
| OC-2 | **GitHub Actions CI** on Windows, macOS and Linux. |
| OC-3 | **No SonarQube.** Code quality is ensured by compiler, analyzers and tests. |
| OC-4 | All documentation in **Markdown**; architecture follows **arc42**. |
| OC-5 | Single developer; the private iCloud API must be proven first (milestone M1) before investing in the rest. |

## 2.3 Conventions

| # | Convention |
|---|---|
| CV-1 | **Onion Architecture**; dependency rules enforced by architecture tests. |
| CV-2 | `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-all</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, shared via `Directory.Build.props`. |
| CV-3 | Central package management (`Directory.Packages.props`). |
| CV-4 | `.editorconfig` defines code style. |
| CV-5 | **xUnit** for all tests. |
| CV-6 | All times stored in UTC (ISO 8601); time is accessed through `TimeProvider`. |
| CV-7 | UI texts in resource files (English first, German prepared). |
