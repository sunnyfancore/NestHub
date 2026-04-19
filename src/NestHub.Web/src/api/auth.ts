import { http } from '@/api/http'
import type { AuthResponse, ProfileResponse } from '@/types/models'

export async function login(payload: {
  email: string
  password: string
}): Promise<AuthResponse> {
  const { data } = await http.post<AuthResponse>('/auth/login', payload)
  return data
}

export async function renewToken(): Promise<AuthResponse> {
  const { data } = await http.post<AuthResponse>('/auth/renew')
  return data
}

export async function getProfile(): Promise<ProfileResponse> {
  const { data } = await http.get<ProfileResponse>('/profile')
  return data
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  await http.post('/auth/change-password', { currentPassword, newPassword })
}

export async function requestPasswordReset(email: string): Promise<void> {
  await http.post('/auth/request-reset', { email })
}

export async function confirmPasswordReset(token: string, newPassword: string): Promise<void> {
  await http.post('/auth/confirm-reset', { token, newPassword })
}
