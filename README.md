# tiktok-dl

Cross-platform CLI for downloading TikTok videos and images — runs on **Windows**, **macOS**, and **Linux**.

---

## Features

- Download individual videos or image posts (SD or HD quality)
- Batch download from a text file of links
- Scrape and download all posts from a user's profile
- Resume interrupted downloads automatically
- Progress output to the terminal via [Spectre.Console](https://spectreconsole.net/)
- Clean Architecture core library (`TikTokDl.Core`) — usable independently

---

## Prerequisites

| Requirement                                                        | Version                           |
| ------------------------------------------------------------------ | --------------------------------- |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0)      | 10.0+                             |
| [Playwright browsers](https://playwright.dev/dotnet/docs/browsers) | Required for `download-user` only |

Install Playwright's bundled Chromium (needed for `download-user`):

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

---

## Build

```bash
# Clone the repo
git clone https://github.com/Jettcodey/tiktok-dl.git
cd tiktok-dl

# Build all projects
dotnet build tiktok-dl.slnx

# Run tests
dotnet test tiktok-dl.slnxx

# Run the CLI directly (without installing)
dotnet run --project TikTokDl.CLI -- download <url>
```

### Self-Contained Publish

Produce a single executable with no runtime dependency:

```bash
# Linux (x64)
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r linux-x64 -p:SelfContained=true -c Release -o ./publish/linux-x64

# macOS (arm64 — Apple Silicon)
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r osx-arm64 -p:SelfContained=true -c Release -o ./publish/osx-arm64

# macOS (x64 — Intel)
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r osx-x64 -p:SelfContained=true -c Release -o ./publish/osx-x64

# Windows (x64)
dotnet publish TikTokDl.CLI/TikTokDl.CLI.csproj -r win-x64 -p:SelfContained=true -c Release -o ./publish/win-x64
```

Add the output folder to your `PATH` to use `tiktok-dl` from anywhere.

---

## Usage

```
tiktok-dl <command> [options]
```

### Commands

#### `download` — Single video or image post

```
tiktok-dl download <url> [options]
```

| Option               | Description                                             |
| -------------------- | ------------------------------------------------------- |
| `--hd`               | Download in HD quality via tikwm.com                    |
| `--watermark`        | Include watermark (SD only; not always available)       |
| `--avatar`           | Also download the poster's profile avatar               |
| `-o, --output <dir>` | Output directory (default: `~/Downloads/TikTokDownloads`) |

**Examples:**

```bash
# Standard download
tiktok-dl download https://www.tiktok.com/@user/video/1234567890

# HD download
tiktok-dl download https://www.tiktok.com/@user/video/1234567890 --hd

# Short URL (auto-resolved)
tiktok-dl download https://vm.tiktok.com/abc123

# Custom output directory
tiktok-dl download https://www.tiktok.com/@user/photo/9876543210 -o ~/Downloads/tiktok
```

---

#### `download-user` — All posts from a user's profile

```
tiktok-dl download-user <username> [options]
```

| Option               | Description                                             |
| -------------------- | ------------------------------------------------------- |
| `--hd`               | Download in HD quality                                  |
| `-o, --output <dir>` | Output directory (default: `~/Downloads/TikTokDownloads`) |
| `--browser <path>`   | Path to a custom browser executable for Playwright      |

> Opens a browser window to scroll through the profile and collect links. You may need to solve a CAPTCHA or dismiss a cookie banner on first run.

**Examples:**

```bash
tiktok-dl download-user @username
tiktok-dl download-user @username --hd -o ~/Videos/tiktok
```

---

#### `download-file` — Batch download from a text file

```
tiktok-dl download-file <file.txt> [options]
```

| Option               | Description                                             |
| -------------------- | ------------------------------------------------------- |
| `--hd`               | Download in HD quality                                  |
| `-o, --output <dir>` | Output directory (default: `~/Downloads/TikTokDownloads`) |

The text file should contain one TikTok URL per line:

```
https://www.tiktok.com/@user/video/1234567890
https://www.tiktok.com/@user/photo/9876543210
https://vm.tiktok.com/abc123
```

**Example:**

```bash
tiktok-dl download-file links.txt --hd -o ~/Downloads/tiktok
```

---

## Output Structure

Downloads are organised automatically:

```
<output-dir>/
└── @username/
    ├── Videos/
    │   └── video_<id>.mp4
    ├── Images/
    │   ├── image_<id>_1.jpeg
    │   └── image_<id>_2.jpeg
    └── Avatars/
        └── avatar_<username>.jpeg
```

An index file (`downloaded_<username>.txt`) is maintained per user to skip already-downloaded posts when re-running.

---

## API Notes

| Mode           | API Used                           | Notes                                             |
| -------------- | ---------------------------------- | ------------------------------------------------- |
| SD video       | `api22-normal-c-alisg.tiktokv.com` | May be unreliable after recent TikTok API changes |
| HD video       | `tikwm.com` (submit + poll)        | More reliable; requires two API calls             |
| HD images      | `tikwm.com/api/`                   | Single GET request                                |
| Profile scrape | Playwright (Chromium)              | Required for `download-user`                      |

---

## Project Structure

```
tiktok-dl/
├── tiktok-dl.slnx
├── TikTokDl.Core/                  # Platform-agnostic business logic
│   ├── Domain/
│   │   ├── Common/Result.cs        # Result<T> error handling
│   │   └── Models/                 # VideoData, DownloadOptions
│   ├── Application/
│   │   ├── Interfaces/             # IMediaApiService, IHdApiService, ...
│   │   └── UseCases/               # DownloadSingle, DownloadFromFile, DownloadByUsername
│   └── Infrastructure/
│       └── Services/               # TikTokApiService, TikWmApiService, FileDownloadService, ...
├── TikTokDl.CLI/                   # CLI entry point
│   ├── Commands/                   # download, download-user, download-file
│   ├── CliProgress.cs              # Spectre.Console progress reporter
│   └── Program.cs                  # DI setup + command registration
├── TikTokDl.Tests/                 # xUnit tests
└── NuGet.config                    # Scoped NuGet source (nuget.org only)
```

---

## Running Tests

```bash
dotnet test tiktok-dl.slnx
```

---

## Credits

This project is a C# reimplementation based on the original work by [Jettcodey](https://github.com/Jettcodey):
[https://github.com/Jettcodey/TikTok-Downloader](https://github.com/Jettcodey/TikTok-Downloader)

This repository was generated with AI assistance.

---

## License

See [LICENSE](LICENSE) in the repository root.
