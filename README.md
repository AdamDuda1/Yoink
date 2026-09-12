# Yoink Downloader

A WinUI 3 desktop wrapper for downloading and processing video/audio, built on top of CLI tools instead of reinventing them.

## Features

- Fetch video metadata and thumbnails before downloading
- Multiple format/quality selection
- Optional accelerated downloads via aria2c
- Post-processing (merging, converting, trimming) via ffmpeg
- Metadata inspection via exiftool
- Native, *beautiful* Windows UI

## Built On

| Tool | Purpose |
|------|---------|
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | extraction and downloading (youtube, spotify, soundcloud and youtube music) |
| [ffmpeg](https://ffmpeg.org/) | encoding, muxing, format conversion |
| [aria2](https://aria2.github.io/) | optional faster, multi-connection downloads |
| [exiftool](https://exiftool.org/) | metadata reading and writing |

Yoink does not reimplement any of these, it shells out to them and gives you a UI on top.

## Requirements

- Windows 10/11
- yt-dlp, ffmpeg installed and on PATH (OR configured in-app OR downloaded through the app OR (maybe) bundled with the EXE)
- aria2c and exiftool optional, only needed if you enable those features

## TODO:

todo

## License

TBD