import { bulkPut, put } from './db.js';

const API_BASE = '/api';

async function apiGet(path) {
  try {
    const response = await fetch(`${API_BASE}${path}`, { cache: 'no-store' });
    if (!response.ok) {
      const errorText = await response.text().catch(() => '');
      throw new Error(`Failed to fetch ${path}: ${response.status} ${response.statusText}. ${errorText}`);
    }
    return response.json();
  } catch (error) {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      throw new Error(`Network error: Unable to reach server at ${API_BASE}${path}. Is the server running?`);
    }
    throw error;
  }
}

function parseYear(title) {
  if (!title) return null;
  const match = title.match(/\b(19|20)\d{2}\b/);
  return match ? parseInt(match[0], 10) : null;
}

function getPosterUrl(itemId) {
  // The API endpoint searches for artwork by name using LIKE
  // Artwork files are stored as {itemId}.{ext}, so just the itemId should work
  // The API will return 404 if not found, which the UI can handle gracefully
  return `${API_BASE}/art/${itemId}`;
}

function parseSeasonEpisode(fileName) {
  // Match patterns like S01E01, S1E1, 1x01, 1x1
  const patterns = [
    /[Ss](\d+)[Ee](\d+)/,
    /(\d+)[xX](\d+)/
  ];
  
  for (const pattern of patterns) {
    const match = fileName.match(pattern);
    if (match) {
      return {
        season: parseInt(match[1], 10),
        episode: parseInt(match[2], 10)
      };
    }
  }
  return null;
}

function parseMusicMetadata(filePath, fileName) {
  // Try to extract artist/album from path structure
  // e.g., "/Music/Artist/Album/Track.mp3" or "/Music/Artist - Album/Track.mp3"
  const parts = filePath.split(/[/\\]/).filter(Boolean);
  let artist = null;
  let album = null;
  
  // Look for common music library structures
  const musicIndex = parts.findIndex(p => p.toLowerCase() === 'music');
  if (musicIndex >= 0 && parts.length > musicIndex + 1) {
    artist = parts[musicIndex + 1];
    if (parts.length > musicIndex + 2) {
      // Check if next part is album or "Artist - Album" format
      const next = parts[musicIndex + 2];
      if (next.includes(' - ')) {
        const [a, b] = next.split(' - ');
        artist = a.trim();
        album = b.trim();
      } else {
        album = next;
      }
    }
  }
  
  // Fallback: parse from filename like "Artist - Title.mp3"
  if (!artist && fileName.includes(' - ')) {
    const parts = fileName.split(' - ');
    artist = parts[0].trim();
  }
  
  return { artist: artist || 'Unknown Artist', album: album || 'Unknown Album' };
}

async function fetchItemsForLibrary(libraryId) {
  // Fetch all items with pagination
  const allItems = [];
  const pageSize = 100;
  let skip = 0;
  let hasMore = true;
  
  while (hasMore) {
    const items = await apiGet(`/library/${libraryId}/items?skip=${skip}&take=${pageSize}`);
    allItems.push(...items);
    hasMore = items.length === pageSize;
    skip += pageSize;
  }
  
  return allItems;
}

export async function importLibrary() {
  // Fetch all libraries
  const libraries = await apiGet('/libraries');
  
  if (!libraries || libraries.length === 0) {
    throw new Error('No libraries found. Please configure libraries and run a scan from the server, or use the Admin page to trigger a scan.');
  }
  
  const movies = [];
  const series = [];
  const episodes = [];
  const tracks = [];
  
  // Process each library based on its kind
  for (const library of libraries) {
    const items = await fetchItemsForLibrary(library.id);
    
    for (const item of items) {
      const baseItem = {
        id: `item_${item.id}`,
        title: item.title || item.fileName.replace(/\.[^.]+$/, ''),
        added_at: item.createdAt ? new Date(item.createdAt).getTime() : Date.now()
      };
      
      // Get poster URL (will be checked when rendering)
      const poster = getPosterUrl(item.id);
      
      // File info for playback
      const fileInfo = {
        path: `${API_BASE}/item/${item.id}/file`,
        quality: item.videoCodec ? 'Video' : 'Audio',
        size: null // Size not available from API
      };
      
      if (library.kind === 'movies') {
        const year = parseYear(item.title || item.fileName);
        movies.push({
          ...baseItem,
          year,
          overview: '', // Not available from base API
          runtime: item.duration ? Math.round(item.duration / 60) : null,
          poster,
          files: [fileInfo]
        });
      } else if (library.kind === 'series') {
        // Check if this item is an episode by parsing filename
        const seInfo = parseSeasonEpisode(item.fileName);
        
        if (seInfo) {
          // This is an episode
          const seriesId = `series_${library.id}`;
          episodes.push({
            id: `ep_${item.id}`,
            series_id: seriesId,
            season_number: seInfo.season,
            episode_number: seInfo.episode,
            title: baseItem.title,
            overview: '',
            runtime: item.duration ? Math.round(item.duration / 60) : null,
            file: fileInfo.path
          });
          
          // Check if we already added this series
          if (!series.find(s => s.id === seriesId)) {
            series.push({
              id: seriesId,
              title: library.name, // Use library name as series name, or parse from path
              overview: '',
              poster
            });
          }
        } else {
          // Treat as standalone series entry
          series.push({
            ...baseItem,
            overview: '',
            poster
          });
        }
      } else if (library.kind === 'music') {
        const { artist, album } = parseMusicMetadata(item.filePath, item.fileName);
        tracks.push({
          id: `track_${item.id}`,
          artist,
          album,
          title: baseItem.title,
          duration: item.duration,
          file: fileInfo.path
        });
      }
    }
  }
  
  await Promise.all([
    bulkPut('movies', movies),
    bulkPut('series', series),
    bulkPut('episodes', episodes),
    bulkPut('tracks', tracks),
    put('settings', { key: 'library_version', value: Date.now() }),
    put('settings', { key: 'last_import', value: new Date().toISOString() })
  ]);

  return {
    movies: movies.length,
    series: series.length,
    episodes: episodes.length,
    tracks: tracks.length
  };
}
