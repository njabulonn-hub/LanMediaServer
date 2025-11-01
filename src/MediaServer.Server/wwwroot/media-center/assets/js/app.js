import './db.js';
import { init, hasData } from './db.js';
import { importLibrary } from './import.js';
import { initRouter, onRouteChange, navigate } from './router.js';
import * as homeScreen from './ui/home.js';
import * as moviesScreen from './ui/movies.js';
import * as seriesScreen from './ui/series.js';
import * as musicScreen from './ui/music.js';
import * as adminScreen from './ui/admin.js';

const screens = {
  home: homeScreen,
  movies: moviesScreen,
  series: seriesScreen,
  music: musicScreen,
  admin: adminScreen
};

const content = document.getElementById('content');
const loading = document.getElementById('loading-state');
const nav = document.getElementById('app-nav');
const navToggle = document.getElementById('nav-toggle');
const navLinks = Array.from(document.querySelectorAll('.nav-link'));

async function ensureData() {
  const dataExists = await hasData().catch(() => false);
  if (!dataExists) {
    loading.textContent = 'Importing library metadata…';
    await importLibrary();
  }
  loading.remove();
}

async function showRoute(route) {
  if (!screens[route]) {
    navigate('home');
    return;
  }

  navLinks.forEach(link => {
    if (link.dataset.route === route) {
      link.classList.add('active');
    } else {
      link.classList.remove('active');
    }
  });

  if (nav.classList.contains('open')) {
    nav.classList.remove('open');
  }

  await screens[route].render(content);
}

async function bootstrap() {
  try {
    await init();
    await ensureData();
    onRouteChange(showRoute);
    initRouter();
    if (!location.hash) navigate('home');
    registerServiceWorker();
  } catch (error) {
    console.error('Failed to prepare library', error);
    content.innerHTML = '<p class="card-meta">Unable to initialize the app.</p>';
  }
}

function registerServiceWorker() {
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker
      .register('/media-center/service-worker.js')
      .catch(error => console.warn('Service worker registration failed', error));
  }
}

navToggle.addEventListener('click', () => {
  nav.classList.toggle('open');
});

bootstrap();
