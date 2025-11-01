import { savePlayback, watchPlayback } from './db.js';

const video = () => document.getElementById('video-player');
const audio = () => document.getElementById('audio-player');

let currentVideoId = null;
let currentAudioId = null;

function saveProgress(type, id, position, duration, title) {
  savePlayback({
    id: `${type}-${id}`,
    itemId: id,
    type,
    title,
    position,
    duration,
    updatedAt: new Date().toISOString()
  });
}

function restoreProgress(element, playback) {
  if (!playback || !playback.position) return;
  const seek = () => {
    if (!element.duration) return;
    const percent = playback.duration ? playback.position / playback.duration : 0;
    if (!percent || percent >= 0.95) return;
    element.currentTime = playback.position;
    element.removeEventListener('loadedmetadata', seek);
  };
  element.addEventListener('loadedmetadata', seek);
  seek();
}

export function playVideo(itemId, path, title = '') {
  const el = video();
  if (!el) return;
  currentVideoId = itemId;
  el.src = path;
  el.play();

  const onTimeUpdate = () => {
    saveProgress('video', itemId, el.currentTime, el.duration || 0, title);
  };

  el.removeEventListener('timeupdate', el._timeHandler);
  el._timeHandler = onTimeUpdate;
  el.addEventListener('timeupdate', onTimeUpdate);

  watchPlayback(itemId, playback => {
    restoreProgress(el, playback);
  });
}

export function playAudio(itemId, path, title = '') {
  const el = audio();
  if (!el) return;
  currentAudioId = itemId;
  el.src = path;
  el.play();

  const onTimeUpdate = () => {
    saveProgress('audio', itemId, el.currentTime, el.duration || 0, title);
  };

  el.removeEventListener('timeupdate', el._timeHandler);
  el._timeHandler = onTimeUpdate;
  el.addEventListener('timeupdate', onTimeUpdate);

  watchPlayback(itemId, playback => {
    restoreProgress(el, playback);
  });
}

export function stopVideo() {
  const el = video();
  if (el) {
    el.pause();
    el.removeAttribute('src');
    currentVideoId = null;
  }
}

export function stopAudio() {
  const el = audio();
  if (el) {
    el.pause();
    el.removeAttribute('src');
    currentAudioId = null;
  }
}

export function getCurrentPlaying() {
  return { video: currentVideoId, audio: currentAudioId };
}
