const listeners = new Set();

function notify(route) {
  console.log('Router: navigating to', route);
  listeners.forEach(listener => {
    try {
      listener(route);
    } catch (error) {
      console.error('Router: error in listener for route', route, error);
    }
  });
}

function parseRoute(hash) {
  const route = hash.replace('#', '') || 'home';
  console.log('Router: parsed route from hash', hash, '->', route);
  return route;
}

export function initRouter() {
  window.addEventListener('hashchange', () => {
    console.log('Router: hash changed to', location.hash);
    notify(parseRoute(location.hash));
  });

  console.log('Router: initializing with hash', location.hash);
  notify(parseRoute(location.hash));
}

export function onRouteChange(callback) {
  listeners.add(callback);
  return () => listeners.delete(callback);
}

export function navigate(route) {
  location.hash = `#${route}`;
}
