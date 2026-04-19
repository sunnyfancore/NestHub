import { http } from '@/api/http'
import type { PortalCategory, SavePortalCategoryRequest } from '@/types/models'

function tenantParams(targetTenantId?: string) {
  return targetTenantId ? { targetTenantId } : {}
}

export async function createCategory(payload: SavePortalCategoryRequest, targetTenantId?: string): Promise<PortalCategory> {
  const { data } = await http.post<PortalCategory>('/categories', payload, { params: tenantParams(targetTenantId) })
  return data
}

export async function updateCategory(id: string, payload: SavePortalCategoryRequest): Promise<PortalCategory> {
  const { data } = await http.put<PortalCategory>(`/categories/${id}`, payload)
  return data
}

export async function deleteCategory(id: string): Promise<void> {
  await http.delete(`/categories/${id}`)
}

export async function sortCategories(orderedIds: string[]): Promise<void> {
  await http.post('/categories/sort', { orderedIds })
}
