import { getAll, getCounts } from '../db.js';
import { createElement, renderList } from './common.js';

function createRecentlyAddedSection(items) {
  const section = createElement('section');
  section.append(createElement('h2', { text: 'Recently Added' }));
  if (!items.length) {
    section.append(createElement('p', { className: 'card-meta', text: 'No movies available.' }));
    return section;
  }
  const grid = createElement('div', { className: 'grid' });

  items.slice(0, 8).forEach(item => {
    const card = createElement('article', { className: 'card' });
    if (item.poster) {
      card.append(
        createElement('img', {
          attrs: { src: item.poster, alt: `${item.title} poster`, loading: 'lazy' }
        })
      );
    }
    const body = createElement('div', {
      className: 'card-body',
      children: [
        createElement('h3', { className: 'card-title', text: item.title }),
        createElement('p', {
          className: 'card-meta',
          text: item.year ? `${item.year}` : ''
        })
      ]
    });
    card.append(body);
    grid.append(card);
  });

  section.append(grid);
  return section;
}

function createContinueWatching(playbackEntries) {
  if (!playbackEntries.length) return null;
  const section = createElement('section');
  section.append(createElement('h2', { text: 'Continue Watching' }));
  const list = createElement('div', { className: 'list' });
  playbackEntries.slice(0, 6).forEach(entry => {
    const percent = entry.duration ? Math.round((entry.position / entry.duration) * 100) : 0;
    list.append(
      createElement('div', {
        className: 'list-item',
        children: [
          createElement('h3', { text: entry.title || entry.itemId }),
          createElement('p', {
            className: 'card-meta',
            text: percent ? `${percent}% complete` : 'In progress'
          })
        ]
      })
    );
  });
  section.append(list);
  return section;
}

function createStats(counts) {
  const section = createElement('section');
  section.append(createElement('h2', { text: 'Library stats' }));
  const grid = createElement('div', { className: 'stats-grid' });
  counts.forEach(({ store, count }) => {
    if (['settings', 'playback'].includes(store)) return;
    grid.append(
      createElement('article', {
        className: 'stat-card',
        children: [
          createElement('span', { text: store.charAt(0).toUpperCase() + store.slice(1) }),
          createElement('strong', { text: count.toString() })
        ]
      })
    );
  });
  section.append(grid);
  return section;
}

export async function render(container) {
  container.innerHTML = '';
  const [movies, playback, counts] = await Promise.all([
    getAll('movies'),
    getAll('playback'),
    getCounts()
  ]);

  const sortedMovies = movies
    .slice()
    .sort((a, b) => (b.added_at || 0) - (a.added_at || 0));

  const continueEntries = playback
    .filter(entry => entry.position && entry.position > 0)
    .sort((a, b) => new Date(b.updatedAt) - new Date(a.updatedAt));

  const fragments = [createRecentlyAddedSection(sortedMovies)];
  const continueSection = createContinueWatching(continueEntries);
  if (continueSection) fragments.push(continueSection);
  fragments.push(createStats(counts));

  renderList(container, fragments);
}
