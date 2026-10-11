import { createApp } from 'vue'
import 'element-plus/es/components/message/style/css'
import 'element-plus/es/components/message-box/style/css'
import 'font-awesome/css/font-awesome.min.css'
import './style.css'
import './styles/portal-palette.css'
import './styles/editor-dialogs.css'
import App from './App.vue'
import { router } from './router'
import { pinia } from './stores'
import { useAuthStore } from './stores/auth'

const app = createApp(App)

app.use(pinia)
app.use(router)

const authStore = useAuthStore()

void authStore.restore()
app.mount('#app')
