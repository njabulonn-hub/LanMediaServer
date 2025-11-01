# LocalWifiMediaServer

A LAN-only media server that scans local libraries, stores metadata in SQLite, and serves a PWA browser client.

## Features

- .NET 8 minimal API that can run as a Windows service (`UseWindowsService`).
- Configuration stored on disk at `%ProgramData%/MediaServer/config/config.json` (or platform equivalent).
- SQLite persistence with automatic schema creation and library sync.
- File system scanner with ffprobe enrichment for duration and codec metadata.
- Direct-play streaming with HTTP range support plus on-demand HLS transcoding through ffmpeg.
- Device registration and approval workflow for LAN clients.
- Background metadata enrichment using TMDB with local artwork caching.
- SSDP and mDNS broadcasts for discovery on the local network.
- Browser front-end hosted from `/media-center` with offline support (PWA + service worker).
- PowerShell installer that publishes a self-contained Windows service and opens firewall port 8090.

## Repository layout

```
/
├── MediaServer.sln                 # Solution file
├── src/MediaServer.Server/         # ASP.NET Core host
│   ├── Configuration/              # Config models + loaders
│   ├── Data/                       # SQLite access layer
│   ├── Devices/                    # Device management
│   ├── Discovery/                  # SSDP + mDNS helpers
│   ├── Hosting/                    # Startup hosted services
│   ├── Metadata/                   # TMDB enrichment worker
│   ├── Middleware/                 # LAN-only gate
│   ├── Scanning/                   # Library scanner + ffprobe integration
│   ├── Streaming/                  # HLS orchestration
│   ├── Utilities/                  # Helpers (MIME types)
│   └── wwwroot/media-center/       # Browser client (PWA)
├── docs/                           # API contracts and briefs
└── build/install.ps1               # Windows install script
```

## Getting started

1. Install the .NET 8 SDK on your development machine.
2. Restore dependencies and build:

   ```bash
   dotnet restore
   dotnet build
   ```

3. Run the server locally (Kestrel listens on port 8090 by default):

   ```bash
   dotnet run --project src/MediaServer.Server
   ```

4. Open the browser client at [http://localhost:8090/media-center/](http://localhost:8090/media-center/).

### Configuration

Configuration lives at `%ProgramData%/MediaServer/config/config.json` on Windows (or the equivalent under `$HOME/.local/share/MediaServer` on Linux/macOS).

Example:

```json
{
  "dataRoot": "C:/ProgramData/MediaServer",
  "ffmpegPath": "C:/MediaServer/bin/ffmpeg.exe",
  "ffprobePath": "C:/MediaServer/bin/ffprobe.exe",
  "allowedNetworks": ["10.0.0.0/8", "192.168.0.0/16"],
  "autoApproveLanDevices": true,
  "tmdbApiKey": "YOUR_TMDB_KEY",
  "metadataRefreshMinutes": 30,
  "libraries": [
    { "name": "Movies", "path": "D:/Media/Movies", "kind": "movies" },
    { "name": "Series", "path": "D:/Media/Series", "kind": "series" }
  ]
}
```

Adjust library paths, ffmpeg binaries, and network ranges as needed. The server watches the file for changes and reloads automatically.

### Database

SQLite data lives at `<dataRoot>/db/media.db`. Tables cover libraries, media items, artwork, and devices. Schema is created automatically on startup.

### Scanning and metadata

Trigger a rescan via `POST /api/scan`. ffprobe is executed for each media file to populate duration and codec metadata when available. If a TMDB API key is configured, the metadata worker enriches titles/posters every `metadataRefreshMinutes` minutes.

### Discovery

The discovery background service periodically sends SSDP and mDNS announcements advertising `_lanmedia._tcp.local` on port 8090 with a stable UUID stored alongside configuration files.

### Packaging and service install

To publish a self-contained Windows service and install it:

```powershell
pwsh ./build/install.ps1
```

The script will:

1. Publish `MediaServer.Server` for `win-x64`.
2. Copy the payload to `C:\MediaServer`.
3. Register the Windows service `MediaServer` pointing to the executable.
4. Open Windows Firewall for TCP port 8090.

An `uninstall.ps1` stub can be added later to reverse the process.

## API

A concise endpoint reference is available in [docs/API.md](docs/API.md).
