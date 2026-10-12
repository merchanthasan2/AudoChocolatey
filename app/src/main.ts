import { mount } from 'svelte';
import './lib/styles/fonts.css';
import './lib/styles/tokens.css';
import App from './App.svelte';

export default mount(App, { target: document.getElementById('app')! });
