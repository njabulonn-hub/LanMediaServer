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
    try {
      await importLibrary();
      loading.remove();
    } catch (error) {
      console.error('Failed to import library:', error);
      loading.textContent = `Import failed: ${error.message}. You can still navigate, but the library may be empty. Go to Admin to trigger a scan.`;
      loading.classList.add('error');
      // Don't prevent app from working - allow navigation even if import fails
      // Remove loading after a short delay so user can see the error
      setTimeout(() => loading.remove(), 3000);
    }
  } else {
    loading.remove();
  }
}

async function showRoute(route) {
  console.log('showRoute: attempting to show', route);
  
  if (!screens[route]) {
    console.warn(`Unknown route: ${route}, redirecting to home`);
    navigate('home');
    return;
  }

  console.log('showRoute: route exists, updating nav and rendering');

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

  try {
    console.log('showRoute: calling render for', route);
    await screens[route].render(content);
    console.log('showRoute: successfully rendered', route);
  } catch (error) {
    console.error(`Failed to render ${route}:`, error);
    content.innerHTML = `<p class="card-meta error">Failed to load ${route}: ${error.message}. Check console for details.</p>`;
  }
}

async function bootstrap() {
  try {
    console.log('Bootstrap: initializing database...');
    await init();
    console.log('Bootstrap: database initialized');
    
    // Set up router before ensureData so navigation works even if import fails
    console.log('Bootstrap: setting up router...');
    onRouteChange(showRoute);
    initRouter();
    
    // Try to load data, but don't block navigation if it fails
    console.log('Bootstrap: ensuring data...');
    ensureData().catch(error => {
      console.error('Bootstrap: data loading failed (non-blocking):', error);
    });
    
    // Navigate to current hash or home
    if (!location.hash) {
      console.log('Bootstrap: no hash, navigating to home');
      navigate('home');
    }
    
    console.log('Bootstrap: registering service worker...');
    registerServiceWorker();
    console.log('Bootstrap: complete');
  } catch (error) {
    console.error('Bootstrap: failed to initialize app', error);
    content.innerHTML = '<p class="card-meta error">Unable to initialize the app: ' + error.message + '. Check console for details.</p>';
    // Still try to set up navigation so user can at least see the error
    try {
      onRouteChange(showRoute);
      initRouter();
    } catch (navError) {
      console.error('Bootstrap: failed to set up navigation', navError);
    }
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
