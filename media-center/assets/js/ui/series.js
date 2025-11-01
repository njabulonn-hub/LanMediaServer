import { getAll, getEpisodesBySeries } from '../db.js';
import { playVideo } from '../player.js';
import { createElement, formatDuration } from './common.js';

export async function render(container) {
  container.innerHTML = '';
  const series = await getAll('series');
  let selectedSeries = null;
  let episodes = [];

  const list = createElement('div', { className: 'list' });
  const episodesContainer = createElement('div', { className: 'list' });

  async function selectSeries(item) {
    selectedSeries = item;
    episodes = await getEpisodesBySeries(item.id);
    renderSeriesList();
    renderEpisodes();
  }

  function renderSeriesList() {
    list.innerHTML = '';
    series.forEach(item => {
      const button = createElement('button', {
        className: `button secondary${selectedSeries && selectedSeries.id === item.id ? ' active' : ''}`,
        text: item.title,
        attrs: { type: 'button' }
      });
      button.addEventListener('click', () => selectSeries(item));
      list.append(createElement('div', { className: 'list-item', children: [button] }));
    });
  }

  function renderEpisodes() {
    episodesContainer.innerHTML = '';
    if (!selectedSeries) {
      episodesContainer.append(
        createElement('p', { className: 'card-meta', text: 'Select a series to view episodes.' })
      );
      return;
    }

    const sortedEpisodes = episodes
      .slice()
      .sort((a, b) => a.season_number - b.season_number || a.episode_number - b.episode_number);

    sortedEpisodes.forEach(episode => {
      const item = createElement('div', {
        className: 'list-item',
        children: [
          createElement('div', {
            children: [
              createElement('h3', {
                text: `S${episode.season_number.toString().padStart(2, '0')}E${episode.episode_number
                  .toString()
                  .padStart(2, '0')} · ${episode.title}`
              }),
              createElement('p', {
                className: 'card-meta',
                text: formatDuration(episode.runtime)
              }),
              createElement('p', {
                text: episode.overview || ''
              })
            ]
          }),
          createElement('button', {
            className: 'button',
            text: 'Play',
            attrs: { type: 'button' }
          })
        ]
      });

      const playButton = item.querySelector('button');
      const title = selectedSeries ? `${selectedSeries.title} · ${episode.title}` : episode.title;
      playButton.addEventListener('click', () => playVideo(episode.id, episode.file, title));
      episodesContainer.append(item);
    });
  }

  renderSeriesList();
  renderEpisodes();

  const layout = createElement('div', {
    className: 'grid',
    children: [
      createElement('section', { children: [createElement('h2', { text: 'Series' }), list] }),
      createElement('section', {
        children: [
          createElement('h2', { text: selectedSeries ? selectedSeries.title : 'Episodes' }),
          episodesContainer
        ]
      })
    ]
  });

  container.append(layout);

  if (series.length) {
    selectSeries(series[0]);
  }
}
