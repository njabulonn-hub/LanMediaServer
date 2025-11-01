import { getAll } from '../db.js';
import { playVideo } from '../player.js';
import { createElement, renderList } from './common.js';

function createMovieCard(movie, onSelect) {
  const card = createElement('article', { className: 'card', attrs: { tabindex: 0 } });
  if (movie.poster) {
    card.append(
      createElement('img', {
        attrs: { src: movie.poster, alt: `${movie.title} poster`, loading: 'lazy' }
      })
    );
  }
  const body = createElement('div', {
    className: 'card-body',
    children: [
      createElement('h3', { className: 'card-title', text: movie.title }),
      createElement('p', { className: 'card-meta', text: movie.year ? `${movie.year}` : '' })
    ]
  });
  card.append(body);
  card.addEventListener('click', () => onSelect(movie));
  card.addEventListener('keypress', event => {
    if (event.key === 'Enter') onSelect(movie);
  });
  return card;
}

function createDetails(movie) {
  if (!movie) return createElement('div');
  const section = createElement('section');
  section.append(createElement('h2', { text: movie.title }));
  const meta = [movie.year, movie.runtime ? `${movie.runtime} min` : null]
    .filter(Boolean)
    .join(' · ');

  section.append(createElement('p', { className: 'card-meta', text: meta }));
  section.append(createElement('p', { text: movie.overview || 'No overview available.' }));

  const files = movie.files || [];
  if (!files.length) {
    section.append(createElement('p', { className: 'card-meta', text: 'No media files linked.' }));
    return section;
  }

  const filesList = createElement('div', { className: 'list' });
  files.forEach(file => {
    filesList.append(
      createElement('div', {
        className: 'list-item',
        children: [
          createElement('div', {
            children: [
              createElement('strong', { text: file.quality || 'Primary file' }),
              createElement('p', {
                className: 'card-meta',
                text: file.size ? `${(file.size / (1024 * 1024 * 1024)).toFixed(1)} GB` : ''
              })
            ]
          }),
          createElement('button', {
            className: 'button',
            text: 'Play',
            attrs: { type: 'button' }
          })
        ]
      })
    );
  });

  filesList.querySelectorAll('button').forEach((button, index) => {
    const file = files[index];
    button.addEventListener('click', () => playVideo(movie.id, file.path, movie.title));
  });

  section.append(filesList);
  return section;
}

export async function render(container) {
  container.innerHTML = '';
  const movies = await getAll('movies');
  let filteredMovies = movies;
  let selectedMovie = movies[0] || null;

  const years = Array.from(new Set(movies.map(movie => movie.year))).filter(Boolean).sort((a, b) => b - a);

  const controls = createElement('div', { className: 'list-item', children: [] });
  const label = createElement('label', { text: 'Filter by year: ' });
  const select = createElement('select');
  select.append(createElement('option', { text: 'All years', attrs: { value: '' } }));
  years.forEach(year => {
    select.append(createElement('option', { text: year.toString(), attrs: { value: year } }));
  });
  select.addEventListener('change', () => {
    const year = select.value;
    filteredMovies = year ? movies.filter(movie => movie.year === Number(year)) : movies;
    selectedMovie = filteredMovies[0] || null;
    renderGrid();
    renderDetails();
  });
  label.append(select);
  controls.append(label);

  const grid = createElement('div', { className: 'grid' });
  const details = createElement('div');

  function renderGrid() {
    grid.innerHTML = '';
    filteredMovies.forEach(movie => {
      grid.append(createMovieCard(movie, movieObj => {
        selectedMovie = movieObj;
        renderDetails();
      }));
    });
  }

  function renderDetails() {
    details.innerHTML = '';
    details.append(createDetails(selectedMovie));
  }

  renderGrid();
  renderDetails();

  renderList(container, [controls, grid, details]);
}
