# tiktok-dl — Agent Guide

Cross-platform CLI for downloading TikTok videos and images. Built in C# / .NET 10.0 using Clean Architecture.

---

## Build & Test

```bash
# Build everything
dotnet build tiktok-dl.slnx

# Run all tests
dotnet test tiktok-dl.slnx

# Run the CLI without installing
dotnet run --project TikTokDl.CLI -- download <url>
```

**Always run `dotnet build` and `dotnet test` before considering a task complete.** Both must pass with zero errors and zero warnings.

---

## Project Layout

```
tiktok-dl/
├── tiktok-dl.slnx
├── TikTokDl.Core/          # Platform-agnostic business logic (library)
│   ├── Domain/
│   │   ├── Common/         # Result<T> — never throw; always return Result
│   │   └── Models/         # TikTokUrl, VideoData, DownloadOptions, TikTokUrlType
│   ├── Application/
│   │   ├── Interfaces/     # IMediaApiService, IHdApiService, IFileDownloadService,
│   │   │                   # IBrowserService, IProgressReporter, IUrlExpansionService,
│   │   │                   # INotificationService
│   │   └── UseCases/       # DownloadSingleMediaUseCase, DownloadFromFileUseCase,
│   │                       # DownloadByUsernameUseCase, ValidateAndProcessUrlUseCase
│   └── Infrastructure/
│       └── Services/       # TikTokApiService, TikWmApiService, FileDownloadService,
│                           # PlaywrightBrowserService, HttpUrlExpansionService
├── TikTokDl.CLI/           # Thin CLI adapter
│   ├── Program.cs          # DI wiring + command registration
│   ├── CliProgress.cs      # Spectre.Console IProgressReporter implementation
│   └── Commands/           # DownloadCommand, DownloadUserCommand, DownloadFileCommand
└── TikTokDl.Tests/         # xUnit tests (Moq + FluentAssertions)
```

---

## Architecture Rules

### Layer boundaries (enforced by project references)
- `TikTokDl.Core` has **no dependency** on CLI packages.
- `TikTokDl.CLI` references `TikTokDl.Core` only — never the reverse.
- `TikTokDl.Tests` references `TikTokDl.Core` only.

### Error handling
- All operations that can fail return `Result` or `Result<T>` (see [TikTokDl.Core/Domain/Common/Result.cs](TikTokDl.Core/Domain/Common/Result.cs)).
- **Do not throw exceptions** for expected failures (bad URL, network error, rate limit). Use `Result.Failure(message)`.
- Exceptions are acceptable only for truly unexpected conditions (programming errors).

### Adding a new service
1. Define the interface in `TikTokDl.Core/Application/Interfaces/`.
2. Implement it in `TikTokDl.Core/Infrastructure/Services/`.
3. Register it in `TikTokDl.CLI/Program.cs` via `services.Add*<IFoo, FooImpl>()`.
4. Inject it into the relevant use case via constructor.

### Adding a new command
1. Create `TikTokDl.CLI/Commands/MyCommand.cs` with a static `Build(IServiceProvider sp)` method returning a `Command`.
2. Register it in `Program.cs`: `rootCommand.AddCommand(MyCommand.Build(sp))`.
3. Strip leading `@` from username args — `System.CommandLine` beta4 treats `@` as a response-file prefix (handled globally in `Program.cs` already).

---

## Key Patterns

### TikTokUrl value object
`TikTokUrl.Create(string url)` returns `Result<TikTokUrl>`. It validates and classifies the URL into one of four types (`Video`, `PhotoCarousel`, `Profile`, `Short`) via regex, and exposes `Username` and `MediaId`. Short URLs set `RequiresExpansion = true` and must be resolved via `IUrlExpansionService` before use.

### Rate limiting & retries
- TikTok SD API: 1 request per 1–30 seconds; exponential backoff up to 60 s on HTTP 429 (max 5 retries).
- tikwm.com HD API: submit → poll with `task_id`, 500 ms interval, 15 poll retries; 5,000 requests/day limit.
- A 1.9-second pre-download delay is applied in `FileDownloadService` to stay within rate limits.

### Resume support
Each user gets a `<username>_index.txt` file in the output directory listing downloaded media IDs. `DownloadSingleMediaUseCase` checks this index and skips already-downloaded items. Partial files are continued using `Range` HTTP headers.

### HD vs SD
- **SD**: `IMediaApiService` → `TikTokApiService` (official TikTok API, no watermark by default).
- **HD**: `IHdApiService` → `TikWmApiService` (tikwm.com, two-step submit/poll for videos, single GET for images).

### Playwright / profile scraping
`PlaywrightBrowserService` opens Chromium, navigates to the profile page, and scrolls to collect all post links. If Chromium is not installed it runs `playwright install chromium` automatically. A custom browser path can be passed via `--browser`.

---

## Testing

- Test framework: **xUnit** with **Moq** for mocks and **FluentAssertions** for assertions.
- Tests live in `TikTokDl.Tests/` mirroring the `Core/` structure.
- **Do not** make real HTTP calls or launch a browser in tests — mock `IMediaApiService`, `IHdApiService`, `IFileDownloadService`, `IBrowserService`, and `IUrlExpansionService`.
- Tests for infrastructure services that make HTTP calls (`TikTokApiService`, `TikWmApiService`) belong in integration tests (not yet present); keep them out of the unit-test project.

Run tests:
```bash
dotnet test tiktok-dl.slnx
```

---

## Code Style

- C# 13 / .NET 10 idioms: `latest` `LangVersion`, implicit usings, nullable reference types enabled.
- Prefer `record` for immutable data (`DownloadOptions`, `VideoData`).
- Prefer `sealed` for value objects that should not be subclassed (`TikTokUrl`).
- Log at `Warning` by default; use structured logging with named parameters:
  ```csharp
  _logger.LogInformation("Downloaded {Count} files for @{Username}", count, username);
  ```
- No public `async void` — always `async Task` or `async Task<T>`.
- Namespace must match folder path relative to the project root.

---

## Output Directory Convention

```
<output-dir>/
└── @username/
    ├── Videos/          # <media-id>.mp4  |  <media-id>_HD.mp4  |  <media-id>_Watermark.mp4
    ├── Images/           # <media-id>_1.jpeg, <media-id>_2.jpeg, …
    └── Avatars/          # avatar_<username>.jpeg
```

Index file for resume tracking: `<output-dir>/@username/<username>_index.txt`

---

## Publishing

Self-contained single executables (no runtime required on the target machine):

```bash
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r linux-x64   -p:SelfContained=true -c Release -o ./publish/linux-x64
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r osx-arm64   -p:SelfContained=true -c Release -o ./publish/osx-arm64
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r osx-x64     -p:SelfContained=true -c Release -o ./publish/osx-x64
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r win-x64     -p:SelfContained=true -c Release -o ./publish/win-x64
```
