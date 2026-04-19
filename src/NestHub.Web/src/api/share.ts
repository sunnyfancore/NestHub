import { http } from '@/api/http'

export interface ShareLinkItem {
  id: string
  shareCode: string
  categoryId: string
  categoryName: string
  password?: string | null
  note?: string | null
  expireAt?: string | null
  createdAt: string
}

export interface SaveShareLinkRequest {
  categoryId: string
  password?: string
  note?: string
  expireAt?: string | null
}

export async function getShareLinks(): Promise<ShareLinkItem[]> {
  const { data } = await http.get<ShareLinkItem[]>('/shares')
  return data
}

export async function createShareLink(payload: SaveShareLinkRequest): Promise<ShareLinkItem> {
  const { data } = await http.post<ShareLinkItem>('/shares', payload)
  return data
}

export async function deleteShareLink(id: string): Promise<void> {
  await http.delete(`/shares/${id}`)
}
