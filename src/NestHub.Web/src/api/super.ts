import { http } from '@/api/http'
import type { AdminTenant } from '@/types/models'

export async function getTenants(): Promise<AdminTenant[]> {
  const { data } = await http.get<AdminTenant[]>('/super/tenants')
  return data
}

export async function createTenant(payload: {
  name: string
  email: string
  displayName?: string
  password: string
}): Promise<AdminTenant> {
  const { data } = await http.post<AdminTenant>('/super/tenants', payload)
  return data
}

export async function updateTenant(tenantId: string, payload: { name: string; displayName?: string }): Promise<void> {
  await http.put(`/super/tenants/${tenantId}`, payload)
}

export async function toggleTenantActive(tenantId: string): Promise<void> {
  await http.patch(`/super/tenants/${tenantId}/toggle-active`)
}

export async function resetTenantPassword(tenantId: string, newPassword: string): Promise<void> {
  await http.patch(`/super/tenants/${tenantId}/reset-password`, { newPassword })
}
