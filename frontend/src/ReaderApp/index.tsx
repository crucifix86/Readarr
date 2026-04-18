import React from 'react';
import ReactDOM from 'react-dom';
import App from './App';

import './styles.css';

const root = document.getElementById('reader-root');
if (root) {
  ReactDOM.render(<App />, root);
}
