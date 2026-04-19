import { http } from '@/api/http'
import type { PortalLink, SavePortalLinkRequest } from '@/types/models'

function tenantParams(targetTenantId?: string) {
  return targetTenantId ? { targetTenantId } : {}
}

export async function createLink(payload: SavePortalLinkRequest, targetTenantId?: string): Promise<PortalLink> {
  const { data } = await http.post<PortalLink>('/links', payload, { params: tenantParams(targetTenantId) })
  return data
}

export async function updateLink(id: string, payload: SavePortalLinkRequest): Promise<PortalLink> {
  const { data } = await http.put<PortalLink>(`/links/${id}`, payload)
  return data
}

export async function deleteLink(id: string): Promise<void> {
  await http.delete(`/links/${id}`)
}

export async function sortLinks(orderedIds: string[]): Promise<void> {
  await http.post('/links/sort', { orderedIds })
}
