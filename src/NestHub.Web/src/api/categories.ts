import { http } from '@/api/http'
import type { PortalCategory, SavePortalCategoryRequest } from '@/types/models'

function tenantParams(targetTenantId?: string) {
  return targetTenantId ? { targetTenantId } : {}
}

export async function createCategory(payload: SavePortalCategoryRequest, targetTenantId?: string): Promise<PortalCategory> {
  const { data } = await http.post<PortalCategory>('/categories', payload, { params: tenantParams(targetTenantId) })
  return data
}

export async function updateCategory(id: string, payload: SavePortalCategoryRequest, targetTenantId?: string): Promise<PortalCategory> {
  const { data } = await http.put<PortalCategory>(`/categories/${id}`, payload, { params: tenantParams(targetTenantId) })
  return data
}

export async function deleteCategory(id: string, targetTenantId?: string): Promise<void> {
  await http.delete(`/categories/${id}`, { params: tenantParams(targetTenantId) })
}

export async function sortCategories(orderedIds: string[], targetTenantId?: string): Promise<void> {
  await http.post('/categories/sort', { orderedIds }, { params: tenantParams(targetTenantId) })
}
