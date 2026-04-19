import { http } from '@/api/http'
import type { PortalSite, SaveSiteSettingsRequest } from '@/types/models'

export async function getSiteSettings(targetTenantId?: string): Promise<PortalSite> {
  const params = targetTenantId ? { targetTenantId } : {}
  const { data } = await http.get<PortalSite>('/site', { params })
  return data
}

export async function updateSiteSettings(payload: SaveSiteSettingsRequest, targetTenantId?: string): Promise<PortalSite> {
  const params = targetTenantId ? { targetTenantId } : {}
  const { data } = await http.put<PortalSite>('/site', payload, { params })
  return data
}

export async function updateSearchEngine(searchEngine: string): Promise<void> {
  await http.patch('/site/search-engine', { searchEngine })
}

export async function uploadLogo(file: File, targetTenantId?: string): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)
  const params = targetTenantId ? { targetTenantId } : {}
  const { data } = await http.post<{ logoUrl: string }>('/site/upload-logo', formData, {
    params,
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data.logoUrl
}
