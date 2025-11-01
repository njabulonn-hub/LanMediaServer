const listeners = new Set();

function notify(route) {
  listeners.forEach(listener => listener(route));
}

function parseRoute(hash) {
  return hash.replace('#', '') || 'home';
}

export function initRouter() {
  window.addEventListener('hashchange', () => {
    notify(parseRoute(location.hash));
  });

  notify(parseRoute(location.hash));
}

export function onRouteChange(callback) {
  listeners.add(callback);
  return () => listeners.delete(callback);
}

export function navigate(route) {
  location.hash = `#${route}`;
}
