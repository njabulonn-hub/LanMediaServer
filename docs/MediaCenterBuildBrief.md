# Media Center (HTML+JS) — Developer Plan

## 0. Goal

Build a **browser-based media center** that:

1. Reads media metadata from static JSON files.
2. Stores it in the browser (IndexedDB) for fast, offline use.
3. Shows Movies, Series, Music in a clean UI.
4. Streams actual media files that are exposed over HTTP from the same LAN server.
5. Has an **optional** PHP importer to build/update the JSON files.

## 1. Deliverables (MVP)

1. `index.html` — app shell
2. `assets/css/app.css` — basic layout
3. `assets/js/app.js` — app boot + routing
4. `assets/js/db.js` — IndexedDB layer
5. `assets/js/import.js` — load `/data/*.json` → IndexedDB
6. `assets/js/ui/*.js` — render Movies, Series, Music
7. `assets/js/player.js` — HTML5 `<video>` / `<audio>` player
8. `service-worker.js` — cache shell for offline
9. `/data/movies.json`, `/data/series.json`, `/data/music.json` — sample data
10. (Optional) `/tools/export_movies.php` — turns SQLite → JSON

## 2. Project structure

```text
/media-center/
├── index.html
├── service-worker.js
├── assets/
│   ├── css/
│   │   └── app.css
│   └── js/
│       ├── app.js
│       ├── router.js
│       ├── db.js
│       ├── import.js
│       ├── player.js
│       └── ui/
│           ├── home.js
│           ├── movies.js
│           ├── series.js
│           ├── music.js
│           └── admin.js
├── data/
│   ├── movies.json
│   ├── series.json
│   └── music.json
└── tools/
    ├── fetch_meta.php        # optional
    └── export_movies.php     # optional
```

Notes for the dev:

* **No build step required**. Pure static.
* Keep JS modular using ES modules (`type="module"`).

## 3. App flow (very important)

1. User goes to `http://<server-ip>/media-center/`
2. `index.html` loads `app.js`
3. `app.js`:

   * opens IndexedDB
   * checks if `movies` (and others) have data
   * if empty → calls `import.js` to fetch `/data/movies.json`, `/data/series.json`, `/data/music.json`
   * saves into IndexedDB
   * then calls `router.show('home')`
4. UI pulls **only from IndexedDB**, not from JSON again
5. From now on, the app works offline (because DB + service worker)

## 4. Data model (what to save in IndexedDB)

Create 4 object stores:

1. `movies`

   * `id` (string, e.g. `"mov_0001"`)
   * `title`
   * `year`
   * `overview`
   * `runtime`
   * `poster` (URL, can be `/media/posters/...jpg`)
   * `files` (array of objects: `{path, quality, size}`)
   * `added_at`
2. `series`

   * `id`
   * `title`
   * `overview`
   * `poster`
3. `episodes`

   * `id`
   * `series_id`
   * `season_number`
   * `episode_number`
   * `title`
   * `overview`
   * `runtime`
   * `file` (string path)
4. `tracks`

   * `id`
   * `artist`
   * `album`
   * `title`
   * `duration`
   * `file`

Also create a 5th store: `settings`

* for: version of data (`library_version`), last import time, language

**db.js** must expose:

```js
await db.init();
await db.put('movies', movieObj);
const movies = await db.getAll('movies');
```

## 5. JSON format (what the PHP/exporter must produce)

### 5.1 `/data/movies.json`

```json
[
  {
    "id": "mov_0001",
    "title": "Interstellar",
    "year": 2014,
    "overview": "A team of explorers travel...",
    "runtime": 169,
    "poster": "/media/posters/interstellar.jpg",
    "files": [
      {
        "path": "/media/movies/Interstellar (2014).mp4",
        "quality": "1080p",
        "size": 7340032000
      }
    ]
  }
]
```

### 5.2 `/data/series.json`

```json
[
  {
    "id": "ser_0001",
    "title": "Breaking Bad",
    "overview": "",
    "poster": "/media/posters/breakingbad.jpg",
    "seasons": [
      {
        "season_number": 1,
        "episodes": [
          {
            "id": "ep_bb_s01e01",
            "episode_number": 1,
            "title": "Pilot",
            "runtime": 58,
            "file": "/media/series/Breaking Bad/S01E01 - Pilot.mkv"
          }
        ]
      }
    ]
  }
]
```

**import.js** must **flatten** series → episodes into the `episodes` store.

### 5.3 `/data/music.json`

```json
[
  {
    "id": "trk_001",
    "artist": "Daft Punk",
    "album": "Random Access Memories",
    "title": "Give Life Back to Music",
    "duration": 272,
    "file": "/media/music/Daft Punk/Random Access Memories/01 - Give Life Back to Music.flac"
  }
]
```

## 6. UI requirements

### 6.1 Layout

* Left/top nav: **Home, Movies, Series, Music, Admin**
* Content area: cards / lists
* Mobile: collapsible nav
* TV: big tap targets

### 6.2 Home screen

* “Recently added” (sort by `added_at` desc)
* “Continue watching” (read from IndexedDB `playback` object inside each item or separate store)
* “Stats” (counts from DB)

### 6.3 Movies screen

* Grid view (poster, title, year)
* Filter: year
* Click → Detail:

  * poster
  * title (year)
  * overview
  * list of files (if more than 1)
  * Play button

### 6.4 Series screen

* List of series → click → seasons → episodes
* Episode item shows:

  * title
  * runtime
  * play button

### 6.5 Music screen

* Simple list grouped by artist → album → tracks
* Click track → play in `<audio>`

### 6.6 Admin screen

* Button: “Re-import metadata” → re-run the JSON → IndexedDB step
* Show current library version
* Show item counts

## 7. Player logic

File will be a normal HTTP URL on the same server.

In `player.js`:

```js
export function playVideo(path) {
  const video = document.getElementById('video-player');
  video.src = path;
  video.play();
}
```

* Add listener to save playback position to IndexedDB:

  ```js
  video.addEventListener('timeupdate', () => {
    savePlayback(movieId, video.currentTime);
  });
  ```

* On open: seek to last position if < 95% watched.

## 8. PWA / Offline

**service-worker.js** (simple version):

* Cache:

  * `/`
  * `/index.html`
  * `/assets/css/app.css`
  * `/assets/js/*`
  * `/data/*.json` (optional)
* Strategy:

  * Network-first for `/data/*.json` (so re-import can fetch new data)
  * Cache-first for static assets

This way, if user opens on phone and later opens again with PC off, they **still see library**, even if they can’t stream.

## 9. Local network access (dev must do this!)

1. App must **NOT** bind only to `localhost`. During dev, run:

   ```bash
   php -S 0.0.0.0:8000
   ```

   or

   ```bash
   npx serve . --listen 0.0.0.0:8000
   ```
2. Test from phone: `http://<dev-pc-ip>:8000/media-center/`
3. Make sure all media URLs in JSON are **relative** (`/media/...`), not `http://localhost/...`

## 10. Optional PHP tools (only when we need to download metadata)

You can give this to a different dev.

### 10.1 `tools/fetch_meta.php`

* Input: list of local files (maybe from a scan)
* For each:

  * parse filename
  * call remote API (if enabled)
  * store in SQLite (`/data/meta.sqlite`)

### 10.2 `tools/export_movies.php`

* Read from SQLite
* Output JSON in the exact format above
* Save to `/data/movies.json`

After that → frontend works with no PHP.

## 11. Milestones (for you to track)

**Milestone 1 – Shell & DB (1–2 days)**

* `index.html` with nav
* `db.js` with 4 object stores
* `import.js` that loads `/data/movies.json` → IndexedDB
* Console shows “Imported 123 movies”

**Milestone 2 – UI listing (1–2 days)**

* Movies grid
* Series list
* Music list
* Click → details

**Milestone 3 – Playback (1 day)**

* `<video>` player component
* `<audio>` player component
* Works from phone on same Wi-Fi

**Milestone 4 – Admin & re-import (0.5–1 day)**

* Button to clear + re-import
* Show counts

**Milestone 5 – PWA (0.5 day)**

* service worker
* manifest
* test offline

After Milestone 3 you can already **use** it.

## 12. Acceptance tests (what the dev must demo)

1. On PC: open `http://localhost:8000/` → see library
2. On phone (same Wi-Fi): open `http://192.168.1.xx:8000/` → see same library
3. Click Movie → Play → video plays
4. Turn off Wi-Fi on phone → open again → library still shows (from cache/IndexedDB). Playback can fail (that’s OK) but metadata must show.
5. Add new JSON (e.g. add 1 movie), click “Re-import” → new movie appears.

That’s enough for a dev to start right now.

If you want, I can now generate the **starter files** (`index.html`, `app.js`, `db.js`, `import.js`) exactly in the structure above so you can drop them into `/var/www/html/media-center/` and test from your phone.
