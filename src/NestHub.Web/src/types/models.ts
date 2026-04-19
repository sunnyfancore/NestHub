export interface ProfileUser {
  id: string
  email: string
  displayName: string
}

export interface ProfileTenant {
  id: string
  name: string
}

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  user: ProfileUser
  tenant: ProfileTenant | null
  isSuperAdmin?: boolean
}

export interface ProfileResponse {
  user: ProfileUser
  tenant: ProfileTenant | null
}

export interface PortalSite {
  title: string
  subtitle: string
  description: string
  logoText: string
  logoUrl?: string | null
  searchPlaceholder: string
  footerText: string
  themeName: string
  mobileThemeName: string
  defaultSearchEngine?: string | null
  logoMode?: string
}

export interface PortalTenant {
  id: string
  name: string
}

export interface PortalCategoryOption {
  id: string
  parentId?: string | null
  name: string
  level: number
}

export interface PortalEditorData {
  categoryOptions: PortalCategoryOption[]
}

export interface PortalLink {
  id: string
  categoryId: string
  categoryName: string
  title: string
  url: string
  standbyUrl?: string | null
  description?: string | null
  tags?: string | null
  iconUrl?: string | null
  fontIcon?: string | null
  isPinned: boolean
  sortOrder: number
  clickCount: number
  checkStatus: number
  lastCheckedAt?: string | null
  lastVisitedAt?: string | null
}

export interface PortalCategory {
  id: string
  parentId?: string | null
  name: string
  description?: string | null
  icon?: string | null
  sortOrder: number
  links: PortalLink[]
  children: PortalCategory[]
}

export interface PortalResponse {
  site: PortalSite
  tenant: PortalTenant
  isAuthenticated: boolean
  canEdit: boolean
  featuredLinks: PortalLink[]
  categories: PortalCategory[]
  editor?: PortalEditorData | null
}

export interface PortalSearchResult {
  id: string
  title: string
  url: string
  standbyUrl?: string | null
  description?: string | null
  iconUrl?: string | null
  categoryName: string
}

export interface SavePortalCategoryRequest {
  name: string
  description?: string
  icon?: string
  parentId?: string | null
  sortOrder: number
}

export interface SavePortalLinkRequest {
  categoryId: string
  title: string
  url: string
  standbyUrl?: string
  description?: string
  tags?: string
  iconUrl?: string
  fontIcon?: string
  isPinned: boolean
  sortOrder: number
}

export interface SaveSiteSettingsRequest {
  title: string
  subtitle?: string
  description?: string
  logoText?: string
  logoUrl?: string | null
  searchPlaceholder?: string
  footerText?: string
  themeName?: string
  mobileThemeName?: string
  logoMode?: string
}

export interface AdminCategoryItem {
  id: string
  name: string
  parentId?: string | null
  parentName?: string | null
  description?: string | null
  icon?: string | null
  sortOrder: number
  linkCount: number
  updatedAt: string
}

export interface AdminLinkItem {
  id: string
  categoryId: string
  categoryName: string
  title: string
  url: string
  standbyUrl?: string | null
  description?: string | null
  tags?: string | null
  iconUrl?: string | null
  isPinned: boolean
  sortOrder: number
  clickCount: number
  updatedAt: string
}

export interface AdminThemeOption {
  name: string
  title: string
  version: string
  description: string
  screenshotUrl: string
  installed: boolean
}

export interface BookmarkImportResult {
  total: number
  imported: number
  failed: number
}

// ── Super Admin ──

export interface AdminTenant {
  id: string
  name: string
  displayName: string | null
  email: string | null
  isActive: boolean
  isSuperAdmin: boolean
  createdAt: string
  linkCount: number
}
