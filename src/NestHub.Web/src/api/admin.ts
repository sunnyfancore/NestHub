import { http } from '@/api/http'
import type {
  BookmarkImportResult,
  AdminCategoryItem,
  AdminLinkItem,
  AdminThemeOption,
} from '@/types/models'

export async function getAdminCategories(targetTenantId?: string): Promise<AdminCategoryItem[]> {
  const params = targetTenantId ? { targetTenantId } : {}
  const { data } = await http.get<AdminCategoryItem[]>('/admin/categories', { params })
  return data
}

export async function getAdminLinks(params?: {
  keyword?: string
  categoryId?: string
  targetTenantId?: string
}): Promise<AdminLinkItem[]> {
  const { data } = await http.get<AdminLinkItem[]>('/admin/links', { params })
  return data
}

export async function getAdminThemes(): Promise<AdminThemeOption[]> {
  const { data } = await http.get<AdminThemeOption[]>('/admin/themes')
  return data
}

export async function importBookmarks(categoryId: string | undefined, file: File): Promise<BookmarkImportResult> {
  const formData = new FormData()
  if (categoryId) formData.append('categoryId', categoryId)
  formData.append('file', file)

  const { data } = await http.post<BookmarkImportResult>('/admin/import', formData, {
    headers: {
      'Content-Type': 'multipart/form-data',
    },
  })

  return data
}

export async function exportBackup(): Promise<{
  site: Record<string, unknown>
  categories: AdminCategoryItem[]
  links: AdminLinkItem[]
}> {
  const { data } = await http.get('/admin/export')
  return data
}

export async function exportBookmarkHtml(): Promise<Blob> {
  const { data } = await http.get('/admin/export-bookmarks', {
    responseType: 'blob',
  })
  return data as Blob
}
