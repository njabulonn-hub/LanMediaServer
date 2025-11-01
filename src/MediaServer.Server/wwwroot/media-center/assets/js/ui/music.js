import { getAll } from '../db.js';
import { playAudio } from '../player.js';
import { createElement } from './common.js';

function groupByArtistAlbum(tracks) {
  const grouped = new Map();
  tracks.forEach(track => {
    const artist = track.artist || 'Unknown artist';
    const album = track.album || 'Unknown album';
    if (!grouped.has(artist)) grouped.set(artist, new Map());
    const albums = grouped.get(artist);
    if (!albums.has(album)) albums.set(album, []);
    albums.get(album).push(track);
  });
  return grouped;
}

export async function render(container) {
  container.innerHTML = '';
  const tracks = await getAll('tracks');
  const grouped = groupByArtistAlbum(tracks);

  grouped.forEach(albums => {
    albums.forEach(albumTracks => albumTracks.sort((a, b) => (a.track || 0) - (b.track || 0)));
  });

  grouped.forEach((albums, artist) => {
    const section = createElement('section');
    section.append(createElement('h2', { text: artist }));

    albums.forEach((albumTracks, album) => {
      const albumSection = createElement('div', { className: 'list-item' });
      albumSection.append(createElement('h3', { text: album }));
      const list = createElement('ol');
      albumTracks.forEach(track => {
        const item = createElement('li');
        const playButton = createElement('button', {
          className: 'button',
          text: 'Play',
          attrs: { type: 'button' }
        });
        playButton.addEventListener('click', () => playAudio(track.id, track.file, track.title));
        item.append(
          createElement('span', {
            text: `${track.title} ${track.duration ? `(${Math.round(track.duration / 60)}m)` : ''}`
          })
        );
        item.append(playButton);
        list.append(item);
      });
      albumSection.append(list);
      section.append(albumSection);
    });

    container.append(section);
  });

  if (!grouped.size) {
    container.append(createElement('p', { className: 'card-meta', text: 'No music found.' }));
  }
}
