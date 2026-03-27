# tiktok-dl

Cross-platform CLI for downloading TikTok videos and images — runs on **Windows**, **macOS**, and **Linux** with no runtime required.

---

## Installation

Download the latest binary for your platform from the [Releases](../../releases/latest) page:

| Platform         | File                          |
| ---------------- | ----------------------------- |
| macOS (Apple Silicon) | `tiktok-dl-osx-arm64.tar.gz` |
| macOS (Intel)    | `tiktok-dl-osx-x64.tar.gz`   |
| Linux (x64)      | `tiktok-dl-linux-x64.tar.gz` |
| Windows (x64)    | `tiktok-dl-win-x64.zip`       |

Extract the archive and place the binary somewhere on your `PATH`.

**macOS / Linux:**
```bash
mkdir tiktok-dl && tar -xzf tiktok-dl-osx-arm64.tar.gz -C tiktok-dl
chmod +x tiktok-dl/tiktok-dl
sudo mv tiktok-dl /usr/local/lib/
sudo ln -s /usr/local/lib/tiktok-dl/tiktok-dl /usr/local/bin/tiktok-dl
```

**Windows:** extract the zip folder and add it to your `PATH` via System Settings, or run `tiktok-dl.exe` directly from that folder.

> **First run of `download-user`:** Chromium is required for profile scraping and will be downloaded automatically on first use (~150 MB).

---

## Features

- Download individual videos or image posts (SD or HD quality)
- Batch download from a text file of links
- Scrape and download all posts from a user's profile
- Resume interrupted downloads automatically
- No watermark by default (SD mode)

---

## Usage

```
tiktok-dl <command> [options]
```

### `download` — Single video or image post

```
tiktok-dl download <url> [options]
```

| Option               | Description                                               |
| -------------------- | --------------------------------------------------------- |
| `--hd`               | Download in HD quality via tikwm.com                      |
| `--watermark`        | Include watermark (SD only; not always available)         |
| `--avatar`           | Also download the poster's profile avatar                 |
| `-o, --output <dir>` | Output directory (default: `~/Downloads/TikTokDownloads`) |

**Examples:**

```bash
# Standard SD download
tiktok-dl download https://www.tiktok.com/@user/video/1234567890

# HD download
tiktok-dl download https://www.tiktok.com/@user/video/1234567890 --hd

# Short URL (auto-resolved)
tiktok-dl download https://vm.tiktok.com/abc123

# Custom output directory
tiktok-dl download https://www.tiktok.com/@user/photo/9876543210 -o ~/Downloads/tiktok
```

---

### `download-user` — All posts from a user's profile

```
tiktok-dl download-user <username> [options]
```

| Option               | Description                                               |
| -------------------- | --------------------------------------------------------- |
| `--hd`               | Download in HD quality                                    |
| `-o, --output <dir>` | Output directory (default: `~/Downloads/TikTokDownloads`) |
| `--browser <path>`   | Path to a custom Chromium executable                      |

Opens a browser window to scroll through the profile and collect all post links. You may need to solve a CAPTCHA or dismiss a cookie banner on first run.

**Examples:**

```bash
tiktok-dl download-user @username
tiktok-dl download-user @username --hd -o ~/Videos/tiktok
```

---

### `download-file` — Batch download from a text file

```
tiktok-dl download-file <file.txt> [options]
```

| Option               | Description                                               |
| -------------------- | --------------------------------------------------------- |
| `--hd`               | Download in HD quality                                    |
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
    │   └── <media-id>.mp4          # or <media-id>_HD.mp4 / <media-id>_Watermark.mp4
    ├── Images/
    │   ├── <media-id>_1.jpeg
    │   └── <media-id>_2.jpeg
    └── Avatars/
        └── avatar_<username>.jpeg
```

An index file (`<username>_index.txt`) is maintained per user so already-downloaded posts are skipped when re-running.

---

## API Notes

| Mode           | API Used                           | Notes                                             |
| -------------- | ---------------------------------- | ------------------------------------------------- |
| SD video       | `api22-normal-c-alisg.tiktokv.com` | May be unreliable after recent TikTok API changes |
| HD video       | `tikwm.com` (submit + poll)        | More reliable; 5,000 requests/day limit           |
| HD images      | `tikwm.com/api/`                   | Single GET request                                |
| Profile scrape | Playwright (Chromium)              | Required for `download-user`                      |

---

## Credits

This project is a C# reimplementation based on the original work by [Jettcodey](https://github.com/Jettcodey):
[https://github.com/Jettcodey/TikTok-Downloader](https://github.com/Jettcodey/TikTok-Downloader)

---

## License

See [LICENSE](LICENSE) in the repository root.
