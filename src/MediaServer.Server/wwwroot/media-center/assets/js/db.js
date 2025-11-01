const DB_NAME = 'media-center';
const DB_VERSION = 1;

const stores = {
  movies: { keyPath: 'id' },
  series: { keyPath: 'id' },
  episodes: { keyPath: 'id' },
  tracks: { keyPath: 'id' },
  settings: { keyPath: 'key' },
  playback: { keyPath: 'id' }
};

let dbPromise = null;

function openDb() {
  if (dbPromise) return dbPromise;

  dbPromise = new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onupgradeneeded = () => {
      const db = request.result;
      Object.entries(stores).forEach(([name, options]) => {
        if (!db.objectStoreNames.contains(name)) {
          db.createObjectStore(name, options);
        }
      });
      const episodes = request.transaction.objectStore('episodes');
      if (!episodes.indexNames.contains('series_id')) {
        episodes.createIndex('bySeries', 'series_id', { unique: false });
      }
      const playback = request.transaction.objectStore('playback');
      if (!playback.indexNames.contains('byItem')) {
        playback.createIndex('byItem', 'itemId', { unique: true });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });

  return dbPromise;
}

export async function init() {
  const db = await openDb();
  db.onversionchange = () => {
    db.close();
    alert('A new version of the Media Center is available. Please reload.');
  };
  return db;
}

async function withStore(storeName, mode, callback) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(storeName, mode);
    const store = tx.objectStore(storeName);

    let settled = false;
    const resolveOnce = value => {
      if (!settled) {
        settled = true;
        resolve(value);
      }
    };
    const rejectOnce = error => {
      if (!settled) {
        settled = true;
        reject(error);
      }
    };

    let result;
    try {
      result = callback(store, tx);
    } catch (error) {
      rejectOnce(error);
      tx.abort();
      return;
    }

    const isRequest =
      result &&
      typeof result === 'object' &&
      'result' in result &&
      'onsuccess' in result &&
      'onerror' in result;

    if (isRequest) {
      const handleSuccess = () => resolveOnce(result.result);
      const handleError = () => rejectOnce(result.error);

      if (typeof result.addEventListener === 'function') {
        result.addEventListener('success', handleSuccess, { once: true });
        result.addEventListener('error', handleError, { once: true });
      } else {
        result.onsuccess = handleSuccess;
        result.onerror = handleError;
      }
    } else {
      tx.oncomplete = () => resolveOnce(result);
    }

    tx.onerror = () => rejectOnce(tx.error);
    tx.onabort = () => rejectOnce(tx.error ?? new Error('Transaction aborted'));
  });
}

export function put(store, value) {
  return withStore(store, 'readwrite', objectStore => objectStore.put(value));
}

export function bulkPut(store, values) {
  return withStore(store, 'readwrite', objectStore => {
    values.forEach(value => objectStore.put(value));
  });
}

export function get(store, key) {
  return withStore(store, 'readonly', objectStore => objectStore.get(key));
}

export function getAll(store) {
  return withStore(store, 'readonly', objectStore => objectStore.getAll());
}

export function clear(store) {
  return withStore(store, 'readwrite', objectStore => objectStore.clear());
}

export async function getCounts() {
  const db = await openDb();
  return Promise.all(
    Object.keys(stores).map(
      storeName =>
        new Promise((resolve, reject) => {
          const tx = db.transaction(storeName, 'readonly');
          const store = tx.objectStore(storeName);
          const request = store.count();
          request.onsuccess = () => resolve({ store: storeName, count: request.result });
          request.onerror = () => reject(request.error);
        })
    )
  );
}

export async function getEpisodesBySeries(seriesId) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction('episodes', 'readonly');
    const store = tx.objectStore('episodes');
    const index = store.index('bySeries');
    const request = index.getAll(seriesId);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export function watchPlayback(itemId, callback) {
  openDb().then(db => {
    const tx = db.transaction('playback', 'readonly');
    const index = tx.objectStore('playback').index('byItem');
    const request = index.get(itemId);
    request.onsuccess = () => callback(request.result || null);
  });
}

export function savePlayback(entry) {
  return put('playback', entry);
}

export async function hasData() {
  const db = await openDb();
  return Promise.all(
    ['movies', 'series', 'episodes', 'tracks'].map(
      storeName =>
        new Promise((resolve, reject) => {
          const tx = db.transaction(storeName, 'readonly');
          const store = tx.objectStore(storeName);
          const request = store.count();
          request.onsuccess = () => resolve(request.result > 0);
          request.onerror = () => reject(request.error);
        })
    )
  ).then(results => results.every(Boolean));
}
