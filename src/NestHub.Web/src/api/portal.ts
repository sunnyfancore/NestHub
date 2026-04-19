import { http } from '@/api/http'
import type { PortalResponse, PortalSearchResult } from '@/types/models'

export async function getPortal(publicView?: boolean): Promise<PortalResponse> {
  const params: Record<string, unknown> = {}
  if (publicView) params.publicView = true
  const { data } = await http.get<PortalResponse>('/portal', { params })
  return data
}

export async function searchPortal(keyword: string, publicView?: boolean): Promise<PortalSearchResult[]> {
  const params: Record<string, unknown> = { keyword }
  if (publicView) params.publicView = true
  const { data } = await http.get<PortalSearchResult[]>('/portal/search', { params })
  return data
}

export async function clickPortalLink(id: string): Promise<void> {
  await http.post(`/portal/links/${id}/click`)
}
