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

async function triggerScan() {
  try {
    const response = await fetch('/api/scan', { method: 'POST' });
    if (!response.ok) {
      throw new Error(`Scan request failed: ${response.status}`);
    }
    return true;
  } catch (error) {
    throw new Error(`Failed to trigger scan: ${error.message}`);
  }
}

export async function render(container) {
  container.innerHTML = '';
  const heading = createElement('h2', { text: 'Admin' });
  const statsContainer = createElement('div');
  
  const buttonGroup = createElement('div', { 
    className: 'button-group',
    style: 'display: flex; gap: 1rem; margin-bottom: 1rem;'
  });
  
  const scanButton = createElement('button', {
    className: 'button',
    text: 'Trigger Scan',
    attrs: { type: 'button' }
  });
  
  const importButton = createElement('button', {
    className: 'button',
    text: 'Re-import metadata',
    attrs: { type: 'button' }
  });
  
  const status = createElement('p', { className: 'card-meta' });

  scanButton.addEventListener('click', async () => {
    scanButton.disabled = true;
    status.textContent = 'Triggering scan…';
    status.className = 'card-meta';
    try {
      await triggerScan();
      status.textContent = 'Scan triggered successfully. This may take a few minutes. Refresh this page to check status.';
      status.className = 'card-meta';
    } catch (error) {
      console.error('Scan error:', error);
      status.textContent = `Scan failed: ${error.message}. Check browser console (F12) for details.`;
      status.className = 'card-meta error';
    } finally {
      scanButton.disabled = false;
    }
  });

  importButton.addEventListener('click', async () => {
    importButton.disabled = true;
    status.textContent = 'Importing…';
    status.className = 'card-meta';
    try {
      await Promise.all([
        clear('movies'),
        clear('series'),
        clear('episodes'),
        clear('tracks')
      ]);
      const results = await importLibrary();
      status.textContent = `Imported ${results.movies} movies, ${results.series} series, ${results.episodes} episodes, ${results.tracks} tracks.`;
      status.className = 'card-meta';
      await refreshStats(statsContainer);
    } catch (error) {
      console.error('Import error:', error);
      status.textContent = `Import failed: ${error.message}. Check browser console (F12) for details.`;
      status.className = 'card-meta error';
    } finally {
      importButton.disabled = false;
    }
  });

  buttonGroup.append(scanButton, importButton);
  container.append(heading, buttonGroup, status, statsContainer);
  await refreshStats(statsContainer);
}
