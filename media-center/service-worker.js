const CACHE_NAME = 'media-center-shell-v2';
const SHELL_ASSETS = [
  '/media-center/',
  '/media-center/index.html',
  '/media-center/manifest.json',
  '/media-center/assets/css/app.css',
  '/media-center/assets/js/app.js',
  '/media-center/assets/js/router.js',
  '/media-center/assets/js/db.js',
  '/media-center/assets/js/import.js',
  '/media-center/assets/js/player.js',
  '/media-center/assets/js/ui/common.js',
  '/media-center/assets/js/ui/home.js',
  '/media-center/assets/js/ui/movies.js',
  '/media-center/assets/js/ui/series.js',
  '/media-center/assets/js/ui/music.js',
  '/media-center/assets/js/ui/admin.js'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(SHELL_ASSETS))
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key)))
    )
  );
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  if (url.pathname.startsWith('/media-center/data/')) {
    event.respondWith(networkFirst(event.request));
    return;
  }

  if (SHELL_ASSETS.includes(url.pathname)) {
    event.respondWith(cacheFirst(event.request));
  }
});

async function cacheFirst(request) {
  const cache = await caches.open(CACHE_NAME);
  const cached = await cache.match(request);
  if (cached) {
    return cached;
  }
  const response = await fetch(request);
  cache.put(request, response.clone());
  return response;
}

async function networkFirst(request) {
  const cache = await caches.open(CACHE_NAME);
  try {
    const response = await fetch(request);
    cache.put(request, response.clone());
    return response;
  } catch (error) {
    const cached = await cache.match(request);
    if (cached) return cached;
    throw error;
  }
}
