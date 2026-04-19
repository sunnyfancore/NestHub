import axios from 'axios'
import { AUTH_TOKEN_KEY } from '@/stores/auth'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 15000,
})

let isRefreshing = false
let refreshPromise: Promise<void> | null = null

http.interceptors.request.use((config) => {
  const token = localStorage.getItem(AUTH_TOKEN_KEY)

  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

http.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config
    if (error.response?.status === 401 && !originalRequest._retried) {
      originalRequest._retried = true

      if (!isRefreshing) {
        isRefreshing = true
        refreshPromise = axios.post('/api/auth/renew', null, {
          headers: { Authorization: `Bearer ${localStorage.getItem(AUTH_TOKEN_KEY)}` },
        }).then((res) => {
          const newToken = res.data.token
          localStorage.setItem(AUTH_TOKEN_KEY, newToken)
          if (res.data.expiresAtUtc) {
            localStorage.setItem('nesthub.expiresAt', res.data.expiresAtUtc)
          }
        }).catch(() => {
          localStorage.removeItem(AUTH_TOKEN_KEY)
          localStorage.removeItem('nesthub.expiresAt')
        }).finally(() => {
          isRefreshing = false
          refreshPromise = null
        })
      }

      await refreshPromise

      const newToken = localStorage.getItem(AUTH_TOKEN_KEY)
      if (newToken) {
        originalRequest.headers.Authorization = `Bearer ${newToken}`
        return http(originalRequest)
      }
    }

    return Promise.reject(error)
  },
)
