import React from 'react';
import ReactDOM from 'react-dom';
import App from './App';

import './styles.css';

declare global {
  interface Window {
    readarrPreboot?: HTMLElement;
  }
}

function note(step: string) {
  if (window.readarrPreboot) {
    window.readarrPreboot.textContent = step;
  }
}

note('Bundle started executing');

function showError(message: string) {
  const root = document.getElementById('reader-root');
  if (root) {
    root.innerHTML = `<div style="padding:24px;color:#f77;font-family:monospace;white-space:pre-wrap;overflow-x:auto">Reader boot error:\n\n${message}</div>`;
  }
}

window.addEventListener('error', (e) => {
  showError(
    `${e.message}\n  at ${e.filename}:${e.lineno}:${e.colno}\n\n${
      e.error?.stack || ''
    }`
  );
});

window.addEventListener('unhandledrejection', (e) => {
  showError(
    `Unhandled promise rejection: ${e.reason?.message || e.reason}\n\n${
      e.reason?.stack || ''
    }`
  );
});

function mount() {
  try {
    note('About to call ReactDOM.render');
    const root = document.getElementById('reader-root');
    if (!root) {
      showError('reader-root element not found');
      return;
    }
    ReactDOM.render(<App />, root);
  } catch (err) {
    showError(
      err instanceof Error ? `${err.message}\n\n${err.stack}` : String(err)
    );
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', mount);
} else {
  mount();
}
