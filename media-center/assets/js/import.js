import { bulkPut, put } from './db.js';

async function fetchJson(path) {
  const response = await fetch(path, { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Failed to fetch ${path}: ${response.status}`);
  }
  return response.json();
}

function flattenEpisodes(seriesList) {
  const episodes = [];
  seriesList.forEach(series => {
    if (!Array.isArray(series.seasons)) return;
    series.seasons.forEach(season => {
      season.episodes.forEach(episode => {
        episodes.push({
          ...episode,
          series_id: series.id,
          season_number: season.season_number
        });
      });
    });
  });
  return episodes;
}

function normalizeSeries(seriesList) {
  return seriesList.map(({ seasons, ...rest }) => rest);
}

export async function importLibrary() {
  const [movies, seriesRaw, tracks] = await Promise.all([
    fetchJson('data/movies.json'),
    fetchJson('data/series.json'),
    fetchJson('data/music.json')
  ]);

  const episodes = flattenEpisodes(seriesRaw);
  const series = normalizeSeries(seriesRaw);

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
