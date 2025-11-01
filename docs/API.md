# Media Server API

This document describes the HTTP endpoints exposed by the media server. All routes are rooted at `/api` unless noted otherwise.

## Status

`GET /api/status`

Returns service metadata, including version, configured libraries, and the current scan state.

Example response:

```json
{
  "service": "MediaServer",
  "version": "1.0.0",
  "scanning": {
    "isScanning": false,
    "lastScanStarted": "2024-06-01T10:15:30Z",
    "lastScanCompleted": "2024-06-01T10:16:01Z"
  },
  "config": {
    "libraries": [
      { "name": "Movies", "path": "D:/Media/Movies", "kind": "movies" }
    ]
  }
}
```

## Libraries and media

- `GET /api/libraries`
  - Returns all libraries and their metadata.
- `GET /api/library/{id}/items?skip={skip}&take={take}`
  - Lists media items inside the given library. Supports pagination via `skip`/`take`.
- `GET /api/item/{id}`
  - Returns metadata for a single media item.
- `GET /api/item/{id}/file`
  - Streams the original media file with HTTP range support for direct play.
- `GET /api/item/{id}/stream`
  - Produces an adaptive HLS stream. The response body includes the path to the generated `index.m3u8` manifest.
- `GET /api/art/{name}`
  - Serves poster or artwork files cached on disk. The `{name}` parameter matches the stored filename.

## Scanning

- `POST /api/scan`
  - Queues a full rescan of all libraries. The response status is `202 Accepted` once work is scheduled.

## Devices

- `GET /api/devices`
  - Returns all registered devices and their approval state.
- `POST /api/devices/register`
  - Registers or refreshes a device. Body:

    ```json
    { "name": "Living Room TV" }
    ```

    Devices originating from LAN ranges are auto-approved when enabled in configuration.
- `POST /api/devices/approve`
  - Updates the status for a device. Body:

    ```json
    { "deviceId": 1, "status": "approved" }
    ```

    Accepted status values: `approved`, `pending`, `blocked`.

## Front-end assets

The browser application is served from `/media-center`. The root `index.html` lives at `/media-center/index.html`, and a fallback route is configured so deep links resolve correctly.

## Web socket notifications

Real-time push notifications are not part of this implementation. A future update could expose `/ws` to publish scan and metadata events.
