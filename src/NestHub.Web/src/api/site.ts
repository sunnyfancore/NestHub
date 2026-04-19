import { http } from '@/api/http'
import type { PortalSite, SaveSiteSettingsRequest } from '@/types/models'

export async function getSiteSettings(): Promise<PortalSite> {
  const { data } = await http.get<PortalSite>('/site')
  return data
}

export async function updateSiteSettings(payload: SaveSiteSettingsRequest): Promise<PortalSite> {
  const { data } = await http.put<PortalSite>('/site', payload)
  return data
}

export async function updateSearchEngine(searchEngine: string): Promise<void> {
  await http.patch('/site/search-engine', { searchEngine })
}

export async function uploadLogo(file: File): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)
  const { data } = await http.post<{ logoUrl: string }>('/site/upload-logo', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data.logoUrl
}
