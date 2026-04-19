import { defineStore } from 'pinia'
import { getProfile, login, renewToken } from '@/api/auth'
import type { AuthResponse, ProfileResponse } from '@/types/models'

export const AUTH_TOKEN_KEY = 'nesthub.token'

interface AuthState {
  token: string | null
  expiresAtUtc: string | null
  user: AuthResponse['user'] | null
  tenant: AuthResponse['tenant'] | null
  isSuperAdmin: boolean
}

const EXPIRES_KEY = 'nesthub.expiresAt'
const SUPER_ADMIN_KEY = 'nesthub.isSuperAdmin'

function persistAuth(state: AuthState): void {
  if (state.token) {
    localStorage.setItem(AUTH_TOKEN_KEY, state.token)
  } else {
    localStorage.removeItem(AUTH_TOKEN_KEY)
  }

  if (state.expiresAtUtc) {
    localStorage.setItem(EXPIRES_KEY, state.expiresAtUtc)
  } else {
    localStorage.removeItem(EXPIRES_KEY)
  }

  if (state.isSuperAdmin) {
    localStorage.setItem(SUPER_ADMIN_KEY, 'true')
  } else {
    localStorage.removeItem(SUPER_ADMIN_KEY)
  }
}

export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: null,
    expiresAtUtc: null,
    user: null,
    tenant: null,
    isSuperAdmin: false,
  }),
  getters: {
    isAuthenticated: (state) => Boolean(state.token),
  },
  actions: {
    async restore() {
      const token = localStorage.getItem(AUTH_TOKEN_KEY)
      const expiresAtUtc = localStorage.getItem(EXPIRES_KEY)
      const isSuperAdmin = localStorage.getItem(SUPER_ADMIN_KEY) === 'true'

      if (!token) {
        return
      }

      this.token = token
      this.expiresAtUtc = expiresAtUtc
      this.isSuperAdmin = isSuperAdmin

      // Auto-renew if token expires within 7 days
      if (expiresAtUtc) {
        const expires = new Date(expiresAtUtc).getTime()
        const now = Date.now()
        const daysLeft = (expires - now) / (1000 * 60 * 60 * 24)
        if (daysLeft < 7) {
          try {
            const payload = await renewToken()
            this.applyAuth(payload)
          } catch {
            // renewal failed — will clear on next 401
          }
        }
      }
    },
    applyAuth(payload: AuthResponse | ProfileResponse) {
      if ('token' in payload) {
        this.token = payload.token
        this.expiresAtUtc = payload.expiresAtUtc
      }

      if ('isSuperAdmin' in payload) {
        this.isSuperAdmin = (payload as AuthResponse).isSuperAdmin ?? false
      }

      this.user = payload.user
      this.tenant = payload.tenant ?? null
      persistAuth(this.$state)
    },
    async login(credentials: { email: string; password: string }) {
      const payload = await login(credentials)
      this.applyAuth(payload)
      return payload
    },
    async loadProfile() {
      if (!this.token) {
        return
      }

      const payload = await getProfile()
      this.applyAuth(payload)
    },
    clear() {
      this.token = null
      this.expiresAtUtc = null
      this.user = null
      this.tenant = null
      this.isSuperAdmin = false
      persistAuth(this.$state)
    },
  },
})
