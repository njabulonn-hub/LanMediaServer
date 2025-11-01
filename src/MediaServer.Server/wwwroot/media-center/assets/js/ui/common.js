export function createElement(tag, options = {}) {
  const el = document.createElement(tag);
  if (options.className) el.className = options.className;
  if (options.text !== undefined) el.textContent = options.text;
  if (options.html !== undefined) el.innerHTML = options.html;
  if (options.attrs) {
    Object.entries(options.attrs).forEach(([key, value]) => {
      if (value !== undefined && value !== null) {
        el.setAttribute(key, value);
      }
    });
  }
  if (Array.isArray(options.children)) {
    options.children.forEach(child => {
      if (child) el.append(child);
    });
  }
  return el;
}

export function renderList(container, items) {
  container.innerHTML = '';
  items.forEach(item => container.append(item));
  return container;
}

export function formatDuration(minutes) {
  if (minutes === undefined || minutes === null) return '';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h) {
    return `${h}h ${m.toString().padStart(2, '0')}m`;
  }
  return `${m}m`;
}
