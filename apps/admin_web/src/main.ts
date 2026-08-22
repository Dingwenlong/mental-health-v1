import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './style.css'
import App from './App.vue'
import { getUiCopy } from './generated/uiCopy.generated'

document.title = getUiCopy('admin.title')
createApp(App).use(createPinia()).mount('#app')
