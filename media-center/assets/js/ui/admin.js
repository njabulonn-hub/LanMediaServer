import { clear, get, getCounts } from '../db.js';
import { importLibrary } from '../import.js';
import { createElement } from './common.js';

async function refreshStats(container) {
  const counts = await getCounts();
  const version = await get('settings', 'library_version');
  const lastImport = await get('settings', 'last_import');

  container.innerHTML = '';
  const list = createElement('ul');
  counts
    .filter(({ store }) => !['settings', 'playback'].includes(store))
    .forEach(({ store, count }) => {
      const item = createElement('li', { text: `${store}: ${count}` });
      list.append(item);
    });
  container.append(list);
  container.append(
    createElement('p', {
      className: 'card-meta',
      text: `Library version: ${version ? version.value : 'n/a'} | Last import: ${lastImport ? lastImport.value : 'never'}`
    })
  );
}

export async function render(container) {
  container.innerHTML = '';
  const heading = createElement('h2', { text: 'Admin' });
  const statsContainer = createElement('div');
  const importButton = createElement('button', {
    className: 'button',
    text: 'Re-import metadata',
    attrs: { type: 'button' }
  });
  const status = createElement('p', { className: 'card-meta' });

  importButton.addEventListener('click', async () => {
    importButton.disabled = true;
    status.textContent = 'Importing…';
    try {
      await Promise.all([
        clear('movies'),
        clear('series'),
        clear('episodes'),
        clear('tracks')
      ]);
      const results = await importLibrary();
      status.textContent = `Imported ${results.movies} movies, ${results.series} series, ${results.tracks} tracks.`;
      await refreshStats(statsContainer);
    } catch (error) {
      console.error(error);
      status.textContent = 'Import failed. Check console for details.';
    } finally {
      importButton.disabled = false;
    }
  });

  container.append(heading, importButton, status, statsContainer);
  await refreshStats(statsContainer);
}
