<script setup lang="ts">
import {
  Connection,
  Edit,
  FolderAdd,
  Link,
  Plus,
  Refresh,
  Setting,
  SwitchButton,
  Top,
} from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import Sortable from 'sortablejs'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { createCategory, deleteCategory, sortCategories, updateCategory } from '@/api/categories'
import { createLink, deleteLink, sortLinks, updateLink } from '@/api/links'
import { clickPortalLink, getPortal, searchPortal } from '@/api/portal'
import { updateSearchEngine, updateSiteSettings } from '@/api/site'
import AppInfoDialog from '@/components/portal/AppInfoDialog.vue'
import LoginDialog from '@/components/portal/LoginDialog.vue'
import PortalCategoryDialog from '@/components/portal/PortalCategoryDialog.vue'
import PortalLinkDialog from '@/components/portal/PortalLinkDialog.vue'
import SiteSettingsDialog from '@/components/portal/SiteSettingsDialog.vue'
import { useAuthStore } from '@/stores/auth'
import type {
  PortalCategory,
  PortalLink,
  PortalResponse,
  PortalSearchResult,
  SavePortalCategoryRequest,
  SavePortalLinkRequest,
  SaveSiteSettingsRequest,
} from '@/types/models'

type SearchEngine = 'baidu' | 'google' | 'bing' | 'toutiao' | 'sogou' | 'so360' | 'zhihu' | 'github'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const loading = ref(false)
const saving = ref(false)
const VIEW_KEY = 'nesthub.public-view'
const isPublicView = ref(localStorage.getItem(VIEW_KEY) === 'true')
const searchLoading = ref(false)
const portal = ref<PortalResponse | null>(null)
const searchKeyword = ref('')
const searchResults = ref<PortalSearchResult[]>([])
const searchDialogVisible = ref(false)
const searchEngine = ref<SearchEngine>('google')
const searchEngineMenuVisible = ref(false)
const expandedMenu = ref<Record<string, boolean>>({})
const activeChildTabs = ref<Record<string, string>>({})
const mobileSidebarOpen = ref(false)
const mobileRailOpen = ref(false)
const searchSuggestionIndex = ref(-1)
const searchSuggestions = ref<PortalSearchResult[]>([])

const DARK_KEY = 'nesthub.dark'
const isDark = ref(localStorage.getItem(DARK_KEY) === 'true')

const activeTheme = computed(() => {
  const preview = route.query.preview as string
  if (preview) return preview
  const isMobile = window.innerWidth < 768
  if (isMobile && portal.value?.site.mobileThemeName) return portal.value.site.mobileThemeName
  return portal.value?.site.themeName || 'default2'
})

function applyDarkMode(dark: boolean) {
  document.documentElement.classList.toggle('dark', dark)
  isDark.value = dark
  localStorage.setItem(DARK_KEY, String(dark))
}

function toggleDarkMode() {
  applyDarkMode(!isDark.value)
}

applyDarkMode(isDark.value)
const searchSuggestionsVisible = ref(false)

const categoryDialogVisible = ref(false)
const linkDialogVisible = ref(false)
const siteDialogVisible = ref(false)
const appInfoDialogVisible = ref(false)
const loginDialogVisible = ref(false)
const qrDialogVisible = ref(false)

const editingCategory = ref<PortalCategory | null>(null)
const editingLink = ref<PortalLink | null>(null)
const defaultParentId = ref<string | null>(null)
const defaultCategoryId = ref<string | null>(null)
const currentQrLink = ref<PortalLink | null>(null)
const contextMenu = ref<{
  visible: boolean
  x: number
  y: number
  link: PortalLink | null
}>({
  visible: false,
  x: 0,
  y: 0,
  link: null,
})

const categoryContextMenu = ref<{
  visible: boolean
  x: number
  y: number
  category: PortalCategory | null
}>({
  visible: false,
  x: 0,
  y: 0,
  category: null,
})

const activeSectionId = ref<string>('')

const contextMenuStyle = computed(() => {
  const menuWidth = 180
  const menuHeight = 280
  let x = contextMenu.value.x - 10
  let y = contextMenu.value.y - 5
  if (x + menuWidth > window.innerWidth) {
    x = window.innerWidth - menuWidth - 10
  }
  if (y + menuHeight > window.innerHeight) {
    y = window.innerHeight - menuHeight - 10
  }
  return { left: `${x}px`, top: `${y}px` }
})

const categoryContextMenuStyle = computed(() => {
  const menuWidth = 160
  const menuHeight = 200
  let x = categoryContextMenu.value.x
  let y = categoryContextMenu.value.y
  if (x + menuWidth > window.innerWidth) {
    x = window.innerWidth - menuWidth - 10
  }
  if (y + menuHeight > window.innerHeight) {
    y = window.innerHeight - menuHeight - 10
  }
  return { left: `${x}px`, top: `${y}px` }
})

const rootSectionContainerRef = ref<HTMLElement | null>(null)
const mainScrollRef = ref<HTMLElement | null>(null)
const childSectionContainerRefs = ref<Record<string, HTMLElement | null>>({})
const linkContainerRefs = ref<Record<string, HTMLElement | null>>({})
const sidebarNavRef = ref<HTMLElement | null>(null)
const sidebarChildrenRefs = ref<Record<string, HTMLElement | null>>({})
const sortables = ref<any[]>([])

const searchEngines = [
  { label: '百度', value: 'baidu' as SearchEngine },
  { label: 'Bing', value: 'bing' as SearchEngine },
  { label: 'Google', value: 'google' as SearchEngine },
  { label: '头条', value: 'toutiao' as SearchEngine },
  { label: '搜狗', value: 'sogou' as SearchEngine },
  { label: '360', value: 'so360' as SearchEngine },
  { label: '知乎', value: 'zhihu' as SearchEngine },
  { label: 'GitHub', value: 'github' as SearchEngine },
]
const searchEngineValues = searchEngines.map((item) => item.value)

const currentSearchEngineLabel = computed(() => {
  const current = searchEngines.find((item) => item.value === searchEngine.value)
  return current?.label ?? 'Google'
})
const categoryParents = computed(() =>
  (portal.value?.editor?.categoryOptions || []).filter((item) => item.level === 1),
)

const categoryOptions = computed(() => portal.value?.editor?.categoryOptions || [])

function rootLinks(category: PortalCategory) {
  return [...category.links].sort((left, right) => right.sortOrder - left.sortOrder)
}

function childLinks(category: PortalCategory) {
  return [...category.links].sort((left, right) => right.sortOrder - left.sortOrder)
}

function activeChildTab(category: PortalCategory) {
  const activeId = activeChildTabs.value[category.id]
  if (activeId && category.children.some((child) => child.id === activeId)) {
    return activeId
  }
  return category.children[0]?.id || ''
}

function setActiveChildTab(category: PortalCategory, childId: string) {
  activeChildTabs.value[category.id] = childId
  if (childId) {
    activeSectionId.value = childId
  }
  if (portal.value?.canEdit) {
    void nextTick(() => {
      initializeSortables()
    })
  }
}

function categoryDisplayLinks(category: PortalCategory) {
  if (!category.children.length) {
    return rootLinks(category)
  }

  const activeId = activeChildTab(category)
  const child = category.children.find((item) => item.id === activeId)
  return child ? childLinks(child) : []
}

function activeLinkContainerId(category: PortalCategory) {
  if (!category.children.length) {
    return category.id
  }

  return activeChildTab(category)
}

async function ensureProfile() {
  if (!authStore.token) {
    return
  }

  if (!authStore.user) {
    try {
      await authStore.loadProfile()
    } catch {
      authStore.clear()
    }
  }
}

const CACHE_KEY = 'nesthub-portal-cache'
const CACHE_TTL = 30 * 24 * 60 * 60 * 1000 // 30 days

function savePortalCache(data: PortalResponse) {
  try {
    localStorage.setItem(CACHE_KEY, JSON.stringify({ data, ts: Date.now(), publicView: isPublicView.value }))
  } catch {
    // ignore quota errors
  }
}

function applyPortalData(data: PortalResponse) {
  portal.value = data
  document.title = data.site.title
  initSearchEngine()
  initExpandedMenu()
}

async function loadPortal(forceRefresh = false) {
  loading.value = true

  try {
    if (!forceRefresh) {
      const raw = localStorage.getItem(CACHE_KEY)
      if (raw) {
        try {
          const { data, ts, publicView: cachedPublicView } = JSON.parse(raw)
          if (Date.now() - ts <= CACHE_TTL) {
            const cachedIsPublicView = !!cachedPublicView
            const cachedIsPrivateView = !cachedIsPublicView
            const authChanged = authStore.isAuthenticated && cachedIsPrivateView && !data.canEdit
            const unauthenticatedPrivateCache = !authStore.isAuthenticated && cachedIsPrivateView && (data.isAuthenticated || data.canEdit)
            const viewChanged = isPublicView.value !== !!cachedPublicView
            if (!authChanged && !unauthenticatedPrivateCache && !viewChanged) {
              applyPortalData(data)
              await nextTick()
              initializeSortables()
              setupSectionObserver()
              loading.value = false
              return true
            }
          }
        } catch {
          // invalid cache
        }
      }
    }

    const data = await getPortal(isPublicView.value)
    savePortalCache(data)
    applyPortalData(data)
    await nextTick()
    initializeSortables()
    setupSectionObserver()
    return true
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '加载导航门户失败。')
    return false
  } finally {
    loading.value = false
  }
}

function initSearchEngine() {
  const saved = portal.value?.site.defaultSearchEngine as SearchEngine | undefined
  if (saved && searchEngineValues.includes(saved)) {
    searchEngine.value = saved
  } else {
    const local = localStorage.getItem('nesthub-search-engine') as SearchEngine | null
    if (local && searchEngineValues.includes(local)) {
      searchEngine.value = local
    } else {
      searchEngine.value = 'google'
      localStorage.removeItem('nesthub-search-engine')
    }
  }
}

function initExpandedMenu() {
  if (!portal.value) {
    return
  }

  for (const category of portal.value.categories) {
    if (!(category.id in expandedMenu.value)) {
      expandedMenu.value[category.id] = false
    }
  }
}

function openLoginDialog() {
  loginDialogVisible.value = true
}

function openAdmin() {
  router.push('/admin')
}

async function logout() {
  try {
    await ElMessageBox.confirm('确认退出登录吗？', '退出确认', { type: 'warning', confirmButtonText: '退出', cancelButtonText: '取消' })
  } catch {
    return
  }
  authStore.clear()
  localStorage.removeItem('nesthub-portal-cache')
  localStorage.removeItem(VIEW_KEY)
  isPublicView.value = false
  await loadPortal(true)
}

const PUBLIC_TENANT_ID = '00000000-0000-0000-0000-000000000001'

function getTargetTenantId(): string | undefined {
  return isPublicView.value ? PUBLIC_TENANT_ID : undefined
}

async function setPublicView(publicView: boolean) {
  if (isPublicView.value === publicView) {
    return
  }

  isPublicView.value = publicView
  localStorage.setItem(VIEW_KEY, String(isPublicView.value))
  expandedMenu.value = {}
  activeSectionId.value = ''
  localStorage.removeItem('nesthub-portal-cache')
  await loadPortal(true)
}

function openSearchEngine(keyword: string) {
  switch (searchEngine.value) {
    case 'baidu':
      window.open(`https://www.baidu.com/s?wd=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'google':
      window.open(`https://www.google.com/search?q=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'bing':
      window.open(`https://cn.bing.com/search?q=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'toutiao':
      window.open(`https://so.toutiao.com/search?keyword=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'sogou':
      window.open(`https://www.sogou.com/web?query=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'so360':
      window.open(`https://www.so.com/s?q=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'zhihu':
      window.open(`https://www.zhihu.com/search?type=content&q=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
    case 'github':
      window.open(`https://github.com/search?q=${encodeURIComponent(keyword)}`, '_blank', 'noopener,noreferrer')
      break
  }
}

let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null

async function onSearchInput() {
  const keyword = searchKeyword.value.trim()
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  searchSuggestionIndex.value = -1

  if (keyword.length < 2) {
    searchSuggestions.value = []
    searchSuggestionsVisible.value = false
    return
  }

  searchDebounceTimer = setTimeout(async () => {
    try {
      const results = await searchPortal(keyword, isPublicView.value)
      searchSuggestions.value = results.slice(0, 8)
      searchSuggestionsVisible.value = searchSuggestions.value.length > 0
    } catch {
      searchSuggestions.value = []
      searchSuggestionsVisible.value = false
    }
  }, 250)
}

function handleSearchKeydown(e: KeyboardEvent) {
  if (!searchSuggestionsVisible.value || searchSuggestions.value.length === 0) {
    if (e.key === 'Enter') runSearch()
    return
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    searchSuggestionIndex.value = (searchSuggestionIndex.value + 1) % searchSuggestions.value.length
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    searchSuggestionIndex.value = searchSuggestionIndex.value <= 0
      ? searchSuggestions.value.length - 1
      : searchSuggestionIndex.value - 1
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (searchSuggestionIndex.value >= 0 && searchSuggestionIndex.value < searchSuggestions.value.length) {
      const item = searchSuggestions.value[searchSuggestionIndex.value]
      window.open(item.url, '_blank', 'noopener,noreferrer')
      closeSearchSuggestions()
    } else {
      runSearch()
    }
  } else if (e.key === 'Escape') {
    closeSearchSuggestions()
  }
}

function selectSuggestion(item: PortalSearchResult) {
  window.open(item.url, '_blank', 'noopener,noreferrer')
  closeSearchSuggestions()
}

function closeSearchSuggestions() {
  searchSuggestionsVisible.value = false
  searchSuggestionIndex.value = -1
}

async function runSearch() {
  const keyword = searchKeyword.value.trim()
  closeSearchSuggestions()
  if (keyword.length < 2) {
    ElMessage.warning('请输入至少两个字符。')
    return
  }

  openSearchEngine(keyword)
}

async function openLink(link: PortalLink, useStandby = false) {
  const target = useStandby && link.standbyUrl ? link.standbyUrl : link.url
  window.open(target, '_blank', 'noopener,noreferrer')

  try {
    await clickPortalLink(link.id)
    updateLocalLink(link.id, (item) => {
      item.clickCount += 1
      item.lastVisitedAt = new Date().toISOString()
    })
  } catch {
    // ignore click tracking
  }
}

function updateLocalLink(id: string, patch: (link: PortalLink) => void) {
  if (!portal.value) {
    return
  }

  for (const category of portal.value.categories) {
    for (const link of category.links) {
      if (link.id === id) {
        patch(link)
      }
    }

    for (const child of category.children) {
      for (const link of child.links) {
        if (link.id === id) {
          patch(link)
        }
      }
    }
  }
}

function openCreateCategory(parentId?: string) {
  editingCategory.value = null
  defaultParentId.value = parentId || null
  categoryDialogVisible.value = true
}

function openEditCategory(category: PortalCategory) {
  editingCategory.value = category
  defaultParentId.value = null
  categoryDialogVisible.value = true
}

async function submitCategory(payload: SavePortalCategoryRequest) {
  saving.value = true

  try {
    if (editingCategory.value) {
      await updateCategory(editingCategory.value.id, payload, getTargetTenantId())
      ElMessage.success('分类已更新。')
    } else {
      await createCategory(payload, getTargetTenantId())
      ElMessage.success('分类已创建。')
    }

    categoryDialogVisible.value = false
    await loadPortal(true)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '分类保存失败。')
  } finally {
    saving.value = false
  }
}

async function removeCategory(category: PortalCategory) {
  await ElMessageBox.confirm(`确认删除分类「${category.name}」吗？`, '删除分类', {
    type: 'warning',
  })

  try {
    await deleteCategory(category.id, getTargetTenantId())
    ElMessage.success('分类已删除。')
    await loadPortal(true)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '分类删除失败。')
  }
}

function openCreateLink(categoryId?: string) {
  editingLink.value = null
  defaultCategoryId.value = categoryId || null
  linkDialogVisible.value = true
}

function openEditLink(link: PortalLink) {
  editingLink.value = link
  defaultCategoryId.value = null
  linkDialogVisible.value = true
}

async function submitLink(payload: SavePortalLinkRequest) {
  saving.value = true

  try {
    if (editingLink.value) {
      await updateLink(editingLink.value.id, payload, getTargetTenantId())
      ElMessage.success('链接已更新。')
    } else {
      await createLink(payload, getTargetTenantId())
      ElMessage.success('链接已创建。')
    }

    linkDialogVisible.value = false
    await loadPortal(true)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '链接保存失败。')
  } finally {
    saving.value = false
  }
}

async function removeLink(link: PortalLink) {
  await ElMessageBox.confirm(`确认删除链接「${link.title}」吗？`, '删除链接', {
    type: 'warning',
  })

  try {
    await deleteLink(link.id, getTargetTenantId())
    ElMessage.success('链接已删除。')
    await loadPortal(true)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '链接删除失败。')
  }
}

async function submitSiteSettings(payload: SaveSiteSettingsRequest) {
  saving.value = true

  try {
    await updateSiteSettings(payload, getTargetTenantId())
    siteDialogVisible.value = false
    ElMessage.success('站点设置已更新。')
    await loadPortal(true)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '站点设置保存失败。')
  } finally {
    saving.value = false
  }
}

function handleCategoryCommand(category: PortalCategory, command: string | number | object) {
  if (command === 'edit') {
    openEditCategory(category)
    return
  }

  void removeCategory(category)
}

function toggleMenu(categoryId: string) {
  expandedMenu.value[categoryId] = !expandedMenu.value[categoryId]

  if (portal.value?.canEdit && expandedMenu.value[categoryId]) {
    void nextTick(() => {
      initializeSortables()
    })
  }
}

function anchorId(categoryId: string) {
  return `section-${categoryId}`
}

function childAnchorId(categoryId: string) {
  return `child-${categoryId}`
}

function iconCandidates(link: PortalLink) {
  const candidates: string[] = []
  if (link.iconUrl) {
    candidates.push(link.iconUrl)
  }
  try {
    const u = new URL(link.url)
    const host = u.port ? `${u.hostname}:${u.port}` : u.hostname
    const domain = u.hostname
    const base64 = btoa(host)
    const origin = `${u.protocol}//${u.host}`
    candidates.push(
      `https://t0.gstatic.cn/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&size=32&url=${encodeURIComponent(origin)}`,
      `https://favicon.im/${encodeURIComponent(domain)}?larger=true`,
      `https://icons.duckduckgo.com/ip3/${domain}.ico`,
      `https://favicon.png.pub/v1/${base64}`,
      `https://www.google.com/s2/favicons?domain=${encodeURIComponent(domain)}&sz=64`,
    )
  } catch {
    // ignore invalid urls and fall through to the fallback letter
  }
  return candidates
}

function resolveIcon(link: PortalLink) {
  return iconCandidates(link)[0] || ''
}

function handleIconError(event: Event, link: PortalLink) {
  const img = event.target as HTMLImageElement
  if (!img) return
  const candidates = iconCandidates(link)
  const currentIndex = Number(img.dataset.iconIndex || '0')
  const nextIcon = candidates[currentIndex + 1]
  if (nextIcon) {
    img.dataset.iconIndex = String(currentIndex + 1)
    img.src = nextIcon
    return
  }

  const span = img.parentElement
  if (!span) return
  const cardEl = span.closest('.portal-card') as HTMLElement | null
  const title = cardEl?.querySelector('.portal-card__copy strong')?.textContent?.trim() || '?'
  span.innerHTML = `<span class="portal-card__fallback">${title.charAt(0).toUpperCase()}</span>`
}

function openContextMenu(link: PortalLink, event: MouseEvent) {
  contextMenu.value = {
    visible: true,
    x: event.clientX,
    y: event.clientY,
    link,
  }
}

function closeContextMenu() {
  contextMenu.value.visible = false
  contextMenu.value.link = null
}

function openCategoryContextMenu(category: PortalCategory, event: MouseEvent) {
  categoryContextMenu.value = {
    visible: true,
    x: event.clientX,
    y: event.clientY,
    category,
  }
}

function closeCategoryContextMenu() {
  categoryContextMenu.value.visible = false
  categoryContextMenu.value.category = null
}

async function copyLink(link: PortalLink) {
  await navigator.clipboard.writeText(link.url)
  ElMessage.success('链接已复制。')
  closeContextMenu()
}

function showQrCode(link: PortalLink) {
  currentQrLink.value = link
  qrDialogVisible.value = true
  closeContextMenu()
}

function qrCodeUrl(link: PortalLink | null) {
  if (!link) {
    return ''
  }

  return `https://api.qrserver.com/v1/create-qr-code/?size=240x240&data=${encodeURIComponent(link.url)}`
}

function extractDomain(url: string): string {
  try {
    return new URL(url).hostname
  } catch {
    return url
  }
}

function getScrollContainer() {
  return mainScrollRef.value
}

function getAnchorOffset() {
  const searchbar = mainScrollRef.value?.querySelector<HTMLElement>('.portal-searchbar')
  return (searchbar?.offsetHeight || 80) - 2
}

function scrollToTop() {
  const container = getScrollContainer()
  if (container) {
    container.scrollTo({ top: 0, behavior: 'smooth' })
    return
  }

  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function scrollToSection(categoryId: string) {
  const el = document.getElementById(anchorId(categoryId)) || document.getElementById(childAnchorId(categoryId))
  if (!el) return

  activeSectionId.value = categoryId
  const container = getScrollContainer()
  const offset = getAnchorOffset()
  if (container) {
    const y = el.getBoundingClientRect().top - container.getBoundingClientRect().top + container.scrollTop - offset
    container.scrollTo({ top: y, behavior: 'smooth' })
    return
  }

  const y = el.getBoundingClientRect().top + window.scrollY - offset
  window.scrollTo({ top: y, behavior: 'smooth' })
}

async function refreshPortal() {
  localStorage.removeItem(CACHE_KEY)
  const refreshed = await loadPortal(true)

  if (refreshed) {
    ElMessage.success('已刷新最新数据')
  }
}

function selectChildCategory(category: PortalCategory, childId: string) {
  setActiveChildTab(category, childId)
  scrollToSection(category.id)
  activeSectionId.value = childId
}

function toggleSearchEngineMenu() {
  searchEngineMenuVisible.value = !searchEngineMenuVisible.value
}

function selectSearchEngine(value: SearchEngine) {
  searchEngine.value = value
  searchEngineMenuVisible.value = false
  localStorage.setItem('nesthub-search-engine', value)
  if (authStore.isAuthenticated) {
    updateSearchEngine(value).catch(() => {})
  }
}

async function handleLoginSuccess() {
  await ensureProfile()
  localStorage.removeItem(CACHE_KEY)
  await loadPortal(true)
}

function setChildContainerRef(categoryId: string, element: unknown) {
  childSectionContainerRefs.value[categoryId] = element instanceof HTMLElement ? element : null
}

function setLinkContainerRef(categoryId: string | null, element: unknown) {
  if (!categoryId) return
  linkContainerRefs.value[categoryId] = element instanceof HTMLElement ? element : null
}

function destroySortables() {
  for (const sortable of sortables.value) {
    sortable.destroy()
  }
  sortables.value = []
}

function initializeSortables() {
  destroySortables()

  if (!portal.value?.canEdit) {
    return
  }

  const sortableOpts = {
    animation: 160,
    ghostClass: 'portal-card--ghost',
    chosenClass: 'portal-card--chosen',
  }

  // Sidebar drag: parent categories
  if (sidebarNavRef.value) {
    sortables.value.push(
      Sortable.create(sidebarNavRef.value, {
        animation: 160,
        draggable: '.portal-sidebar__group',
        handle: '.portal-sidebar__drag',
        ghostClass: 'portal-sidebar__group--ghost',
        onEnd: async () => {
          if (!sidebarNavRef.value) return
          const orderedIds = Array.from(sidebarNavRef.value.querySelectorAll<HTMLElement>('.portal-sidebar__group'))
            .map((el) => el.dataset.categoryId || '')
            .filter(Boolean)
          try {
            await sortCategories(orderedIds, getTargetTenantId())
            /* silently updated */
            await loadPortal(true)
          } catch (error: any) {
            ElMessage.error(error?.response?.data?.message || '分类排序失败。')
          }
        },
      }),
    )
  }

  // Sidebar drag: child categories within each parent
  for (const root of portal.value.categories) {
    const childContainer = sidebarChildrenRefs.value[root.id]
    if (childContainer && root.children.length > 1) {
      sortables.value.push(
        Sortable.create(childContainer, {
          animation: 160,
          draggable: '.portal-sidebar__child-item',
          handle: '.portal-sidebar__child-drag',
          ghostClass: 'portal-sidebar__child-item--ghost',
          onEnd: async () => {
            const orderedIds = Array.from(childContainer.querySelectorAll<HTMLElement>('.portal-sidebar__child-item'))
              .map((el) => el.dataset.categoryId || '')
              .filter(Boolean)
            try {
              await sortCategories(orderedIds, getTargetTenantId())
              /* silently updated */
              await loadPortal(true)
            } catch (error: any) {
              ElMessage.error(error?.response?.data?.message || '子分类排序失败。')
            }
          },
        }),
      )
    }
  }

  if (rootSectionContainerRef.value) {
    sortables.value.push(
      Sortable.create(rootSectionContainerRef.value, {
        ...sortableOpts,
        draggable: '.portal-section',
        handle: '.portal-section__drag',
        onEnd: async () => {
          if (!rootSectionContainerRef.value) return
          const orderedIds = Array.from(rootSectionContainerRef.value.querySelectorAll<HTMLElement>('.portal-section'))
            .map((element) => element.dataset.categoryId || '')
            .filter(Boolean)

          try {
            await sortCategories(orderedIds, getTargetTenantId())
            /* silently updated */
            await loadPortal(true)
          } catch (error: any) {
            ElMessage.error(error?.response?.data?.message || '一级分类排序失败。')
          }
        },
      }),
    )
  }

  for (const root of portal.value.categories) {
    const childContainer = childSectionContainerRefs.value[root.id]
    if (childContainer && root.children.length > 1) {
      sortables.value.push(
        Sortable.create(childContainer, {
          ...sortableOpts,
          draggable: '.portal-child-tab',
          onEnd: async () => {
            const orderedIds = Array.from(childContainer.querySelectorAll<HTMLElement>('.portal-child-tab'))
              .map((element) => element.dataset.categoryId || '')
              .filter(Boolean)

            try {
              await sortCategories(orderedIds, getTargetTenantId())
              /* silently updated */
              await loadPortal(true)
            } catch (error: any) {
              ElMessage.error(error?.response?.data?.message || '二级分类排序失败。')
            }
          },
        }),
      )
    }

    const rootLinksContainer = linkContainerRefs.value[root.id]
    if (rootLinksContainer && root.links.length > 1) {
      sortables.value.push(
        Sortable.create(rootLinksContainer, {
          ...sortableOpts,
          draggable: '.portal-card',
          handle: '.portal-card__icon',
          onEnd: async () => {
            const orderedIds = Array.from(rootLinksContainer.querySelectorAll<HTMLElement>('.portal-card'))
              .map((element) => element.dataset.linkId || '')
              .filter(Boolean)

            try {
              await sortLinks(orderedIds, getTargetTenantId())
              /* silently updated */
              await loadPortal(true)
            } catch (error: any) {
              ElMessage.error(error?.response?.data?.message || '链接排序失败。')
            }
          },
        }),
      )
    }

    for (const child of root.children) {
      const childLinksContainer = linkContainerRefs.value[child.id]
      if (!childLinksContainer || child.links.length <= 1) {
        continue
      }

      sortables.value.push(
        Sortable.create(childLinksContainer, {
          ...sortableOpts,
          draggable: '.portal-card',
          handle: '.portal-card__icon',
          onEnd: async () => {
            const orderedIds = Array.from(childLinksContainer.querySelectorAll<HTMLElement>('.portal-card'))
              .map((element) => element.dataset.linkId || '')
              .filter(Boolean)

            try {
              await sortLinks(orderedIds, getTargetTenantId())
              /* silently updated */
              await loadPortal(true)
            } catch (error: any) {
              ElMessage.error(error?.response?.data?.message || '子分类链接排序失败。')
            }
          },
        }),
      )
    }
  }
}

watch(
  () => authStore.tenant?.id,
  async (current, previous) => {
    if (current && previous && current !== previous) {
      await loadPortal(true)
    }
  },
)

let sectionObserver: IntersectionObserver | null = null
let activeSectionFrame = 0

function normalizeSectionId(sectionId: string) {
  return sectionId.replace('section-', '').replace('child-', '')
}

function getSectionElements() {
  return Array.from(document.querySelectorAll<HTMLElement>('.portal-section[id], .portal-subsection[id]'))
}

function updateActiveSection() {
  const sections = getSectionElements()
  if (!sections.length) {
    activeSectionId.value = ''
    return
  }

  const container = mainScrollRef.value
  if (!container) {
    activeSectionId.value = normalizeSectionId(sections[0].id)
    return
  }

  const topThreshold = 4
  const bottomThreshold = 4
  const activationOffset = getAnchorOffset() + 8
  const { scrollTop, clientHeight, scrollHeight } = container

  if (scrollTop <= topThreshold) {
    activeSectionId.value = normalizeSectionId(sections[0].id)
    return
  }

  if (scrollTop + clientHeight >= scrollHeight - bottomThreshold) {
    activeSectionId.value = normalizeSectionId(sections[sections.length - 1].id)
    return
  }

  const containerTop = container.getBoundingClientRect().top
  const activationLine = scrollTop + activationOffset
  let currentSection = sections[0]

  for (const section of sections) {
    const sectionTop = section.getBoundingClientRect().top - containerTop + scrollTop
    if (sectionTop <= activationLine) {
      currentSection = section
    } else {
      break
    }
  }

  activeSectionId.value = normalizeSectionId(currentSection.id)
}

function scheduleActiveSectionUpdate() {
  if (activeSectionFrame) {
    return
  }

  activeSectionFrame = window.requestAnimationFrame(() => {
    activeSectionFrame = 0
    updateActiveSection()
  })
}

function handleMainScroll() {
  closeContextMenu()
  closeCategoryContextMenu()
  scheduleActiveSectionUpdate()
}

function setupSectionObserver() {
  if (sectionObserver) {
    sectionObserver.disconnect()
    sectionObserver = null
  }

  updateActiveSection()
}

onMounted(async () => {
  if (!authStore.isAuthenticated && isPublicView.value) {
    isPublicView.value = false
    localStorage.removeItem(VIEW_KEY)
  }
  await Promise.all([
    ensureProfile(),
    loadPortal(),
  ])
  setupSectionObserver()
  window.addEventListener('click', closeContextMenu)
  window.addEventListener('click', closeCategoryContextMenu)
  window.addEventListener('click', handleGlobalClick)
  window.addEventListener('scroll', closeContextMenu)
  window.addEventListener('scroll', closeCategoryContextMenu)
  mainScrollRef.value?.addEventListener('scroll', handleMainScroll)
})

function handleGlobalClick(e: MouseEvent) {
  const target = e.target as HTMLElement
  if (!target.closest('.portal-search-shell')) {
    closeSearchSuggestions()
    searchEngineMenuVisible.value = false
  }
}

onBeforeUnmount(() => {
  destroySortables()
  if (sectionObserver) {
    sectionObserver.disconnect()
  }
  if (activeSectionFrame) {
    window.cancelAnimationFrame(activeSectionFrame)
  }
  window.removeEventListener('click', closeContextMenu)
  window.removeEventListener('click', closeCategoryContextMenu)
  window.removeEventListener('click', handleGlobalClick)
  window.removeEventListener('scroll', closeContextMenu)
  window.removeEventListener('scroll', closeCategoryContextMenu)
  mainScrollRef.value?.removeEventListener('scroll', handleMainScroll)
})
</script>

<template>
  <div class="portal-page" :class="'theme-' + activeTheme" v-loading="loading">
    <!-- mobile sidebar overlay -->
    <div v-if="mobileSidebarOpen" class="portal-sidebar-overlay" @click="mobileSidebarOpen = false"></div>

    <aside class="portal-sidebar" :class="{ 'is-mobile-open': mobileSidebarOpen }">
      <div class="portal-sidebar__logo" :class="{ 'is-full': portal?.site.logoMode === 'full' }">
        <template v-if="portal">
          <template v-if="portal.site.logoMode === 'full'">
            <img v-if="portal.site.logoUrl" :src="portal.site.logoUrl" :alt="portal.site.title" class="portal-sidebar__logo-img-full" />
            <div v-else class="portal-sidebar__logo-full-fallback">
              <span class="portal-sidebar__logo-letter-full">{{ portal.site.logoText || 'N' }}</span>
              <span class="portal-sidebar__logo-title-full">{{ portal.site.title || 'NestHub' }}</span>
            </div>
          </template>
          <template v-else>
            <img v-if="portal.site.logoUrl" :src="portal.site.logoUrl" :alt="portal.site.title" class="portal-sidebar__logo-img" />
            <span v-else class="portal-sidebar__logo-letter">{{ portal.site.logoText || 'N' }}</span>
            <span class="portal-sidebar__logo-title">{{ portal.site.title || 'NestHub' }}</span>
          </template>
        </template>
        <button
          class="portal-sidebar__close"
          type="button"
          title="关闭导航"
          aria-label="关闭导航"
          @click="mobileSidebarOpen = false"
        >
          <i class="fa fa-times"></i>
        </button>
      </div>

      <nav ref="sidebarNavRef" class="portal-sidebar__nav">
        <div
          v-for="category in portal?.categories || []"
          :key="category.id"
          class="portal-sidebar__group"
          :data-category-id="category.id"
        >
          <a
            class="portal-sidebar__link"
            :class="{ 'is-active': activeSectionId === category.id }"
            @click.prevent="scrollToSection(category.id); if (category.children.length) toggleMenu(category.id); mobileSidebarOpen = false"
          >
            <span v-if="portal?.canEdit" class="portal-sidebar__drag">
              <i v-if="category.icon" :class="['fa', category.icon, 'portal-sidebar__link-icon']"></i>
              <i v-else class="fa fa-grip-vertical portal-sidebar__grip"></i>
            </span>
            <template v-else>
              <i v-if="category.icon" :class="['fa', category.icon, 'portal-sidebar__link-icon']"></i>
              <i v-else class="fa fa-folder portal-sidebar__link-icon"></i>
            </template>
            <span class="portal-sidebar__link-text">{{ category.name }}</span>
            <i
              v-if="category.children.length"
              class="fa portal-sidebar__link-arrow"
              :class="expandedMenu[category.id] ? 'fa-angle-down' : 'fa-angle-right'"
            ></i>
          </a>

          <transition name="sidebar-slide">
            <div
              v-if="category.children.length && expandedMenu[category.id]"
              class="portal-sidebar__children"
              :ref="(el: any) => { sidebarChildrenRefs[category.id] = el as HTMLElement | null }"
            >
              <div
                v-for="child in category.children"
                :key="child.id"
                class="portal-sidebar__child-item"
                :data-category-id="child.id"
              >
                <a
                  @click.prevent="selectChildCategory(category, child.id); mobileSidebarOpen = false"
                  class="portal-sidebar__child-link"
                  :class="{ 'is-active': activeSectionId === child.id }"
                >
                  <span v-if="portal?.canEdit" class="portal-sidebar__child-drag">
                    <i v-if="child.icon" :class="['fa', child.icon, 'portal-sidebar__child-icon']"></i>
                    <i v-else class="fa fa-grip-vertical portal-sidebar__grip"></i>
                  </span>
                  <template v-else>
                    <i v-if="child.icon" :class="['fa', child.icon, 'portal-sidebar__child-icon']"></i>
                    <i v-else class="fa fa-folder-o portal-sidebar__child-icon"></i>
                  </template>
                  <span>{{ child.name }}</span>
                </a>
              </div>
            </div>
          </transition>
        </div>
      </nav>
    </aside>

    <main ref="mainScrollRef" class="portal-main">
      <header class="portal-searchbar">
        <button
          class="portal-hamburger"
          type="button"
          title="打开导航"
          aria-label="打开导航"
          @click="mobileSidebarOpen = true"
        >
          <i class="fa fa-bars"></i>
        </button>
        <div class="portal-searchbar__inner">
          <div class="portal-search-shell">
            <button class="portal-search-engine-btn" type="button" @click="toggleSearchEngineMenu">
              {{ currentSearchEngineLabel }}
              <i class="fa" :class="searchEngineMenuVisible ? 'fa-caret-up' : 'fa-caret-down'"></i>
            </button>
            <input
              v-model="searchKeyword"
              class="portal-search-field"
              :placeholder="portal?.site.searchPlaceholder || '请输入关键词'"
              @input="onSearchInput"
              @keydown="handleSearchKeydown"
              @focus="searchSuggestionsVisible = searchSuggestions.length > 0"
            />
            <button
              class="portal-search-submit"
              type="button"
              title="搜索"
              aria-label="搜索"
              @click="runSearch"
            >
              <i class="fa fa-search"></i>
            </button>

            <transition name="portal-engine-pop">
              <div v-if="searchEngineMenuVisible" class="portal-engine-menu" @click.stop>
                <button
                  v-for="option in searchEngines"
                  :key="option.value"
                  type="button"
                  class="portal-engine-menu__item"
                  :class="{ 'is-active': option.value === searchEngine }"
                  @click="selectSearchEngine(option.value)"
                >
                  {{ option.label }}
                </button>
              </div>
            </transition>

            <transition name="portal-suggest">
              <div
                v-if="searchSuggestionsVisible && searchSuggestions.length"
                class="portal-suggest"
              >
                <button
                  v-for="(item, idx) in searchSuggestions"
                  :key="item.id"
                  type="button"
                  class="portal-suggest__item"
                  :class="{ 'is-active': idx === searchSuggestionIndex }"
                  @click="selectSuggestion(item)"
                >
                  <img
                    v-if="item.iconUrl"
                    :src="item.iconUrl"
                    class="portal-suggest__icon"
                    @error="($event.target as HTMLImageElement).style.display = 'none'"
                  />
                  <i v-else class="fa fa-link portal-suggest__icon-fallback"></i>
                  <span class="portal-suggest__title">{{ item.title }}</span>
                  <span class="portal-suggest__category">{{ item.categoryName }}</span>
                </button>
                <div class="portal-suggest__footer">
                  按回车搜索更多，↑↓ 选择，Enter 打开
                </div>
              </div>
            </transition>
          </div>
        </div>

        <div v-if="authStore.isAuthenticated" class="portal-viewbar">
          <button
            class="portal-view-toggle"
            type="button"
            :title="isPublicView ? '切换到我的视图' : '切换到公共视图'"
            @click="setPublicView(!isPublicView)"
          >
            <span :class="{ 'is-active': !isPublicView }">我的视图</span>
            <span :class="{ 'is-active': isPublicView }">公共视图</span>
          </button>
        </div>

      </header>

      <div ref="rootSectionContainerRef" class="portal-content">
        <section
          v-for="category in portal?.categories || []"
          :id="anchorId(category.id)"
          :key="category.id"
          class="portal-section"
          :data-category-id="category.id"
        >
          <div class="portal-section__head" @contextmenu.prevent="openCategoryContextMenu(category, $event)">
            <div class="portal-section__title portal-section__drag">
              <h2>{{ category.name }}</h2>
            </div>

          </div>

          <div
            v-if="category.children.length"
            class="portal-child-tabs"
            :ref="(element) => setChildContainerRef(category.id, element)"
          >
            <button
              v-for="child in category.children"
              :key="child.id"
              type="button"
              class="portal-child-tab"
              :class="{ 'is-active': activeChildTab(category) === child.id }"
              :data-category-id="child.id"
              @click="setActiveChildTab(category, child.id)"
              @contextmenu.prevent="openCategoryContextMenu(child, $event)"
            >
              {{ child.name }}
            </button>
          </div>

          <div
            class="portal-grid"
            :ref="(element) => setLinkContainerRef(activeLinkContainerId(category), element)"
          >
            <article
              v-for="link in categoryDisplayLinks(category)"
              :key="link.id"
              class="portal-card"
              :data-link-id="link.id"
              @click="openLink(link)"
              @contextmenu.prevent="openContextMenu(link, $event)"
            >
              <button class="portal-card__main" type="button">
                <span class="portal-card__icon">
                  <i v-if="link.fontIcon" :class="['fa', link.fontIcon]"></i>
                  <img v-else :src="resolveIcon(link)" :alt="link.title" data-icon-index="0" @error="handleIconError($event, link)" />
                </span>

                <div class="portal-card__copy">
                  <strong>{{ link.title }}</strong>
                  <p>{{ extractDomain(link.url) }}</p>
                </div>
              </button>

            </article>
          </div>

          <div
            v-if="false && category.children.length"
            class="portal-subsection-list"
            :ref="(element) => setChildContainerRef(category.id, element)"
          >
            <section
              v-for="child in category.children"
              :id="childAnchorId(child.id)"
              :key="child.id"
              class="portal-subsection"
              :data-category-id="child.id"
            >
              <div class="portal-subsection__head" @contextmenu.prevent="openCategoryContextMenu(child, $event)">
                <div class="portal-subsection__title portal-subsection__drag">
                  <span>{{ child.name }}</span>
                </div>

                <div v-if="portal?.canEdit" class="portal-section__actions">
                  <el-button text size="small" title="添加链接" aria-label="添加链接" @click="openCreateLink(child.id)">
                    <el-icon><Plus /></el-icon>
                  </el-button>
                  <el-dropdown @command="handleCategoryCommand(child, $event)">
                    <el-button text size="small" title="子分类操作" aria-label="子分类操作">
                      <el-icon><Edit /></el-icon>
                    </el-button>
                    <template #dropdown>
                      <el-dropdown-menu>
                        <el-dropdown-item command="edit">编辑子分类</el-dropdown-item>
                        <el-dropdown-item command="delete">删除子分类</el-dropdown-item>
                      </el-dropdown-menu>
                    </template>
                  </el-dropdown>
                </div>
              </div>

              <div
                class="portal-grid"
                :ref="(element) => setLinkContainerRef(child.id, element)"
              >
                <article
                  v-for="link in childLinks(child)"
                  :key="link.id"
                  class="portal-card"
                  :data-link-id="link.id"
                  @click="openLink(link)"
                  @contextmenu.prevent="openContextMenu(link, $event)"
                >
                  <button class="portal-card__main" type="button">
                    <span class="portal-card__icon">
                      <i v-if="link.fontIcon" :class="['fa', link.fontIcon]"></i>
                      <img v-else :src="resolveIcon(link)" :alt="link.title" data-icon-index="0" @error="handleIconError($event, link)" />
                    </span>

                    <div class="portal-card__copy">
                      <strong>{{ link.title }}</strong>
                      <p>{{ extractDomain(link.url) }}</p>
                    </div>
                  </button>
                </article>
              </div>
            </section>
          </div>
        </section>
      </div>

      <footer v-if="portal?.site.footerText && portal?.categories?.length" class="portal-footer" v-html="portal.site.footerText">
      </footer>

      <div class="portal-right-rail" :class="{ 'is-mobile-open': mobileRailOpen }">
        <el-button
          class="portal-mobile-tools-toggle"
          circle
          :aria-label="mobileRailOpen ? '收起操作' : '展开操作'"
          @click="mobileRailOpen = !mobileRailOpen"
        >
          <i :class="['fa', mobileRailOpen ? 'fa-times' : 'fa-ellipsis-h']"></i>
        </el-button>
        <el-button
          class="portal-rail-action"
          circle
          :title="isDark ? '切换亮色模式' : '切换暗色模式'"
          :aria-label="isDark ? '切换亮色模式' : '切换暗色模式'"
          @click="toggleDarkMode"
        >
          <i :class="['fa', isDark ? 'fa-sun-o' : 'fa-moon-o']"></i>
        </el-button>
        <el-button
          v-if="authStore.isAuthenticated"
          class="portal-rail-action portal-mobile-view-switch"
          circle
          :title="isPublicView ? '切换到我的视图' : '切换到公共视图'"
          :aria-label="isPublicView ? '切换到我的视图' : '切换到公共视图'"
          @click="setPublicView(!isPublicView)"
        >
          <i :class="['fa', isPublicView ? 'fa-user-o' : 'fa-globe']"></i>
        </el-button>
        <template v-if="portal?.canEdit">
          <el-button class="portal-rail-action" circle title="新增分类" aria-label="新增分类" @click="openCreateCategory()">
            <el-icon><FolderAdd /></el-icon>
          </el-button>
          <el-button class="portal-rail-action" circle title="新增链接" aria-label="新增链接" @click="openCreateLink()">
            <el-icon><Link /></el-icon>
          </el-button>
          <el-button class="portal-rail-action" circle title="后台管理" aria-label="后台管理" @click="openAdmin">
            <el-icon><Setting /></el-icon>
          </el-button>
          <el-button class="portal-rail-action" circle title="退出登录" aria-label="退出登录" @click="logout">
            <el-icon><SwitchButton /></el-icon>
          </el-button>
        </template>
        <template v-else-if="!authStore.isAuthenticated">
        <el-button class="portal-rail-action" circle title="登录" aria-label="登录" @click="openLoginDialog">
          <el-icon><Connection /></el-icon>
        </el-button>
      </template>
      <el-button class="portal-rail-action" circle title="刷新数据" aria-label="刷新数据" @click="refreshPortal">
        <el-icon><Refresh /></el-icon>
      </el-button>
    </div>

      <el-button class="portal-back-top" circle title="返回顶部" aria-label="返回顶部" @click="scrollToTop">
        <el-icon><Top /></el-icon>
      </el-button>

      <div v-if="portal?.categories?.length && portal?.site.showBottomDock !== false" class="portal-bottom-dock">
        <template v-if="portal?.canEdit">
          <el-button circle title="新增分类" aria-label="新增分类" @click="openCreateCategory()">
            <el-icon><FolderAdd /></el-icon>
          </el-button>
          <el-button circle title="新增链接" aria-label="新增链接" @click="openCreateLink()">
            <el-icon><Link /></el-icon>
          </el-button>
          <el-button circle title="后台管理" aria-label="后台管理" @click="openAdmin">
            <el-icon><Setting /></el-icon>
          </el-button>
        </template>
        <template v-else-if="!authStore.isAuthenticated">
          <el-button circle title="登录" aria-label="登录" @click="openLoginDialog">
            <el-icon><Connection /></el-icon>
          </el-button>
        </template>
        <el-button
          circle
          :title="isDark ? '切换亮色模式' : '切换暗色模式'"
          :aria-label="isDark ? '切换亮色模式' : '切换暗色模式'"
          @click="toggleDarkMode"
        >
          <i :class="['fa', isDark ? 'fa-sun-o' : 'fa-moon-o']"></i>
        </el-button>
      </div>

    </main>

    <el-dialog v-model="searchDialogVisible" title="搜索结果" width="720px">
      <div v-loading="searchLoading">
        <div v-if="searchResults.length" class="search-list">
          <button
            v-for="result in searchResults"
            :key="result.id"
            class="search-result"
            type="button"
            @click="openLink({
              id: result.id,
              categoryId: '',
              categoryName: result.categoryName,
              title: result.title,
              url: result.url,
              standbyUrl: result.standbyUrl,
              description: result.description,
              iconUrl: result.iconUrl,
              fontIcon: '',
              tags: '',
              isPinned: false,
              sortOrder: 0,
              clickCount: 0,
              checkStatus: 0,
              lastCheckedAt: null,
              lastVisitedAt: null
            })"
          >
            <div>
              <strong>{{ result.title }}</strong>
              <p>{{ result.description || result.url }}</p>
            </div>
            <span>{{ result.categoryName }}</span>
          </button>
        </div>
        <div v-else class="empty-hint">没有匹配结果，换个关键词试试。</div>
      </div>
    </el-dialog>

    <div
      v-if="contextMenu.visible && contextMenu.link"
      class="portal-context-menu"
      :style="contextMenuStyle"
      @click.stop
    >
      <button type="button" class="portal-context-menu__item" @click="openLink(contextMenu.link); closeContextMenu()">在新标签页打开</button>
      <button type="button" class="portal-context-menu__item" @click="copyLink(contextMenu.link)">复制链接</button>
      <button type="button" class="portal-context-menu__item" @click="showQrCode(contextMenu.link)">显示二维码</button>
      <button
        v-if="contextMenu.link.standbyUrl"
        type="button"
        class="portal-context-menu__item"
        @click="openLink(contextMenu.link, true); closeContextMenu()"
      >
        打开备用链接
      </button>
      <template v-if="portal?.canEdit">
        <div class="portal-context-menu__divider" />
        <button type="button" class="portal-context-menu__item" @click="openEditLink(contextMenu.link); closeContextMenu()">编辑</button>
        <button type="button" class="portal-context-menu__item is-danger" @click="removeLink(contextMenu.link); closeContextMenu()">删除</button>
      </template>
    </div>

    <div
      v-if="categoryContextMenu.visible && categoryContextMenu.category && portal?.canEdit"
      class="portal-context-menu"
      :style="categoryContextMenuStyle"
      @click.stop
    >
      <button
        v-if="!categoryContextMenu.category.parentId"
        type="button"
        class="portal-context-menu__item"
        @click="openCreateCategory(categoryContextMenu.category.id); closeCategoryContextMenu()"
      >
        添加子分类
      </button>
      <button
        type="button"
        class="portal-context-menu__item"
        @click="openCreateLink(categoryContextMenu.category.id); closeCategoryContextMenu()"
      >
        添加链接
      </button>
      <div class="portal-context-menu__divider" />
      <button type="button" class="portal-context-menu__item" @click="openEditCategory(categoryContextMenu.category); closeCategoryContextMenu()">编辑分类</button>
      <button type="button" class="portal-context-menu__item is-danger" @click="removeCategory(categoryContextMenu.category); closeCategoryContextMenu()">删除</button>
    </div>

    <el-dialog v-model="qrDialogVisible" title="链接二维码" width="360px">
      <div class="portal-qr-dialog">
        <img v-if="currentQrLink" :src="qrCodeUrl(currentQrLink)" :alt="currentQrLink.title" />
        <p>{{ currentQrLink?.title }}</p>
      </div>
    </el-dialog>

    <PortalCategoryDialog
      v-model="categoryDialogVisible"
      :category="editingCategory"
      :parent-options="categoryParents"
      :default-parent-id="defaultParentId"
      :submitting="saving"
      @submit="submitCategory"
    />

    <PortalLinkDialog
      v-model="linkDialogVisible"
      :link="editingLink"
      :category-options="categoryOptions"
      :default-category-id="defaultCategoryId"
      :submitting="saving"
      @submit="submitLink"
    />

    <SiteSettingsDialog
      v-if="portal"
      v-model="siteDialogVisible"
      :site="portal.site"
      :submitting="saving"
      @submit="submitSiteSettings"
    />

    <AppInfoDialog v-model="appInfoDialogVisible" />

    <LoginDialog
      v-model="loginDialogVisible"
      @success="handleLoginSuccess"
    />
  </div>
</template>

<style scoped>
/* ── page layout ── */
.portal-page {
  min-height: 100vh;
  background: #f4f5f7;
  display: grid;
  grid-template-columns: 200px minmax(0, 1fr);
}

/* ── sidebar ── */
.portal-sidebar {
  min-height: 100vh;
  max-height: 100vh;
  background: #fff;
  position: sticky;
  top: 0;
  align-self: start;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  border-right: 1px solid #e8eaed;
}

.portal-sidebar::-webkit-scrollbar {
  width: 4px;
}
.portal-sidebar::-webkit-scrollbar-thumb {
  background: rgba(0,0,0,0.08);
  border-radius: 2px;
}

.portal-sidebar__logo {
  padding: 8px 20px;
  min-height: 72px;
  border-bottom: 1px solid #e8eaed;
  display: flex;
  align-items: center;
  gap: 12px;
}

.portal-sidebar__logo-letter {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  font-size: 18px;
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  letter-spacing: -0.02em;
}

.portal-sidebar__logo-title {
  font-size: 16px;
  font-weight: 700;
  color: #1a1a2e;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.3;
}

.portal-sidebar__logo-img {
  width: 48px;
  height: 48px;
  object-fit: contain;
  border-radius: 8px;
  flex-shrink: 0;
  animation: logo-fadein 0.3s ease;
}

.portal-sidebar__logo.is-full {
  flex-direction: column;
  align-items: center;
  padding: 8px 20px;
  min-height: 72px;
}

.portal-sidebar__logo-img-full {
  height: 48px;
  width: auto;
  max-width: 100%;
  object-fit: contain;
  border-radius: 8px;
  animation: logo-fadein 0.3s ease;
}

@keyframes logo-fadein {
  from { opacity: 0; transform: scale(0.92); }
  to { opacity: 1; transform: scale(1); }
}

.portal-sidebar__logo-full-fallback {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.portal-sidebar__logo-letter-full {
  width: 52px;
  height: 52px;
  border-radius: 10px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  font-size: 22px;
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.portal-sidebar__logo-title-full {
  font-size: 14px;
  font-weight: 600;
  color: #1a1a2e;
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.portal-sidebar__nav {
  padding: 12px 0;
  flex: 1;
}

.portal-sidebar__group {
  margin-bottom: 2px;
}

.portal-sidebar__group--ghost {
  opacity: 0.3;
}

.portal-sidebar__child-item {
}

.portal-sidebar__child-item--ghost {
  opacity: 0.3;
}

.portal-sidebar__link {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 11px 20px;
  color: #444;
  font-size: 14px;
  transition: all 0.15s;
  position: relative;
  cursor: pointer;
}

.portal-sidebar__link:hover {
  color: #333;
  background: rgba(255, 255, 255, 0.5);
  border-radius: 8px;
}

.portal-sidebar__link.is-active {
  color: #5b6abf;
  background: rgba(255, 255, 255, 0.7);
  border-radius: 8px;
  font-weight: 500;
}
.portal-sidebar__link.is-active::before {
  content: '';
  position: absolute;
  left: 4px;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 16px;
  border-radius: 3px;
  background: linear-gradient(180deg, #667eea, #764ba2);
}

.portal-sidebar__link-icon {
  width: 22px;
  text-align: center;
  font-size: 15px;
  flex-shrink: 0;
  opacity: 0.65;
}

.portal-sidebar__link.is-active .portal-sidebar__link-icon {
  opacity: 1;
  color: #5b6abf;
}

.portal-sidebar__link-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.portal-sidebar__link-arrow {
  font-size: 12px;
  color: #bbb;
  flex-shrink: 0;
  transition: transform 0.2s;
}

.portal-sidebar__children {
  padding: 2px 0 4px;
}

.portal-sidebar__child-link {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 20px 8px 34px;
  color: #444;
  font-size: 14px;
  transition: all 0.15s;
  cursor: pointer;
}

.portal-sidebar__child-link:hover {
  color: #333;
  background: rgba(255, 255, 255, 0.5);
  border-radius: 8px;
}

.portal-sidebar__child-link.is-active {
  color: #5b6abf;
  background: rgba(255, 255, 255, 0.6);
  border-radius: 8px;
}

.portal-sidebar__child-icon {
  width: 16px;
  text-align: center;
  font-size: 13px;
  flex-shrink: 0;
  opacity: 0.65;
}

.portal-sidebar__drag,
.portal-sidebar__child-drag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 4px;
  cursor: grab;
  flex-shrink: 0;
  color: #999;
  font-size: 14px;
  transition: background 0.15s, color 0.15s;
}

.portal-sidebar__drag:hover,
.portal-sidebar__child-drag:hover {
  background: rgba(0, 0, 0, 0.06);
  color: #555;
}

.portal-sidebar__grip {
  font-size: 11px;
  opacity: 0.4;
}

.portal-sidebar__drag:hover .portal-sidebar__grip,
.portal-sidebar__child-drag:hover .portal-sidebar__grip {
  opacity: 0.7;
}

.sidebar-slide-enter-active,
.sidebar-slide-leave-active {
  transition: max-height 0.25s ease, opacity 0.2s ease;
  overflow: hidden;
}

.sidebar-slide-enter-from,
.sidebar-slide-leave-to {
  max-height: 0;
  opacity: 0;
}

.sidebar-slide-enter-to,
.sidebar-slide-leave-from {
  max-height: 600px;
  opacity: 1;
}

/* ── main area ── */
.portal-main {
  min-width: 0;
  background: linear-gradient(135deg, #f2f4ff 0%, #f4f0f8 30%, #f8f2f6 60%, #f2f5fa 100%);
  height: 100vh;
  overflow-y: auto;
  overflow-x: hidden;
  position: relative;
  scrollbar-width: thin;
}

/* ── search bar ── */
.portal-searchbar {
  position: sticky;
  top: 0;
  z-index: 10;
  min-height: 72px;
  background: #fff;
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
  border-bottom: 1px solid #e8eaed;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 14px 28px;
  gap: 12px;
}

.portal-searchbar__inner {
  width: min(680px, 100%);
}

.portal-searchbar__ops {
  position: absolute;
  right: 20px;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  align-items: center;
  gap: 8px;
}

.portal-view-toggle {
  --view-toggle-accent: #667eea;
  --view-toggle-accent-strong: #764ba2;
  --view-toggle-shadow: rgba(102, 126, 234, 0.2);
  border: 1px solid #e0e3e8;
  background: #fff;
  color: var(--view-toggle-accent);
  height: 34px;
  padding: 0 14px;
  border-radius: 999px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.01em;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.08);
  transition: transform 0.2s, box-shadow 0.2s, background 0.2s;
  white-space: nowrap;
}

.portal-view-toggle:hover {
  transform: translateY(-1px);
  background: rgba(255, 255, 255, 0.7);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.12);
}

.portal-view-toggle i:first-child {
  font-size: 12px;
}

.portal-view-toggle__switch {
  font-size: 10px;
  opacity: 0.7;
  margin-left: 2px;
}

.portal-search-shell {
  position: relative;
  display: flex;
  align-items: center;
  min-width: 0;
  height: 44px;
  border: 2px solid #4285f4;
  border-radius: 24px;
  background: #fff;
  transition: box-shadow 0.2s;
}

.portal-search-shell:focus-within {
  box-shadow: 0 2px 12px rgba(66, 133, 244, 0.18);
}

.portal-search-field {
  flex: 1;
  min-width: 0;
  height: 100%;
  border: 0;
  outline: none;
  background: transparent;
  padding: 0 16px;
  color: #222;
  font-size: 14px;
}

.portal-search-field::placeholder {
  color: #888;
}

.portal-search-engine-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 0 14px 0 18px;
  border: 0;
  background: transparent;
  color: #4285f4;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
  border-radius: 24px 0 0 24px;
}

.portal-search-engine-btn:hover {
  background: rgba(66, 133, 244, 0.06);
}

.portal-search-engine-btn i {
  font-size: 12px;
}

.portal-search-submit {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border: 0;
  background: transparent;
  color: #4285f4;
  cursor: pointer;
  font-size: 16px;
  transition: color 0.15s;
  border-radius: 0 24px 24px 0;
  flex-shrink: 0;
}

.portal-search-submit:hover {
  color: #3367d6;
}

.portal-engine-menu {
  position: absolute;
  left: 4px;
  top: calc(100% + 6px);
  width: 120px;
  padding: 4px 0;
  border: 1px solid #e0e0e0;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 8px 24px rgba(0,0,0,0.12);
  z-index: 30;
}

.portal-engine-menu__item {
  width: 100%;
  border: 0;
  background: transparent;
  padding: 8px 14px;
  text-align: left;
  color: #666;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.1s;
}

.portal-engine-menu__item:hover {
  color: #ff4d55;
  background: #fff1f2;
}

.portal-engine-menu__item.is-active {
  color: #ff4d55;
  font-weight: 600;
}

.portal-engine-pop-enter-active,
.portal-engine-pop-leave-active {
  transition: all 0.15s ease;
}

.portal-engine-pop-enter-from,
.portal-engine-pop-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

/* ── search suggestions ── */
.portal-suggest {
  position: absolute;
  left: 0;
  right: 0;
  top: calc(100% + 6px);
  background: #fff;
  border: 1px solid #e0e0e0;
  border-radius: 12px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  z-index: 30;
  overflow: hidden;
}

.portal-suggest__item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  border: 0;
  background: transparent;
  padding: 10px 16px;
  cursor: pointer;
  transition: background 0.1s;
  text-align: left;
}

.portal-suggest__item:hover,
.portal-suggest__item.is-active {
  background: #f0f4ff;
}

.portal-suggest__icon {
  width: 20px;
  height: 20px;
  border-radius: 4px;
  object-fit: contain;
  flex-shrink: 0;
}

.portal-suggest__icon-fallback {
  width: 20px;
  text-align: center;
  color: #777;
  font-size: 14px;
  flex-shrink: 0;
}

.portal-suggest__title {
  flex: 1;
  min-width: 0;
  font-size: 14px;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.portal-suggest__category {
  font-size: 12px;
  color: #777;
  flex-shrink: 0;
}

.portal-suggest__footer {
  padding: 8px 16px;
  font-size: 12px;
  color: #888;
  border-top: 1px solid #f0f0f0;
  text-align: center;
}

.portal-suggest-enter-active,
.portal-suggest-leave-active {
  transition: all 0.15s ease;
}

.portal-suggest-enter-from,
.portal-suggest-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

/* ── content sections ── */
.portal-content {
  max-width: 1320px;
  width: 100%;
  margin: 0 auto;
  padding: 28px 28px 100px;
  display: grid;
  gap: 24px;
}

.portal-content::-webkit-scrollbar {
  width: 6px;
}

.portal-content::-webkit-scrollbar-thumb {
  background: rgba(0,0,0,0.12);
  border-radius: 3px;
}

.portal-main::-webkit-scrollbar {
  width: 6px;
}

.portal-main::-webkit-scrollbar-thumb {
  background: rgba(0,0,0,0.12);
  border-radius: 3px;
}

.portal-section {
  background: #fff;
  border-radius: 10px;
  padding: 22px 24px 24px;
  border: 1px solid #e8eaed;
  scroll-margin-top: 120px;
}

.portal-section__head,
.portal-subsection__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 18px;
}

.portal-section__title,
.portal-subsection__title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 28px;
  min-width: 0;
}

.portal-section__drag,
.portal-subsection__drag {
  cursor: grab;
  flex: 1;
  min-width: 0;
}

.portal-section__title h2,
.portal-subsection__title span {
  margin: 0;
  font-size: 17px;
  font-weight: 700;
  color: #1a1a2e;
}

.portal-subsection__title {
  padding-left: 12px;
  border-left: 3px solid #667eea;
}

.portal-section__lock {
  color: #52c41a;
  font-size: 14px;
  flex: 0 0 auto;
}

.portal-section__actions {
  display: flex;
  align-items: center;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s;
}

.portal-section:hover .portal-section__actions,
.portal-subsection:hover .portal-section__actions {
  opacity: 1;
}

.portal-section__actions :deep(.el-button) {
  font-size: 16px !important;
  padding: 6px !important;
}

.portal-child-tabs {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0 0 24px;
  padding: 2px 0 4px;
  overflow-x: auto;
  scrollbar-width: none;
}

.portal-child-tabs::-webkit-scrollbar {
  display: none;
}

.portal-child-tab {
  border: 1px solid transparent;
  background: transparent;
  color: #7a86a0;
  border-radius: 10px;
  padding: 9px 16px;
  min-height: 42px;
  font-size: 15px;
  font-weight: 500;
  line-height: 20px;
  white-space: nowrap;
  cursor: pointer;
  box-shadow: inset 0 0 0 1px transparent;
  transition: background 0.2s ease, border-color 0.2s ease, color 0.2s ease, box-shadow 0.2s ease;
}

.portal-child-tab:hover {
  color: #2f66b7;
  background: #f5f8ff;
  border-color: #e7eefb;
}

.portal-child-tab.is-active {
  color: #1657b8;
  background: linear-gradient(180deg, #edf5ff 0%, #e6f0ff 100%);
  border-color: #c5dbfb;
  box-shadow: inset 0 0 0 1px rgba(255,255,255,0.72), 0 6px 14px rgba(22, 87, 184, 0.08);
}

/* ── link grid ── */
.portal-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 12px;
}

.portal-card {
  background: #fff;
  border: 1px solid #dcdfe3;
  border-radius: 8px;
  padding: 10px 14px;
  display: flex;
  align-items: center;
  gap: 12px;
  position: relative;
  transition: all 0.2s ease;
  cursor: pointer;
}

.portal-card:hover {
  background: #fff;
  border-color: #c5cae9;
  box-shadow: 0 2px 12px rgba(102, 126, 234, 0.08);
  transform: translateY(-1px);
}

.portal-card--ghost {
  opacity: 0.3;
}

.portal-card--chosen {
  box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.portal-card__lock {
  width: 18px;
  height: 18px;
  border-radius: 4px;
  background: #52c41a;
  color: #fff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 9px;
  flex-shrink: 0;
}

.portal-card__main {
  border: 0;
  background: transparent;
  padding: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  text-align: left;
  color: inherit;
  flex: 1;
  min-width: 0;
}

.portal-card__icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  background: #f0f2f8;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  overflow: hidden;
  color: #667eea;
  font-size: 16px;
  cursor: grab;
  touch-action: none;
}

.portal-card__icon img {
  width: 16px;
  height: 16px;
  object-fit: contain;
}

.portal-card__fallback {
  font-size: 16px;
  font-weight: 700;
  color: #667eea;
  user-select: none;
}

.portal-card__copy {
  min-width: 0;
  display: grid;
  gap: 2px;
  flex: 1;
}

.portal-card__copy strong {
  display: block;
  font-size: 13.5px;
  font-weight: 600;
  color: #000;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.portal-card__copy p {
  margin: 0;
  color: #5f6b76;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── subsections ── */
.portal-subsection-list {
  display: grid;
  gap: 18px;
  padding-top: 10px;
}

.portal-subsection {
  display: grid;
  gap: 12px;
  scroll-margin-top: 120px;
}

/* ── footer ── */
.portal-footer {
  text-align: center;
  padding: 4px 24px;
  color: #888;
  font-size: 12px;
  position: sticky;
  bottom: 0;
  background: #f4f5f7;
  border-top: 1px solid;
  border-color: #e8eaed;
  line-height: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  z-index: 10;
}

/* ── right rail ── */
.portal-right-rail {
  position: fixed;
  right: 18px;
  bottom: 90px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  z-index: 20;
}

.portal-right-rail :deep(.el-button) {
  width: 38px !important;
  height: 38px !important;
  min-width: 38px;
  min-height: 38px;
  padding: 0 !important;
  margin: 0;
  border-radius: 50% !important;
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  background: #fff;
  border: none;
  color: #555;
  transition: all 0.2s ease;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.portal-right-rail :deep(.el-button:hover) {
  color: #4f6ef7;
  background: #fff;
  box-shadow: 0 4px 14px rgba(79, 110, 247, 0.18);
  transform: scale(1.08);
}

.portal-right-rail :deep(.el-icon) {
  margin: 0;
  font-size: 15px;
}

.portal-back-top {
  position: fixed;
  right: 18px;
  bottom: 44px;
  z-index: 20;
  width: 38px !important;
  height: 38px !important;
  min-width: 38px;
  min-height: 38px;
  padding: 0 !important;
  margin: 0;
  border: 0;
  border-radius: 50% !important;
  background: #fff;
  color: #555;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  transition: all 0.2s ease;
}

.portal-back-top:hover {
  color: #4f6ef7;
  background: #fff;
  box-shadow: 0 4px 14px rgba(79, 110, 247, 0.18);
  transform: scale(1.08);
}

.portal-back-top :deep(.el-icon) {
  margin: 0;
  font-size: 15px;
}

.portal-right-rail :deep(.portal-mobile-view-switch) {
  display: none !important;
}

.portal-right-rail :deep(.portal-mobile-tools-toggle) {
  display: none !important;
}

/* ── bottom dock ── */
.portal-bottom-dock {
  position: fixed;
  left: calc(200px + (100vw - 200px) / 2);
  bottom: 28px;
  transform: translateX(-50%);
  background: #fff;
  border: none;
  border-radius: 24px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.1);
  padding: 6px 10px;
  display: flex;
  gap: 6px;
  z-index: 20;
}

.portal-bottom-dock :deep(.el-button) {
  width: 38px !important;
  height: 38px !important;
  border-radius: 50% !important;
  border: none;
  background: transparent;
  color: #555;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.portal-bottom-dock :deep(.el-button.is-circle) {
  width: 38px !important;
  height: 38px !important;
}

.portal-bottom-dock :deep(.el-icon) {
  font-size: 15px;
}

.portal-bottom-dock :deep(.el-button:hover) {
  color: #4f6ef7;
  background: rgba(79, 110, 247, 0.08);
  transform: scale(1.08);
}

/* ── context menu ── */
.portal-context-menu {
  position: fixed;
  min-width: 160px;
  padding: 4px 0;
  border-radius: 8px;
  background: #fff;
  border: 1px solid #e0e0e0;
  box-shadow: 0 6px 24px rgba(0,0,0,0.1);
  z-index: 50;
}

.portal-context-menu__item {
  width: 100%;
  border: 0;
  background: transparent;
  padding: 8px 16px;
  text-align: left;
  color: #333;
  font-size: 13px;
  cursor: pointer;
  transition: background 0.1s;
}

.portal-context-menu__item:hover {
  background: #f5f3ff;
  color: #5a4fcf;
}

.portal-context-menu__item.is-danger {
  color: #ff4d4f;
}

.portal-context-menu__item.is-danger:hover {
  background: #fff1f0;
}

.portal-context-menu__divider {
  height: 1px;
  margin: 4px 6px;
  background: #f0f0f0;
}

/* ── qr dialog ── */
.portal-qr-dialog {
  display: grid;
  justify-items: center;
  gap: 12px;
  padding: 4px 0 8px;
}

.portal-qr-dialog img {
  width: 200px;
  height: 200px;
  border-radius: 8px;
  border: 1px solid #e8eaed;
}

.portal-qr-dialog p {
  margin: 0;
  color: #555;
  font-size: 14px;
}

/* ── search results ── */
.search-list {
  display: grid;
  gap: 6px;
}

.search-result {
  border: 1px solid #e8eaed;
  background: #fff;
  border-radius: 8px;
  padding: 12px 14px;
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 14px;
  cursor: pointer;
  text-align: left;
  transition: all 0.15s;
}

.search-result:hover {
  border-color: #c5cae9;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.06);
}

.search-result strong {
  display: block;
  color: #333;
}

.search-result p {
  margin: 4px 0 0;
  color: #777;
  font-size: 13px;
}

.search-result span {
  color: #888;
  font-size: 12px;
  white-space: nowrap;
}

/* ── hamburger (hidden on desktop) ── */
.portal-hamburger {
  display: none;
  border: 0;
  background: transparent;
  font-size: 18px;
  color: #555;
  cursor: pointer;
  padding: 4px 8px;
  flex-shrink: 0;
}

.portal-sidebar__close {
  display: none;
  border: 0;
  background: transparent;
  color: #999;
  font-size: 16px;
  cursor: pointer;
  padding: 4px;
  margin-left: auto;
}

.portal-sidebar-overlay {
  display: none;
}

/* discovery-style default theme */
.theme-default2 {
  --portal-default2-content-top: 140px;
  --portal-default2-side-gutter: clamp(28px, 10vw, 230px);
  --portal-default2-nav-width: 192px;
  --portal-default2-content-gap: 14px;
  --portal-default2-content-width: min(1480px, calc(100vw - var(--portal-default2-side-gutter) * 2 - var(--portal-default2-nav-width) - var(--portal-default2-content-gap)));
  grid-template-columns: var(--portal-default2-side-gutter) var(--portal-default2-nav-width) var(--portal-default2-content-gap) minmax(0, 1fr);
  background: #eef0f4;
}

.theme-default2 .portal-sidebar {
  min-height: calc(100vh - var(--portal-default2-content-top));
  max-height: calc(100vh - var(--portal-default2-content-top));
  grid-column: 2;
  width: var(--portal-default2-nav-width);
  margin: var(--portal-default2-content-top) 0 0;
  border: 0;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.72);
  box-shadow: 0 10px 28px rgba(30, 41, 59, 0.04);
}

.theme-default2 .portal-sidebar__logo {
  position: fixed;
  top: 22px;
  left: var(--portal-default2-side-gutter);
  z-index: 25;
  width: var(--portal-default2-nav-width);
  min-height: 56px;
  padding: 8px 12px;
  border-bottom: 0;
  border-radius: 8px;
  background: transparent;
}

.theme-default2 .portal-sidebar__logo-letter,
.theme-default2 .portal-sidebar__logo-img {
  width: 34px;
  height: 34px;
  border-radius: 8px;
}

.theme-default2 .portal-sidebar__logo-letter,
.theme-default2 .portal-sidebar__logo-letter-full {
  background: linear-gradient(135deg, #ff6269, #ef3f4b);
  color: #fff;
  font-size: 0;
  position: relative;
}

.theme-default2 .portal-sidebar__logo-letter::before,
.theme-default2 .portal-sidebar__logo-letter-full::before {
  content: '\f14e';
  font-family: FontAwesome;
  font-size: 18px;
  line-height: 1;
}

.theme-default2 .portal-sidebar__logo-title {
  color: #4f5661;
  font-size: 16px;
  font-weight: 700;
}

.theme-default2 .portal-sidebar__nav {
  padding: 14px 10px 18px;
}

.theme-default2 .portal-sidebar__group {
  margin-bottom: 4px;
}

.theme-default2 .portal-sidebar__link,
.theme-default2 .portal-sidebar__child-link {
  min-height: 42px;
  padding: 9px 14px;
  border-radius: 8px;
  color: #646b76;
  font-size: 15px;
  font-weight: 600;
}

.theme-default2 .portal-sidebar__child-link {
  min-height: 34px;
  padding-left: 38px;
  font-size: 13px;
  font-weight: 500;
}

.theme-default2 .portal-sidebar__link:hover,
.theme-default2 .portal-sidebar__child-link:hover {
  color: #20242a;
  background: #fff;
}

.theme-default2 .portal-sidebar__link.is-active,
.theme-default2 .portal-sidebar__child-link.is-active {
  color: #ff4d55;
  background: #fff1f2;
}

.theme-default2 .portal-sidebar__link.is-active .portal-sidebar__link-icon,
.theme-default2 .portal-sidebar__child-link.is-active .portal-sidebar__child-icon {
  color: #ff4d55;
}

.theme-default2 .portal-sidebar__link.is-active::before {
  display: none;
}

.theme-default2 .portal-sidebar__link-icon,
.theme-default2 .portal-sidebar__child-icon {
  color: #4f5661;
  opacity: 0.9;
}

.theme-default2 .portal-main {
  grid-column: 4;
  background: #eef0f4;
}

.theme-default2 .portal-searchbar {
  min-height: 118px;
  padding: 24px 0 60px;
  background: #eef0f4;
  border-bottom: 0;
  flex-direction: row;
  justify-content: flex-start;
  gap: 12px;
}

.theme-default2 .portal-searchbar__inner {
  width: var(--portal-default2-content-width);
  max-width: var(--portal-default2-content-width);
}

.theme-default2 .portal-search-shell {
  width: min(1040px, 100%);
  margin: 0 auto;
  height: 56px;
  border: 0;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 18px 38px rgba(30, 41, 59, 0.08);
}

.theme-default2 .portal-search-shell:focus-within {
  box-shadow: 0 20px 42px rgba(239, 68, 68, 0.14);
}

.theme-default2 .portal-search-engine-btn,
.theme-default2 .portal-search-submit {
  color: #ff4d55;
}

.theme-default2 .portal-search-engine-btn {
  min-width: 140px;
  justify-content: center;
  padding: 0 24px;
  border-right: 1px solid #eef0f4;
  border-radius: 12px 0 0 12px;
  font-size: 17px;
  font-weight: 800;
}

.theme-default2 .portal-search-field {
  color: #333941;
  padding: 0 22px;
  font-size: 16px;
}

.theme-default2 .portal-search-submit {
  width: 58px;
  height: 56px;
  border-radius: 0 12px 12px 0;
  font-size: 22px;
}

.theme-default2 .portal-view-toggle {
  height: 34px;
  padding: 0 4px;
  gap: 2px;
  border: 0;
  border-radius: 12px;
  background: #fff;
  color: #9aa0aa;
  box-shadow: 0 12px 28px rgba(30, 41, 59, 0.07);
}

.theme-default2 .portal-viewbar {
  display: flex;
  justify-content: flex-end;
  position: absolute;
  left: 0;
  bottom: 6px;
  z-index: 12;
  width: var(--portal-default2-content-width);
  margin: 0;
  padding: 0;
  background: transparent;
}

.theme-default2 .portal-view-toggle span {
  display: inline-flex;
  align-items: center;
  height: 28px;
  padding: 0 11px;
  border-radius: 7px;
}

.theme-default2 .portal-view-toggle span.is-active {
  background: #ff4d55;
  color: #fff;
}

.theme-default2 .portal-content {
  width: var(--portal-default2-content-width);
  max-width: var(--portal-default2-content-width);
  margin: 0;
  padding: 0 0 90px;
  gap: 18px;
}

.theme-default2 .portal-section {
  padding: 14px 16px 16px;
  border: 0;
  border-radius: 8px;
  background: #fff;
  box-shadow: none;
}

.theme-default2 .portal-section__head {
  min-height: 24px;
  margin: -2px -2px 14px;
  padding: 4px 2px 12px;
  border-bottom: 1px solid #eef0f4;
}

.theme-default2 .portal-subsection__head {
  min-height: 22px;
  margin: 2px 0 10px;
  padding: 0;
  border-bottom: 0;
}

.theme-default2 .portal-section__title h2 {
  color: #555b65;
  font-size: 16px;
  font-weight: 800;
}

.theme-default2 .portal-subsection__title {
  padding-left: 10px;
  border-left: 3px solid #ff4d55;
}

.theme-default2 .portal-subsection__title span {
  color: #7a8392;
  font-size: 14px;
  font-weight: 700;
}

.theme-default2 .portal-section__title h2::before {
  content: '\f00a';
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  margin-right: 8px;
  border-radius: 7px;
  background: #fff1f2;
  color: #ff4d55;
  font-family: FontAwesome;
  font-size: 12px;
}

.theme-default2 .portal-subsection__title span::before {
  content: '';
  display: inline-block;
  width: 7px;
  height: 7px;
  margin-right: 8px;
  border-radius: 50%;
  background: #ff4d55;
  box-shadow: 0 0 0 4px rgba(255, 77, 85, 0.12);
  color: #ff4d55;
}

.theme-default2 .portal-section__actions {
  gap: 6px;
  opacity: 1;
}

.theme-default2 .portal-section__actions :deep(.el-button) {
  width: 30px;
  height: 30px;
  padding: 0 !important;
  border-radius: 8px !important;
  background: #f4f6f8;
  color: #7a8392;
  transition: background 0.18s ease, color 0.18s ease, transform 0.18s ease, box-shadow 0.18s ease;
}

.theme-default2 .portal-section__actions :deep(.el-button:hover) {
  background: #fff1f2;
  color: #ff4d55;
  box-shadow: 0 8px 18px rgba(255, 77, 85, 0.12);
  transform: translateY(-1px);
}

.theme-default2 .portal-section__actions :deep(.el-icon) {
  margin: 0;
  font-size: 15px;
}

.theme-default2 .portal-grid {
  grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
  gap: 18px 16px;
}

.theme-default2 .portal-card {
  min-height: 68px;
  padding: 10px 12px;
  border: 0;
  border-radius: 8px;
  background: #f7f7f8;
  box-shadow: none;
}

.theme-default2 .portal-card:hover {
  background: #fff;
  box-shadow: 0 12px 26px rgba(30, 41, 59, 0.08);
  transform: translateY(-2px);
}

.theme-default2 .portal-card__icon {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  background: #f3f4f6;
  transition: transform 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
}

.theme-default2 .portal-card:hover .portal-card__icon {
  background: #fff0f1;
  box-shadow: 0 8px 18px rgba(255, 77, 85, 0.18);
  transform: scale(1.08);
}

.theme-default2 .portal-card__icon img {
  width: 26px;
  height: 26px;
  transition: transform 0.2s ease;
}

.theme-default2 .portal-card:hover .portal-card__icon img {
  transform: scale(1.05);
}

.theme-default2 .portal-card__copy strong {
  color: #4b515a;
  font-size: 14px;
  font-weight: 700;
}

.theme-default2 .portal-card:hover .portal-card__copy strong {
  color: #ff4d55;
}

.theme-default2 .portal-card__copy p {
  color: #9da3ad;
  font-size: 12px;
}

.theme-default2 .portal-subsection-list {
  gap: 22px;
  padding-top: 20px;
}

.theme-default2 .portal-subsection {
  padding: 4px 0 0;
  border-radius: 0;
  border: 0;
  background: transparent;
}

.theme-default2 .portal-footer {
  background: #eef0f4;
  border-color: transparent;
}

.theme-default2 .portal-right-rail {
  right: 24px;
  bottom: 104px;
  gap: 10px;
}

.theme-default2 .portal-back-top {
  right: 24px;
  bottom: 52px;
  width: 44px !important;
  height: 44px !important;
  min-width: 44px;
  min-height: 44px;
  border: 1px solid rgba(226, 230, 236, 0.92);
  background: rgba(255, 255, 255, 0.9);
  color: #7a8392;
  box-shadow: 0 10px 24px rgba(30, 41, 59, 0.09);
  backdrop-filter: blur(10px);
}

.theme-default2 .portal-back-top:hover {
  border-color: rgba(255, 77, 85, 0.28);
  background: #fff1f2;
  color: #ff4d55;
  box-shadow: 0 14px 28px rgba(255, 77, 85, 0.16);
  transform: translateY(-2px);
}

.theme-default2 .portal-right-rail :deep(.el-button) {
  width: 44px !important;
  height: 44px !important;
  min-width: 44px;
  min-height: 44px;
  border: 1px solid rgba(226, 230, 236, 0.92);
  background: rgba(255, 255, 255, 0.9);
  color: #7a8392;
  box-shadow: 0 10px 24px rgba(30, 41, 59, 0.09);
  backdrop-filter: blur(10px);
}

.theme-default2 .portal-right-rail :deep(.el-button:hover) {
  border-color: rgba(255, 77, 85, 0.28);
  background: #fff1f2;
  color: #ff4d55;
  box-shadow: 0 14px 28px rgba(255, 77, 85, 0.16);
  transform: translateY(-2px);
}

.theme-default2 .portal-right-rail :deep(.el-icon),
.theme-default2 .portal-right-rail :deep(i) {
  font-size: 17px;
}

.theme-default2 .portal-bottom-dock {
  bottom: 26px;
  padding: 8px;
  gap: 8px;
  border: 1px solid rgba(226, 230, 236, 0.9);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.92);
  box-shadow: 0 16px 36px rgba(30, 41, 59, 0.12);
  backdrop-filter: blur(12px);
}

.theme-default2 .portal-bottom-dock :deep(.el-button) {
  width: 42px !important;
  height: 42px !important;
  min-width: 42px;
  min-height: 42px;
  border-radius: 12px !important;
  background: transparent;
  color: #7a8392;
}

.theme-default2 .portal-bottom-dock :deep(.el-button:hover) {
  background: #fff1f2;
  color: #ff4d55;
  transform: translateY(-1px);
}

.theme-default2 .portal-bottom-dock :deep(.el-icon),
.theme-default2 .portal-bottom-dock :deep(i) {
  font-size: 17px;
}

html.dark .theme-default2 {
  background: #10131b;
}

html.dark .theme-default2 .portal-main,
html.dark .theme-default2 .portal-searchbar,
html.dark .theme-default2 .portal-content,
html.dark .theme-default2 .portal-footer {
  background: #10131b;
}

html.dark .theme-default2 .portal-sidebar {
  background: #191d27;
  box-shadow: none;
}

html.dark .theme-default2 .portal-sidebar__logo {
  background: transparent;
}

html.dark .theme-default2 .portal-sidebar__logo-title,
html.dark .theme-default2 .portal-sidebar__logo-title-full {
  color: #d9deea;
}

html.dark .theme-default2 .portal-sidebar__link,
html.dark .theme-default2 .portal-sidebar__child-link {
  color: #c9d2e4;
}

html.dark .theme-default2 .portal-sidebar__link:hover,
html.dark .theme-default2 .portal-sidebar__child-link:hover,
html.dark .theme-default2 .portal-sidebar__link.is-active,
html.dark .theme-default2 .portal-sidebar__child-link.is-active {
  color: #8ea8ff;
  background: #242a3d;
}

html.dark .theme-default2 .portal-sidebar__link-icon,
html.dark .theme-default2 .portal-sidebar__child-icon {
  color: #717b91;
}

html.dark .theme-default2 .portal-search-shell,
html.dark .theme-default2 .portal-section {
  background: #1b202b;
}

html.dark .theme-default2 .portal-section__head {
  border-bottom-color: #2a3040;
}

html.dark .theme-default2 .portal-subsection {
  background: transparent;
}

html.dark .theme-default2 .portal-search-shell {
  box-shadow: 0 18px 38px rgba(0, 0, 0, 0.18);
}

html.dark .theme-default2 .portal-search-field {
  color: #e5e9f3;
}

html.dark .theme-default2 .portal-search-field::placeholder {
  color: #7c8496;
}

html.dark .theme-default2 .portal-search-engine-btn {
  border-right-color: #2a3040;
}

html.dark .theme-default2 .portal-section__title h2,
html.dark .theme-default2 .portal-section__title h2::before,
html.dark .theme-default2 .portal-subsection__title span::before {
  color: #e8edf8;
}

html.dark .theme-default2 .portal-section__title h2::before {
  background: rgba(142, 168, 255, 0.16);
  color: #8ea8ff;
}

html.dark .theme-default2 .portal-subsection__title {
  border-left-color: #8ea8ff;
}

html.dark .theme-default2 .portal-subsection__title span {
  color: #b8c2d6;
}

html.dark .theme-default2 .portal-subsection__title span::before {
  background: #8ea8ff;
  box-shadow: 0 0 0 4px rgba(142, 168, 255, 0.14);
}

html.dark .theme-default2 .portal-section__actions :deep(.el-button) {
  background: #242a36;
  color: #9aa5b8;
}

html.dark .theme-default2 .portal-section__actions :deep(.el-button:hover) {
  background: #2a2633;
  color: #ff7a82;
  box-shadow: 0 8px 18px rgba(0, 0, 0, 0.18);
}

html.dark .theme-default2 .portal-card {
  background: #242934;
  box-shadow: none;
}

html.dark .theme-default2 .portal-card:hover {
  background: #2b3242;
  box-shadow: 0 12px 26px rgba(0, 0, 0, 0.18);
}

html.dark .theme-default2 .portal-card__icon {
  background: #2d3346;
}

html.dark .theme-default2 .portal-card:hover .portal-card__icon {
  background: #3a2d3a;
  box-shadow: 0 8px 18px rgba(255, 122, 130, 0.16);
}

html.dark .theme-default2 .portal-card__copy strong {
  color: #e8edf8;
}

html.dark .theme-default2 .portal-card:hover .portal-card__copy strong {
  color: #ff7a82;
}

html.dark .theme-default2 .portal-card__copy p {
  color: #9aa5b8;
}

html.dark .theme-default2 .portal-right-rail :deep(.el-button) {
  border-color: #2b3241;
  background: rgba(27, 32, 43, 0.92);
  color: #a8b2c5;
  box-shadow: 0 12px 28px rgba(0, 0, 0, 0.22);
}

html.dark .theme-default2 .portal-back-top {
  border-color: #2b3241;
  background: rgba(27, 32, 43, 0.92);
  color: #a8b2c5;
  box-shadow: 0 12px 28px rgba(0, 0, 0, 0.22);
}

html.dark .theme-default2 .portal-right-rail :deep(.el-button:hover) {
  border-color: rgba(255, 122, 130, 0.32);
  background: #2a2633;
  color: #ff7a82;
  box-shadow: 0 14px 28px rgba(0, 0, 0, 0.26);
}

html.dark .theme-default2 .portal-back-top:hover {
  border-color: rgba(255, 122, 130, 0.32);
  background: #2a2633;
  color: #ff7a82;
  box-shadow: 0 14px 28px rgba(0, 0, 0, 0.26);
}

html.dark .theme-default2 .portal-bottom-dock {
  border-color: #2b3241;
  background: rgba(27, 32, 43, 0.94);
  box-shadow: 0 16px 36px rgba(0, 0, 0, 0.28);
}

html.dark .theme-default2 .portal-bottom-dock :deep(.el-button) {
  color: #a8b2c5;
}

html.dark .theme-default2 .portal-bottom-dock :deep(.el-button:hover) {
  background: #2a2633;
  color: #ff7a82;
}

/* ── responsive ── */
@media (max-width: 1160px) {
  .theme-default2 {
    --portal-default2-content-width: auto;
    grid-template-columns: 1fr;
  }

  .portal-page {
    grid-template-columns: 1fr;
  }

  .portal-sidebar {
    position: fixed;
    top: 0;
    left: 0;
    bottom: 0;
    width: 260px;
    z-index: 100;
    transform: translateX(-100%);
    transition: transform 0.25s ease;
    border-right: 1px solid #e8eaed;
  }

  .theme-default2 .portal-sidebar {
    min-height: 100vh;
    max-height: 100vh;
    width: 260px;
    margin: 0;
    border-radius: 0;
    background: #fff;
  }

  .theme-default2 .portal-sidebar__logo {
    position: static;
    width: auto;
    min-height: 0;
    padding: 18px 20px 12px;
  }

  .theme-default2 .portal-content {
    padding: 16px 18px 90px;
    max-width: none;
  }

  .portal-sidebar.is-mobile-open {
    transform: translateX(0);
  }

  .portal-sidebar__close {
    display: block;
  }

  .portal-sidebar-overlay {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.3);
    z-index: 99;
  }

  .portal-hamburger {
    display: block;
  }

  .portal-content {
    max-width: none;
  }

  .portal-grid {
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  }
}

@media (max-width: 760px) {
  .theme-default2 {
    --portal-default2-content-width: 100%;
  }

  .portal-main,
  .theme-default2 .portal-main {
    grid-column: 1;
    background: #eef3f9;
  }

  .portal-searchbar {
    padding: 12px 10px 8px;
    flex-wrap: nowrap;
    min-height: auto;
    gap: 10px;
    background: #eef3f9;
    border-bottom: 1px solid rgba(217, 226, 237, 0.82);
    box-shadow: 0 8px 18px rgba(31, 41, 55, 0.04);
  }

  .portal-hamburger {
    width: 38px;
    height: 38px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
    border-radius: 12px;
    color: #445062;
    background: rgba(255, 255, 255, 0.72);
    box-shadow: 0 8px 20px rgba(31, 41, 55, 0.06);
  }

  .portal-searchbar__inner {
    flex: 1;
    min-width: 0;
    width: auto;
    max-width: none;
  }

  .portal-search-shell {
    height: 46px;
    border: 0;
    border-radius: 14px;
    background: rgba(255, 255, 255, 0.94);
    overflow: hidden;
    box-shadow: 0 12px 28px rgba(31, 41, 55, 0.08);
  }

  .portal-search-field {
    padding: 0 8px;
    font-size: 14px;
  }

  .portal-search-engine-btn {
    padding: 0 10px 0 14px;
    font-size: 13px;
  }

  .portal-search-submit {
    width: 44px;
    height: 46px;
    border-radius: 0 14px 14px 0;
  }

  .portal-engine-menu {
    left: 4px;
    width: 100px;
  }

  .portal-searchbar__ops {
    display: none;
  }

  .portal-content {
    padding: 12px 10px 88px;
    gap: 16px;
  }

  .theme-default2 .portal-searchbar {
    min-height: auto;
    padding: 12px 10px 8px;
    flex-direction: row;
    background: #eef3f9;
    border-bottom: 1px solid rgba(217, 226, 237, 0.82);
    box-shadow: 0 8px 18px rgba(31, 41, 55, 0.04);
  }

  .theme-default2 .portal-searchbar__inner {
    flex: 1;
    min-width: 0;
    width: auto;
    max-width: none;
  }

  .theme-default2 .portal-search-shell {
    height: 46px;
    border-radius: 14px;
    overflow: hidden;
    box-shadow: 0 12px 28px rgba(31, 41, 55, 0.08);
  }

  .theme-default2 .portal-search-engine-btn {
    min-width: 76px;
    max-width: 86px;
    padding: 0 10px;
    border-radius: 14px 0 0 14px;
    font-size: 13px;
  }

  .theme-default2 .portal-search-field {
    padding: 0 8px;
    font-size: 13px;
  }

  .theme-default2 .portal-search-submit {
    width: 44px;
    height: 46px;
    border-radius: 0 14px 14px 0;
    font-size: 17px;
  }

  .theme-default2 .portal-content {
    width: 100%;
    max-width: none;
    padding: 12px 10px 88px;
    gap: 16px;
  }

  .portal-viewbar {
    display: none;
  }

  .theme-default2 .portal-viewbar {
    display: none;
  }

  html.dark .theme-default2 .portal-sidebar {
    background: #191d27;
  }

  .portal-section {
    padding: 14px 14px 16px;
    border: 0;
    border-radius: 16px;
    background: rgba(255, 255, 255, 0.92);
    box-shadow: 0 12px 30px rgba(31, 41, 55, 0.06);
  }

  .theme-default2 .portal-section {
    padding: 14px 14px 16px;
    border: 0;
    border-radius: 16px;
    background: rgba(255, 255, 255, 0.92);
    box-shadow: 0 12px 30px rgba(31, 41, 55, 0.06);
  }

  .theme-default2 .portal-section__head {
    margin: -2px 0 14px;
    padding: 0 0 13px;
  }

  .portal-grid {
    grid-template-columns: 1fr;
    gap: 10px;
  }

  .theme-default2 .portal-grid {
    grid-template-columns: 1fr;
    gap: 10px;
  }

  .portal-card {
    min-height: 74px;
    padding: 12px 14px;
    gap: 12px;
    border-radius: 12px;
    background: #f8fafc;
  }

  .theme-default2 .portal-card {
    min-height: 74px;
    padding: 12px 14px;
    gap: 12px;
    border-radius: 12px;
    background: #f8fafc;
  }

  .portal-card__icon {
    width: 40px;
    height: 40px;
    border-radius: 12px;
    font-size: 16px;
  }

  .theme-default2 .portal-card__icon {
    width: 40px;
    height: 40px;
    border-radius: 12px;
  }

  .portal-card__icon img {
    width: 24px;
    height: 24px;
  }

  .theme-default2 .portal-card__icon img {
    width: 24px;
    height: 24px;
  }

  .portal-card__copy strong {
    font-size: 15px;
    line-height: 20px;
  }

  .theme-default2 .portal-card__copy strong {
    font-size: 15px;
    line-height: 20px;
  }

  .portal-card__copy p {
    font-size: 13px;
  }

  .theme-default2 .portal-card__copy p {
    font-size: 13px;
  }

  .portal-section__head,
  .portal-subsection__head {
    align-items: flex-start;
    flex-direction: column;
    margin-bottom: 10px;
  }

  .portal-section__title h2,
  .portal-subsection__title span {
    font-size: 17px;
  }

  .portal-right-rail {
    right: max(8px, env(safe-area-inset-right));
    bottom: max(18px, calc(env(safe-area-inset-bottom) + 18px));
    top: auto;
    flex-direction: row-reverse;
    align-items: center;
    max-width: calc(100vw - 20px);
    max-height: none;
    gap: 6px;
    padding: 6px;
    border: 1px solid rgba(226, 232, 240, 0.82);
    border-radius: 999px;
    background: rgba(255, 255, 255, 0.68);
    box-shadow: 0 16px 34px rgba(31, 41, 55, 0.14);
    backdrop-filter: blur(14px);
    overflow-x: auto;
    scrollbar-width: none;
  }

  .theme-default2 .portal-right-rail {
    right: max(8px, env(safe-area-inset-right));
    bottom: max(18px, calc(env(safe-area-inset-bottom) + 18px));
    gap: 6px;
  }

  .portal-right-rail::-webkit-scrollbar {
    display: none;
  }

  .portal-right-rail :deep(.el-button) {
    width: 32px !important;
    height: 32px !important;
    min-width: 32px;
    min-height: 32px;
    border: 0;
    background: rgba(255, 255, 255, 0.84);
    color: #64748b;
    box-shadow: none;
  }

  .theme-default2 .portal-right-rail :deep(.el-button) {
    width: 32px !important;
    height: 32px !important;
    min-width: 32px;
    min-height: 32px;
    background: rgba(255, 255, 255, 0.84);
    box-shadow: none;
  }

  .portal-right-rail :deep(.el-icon),
  .portal-right-rail :deep(i),
  .theme-default2 .portal-right-rail :deep(.el-icon),
  .theme-default2 .portal-right-rail :deep(i) {
    font-size: 15px;
  }

  .portal-back-top,
  .theme-default2 .portal-back-top {
    right: max(8px, env(safe-area-inset-right));
    bottom: max(68px, calc(env(safe-area-inset-bottom) + 68px));
    width: 38px !important;
    height: 38px !important;
    min-width: 38px;
    min-height: 38px;
    border: 1px solid rgba(226, 232, 240, 0.82);
    background: rgba(255, 255, 255, 0.86);
    color: #64748b;
    box-shadow: 0 12px 26px rgba(31, 41, 55, 0.12);
    backdrop-filter: blur(14px);
  }

  .portal-back-top :deep(.el-icon),
  .theme-default2 .portal-back-top :deep(.el-icon) {
    font-size: 16px;
  }

  .portal-right-rail :deep(.portal-mobile-view-switch) {
    display: inline-flex !important;
    color: #ff4d55 !important;
  }

  .portal-right-rail :deep(.portal-mobile-tools-toggle) {
    display: inline-flex !important;
    color: #ff4d55 !important;
    background: #fff !important;
    box-shadow: 0 10px 22px rgba(255, 77, 85, 0.18) !important;
  }

  .portal-right-rail:not(.is-mobile-open) :deep(.portal-rail-action) {
    display: none !important;
  }

  .portal-right-rail:not(.is-mobile-open) {
    padding: 5px;
    background: rgba(255, 255, 255, 0.78);
  }

  .portal-bottom-dock {
    display: none;
  }

  .portal-footer,
  .theme-default2 .portal-footer {
    position: static;
    margin: 0;
    padding: 0 14px 18px;
    border: 0;
    background: transparent;
    color: #8a94a6;
  }

  html.dark .portal-searchbar,
  html.dark .theme-default2 .portal-searchbar {
    background: #191d27;
    border-bottom-color: rgba(50, 58, 74, 0.9);
    box-shadow: 0 8px 18px rgba(0, 0, 0, 0.18);
  }
}

@media (max-width: 480px) {
  .portal-grid {
    grid-template-columns: 1fr;
  }
}

/* ── portal dark mode ── */
html.dark .portal-shell {
  background: #12131a;
}

html.dark .portal-topbar {
  background: #12131a;
  border-color: #2e3040;
}

html.dark .portal-sidebar {
  background: #1a1c24;
}

html.dark .portal-sidebar__logo {
  border-color: #2e3040;
}

html.dark .portal-sidebar__logo-title {
  color: #e0e4ec;
}

html.dark .portal-sidebar__logo-title-full {
  color: #e0e4ec;
}

html.dark .portal-sidebar__link {
  color: #b0b8c8;
}

html.dark .portal-sidebar__link:hover {
  color: #d0d4de;
  background: rgba(255, 255, 255, 0.06);
  border-radius: 8px;
}

html.dark .portal-sidebar__link.is-active {
  color: #8b9cf7;
  background: rgba(102, 126, 234, 0.12);
  border-radius: 8px;
}

html.dark .portal-sidebar__link.is-active::before {
  background: linear-gradient(180deg, #8b9cf7, #a78bfa);
}

html.dark .portal-sidebar__child-link {
  color: #b0b8c8;
}

html.dark .portal-sidebar__child-link:hover {
  color: #d0d4de;
  background: rgba(255, 255, 255, 0.06);
  border-radius: 8px;
}

html.dark .portal-sidebar__child-link.is-active {
  color: #8b9cf7;
  background: rgba(102, 126, 234, 0.1);
  border-radius: 8px;
}

html.dark .portal-sidebar__drag,
html.dark .portal-sidebar__child-drag {
  color: #555;
}

html.dark .portal-sidebar__drag:hover,
html.dark .portal-sidebar__child-drag:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #aaa;
}

html.dark .portal-main {
  background: #12131a;
}

html.dark .portal-content {
  background: #12131a;
}

html.dark .portal-featured-section {
  border-color: #2e3040;
  background: #1e2028;
}

html.dark .portal-section {
  border-color: #2e3040;
  background: #1e2028;
}

html.dark .portal-subsection {
  border-color: #2e3040;
  background: transparent;
}

html.dark .portal-subsection__title {
  border-left-color: #8b9cf7;
}

html.dark .portal-section__drag:hover,
html.dark .portal-subsection__drag:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #aaa;
}

html.dark .portal-child-tab {
  color: #9ca8bf;
}

html.dark .portal-child-tab:hover {
  color: #d9e5ff;
  background: rgba(255, 255, 255, 0.055);
  border-color: rgba(139, 156, 247, 0.16);
}

html.dark .portal-child-tab.is-active {
  color: #e3ebff;
  background: linear-gradient(180deg, rgba(76, 99, 198, 0.26), rgba(70, 87, 168, 0.2));
  border-color: rgba(139, 156, 247, 0.42);
  box-shadow: inset 0 0 0 1px rgba(255,255,255,0.06), 0 8px 18px rgba(0, 0, 0, 0.16);
}

html.dark .portal-card {
  background: #252830;
  border-color: #2e3040;
}

html.dark .portal-card:hover {
  background: #2a2d38;
  border-color: #3d5a9e;
  box-shadow: 0 2px 12px rgba(90, 138, 247, 0.08);
}

html.dark .portal-card__icon {
  background: #2e3040;
  color: #8b9cf7;
}

html.dark .portal-card__copy strong {
  color: #e8ecf2;
}

html.dark .portal-card__copy p {
  color: #8a92a4;
}

html.dark .portal-right-rail :deep(.el-button) {
  background: #1e2028;
  border: none;
  color: #b0b8c8;
}

html.dark .portal-back-top {
  background: #1e2028;
  border: none;
  color: #b0b8c8;
}

html.dark .portal-right-rail :deep(.el-button:hover) {
  color: #8b9cf7;
  background: #282a38;
  box-shadow: 0 4px 14px rgba(139, 156, 247, 0.15);
}

html.dark .portal-back-top:hover {
  color: #8b9cf7;
  background: #282a38;
  box-shadow: 0 4px 14px rgba(139, 156, 247, 0.15);
}

@media (max-width: 760px) {
  html.dark .portal-right-rail {
    background: rgba(30, 32, 40, 0.88);
    box-shadow: 0 10px 28px rgba(0, 0, 0, 0.3);
  }

  html.dark .portal-back-top {
    border: 1px solid rgba(50, 58, 74, 0.9);
    background: rgba(30, 32, 40, 0.88);
    box-shadow: 0 10px 28px rgba(0, 0, 0, 0.3);
  }
}

html.dark .portal-bottom-dock {
  background: #1e2028;
  border: none;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

html.dark .portal-bottom-dock :deep(.el-button) {
  background: transparent;
  border: none;
  color: #b0b8c8;
}

html.dark .portal-bottom-dock :deep(.el-button:hover) {
  color: #8b9cf7;
  background: rgba(139, 156, 247, 0.1);
}

html.dark .portal-context-menu {
  background: #1e2028;
  border-color: #2e3040;
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.4);
}

html.dark .portal-sidebar__link-arrow {
  color: #8a92a4;
}

html.dark .portal-sidebar-overlay {
  background: rgba(0, 0, 0, 0.6);
}

html.dark .portal-searchbar {
  background: #252830;
  border-color: #3a3d4a;
}

html.dark .portal-searchbar__input {
  color: #d0d4de;
}

html.dark .portal-searchbar__input::placeholder {
  color: #7a8294;
}

html.dark .portal-view-toggle {
  background: rgba(255, 255, 255, 0.06);
  border-color: rgba(255, 255, 255, 0.08);
  color: #8b9cf7;
}

html.dark .portal-section__head h2 {
  color: #e0e4ec;
}

html.dark .portal-subsection__title span {
  color: #e0e4ec;
}

html.dark .portal-link-card {
  background: #1e2028;
  border-color: #2e3040;
}

html.dark .portal-link-card:hover {
  border-color: #3d7be0;
}

html.dark .portal-link-card__title {
  color: #d0d4de;
}

html.dark .portal-link-card__desc {
  color: #8a92a4;
}

html.dark .portal-footer {
  background: #12131a;
  color: #8a92a4;
  border-color: #2e3040;
}

html.dark .portal-nav-item {
  color: #b0b8c8;
}

html.dark .portal-nav-item:hover,
html.dark .portal-nav-item.is-active {
  color: var(--accent);
}

html.dark .portal-search-shell {
  border-color: #5a8af7;
  background: #1e2028;
}

html.dark .portal-search-shell:focus-within {
  box-shadow: 0 2px 12px rgba(90, 138, 247, 0.15);
}

html.dark .portal-search-field {
  color: #d0d4de;
}

html.dark .portal-search-field::placeholder {
  color: #7a8294;
}

html.dark .portal-search-engine-btn {
  color: #5a8af7;
}

html.dark .portal-search-engine-btn:hover {
  background: rgba(90, 138, 247, 0.08);
}

html.dark .portal-search-submit {
  color: #5a8af7;
}

html.dark .portal-engine-menu {
  background: #1e2028;
  border-color: #2e3040;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

html.dark .portal-engine-menu__item {
  color: #b0b8c8;
}

html.dark .portal-engine-menu__item:hover {
  color: #8b9cf7;
  background: rgba(102, 126, 234, 0.1);
}

html.dark .portal-engine-menu__item.is-active {
  color: #8b9cf7;
}

html.dark .portal-suggest {
  background: #1e2028;
  border-color: #2e3040;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
}

html.dark .portal-suggest__item:hover,
html.dark .portal-suggest__item.is-active {
  background: rgba(102, 126, 234, 0.1);
}

html.dark .portal-suggest__icon-fallback {
  color: #8a92a4;
}

html.dark .portal-suggest__title {
  color: #e0e4ec;
}

html.dark .portal-suggest__category {
  color: #8a92a4;
}

html.dark .portal-suggest__footer {
  color: #8a92a4;
  border-color: #2e3040;
}

html.dark .portal-featured-link {
  border-color: #2e3040;
  background: #1e2028;
}

html.dark .portal-featured-link:hover {
  border-color: #3d7be0;
}

html.dark .portal-featured-link__title {
  color: #d0d4de;
}

html.dark .portal-hamburger {
  color: #b0b8c8;
}

html.dark .portal-sidebar__close {
  color: #b0b8c8;
}

html.dark .portal-context-menu__item {
  color: #d0d4de;
}

html.dark .portal-context-menu__item:hover {
  background: rgba(102, 126, 234, 0.1);
  color: #8b9cf7;
}

html.dark .portal-context-menu__item.is-danger {
  color: #ff6b6b;
}

html.dark .portal-context-menu__item.is-danger:hover {
  background: rgba(255, 77, 79, 0.1);
}

html.dark .portal-context-menu__divider {
  background: #2e3040;
}


/* ═══════════════════════════════════════════
   THEME: ocean — 海洋蔚蓝，大圆角宽边栏，Google Material 风格
   ═══════════════════════════════════════════ */

/* --- ocean STRUCTURAL OVERRIDES --- */
.theme-ocean { grid-template-columns: 260px minmax(0, 1fr) !important; }
.theme-ocean .portal-sidebar {
  background: linear-gradient(180deg, #1e3a5f 0%, #162d4a 100%);
  border-color: #2d4f7a;
  border-right: none;
  box-shadow: 4px 0 24px rgba(30, 58, 95, 0.25);
}
.theme-ocean .portal-sidebar__logo { border-color: #2d4f7a; padding: 28px 24px 26px; }
.theme-ocean .portal-sidebar__logo-letter {
  background: linear-gradient(135deg, #3b82f6, #1d4ed8);
  border-radius: 12px; width: 40px; height: 40px; font-size: 20px;
}
.theme-ocean .portal-sidebar__logo-title { color: #f0f4f8; font-size: 17px; font-weight: 700; letter-spacing: 0.01em; }
.theme-ocean .portal-sidebar__logo-title-full { color: #f0f4f8; }
.theme-ocean .portal-sidebar__nav { padding: 16px 0; }
.theme-ocean .portal-sidebar__link {
  color: #b0c4de; padding: 12px 24px; font-size: 14px; font-weight: 500;
  margin: 2px 10px; border-radius: 10px; transition: all 0.2s ease;
}
.theme-ocean .portal-sidebar__link:hover { color: #dbe4ee; background: rgba(59, 130, 246, 0.12); border-radius: 10px; }
.theme-ocean .portal-sidebar__link.is-active { color: #fff; background: rgba(59, 130, 246, 0.22); border-radius: 10px; font-weight: 600; }
.theme-ocean .portal-sidebar__link.is-active::before { background: #60a5fa; width: 4px; border-radius: 0 4px 4px 0; left: -10px; }
.theme-ocean .portal-sidebar__link-icon { font-size: 16px; opacity: 0.7; }
.theme-ocean .portal-sidebar__link.is-active .portal-sidebar__link-icon { opacity: 1; }
.theme-ocean .portal-sidebar__link-arrow { color: #6b8faf; }
.theme-ocean .portal-sidebar__child-link { color: #3b6e9e; padding: 9px 24px 9px 56px; margin: 0 10px; border-radius: 8px; }
.theme-ocean .portal-sidebar__child-link:hover { color: #dbe4ee; background: rgba(59, 130, 246, 0.1); border-radius: 8px; }
.theme-ocean .portal-sidebar__child-link.is-active { color: #60a5fa; background: rgba(59, 130, 246, 0.12); border-radius: 8px; }

.theme-ocean .portal-main { background: #f0f4f8; }
.theme-ocean .portal-searchbar { background: #fff; border-color: #dbe4ee; min-height: 78px; padding: 16px 32px; }
.theme-ocean .portal-searchbar__inner { width: min(720px, 100%); }
.theme-ocean .portal-view-toggle { --view-toggle-accent: #3b82f6; --view-toggle-accent-strong: #2563eb; --view-toggle-shadow: rgba(59, 130, 246, 0.26); }
.theme-ocean .portal-search-shell {
  background: #fff; border: 2px solid #3b82f6; border-radius: 28px; height: 48px;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.08); transition: box-shadow 0.25s, border-color 0.25s;
}
.theme-ocean .portal-search-shell:focus-within { box-shadow: 0 4px 20px rgba(59, 130, 246, 0.18); border-color: #2563eb; }
.theme-ocean .portal-search-field { color: #1e3a5f; font-size: 15px; }
.theme-ocean .portal-search-field::placeholder { color: #94a3b8; }
.theme-ocean .portal-search-engine-btn { color: #3b82f6; }
.theme-ocean .portal-search-engine-btn:hover { background: rgba(59, 130, 246, 0.06); }
.theme-ocean .portal-search-submit { color: #3b82f6; }

.theme-ocean .portal-content { gap: 28px; padding: 32px 32px 100px; }
.theme-ocean .portal-grid { grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }

.theme-ocean .portal-section {
  background: #fff; border-color: #dbe4ee; border-radius: 16px; padding: 26px 28px 28px;
  box-shadow: 0 1px 4px rgba(30, 58, 95, 0.04);
}
.theme-ocean .portal-section__head,
.theme-ocean .portal-subsection__head { margin-bottom: 22px; }
.theme-ocean .portal-section__title h2,
.theme-ocean .portal-subsection__title span { color: #1e3a5f; font-size: 18px; font-weight: 700; letter-spacing: -0.01em; }
.theme-ocean .portal-subsection__title { border-left-color: #3b82f6; }

.theme-ocean .portal-card {
  background: #f7f9fc; border: 1px solid #dbe4ee; border-radius: 12px; padding: 18px 20px; gap: 14px;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}
.theme-ocean .portal-card:hover {
  background: #fff; border-color: #93c5fd;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.08), 0 8px 24px rgba(59, 130, 246, 0.06), 0 1px 2px rgba(59, 130, 246, 0.1);
  transform: translateY(-2px);
}
.theme-ocean .portal-card__icon { background: #eff6ff; color: #3b82f6; width: 42px; height: 42px; border-radius: 12px; font-size: 18px; }
.theme-ocean .portal-card__icon img { width: 16px; height: 16px; }
.theme-ocean .portal-card__copy strong { color: #000; font-size: 14px; }
.theme-ocean .portal-card__copy p { color: #6b8faf; }
.theme-ocean .portal-subsection-list { gap: 22px; }

.theme-ocean .portal-engine-menu { background: #fff; border-color: #dbe4ee; border-radius: 14px; }
.theme-ocean .portal-engine-menu__item { color: #64748b; border-radius: 8px; margin: 2px 4px; padding: 9px 14px; }
.theme-ocean .portal-engine-menu__item:hover { color: #3b82f6; background: #eff6ff; }
.theme-ocean .portal-suggest { background: #fff; border-color: #dbe4ee; border-radius: 14px; }
.theme-ocean .portal-suggest__item:hover,
.theme-ocean .portal-suggest__item.is-active { background: #eff6ff; }
.theme-ocean .portal-suggest__title { color: #1e3a5f; }
.theme-ocean .portal-suggest__category { color: #94a3b8; }
.theme-ocean .portal-footer { color: #94a3b8; border-color: #dbe4ee; }
.theme-ocean .portal-right-rail :deep(.el-button) { background: #fff; border: none; color: #64748b; border-radius: 50% !important; }
.theme-ocean .portal-right-rail :deep(.el-button:hover) { color: #3b82f6; box-shadow: 0 4px 14px rgba(59, 130, 246, 0.18); }
.theme-ocean .portal-bottom-dock { background: #fff; border: none; border-radius: 24px; }
.theme-ocean .portal-hamburger { color: #64748b; }
.theme-ocean .portal-sidebar__close { color: #b0c4de; }
.theme-ocean .portal-context-menu { background: #fff; border-color: #dbe4ee; border-radius: 14px; }
.theme-ocean .portal-context-menu__item { color: #1e3a5f; border-radius: 6px; }
.theme-ocean .portal-context-menu__item:hover { color: #3b82f6; background: #eff6ff; }

/* --- ocean DARK --- */
html.dark .theme-ocean { grid-template-columns: 260px minmax(0, 1fr) !important; }
html.dark .theme-ocean .portal-main { background: #0c1929; }
html.dark .theme-ocean .portal-content { background: #0c1929; }
html.dark .theme-ocean .portal-searchbar { background: #111f33; border-color: #1e3a5f; min-height: 78px; }
html.dark .theme-ocean .portal-search-shell { background: #0f1d30; border-color: #3b82f6; height: 48px; border-radius: 28px; }
html.dark .theme-ocean .portal-search-field { color: #dbe4ee; }
html.dark .theme-ocean .portal-search-field::placeholder { color: #4a6a8a; }
html.dark .theme-ocean .portal-searchbar__input { color: #dbe4ee; }
html.dark .theme-ocean .portal-searchbar__input::placeholder { color: #4a6a8a; }
html.dark .theme-ocean .portal-sidebar { background: linear-gradient(180deg, #0a1628 0%, #060f1e 100%); box-shadow: 4px 0 24px rgba(0,0,0,0.4); }
html.dark .theme-ocean .portal-sidebar__logo { border-color: #1e3a5f; }
html.dark .theme-ocean .portal-sidebar__logo-title { color: #dbe4ee; }
html.dark .theme-ocean .portal-sidebar__logo-title-full { color: #dbe4ee; }
html.dark .theme-ocean .portal-sidebar__link { color: #6b8faf; border-radius: 10px; }
html.dark .theme-ocean .portal-sidebar__link:hover { color: #dbe4ee; background: #142640; border-radius: 10px; }
html.dark .theme-ocean .portal-sidebar__link.is-active { color: #60a5fa; background: rgba(59, 130, 246, 0.12); border-radius: 10px; }
html.dark .theme-ocean .portal-sidebar__link.is-active::before { background: #60a5fa; }
html.dark .theme-ocean .portal-sidebar__child-link { color: #8daabf; border-radius: 8px; }
html.dark .theme-ocean .portal-sidebar__child-link:hover { color: #dbe4ee; background: #142640; border-radius: 8px; }
html.dark .theme-ocean .portal-sidebar__child-link.is-active { color: #60a5fa; background: rgba(59, 130, 246, 0.12); border-radius: 8px; }
html.dark .theme-ocean .portal-section { background: #111f33; border-color: #1e3a5f; border-radius: 16px; }
html.dark .theme-ocean .portal-section__title h2,
html.dark .theme-ocean .portal-subsection__title span { color: #dbe4ee; }
html.dark .theme-ocean .portal-subsection__title { border-left-color: #3b82f6; }
html.dark .theme-ocean .portal-card { background: #162840; border-color: #1e3a5f; border-radius: 12px; }
html.dark .theme-ocean .portal-card:hover { background: #1a3050; border-color: #3b82f6; box-shadow: 0 4px 16px rgba(59, 130, 246, 0.12), 0 8px 24px rgba(0,0,0,0.2); }
html.dark .theme-ocean .portal-card__icon { background: #1e3a5f; color: #60a5fa; width: 42px; height: 42px; border-radius: 12px; }
html.dark .theme-ocean .portal-card__copy strong { color: #dbe4ee; }
html.dark .theme-ocean .portal-card__copy p { color: #5a7d9a; }
html.dark .theme-ocean .portal-engine-menu { background: #111f33; border-color: #1e3a5f; box-shadow: 0 8px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-ocean .portal-engine-menu__item { color: #6b8faf; }
html.dark .theme-ocean .portal-engine-menu__item:hover { color: #60a5fa; background: #142640; }
html.dark .theme-ocean .portal-suggest { background: #111f33; border-color: #1e3a5f; box-shadow: 0 8px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-ocean .portal-suggest__item:hover,
html.dark .theme-ocean .portal-suggest__item.is-active { background: #142640; }
html.dark .theme-ocean .portal-suggest__title { color: #dbe4ee; }
html.dark .theme-ocean .portal-suggest__category { color: #4a6a8a; }
html.dark .theme-ocean .portal-footer { color: #4a6a8a; border-color: #1e3a5f; }
html.dark .theme-ocean .portal-right-rail :deep(.el-button) { background: #111f33; border: none; color: #6b8faf; border-radius: 50% !important; }
html.dark .theme-ocean .portal-right-rail :deep(.el-button:hover) { color: #60a5fa; }
html.dark .theme-ocean .portal-bottom-dock { background: #111f33; border: none; border-radius: 24px; box-shadow: 0 4px 20px rgba(0,0,0,0.4); }
html.dark .theme-ocean .portal-hamburger { color: #6b8faf; }
html.dark .theme-ocean .portal-sidebar__close { color: #6b8faf; }
html.dark .theme-ocean .portal-context-menu { background: #111f33; border-color: #1e3a5f; box-shadow: 0 6px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-ocean .portal-context-menu__item { color: #dbe4ee; }
html.dark .theme-ocean .portal-context-menu__item:hover { color: #60a5fa; background: #142640; }
html.dark .theme-ocean .portal-context-menu__divider { background: #1e3a5f; }

/* ═══════════════════════════════════════════
   THEME: nord — 北欧极简，细线边框，紧凑布局，等宽字体标题
   ═══════════════════════════════════════════ */

.theme-nord { grid-template-columns: 200px minmax(0, 1fr) !important; }
.theme-nord .portal-sidebar {
  background: #4c566a; border-color: #434c5e; border-right: none; box-shadow: none;
}
.theme-nord .portal-sidebar__logo { border-color: #434c5e; padding: 20px 14px 18px; }
.theme-nord .portal-sidebar__logo-letter {
  background: linear-gradient(135deg, #88c0d0, #5e81ac); width: 32px; height: 32px; border-radius: 4px; font-size: 16px;
}
.theme-nord .portal-sidebar__logo-title { color: #eceff4; font-size: 14px; font-weight: 600; letter-spacing: 0.02em; }
.theme-nord .portal-sidebar__logo-title-full { color: #eceff4; }
.theme-nord .portal-sidebar__nav { padding: 8px 0; }
.theme-nord .portal-sidebar__link {
  color: #d8dee9; padding: 8px 14px; font-size: 13px; font-weight: 400; margin: 0;
  border-radius: 0; transition: all 0.15s; border-left: 2px solid transparent;
}
.theme-nord .portal-sidebar__link:hover { color: #eceff4; background: rgba(136, 192, 208, 0.08); border-left-color: rgba(136, 192, 208, 0.3); border-radius: 0; }
.theme-nord .portal-sidebar__link.is-active { color: #88c0d0; background: rgba(136, 192, 208, 0.1); border-left-color: #88c0d0; border-radius: 0; font-weight: 600; }
.theme-nord .portal-sidebar__link.is-active::before { display: none; }
.theme-nord .portal-sidebar__link-icon { font-size: 13px; opacity: 0.6; width: 18px; }
.theme-nord .portal-sidebar__link.is-active .portal-sidebar__link-icon { opacity: 0.9; }
.theme-nord .portal-sidebar__link-arrow { color: #81a1c1; font-size: 10px; }
.theme-nord .portal-sidebar__child-link { color: #81a1c1; padding: 6px 14px 6px 34px; margin: 0; border-radius: 0; }
.theme-nord .portal-sidebar__child-link:hover { color: #eceff4; background: rgba(136, 192, 208, 0.08); border-radius: 0; }
.theme-nord .portal-sidebar__child-link.is-active { color: #88c0d0; background: rgba(136, 192, 208, 0.1); border-radius: 0; }
.theme-nord .portal-sidebar__child-icon { font-size: 10px; width: 14px; }

.theme-nord .portal-main { background: #eceff4; }
.theme-nord .portal-searchbar { background: #e5e9f0; border-color: #d8dee9; min-height: 56px; padding: 10px 20px; }
.theme-nord .portal-searchbar__inner { width: min(600px, 100%); }
.theme-nord .portal-view-toggle { --view-toggle-accent: #5e81ac; --view-toggle-accent-strong: #88c0d0; --view-toggle-shadow: rgba(94, 129, 172, 0.24); }
.theme-nord .portal-search-shell {
  background: #fff; border: 1px solid #5e81ac; border-radius: 4px; height: 38px;
  box-shadow: none; transition: border-color 0.15s;
}
.theme-nord .portal-search-shell:focus-within { box-shadow: none; border-color: #88c0d0; }
.theme-nord .portal-search-field { color: #2e3440; font-size: 13px; }
.theme-nord .portal-search-field::placeholder { color: #81a1c1; }
.theme-nord .portal-search-engine-btn { color: #5e81ac; font-size: 13px; }
.theme-nord .portal-search-engine-btn:hover { background: rgba(94, 129, 172, 0.08); }
.theme-nord .portal-search-submit { color: #5e81ac; }

.theme-nord .portal-content { gap: 16px; padding: 20px 20px 60px; }
.theme-nord .portal-grid { grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 8px; }

.theme-nord .portal-section {
  background: #fff; border: 1px solid #d8dee9; border-radius: 4px; padding: 16px 18px 18px; box-shadow: none;
}
.theme-nord .portal-section__head,
.theme-nord .portal-subsection__head { margin-bottom: 14px; }
.theme-nord .portal-section__title h2,
.theme-nord .portal-subsection__title span {
  color: #2e3440; font-size: 14px; font-weight: 600;
  font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace;
  letter-spacing: 0.04em; text-transform: uppercase;
}
.theme-nord .portal-subsection__title { border-left-color: #88c0d0; }

.theme-nord .portal-card {
  background: #f8fafc; border: 1px solid #d8dee9; border-radius: 4px; padding: 10px 12px; gap: 10px;
  box-shadow: none; transition: border-color 0.15s, background 0.15s;
}
.theme-nord .portal-card:hover { background: #fff; border-color: #88c0d0; box-shadow: none; transform: none; }
.theme-nord .portal-card__icon { background: #e8eef4; color: #5e81ac; width: 30px; height: 30px; border-radius: 4px; font-size: 13px; }
.theme-nord .portal-card__icon img { width: 16px; height: 16px; }
.theme-nord .portal-card__copy strong { color: #000; font-size: 12.5px; font-weight: 500; }
.theme-nord .portal-card__copy p { color: #7b8a9e; font-size: 11px; }
.theme-nord .portal-subsection-list { gap: 14px; }

.theme-nord .portal-engine-menu { background: #fff; border: 1px solid #d8dee9; border-radius: 4px; box-shadow: none; }
.theme-nord .portal-engine-menu__item { color: #4c566a; font-size: 13px; }
.theme-nord .portal-engine-menu__item:hover { color: #5e81ac; background: #e8eef4; }
.theme-nord .portal-suggest { background: #fff; border: 1px solid #d8dee9; border-radius: 4px; box-shadow: none; }
.theme-nord .portal-suggest__item:hover,
.theme-nord .portal-suggest__item.is-active { background: #e8eef4; }
.theme-nord .portal-suggest__title { color: #2e3440; }
.theme-nord .portal-suggest__category { color: #81a1c1; }
.theme-nord .portal-footer { color: #7b8a9e; border-color: #d8dee9; }
.theme-nord .portal-right-rail :deep(.el-button) { background: #fff; border: none; color: #4c566a; border-radius: 50% !important; }
.theme-nord .portal-right-rail :deep(.el-button:hover) { color: #5e81ac; }
.theme-nord .portal-bottom-dock { background: #fff; border: none; border-radius: 24px; }
.theme-nord .portal-hamburger { color: #4c566a; }
.theme-nord .portal-sidebar__close { color: #d8dee9; }
.theme-nord .portal-context-menu { background: #fff; border: 1px solid #d8dee9; border-radius: 4px; box-shadow: none; }
.theme-nord .portal-context-menu__item { color: #2e3440; }
.theme-nord .portal-context-menu__item:hover { color: #5e81ac; background: #e8eef4; }

/* --- nord DARK --- */
html.dark .theme-nord { grid-template-columns: 200px minmax(0, 1fr) !important; }
html.dark .theme-nord .portal-main { background: #2e3440; }
html.dark .theme-nord .portal-content { background: #2e3440; }
html.dark .theme-nord .portal-searchbar { background: #3b4252; border-color: #434c5e; min-height: 56px; }
html.dark .theme-nord .portal-search-shell { background: #3b4252; border: 1px solid #5e81ac; border-radius: 4px; height: 38px; }
html.dark .theme-nord .portal-search-field { color: #eceff4; }
html.dark .theme-nord .portal-search-field::placeholder { color: #4c566a; }
html.dark .theme-nord .portal-searchbar__input { color: #eceff4; }
html.dark .theme-nord .portal-searchbar__input::placeholder { color: #4c566a; }
html.dark .theme-nord .portal-sidebar { background: #242933; box-shadow: none; }
html.dark .theme-nord .portal-sidebar__logo { border-color: #3b4252; }
html.dark .theme-nord .portal-sidebar__logo-title { color: #eceff4; }
html.dark .theme-nord .portal-sidebar__logo-title-full { color: #eceff4; }
html.dark .theme-nord .portal-sidebar__link { color: #81a1c1; border-left: 2px solid transparent; border-radius: 0; }
html.dark .theme-nord .portal-sidebar__link:hover { color: #eceff4; background: #3b4252; border-left-color: rgba(136, 192, 208, 0.3); border-radius: 0; }
html.dark .theme-nord .portal-sidebar__link.is-active { color: #88c0d0; background: rgba(136, 192, 208, 0.1); border-left-color: #88c0d0; border-radius: 0; }
html.dark .theme-nord .portal-sidebar__child-link { color: #81a1c1; border-radius: 0; }
html.dark .theme-nord .portal-sidebar__child-link:hover { color: #eceff4; background: #3b4252; border-radius: 0; }
html.dark .theme-nord .portal-sidebar__child-link.is-active { color: #88c0d0; background: rgba(136, 192, 208, 0.12); border-radius: 0; }
html.dark .theme-nord .portal-section { background: #3b4252; border: 1px solid #434c5e; border-radius: 4px; }
html.dark .theme-nord .portal-section__title h2,
html.dark .theme-nord .portal-subsection__title span { color: #eceff4; }
html.dark .theme-nord .portal-subsection__title { border-left-color: #88c0d0; }
html.dark .theme-nord .portal-card { background: #434c5e; border: 1px solid #4c566a; border-radius: 4px; }
html.dark .theme-nord .portal-card:hover { background: #4c566a; border-color: #5e81ac; box-shadow: none; transform: none; }
html.dark .theme-nord .portal-card__icon { background: #4c566a; color: #88c0d0; width: 30px; height: 30px; border-radius: 4px; }
html.dark .theme-nord .portal-card__copy strong { color: #eceff4; }
html.dark .theme-nord .portal-card__copy p { color: #6b8a9e; }
html.dark .theme-nord .portal-engine-menu { background: #3b4252; border: 1px solid #434c5e; box-shadow: none; border-radius: 4px; }
html.dark .theme-nord .portal-engine-menu__item { color: #81a1c1; }
html.dark .theme-nord .portal-engine-menu__item:hover { color: #88c0d0; background: #434c5e; }
html.dark .theme-nord .portal-suggest { background: #3b4252; border: 1px solid #434c5e; box-shadow: none; border-radius: 4px; }
html.dark .theme-nord .portal-suggest__item:hover,
html.dark .theme-nord .portal-suggest__item.is-active { background: #434c5e; }
html.dark .theme-nord .portal-suggest__title { color: #eceff4; }
html.dark .theme-nord .portal-suggest__category { color: #6b8a9e; }
html.dark .theme-nord .portal-footer { color: #6b8a9e; border-color: #434c5e; }
html.dark .theme-nord .portal-right-rail :deep(.el-button) { background: #3b4252; border: none; color: #81a1c1; border-radius: 50% !important; }
html.dark .theme-nord .portal-right-rail :deep(.el-button:hover) { color: #88c0d0; }
html.dark .theme-nord .portal-bottom-dock { background: #3b4252; border: none; border-radius: 24px; }
html.dark .theme-nord .portal-hamburger { color: #81a1c1; }
html.dark .theme-nord .portal-sidebar__close { color: #81a1c1; }
html.dark .theme-nord .portal-context-menu { background: #3b4252; border: 1px solid #434c5e; box-shadow: none; border-radius: 4px; }
html.dark .theme-nord .portal-context-menu__item { color: #eceff4; }
html.dark .theme-nord .portal-context-menu__item:hover { color: #88c0d0; background: #434c5e; }
html.dark .theme-nord .portal-context-menu__divider { background: #434c5e; }

/* ═══════════════════════════════════════════
   THEME: glass — 玻璃拟态，磨砂透明，渐变背景，大圆角宽间距
   ═══════════════════════════════════════════ */

.theme-glass .portal-sidebar {
  background: #1e143c;
  border-color: rgba(124, 77, 255, 0.15); border-right: none; box-shadow: 6px 0 32px rgba(124, 77, 255, 0.08);
}
.theme-glass .portal-sidebar__logo { border-color: rgba(124, 77, 255, 0.15); padding: 24px 20px 22px; }
.theme-glass .portal-sidebar__logo-letter {
  background: linear-gradient(135deg, #7c4dff, #448aff); border-radius: 14px; width: 38px; height: 38px; font-size: 18px;
}
.theme-glass .portal-sidebar__logo-title { color: #ede7f6; font-size: 16px; font-weight: 700; }
.theme-glass .portal-sidebar__logo-title-full { color: #ede7f6; }
.theme-glass .portal-sidebar__nav { padding: 12px 0; }
.theme-glass .portal-sidebar__link {
  color: #b39ddb; padding: 11px 20px; font-size: 14px; margin: 3px 8px; border-radius: 12px; transition: all 0.2s ease;
}
.theme-glass .portal-sidebar__link:hover { color: #ede7f6; background: rgba(124, 77, 255, 0.12); border-radius: 12px; }
.theme-glass .portal-sidebar__link.is-active { color: #ede7f6; background: rgba(124, 77, 255, 0.22); border-radius: 12px; font-weight: 600; }
.theme-glass .portal-sidebar__link.is-active::before { background: #b388ff; width: 3px; border-radius: 0 3px 3px 0; left: -8px; }
.theme-glass .portal-sidebar__link-icon { font-size: 15px; opacity: 0.7; }
.theme-glass .portal-sidebar__link.is-active .portal-sidebar__link-icon { opacity: 1; }
.theme-glass .portal-sidebar__link-arrow { color: #9575cd; }
.theme-glass .portal-sidebar__child-link { color: #6a4c93; padding: 8px 20px 8px 48px; margin: 0 8px; border-radius: 10px; }
.theme-glass .portal-sidebar__child-link:hover { color: #ede7f6; background: rgba(124, 77, 255, 0.12); border-radius: 10px; }
.theme-glass .portal-sidebar__child-link.is-active { color: #b388ff; background: rgba(124, 77, 255, 0.18); border-radius: 10px; }

.theme-glass .portal-main { background: linear-gradient(135deg, #ede7f6 0%, #e3f2fd 50%, #e0f2f1 100%); }
.theme-glass .portal-searchbar {
  background: #fff;
  border-color: #e8eaed; min-height: 72px; padding: 14px 28px;
}
.theme-glass .portal-searchbar__inner { width: min(680px, 100%); }
.theme-glass .portal-view-toggle { --view-toggle-accent: #7c4dff; --view-toggle-accent-strong: #5e35b1; --view-toggle-shadow: rgba(124, 77, 255, 0.28); }
.theme-glass .portal-search-shell {
  background: #fff;
  border: 2px solid #7c4dff; border-radius: 20px; height: 46px;
  box-shadow: 0 4px 16px rgba(124, 77, 255, 0.08); transition: box-shadow 0.25s, border-color 0.25s;
}
.theme-glass .portal-search-shell:focus-within { box-shadow: 0 6px 24px rgba(124, 77, 255, 0.16); border-color: #651fff; }
.theme-glass .portal-search-field { color: #311b92; font-size: 14px; }
.theme-glass .portal-search-field::placeholder { color: #9e9e9e; }
.theme-glass .portal-search-engine-btn { color: #7c4dff; }
.theme-glass .portal-search-engine-btn:hover { background: rgba(124, 77, 255, 0.06); }
.theme-glass .portal-search-submit { color: #7c4dff; }

.theme-glass .portal-content { gap: 24px; padding: 28px 28px 100px; }
.theme-glass .portal-grid { grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 18px; }

.theme-glass .portal-section {
  background: #fff;
  border: 1px solid #e8eaed; border-radius: 16px; padding: 24px 24px 26px;
  box-shadow: 0 4px 24px rgba(0,0,0,0.06);
}
.theme-glass .portal-section__head,
.theme-glass .portal-subsection__head { margin-bottom: 20px; }
.theme-glass .portal-section__title h2,
.theme-glass .portal-subsection__title span { color: #311b92; font-size: 17px; font-weight: 700; }
.theme-glass .portal-subsection__title { border-left-color: #7c4dff; }

.theme-glass .portal-card {
  background: #fff;
  border: 1px solid #e8eaed; border-radius: 16px; padding: 16px 18px; gap: 14px;
  transition: all 0.25s ease;
}
.theme-glass .portal-card:hover {
  background: #fff; border-color: rgba(124, 77, 255, 0.3);
  box-shadow: 0 8px 32px rgba(124, 77, 255, 0.1); transform: translateY(-2px);
}
.theme-glass .portal-card__icon { background: rgba(124, 77, 255, 0.1); color: #7c4dff; width: 40px; height: 40px; border-radius: 12px; font-size: 17px; }
.theme-glass .portal-card__icon img { width: 16px; height: 16px; }
.theme-glass .portal-card__copy strong { color: #000; font-size: 13.5px; }
.theme-glass .portal-card__copy p { color: #7e57c2; }
.theme-glass .portal-subsection-list { gap: 20px; }

.theme-glass .portal-engine-menu { background: #fff; border: 1px solid #e8eaed; border-radius: 16px; }
.theme-glass .portal-engine-menu__item { color: #5e35b1; }
.theme-glass .portal-engine-menu__item:hover { color: #7c4dff; background: rgba(124, 77, 255, 0.06); }
.theme-glass .portal-suggest { background: #fff; border: 1px solid #e8eaed; border-radius: 16px; }
.theme-glass .portal-suggest__item:hover,
.theme-glass .portal-suggest__item.is-active { background: rgba(124, 77, 255, 0.06); }
.theme-glass .portal-suggest__title { color: #311b92; }
.theme-glass .portal-suggest__category { color: #9575cd; }
.theme-glass .portal-footer { color: #9575cd; border-color: rgba(255,255,255,0.3); }
.theme-glass .portal-right-rail :deep(.el-button) {
  background: #fff;
  border: none; color: #7e57c2; border-radius: 50% !important;
}
.theme-glass .portal-right-rail :deep(.el-button:hover) { color: #7c4dff; }
.theme-glass .portal-bottom-dock { background: #fff; border: none; border-radius: 24px; }
.theme-glass .portal-hamburger { color: #7e57c2; }
.theme-glass .portal-sidebar__close { color: #b39ddb; }
.theme-glass .portal-context-menu { background: #fff; border: 1px solid #e8eaed; border-radius: 16px; }
.theme-glass .portal-context-menu__item { color: #311b92; }
.theme-glass .portal-context-menu__item:hover { color: #7c4dff; background: rgba(124, 77, 255, 0.06); }

/* --- glass DARK --- */
html.dark .theme-glass .portal-main { background: linear-gradient(135deg, #1a0a3e 0%, #0d1b2a 50%, #0a1929 100%); }
html.dark .theme-glass .portal-content { background: transparent; }
html.dark .theme-glass .portal-searchbar { background: #1a1030; border-color: rgba(124, 77, 255, 0.15); }
html.dark .theme-glass .portal-search-shell { background: #1e143c; border: 2px solid #7c4dff; border-radius: 20px; }
html.dark .theme-glass .portal-search-field { color: #ede7f6; }
html.dark .theme-glass .portal-search-field::placeholder { color: #5e35b1; }
html.dark .theme-glass .portal-searchbar__input { color: #ede7f6; }
html.dark .theme-glass .portal-searchbar__input::placeholder { color: #5e35b1; }
html.dark .theme-glass .portal-sidebar { background: #0a051e; box-shadow: 6px 0 32px rgba(0,0,0,0.3); }
html.dark .theme-glass .portal-sidebar__logo { border-color: rgba(124, 77, 255, 0.15); }
html.dark .theme-glass .portal-sidebar__logo-title { color: #ede7f6; }
html.dark .theme-glass .portal-sidebar__logo-title-full { color: #ede7f6; }
html.dark .theme-glass .portal-sidebar__link { color: #9575cd; border-radius: 12px; }
html.dark .theme-glass .portal-sidebar__link:hover { color: #ede7f6; background: rgba(124, 77, 255, 0.12); border-radius: 12px; }
html.dark .theme-glass .portal-sidebar__link.is-active { color: #ede7f6; background: rgba(124, 77, 255, 0.22); border-radius: 12px; }
html.dark .theme-glass .portal-sidebar__link.is-active::before { background: #b388ff; }
html.dark .theme-glass .portal-sidebar__child-link { color: #9575cd; border-radius: 10px; }
html.dark .theme-glass .portal-sidebar__child-link:hover { color: #ede7f6; background: rgba(124, 77, 255, 0.12); border-radius: 10px; }
html.dark .theme-glass .portal-sidebar__child-link.is-active { color: #b388ff; background: rgba(124, 77, 255, 0.18); border-radius: 10px; }
html.dark .theme-glass .portal-section { background: #1e143c; border: 1px solid rgba(124, 77, 255, 0.12); border-radius: 16px; box-shadow: 0 4px 24px rgba(0,0,0,0.3); }
html.dark .theme-glass .portal-section__title h2,
html.dark .theme-glass .portal-subsection__title span { color: #ede7f6; }
html.dark .theme-glass .portal-subsection__title { border-left-color: #7c4dff; }
html.dark .theme-glass .portal-card { background: #1a1030; border: 1px solid rgba(255,255,255,0.06); border-radius: 16px; }
html.dark .theme-glass .portal-card:hover { background: #221848; border-color: rgba(124, 77, 255, 0.25); box-shadow: 0 8px 32px rgba(124, 77, 255, 0.15); }
html.dark .theme-glass .portal-card__icon { background: rgba(124, 77, 255, 0.15); color: #b388ff; width: 40px; height: 40px; border-radius: 12px; }
html.dark .theme-glass .portal-card__copy strong { color: #ede7f6; }
html.dark .theme-glass .portal-card__copy p { color: #7e57c2; }
html.dark .theme-glass .portal-engine-menu { background: #140a32; border: 1px solid rgba(124, 77, 255, 0.15); border-radius: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.5); }
html.dark .theme-glass .portal-engine-menu__item { color: #9575cd; }
html.dark .theme-glass .portal-engine-menu__item:hover { color: #b388ff; background: rgba(124, 77, 255, 0.12); }
html.dark .theme-glass .portal-suggest { background: #140a32; border: 1px solid rgba(124, 77, 255, 0.15); border-radius: 16px; box-shadow: 0 8px 24px rgba(0,0,0,0.5); }
html.dark .theme-glass .portal-suggest__item:hover,
html.dark .theme-glass .portal-suggest__item.is-active { background: rgba(124, 77, 255, 0.12); }
html.dark .theme-glass .portal-suggest__title { color: #ede7f6; }
html.dark .theme-glass .portal-suggest__category { color: #7e57c2; }
html.dark .theme-glass .portal-footer { color: #5e35b1; border-color: rgba(124, 77, 255, 0.12); }
html.dark .theme-glass .portal-right-rail :deep(.el-button) { background: #1e143c; border: none; color: #9575cd; border-radius: 50% !important; }
html.dark .theme-glass .portal-right-rail :deep(.el-button:hover) { color: #b388ff; }
html.dark .theme-glass .portal-bottom-dock { background: #140a32; border: none; border-radius: 24px; box-shadow: 0 4px 20px rgba(0,0,0,0.4); }
html.dark .theme-glass .portal-hamburger { color: #9575cd; }
html.dark .theme-glass .portal-sidebar__close { color: #9575cd; }
html.dark .theme-glass .portal-context-menu { background: #140a32; border: 1px solid rgba(124, 77, 255, 0.15); border-radius: 16px; box-shadow: 0 6px 24px rgba(0,0,0,0.5); }
html.dark .theme-glass .portal-context-menu__item { color: #ede7f6; }
html.dark .theme-glass .portal-context-menu__item:hover { color: #b388ff; background: rgba(124, 77, 255, 0.12); }
html.dark .theme-glass .portal-context-menu__divider { background: rgba(124, 77, 255, 0.12); }

/* ═══════════════════════════════════════════
   THEME: neon — 赛博朋克，霓虹发光，强制暗色，等宽字体，尖角
   ═══════════════════════════════════════════ */

.theme-neon { grid-template-columns: 220px minmax(0, 1fr) !important; color-scheme: dark; }

.theme-neon .portal-main { background: #0a0618; }
.theme-neon .portal-content { background: #0a0618; }
.theme-neon .portal-sidebar {
  background: #080416; border-color: #1e1550; border-right: none; box-shadow: 4px 0 20px rgba(139, 92, 246, 0.06);
}
.theme-neon .portal-sidebar__logo { border-color: #1e1550; padding: 22px 16px 20px; }
.theme-neon .portal-sidebar__logo-letter {
  background: linear-gradient(135deg, #06d6a0, #8b5cf6); border-radius: 2px; width: 34px; height: 34px; font-size: 17px;
}
.theme-neon .portal-sidebar__logo-title {
  color: #f5f3ff; font-size: 15px; font-weight: 700;
  font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace;
  letter-spacing: 0.06em; text-transform: uppercase;
}
.theme-neon .portal-sidebar__logo-title-full { color: #f5f3ff; font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace; }
.theme-neon .portal-sidebar__nav { padding: 10px 0; }
.theme-neon .portal-sidebar__link {
  color: #7c6ec4; padding: 10px 16px; font-size: 13px;
  font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace;
  margin: 1px 6px; border-radius: 2px; transition: all 0.2s ease; border-left: 2px solid transparent;
}
.theme-neon .portal-sidebar__link:hover { color: #ede9fe; background: rgba(139, 92, 246, 0.08); border-left-color: rgba(139, 92, 246, 0.4); border-radius: 2px; }
.theme-neon .portal-sidebar__link.is-active {
  color: #06d6a0; background: rgba(6, 214, 160, 0.06); border-left-color: #06d6a0; border-radius: 2px;
  font-weight: 600; text-shadow: 0 0 8px rgba(6, 214, 160, 0.3);
}
.theme-neon .portal-sidebar__link.is-active::before { display: none; }
.theme-neon .portal-sidebar__link-icon { font-size: 13px; opacity: 0.5; width: 18px; }
.theme-neon .portal-sidebar__link.is-active .portal-sidebar__link-icon { opacity: 1; }
.theme-neon .portal-sidebar__link-arrow { color: #5b4da6; font-size: 10px; }
.theme-neon .portal-sidebar__child-link { color: #7c6ec4; padding: 7px 16px 7px 36px; margin: 0 6px; border-radius: 2px; }
.theme-neon .portal-sidebar__child-link:hover { color: #ede9fe; background: rgba(139, 92, 246, 0.08); border-radius: 2px; }
.theme-neon .portal-sidebar__child-link.is-active { color: #06d6a0; background: rgba(6, 214, 160, 0.06); border-radius: 2px; text-shadow: 0 0 6px rgba(6, 214, 160, 0.2); }
.theme-neon .portal-sidebar__child-icon { font-size: 10px; }

.theme-neon .portal-searchbar { background: #110d24; border-color: #1e1550; min-height: 64px; padding: 12px 24px; }
.theme-neon .portal-searchbar__inner { width: min(640px, 100%); }
.theme-neon .portal-view-toggle { --view-toggle-accent: #06d6a0; --view-toggle-accent-strong: #7c3aed; --view-toggle-shadow: rgba(6, 214, 160, 0.24); }
.theme-neon .portal-search-shell {
  background: #0d0a1f; border: 1px solid #8b5cf6; border-radius: 2px; height: 42px;
  box-shadow: 0 0 8px rgba(139, 92, 246, 0.15), inset 0 0 4px rgba(139, 92, 246, 0.05);
  transition: box-shadow 0.25s, border-color 0.25s;
}
.theme-neon .portal-search-shell:focus-within {
  border-color: #06d6a0;
  box-shadow: 0 0 12px rgba(6, 214, 160, 0.2), 0 0 24px rgba(139, 92, 246, 0.1), inset 0 0 6px rgba(6, 214, 160, 0.05);
}
.theme-neon .portal-search-field { color: #ede9fe; font-size: 13px; font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace; }
.theme-neon .portal-search-field::placeholder { color: #4c3a99; }
.theme-neon .portal-search-engine-btn { color: #8b5cf6; font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace; font-size: 12px; }
.theme-neon .portal-search-engine-btn:hover { background: rgba(139, 92, 246, 0.06); }
.theme-neon .portal-search-submit { color: #8b5cf6; }

.theme-neon .portal-content { gap: 20px; padding: 24px 24px 100px; }
.theme-neon .portal-grid { grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 12px; }

.theme-neon .portal-section {
  background: #110d24; border: 1px solid #1e1550; border-radius: 2px; padding: 20px 22px 22px;
  box-shadow: 0 0 1px rgba(139, 92, 246, 0.1);
}
.theme-neon .portal-section__head,
.theme-neon .portal-subsection__head { margin-bottom: 16px; }
.theme-neon .portal-section__title h2,
.theme-neon .portal-subsection__title span {
  color: #ede9fe; font-size: 15px; font-weight: 700;
  font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace;
  letter-spacing: 0.08em; text-transform: uppercase; text-shadow: 0 0 10px rgba(139, 92, 246, 0.2);
}
.theme-neon .portal-subsection__title { border-left-color: #06d6a0; }

.theme-neon .portal-card {
  background: #150f30; border: 1px solid #1e1550; border-radius: 2px; padding: 14px 16px; gap: 12px;
  transition: all 0.25s ease;
}
.theme-neon .portal-card:hover {
  background: #1a1340; border-color: #8b5cf6;
  box-shadow: 0 0 12px rgba(139, 92, 246, 0.25), 0 0 24px rgba(139, 92, 246, 0.1), inset 0 0 8px rgba(139, 92, 246, 0.05);
  transform: none;
}
.theme-neon .portal-card__icon { background: #1e1550; color: #a78bfa; width: 34px; height: 34px; border-radius: 2px; font-size: 14px; }
.theme-neon .portal-card__icon img { width: 16px; height: 16px; }
.theme-neon .portal-card__copy strong { color: #000; font-size: 13px; }
.theme-neon .portal-card__copy p { color: #5b4da6; font-size: 11px; }
.theme-neon .portal-subsection-list { gap: 16px; }

.theme-neon .portal-engine-menu { background: #110d24; border: 1px solid #1e1550; border-radius: 2px; box-shadow: 0 0 16px rgba(139, 92, 246, 0.12); }
.theme-neon .portal-engine-menu__item { color: #7c6ec4; font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace; font-size: 12px; }
.theme-neon .portal-engine-menu__item:hover { color: #06d6a0; background: #150f30; }
.theme-neon .portal-suggest { background: #110d24; border: 1px solid #1e1550; border-radius: 2px; box-shadow: 0 0 16px rgba(139, 92, 246, 0.12); }
.theme-neon .portal-suggest__item:hover,
.theme-neon .portal-suggest__item.is-active { background: #150f30; }
.theme-neon .portal-suggest__title { color: #ede9fe; }
.theme-neon .portal-suggest__category { color: #5b4da6; }
.theme-neon .portal-footer { color: #4c3a99; border-color: #1e1550; }
.theme-neon .portal-right-rail :deep(.el-button) { background: #110d24; border: none; color: #7c6ec4; border-radius: 50% !important; }
.theme-neon .portal-right-rail :deep(.el-button:hover) { color: #06d6a0; box-shadow: 0 0 12px rgba(6, 214, 160, 0.2); }
.theme-neon .portal-bottom-dock { background: #110d24; border: none; border-radius: 24px; box-shadow: 0 0 16px rgba(139, 92, 246, 0.08); }
.theme-neon .portal-hamburger { color: #7c6ec4; }
.theme-neon .portal-sidebar__close { color: #7c6ec4; }
.theme-neon .portal-context-menu { background: #110d24; border: 1px solid #1e1550; border-radius: 2px; box-shadow: 0 0 16px rgba(139, 92, 246, 0.12); }
.theme-neon .portal-context-menu__item { color: #ede9fe; font-family: 'SF Mono', 'Fira Code', 'Cascadia Code', 'Consolas', monospace; }
.theme-neon .portal-context-menu__item:hover { color: #06d6a0; background: #150f30; }

/* --- neon DARK (mirrors light since neon is always dark) --- */
html.dark .theme-neon { grid-template-columns: 220px minmax(0, 1fr) !important; }
html.dark .theme-neon .portal-main { background: #0a0618; }
html.dark .theme-neon .portal-content { background: #0a0618; }
html.dark .theme-neon .portal-searchbar { background: #110d24; border-color: #1e1550; }
html.dark .theme-neon .portal-search-shell { background: #0d0a1f; border-color: #8b5cf6; border-radius: 2px; }
html.dark .theme-neon .portal-search-field { color: #ede9fe; }
html.dark .theme-neon .portal-search-field::placeholder { color: #4c3a99; }
html.dark .theme-neon .portal-searchbar__input { color: #ede9fe; }
html.dark .theme-neon .portal-searchbar__input::placeholder { color: #4c3a99; }
html.dark .theme-neon .portal-sidebar { background: #080416; box-shadow: 4px 0 20px rgba(139, 92, 246, 0.06); }
html.dark .theme-neon .portal-sidebar__logo { border-color: #1e1550; }
html.dark .theme-neon .portal-sidebar__logo-title { color: #ede9fe; }
html.dark .theme-neon .portal-sidebar__logo-title-full { color: #ede9fe; }
html.dark .theme-neon .portal-sidebar__link { color: #7c6ec4; border-left: 2px solid transparent; border-radius: 2px; }
html.dark .theme-neon .portal-sidebar__link:hover { color: #ede9fe; background: #150f30; border-radius: 2px; }
html.dark .theme-neon .portal-sidebar__link.is-active { color: #06d6a0; background: rgba(6, 214, 160, 0.08); border-radius: 2px; }
html.dark .theme-neon .portal-sidebar__child-link { color: #7c6ec4; border-radius: 2px; }
html.dark .theme-neon .portal-sidebar__child-link:hover { color: #ede9fe; background: #150f30; border-radius: 2px; }
html.dark .theme-neon .portal-sidebar__child-link.is-active { color: #06d6a0; background: rgba(6, 214, 160, 0.08); border-radius: 2px; }
html.dark .theme-neon .portal-section { background: #110d24; border-color: #1e1550; border-radius: 2px; }
html.dark .theme-neon .portal-section__title h2,
html.dark .theme-neon .portal-subsection__title span { color: #ede9fe; }
html.dark .theme-neon .portal-subsection__title { border-left-color: #06d6a0; }
html.dark .theme-neon .portal-card { background: #150f30; border-color: #1e1550; border-radius: 2px; }
html.dark .theme-neon .portal-card:hover { background: #1a1340; border-color: #8b5cf6; box-shadow: 0 0 20px rgba(139, 92, 246, 0.2), 0 0 40px rgba(139, 92, 246, 0.08), inset 0 0 8px rgba(139, 92, 246, 0.05); }
html.dark .theme-neon .portal-card__icon { background: #1e1550; color: #a78bfa; width: 34px; height: 34px; border-radius: 2px; }
html.dark .theme-neon .portal-card__copy strong { color: #ede9fe; }
html.dark .theme-neon .portal-card__copy p { color: #5b4da6; }
html.dark .theme-neon .portal-engine-menu { background: #110d24; border-color: #1e1550; box-shadow: 0 0 16px rgba(139, 92, 246, 0.12); border-radius: 2px; }
html.dark .theme-neon .portal-engine-menu__item { color: #7c6ec4; }
html.dark .theme-neon .portal-engine-menu__item:hover { color: #06d6a0; background: #150f30; }
html.dark .theme-neon .portal-suggest { background: #110d24; border-color: #1e1550; box-shadow: 0 0 16px rgba(139, 92, 246, 0.12); border-radius: 2px; }
html.dark .theme-neon .portal-suggest__item:hover,
html.dark .theme-neon .portal-suggest__item.is-active { background: #150f30; }
html.dark .theme-neon .portal-suggest__title { color: #ede9fe; }
html.dark .theme-neon .portal-suggest__category { color: #5b4da6; }
html.dark .theme-neon .portal-footer { color: #4c3a99; border-color: #1e1550; }
html.dark .theme-neon .portal-right-rail :deep(.el-button) { background: rgba(17, 13, 36, 0.92); border: none; color: #7c6ec4; border-radius: 50% !important; }
html.dark .theme-neon .portal-right-rail :deep(.el-button:hover) { color: #06d6a0; box-shadow: 0 0 12px rgba(6, 214, 160, 0.2); }
html.dark .theme-neon .portal-bottom-dock { background: #110d24; border: none; border-radius: 24px; box-shadow: 0 0 24px rgba(139, 92, 246, 0.1); }
html.dark .theme-neon .portal-hamburger { color: #7c6ec4; }
html.dark .theme-neon .portal-sidebar__close { color: #7c6ec4; }
html.dark .theme-neon .portal-context-menu { background: #110d24; border-color: #1e1550; box-shadow: 0 0 20px rgba(139, 92, 246, 0.15); border-radius: 2px; }
html.dark .theme-neon .portal-context-menu__item { color: #ede9fe; }
html.dark .theme-neon .portal-context-menu__item:hover { color: #06d6a0; background: #150f30; }
html.dark .theme-neon .portal-context-menu__divider { background: #1e1550; }

/* ═══════════════════════════════════════════
   THEME: paper — 暖色纸张，衬线字体，笔记本风格，大圆角宽间距
   ═══════════════════════════════════════════ */

.theme-paper .portal-sidebar {
  background: #3a3220; border-color: #4a4030; border-right: none; box-shadow: 4px 0 16px rgba(74, 64, 48, 0.15);
}
.theme-paper .portal-sidebar__logo { border-color: #4a4030; padding: 24px 20px 22px; }
.theme-paper .portal-sidebar__logo-letter {
  background: linear-gradient(135deg, #c4903c, #8b6914); border-radius: 14px; width: 38px; height: 38px; font-size: 18px;
}
.theme-paper .portal-sidebar__logo-title { color: #f5f0e8; font-size: 16px; font-weight: 700; }
.theme-paper .portal-sidebar__logo-title-full { color: #f5f0e8; }
.theme-paper .portal-sidebar__nav { padding: 14px 0; }
.theme-paper .portal-sidebar__link {
  color: #c4b596; padding: 11px 20px; font-size: 14px; margin: 2px 8px; border-radius: 10px; transition: all 0.2s ease;
}
.theme-paper .portal-sidebar__link:hover { color: #f5f0e8; background: rgba(196, 144, 60, 0.1); border-radius: 10px; }
.theme-paper .portal-sidebar__link.is-active { color: #f5f0e8; background: rgba(196, 144, 60, 0.18); border-radius: 10px; font-weight: 600; }
.theme-paper .portal-sidebar__link.is-active::before { background: #d4a04c; width: 4px; border-radius: 0 4px 4px 0; left: -8px; }
.theme-paper .portal-sidebar__link-icon { font-size: 15px; opacity: 0.6; }
.theme-paper .portal-sidebar__link.is-active .portal-sidebar__link-icon { opacity: 1; }
.theme-paper .portal-sidebar__link-arrow { color: #aa9a73; }
.theme-paper .portal-sidebar__child-link { color: #7a6b52; padding: 8px 20px 8px 48px; margin: 0 8px; border-radius: 8px; }
.theme-paper .portal-sidebar__child-link:hover { color: #f5f0e8; background: rgba(196, 144, 60, 0.1); border-radius: 8px; }
.theme-paper .portal-sidebar__child-link.is-active { color: #d4a04c; background: rgba(196, 144, 60, 0.15); border-radius: 8px; }

.theme-paper .portal-main { background: #f5f0e8; }
.theme-paper .portal-searchbar { background: #faf7f2; border-color: #e0d5c5; min-height: 72px; padding: 14px 28px; }
.theme-paper .portal-searchbar__inner { width: min(680px, 100%); }
.theme-paper .portal-view-toggle { --view-toggle-accent: #c4903c; --view-toggle-accent-strong: #8b6914; --view-toggle-shadow: rgba(196, 144, 60, 0.24); }
.theme-paper .portal-search-shell {
  background: #fffdf9; border: 2px solid #c4903c; border-radius: 20px; height: 46px;
  box-shadow: 0 2px 8px rgba(74, 64, 48, 0.06); transition: box-shadow 0.25s, border-color 0.25s;
}
.theme-paper .portal-search-shell:focus-within { box-shadow: 0 4px 16px rgba(196, 144, 60, 0.12); border-color: #8b6914; }
.theme-paper .portal-search-field { color: #3a3220; font-size: 14px; }
.theme-paper .portal-search-field::placeholder { color: #b8a88a; }
.theme-paper .portal-search-engine-btn { color: #c4903c; }
.theme-paper .portal-search-engine-btn:hover { background: rgba(196, 144, 60, 0.06); }
.theme-paper .portal-search-submit { color: #c4903c; }

.theme-paper .portal-content { gap: 28px; padding: 32px 28px 100px; }
.theme-paper .portal-grid { grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; }

.theme-paper .portal-section {
  background: #fffdf9; border: 2px solid #e8dcc8; border-radius: 14px; padding: 0 26px 26px;
  box-shadow: 0 3px 16px rgba(74, 64, 48, 0.07);
}
.theme-paper .portal-section__head {
  margin-bottom: 20px; padding: 20px 0 14px; border-bottom: 2px solid #e0d5c5;
}
.theme-paper .portal-subsection__head {
  margin-bottom: 16px; padding-bottom: 10px; border-bottom: 1px dashed #e0d5c5;
}
.theme-paper .portal-section__title h2 {
  color: #3a3220; font-size: 18px; font-weight: 700;
  font-family: Georgia, 'Times New Roman', 'Noto Serif SC', serif; letter-spacing: 0.01em;
}
.theme-paper .portal-subsection__title span {
  color: #3a3220; font-size: 16px; font-weight: 600;
  font-family: Georgia, 'Times New Roman', 'Noto Serif SC', serif;
}
.theme-paper .portal-subsection__title { border-left-color: #c4903c; border-left-width: 4px; padding-left: 14px; }

.theme-paper .portal-card {
  background: #fffdf9; border: 2px solid #e8dcc8; border-radius: 14px; padding: 16px 18px; gap: 14px;
  box-shadow: 0 2px 8px rgba(74, 64, 48, 0.04); transition: all 0.25s ease;
}
.theme-paper .portal-card:hover {
  background: #fff9ee; border-color: #c4903c;
  box-shadow: 0 6px 20px rgba(74, 64, 48, 0.1), 0 2px 4px rgba(196, 144, 60, 0.08); transform: translateY(-2px);
}
.theme-paper .portal-card__icon { background: #f5efe4; color: #8b6914; width: 40px; height: 40px; border-radius: 10px; font-size: 17px; }
.theme-paper .portal-card__icon img { width: 16px; height: 16px; }
.theme-paper .portal-card__copy strong { color: #000; font-size: 14px; font-weight: 600; }
.theme-paper .portal-card__copy p { color: #8b7e66; font-size: 12px; }
.theme-paper .portal-subsection-list { gap: 24px; }

.theme-paper .portal-engine-menu { background: #fffdf9; border: 2px solid #e0d5c5; border-radius: 14px; }
.theme-paper .portal-engine-menu__item { color: #6b5e47; }
.theme-paper .portal-engine-menu__item:hover { color: #c4903c; background: #faf5ec; }
.theme-paper .portal-suggest { background: #fffdf9; border: 2px solid #e0d5c5; border-radius: 14px; }
.theme-paper .portal-suggest__item:hover,
.theme-paper .portal-suggest__item.is-active { background: #faf5ec; }
.theme-paper .portal-suggest__title { color: #3a3220; }
.theme-paper .portal-suggest__category { color: #aa9a73; }
.theme-paper .portal-footer { color: #8b7e66; border-color: #e0d5c5; }
.theme-paper .portal-right-rail :deep(.el-button) { background: #fffdf9; border: none; color: #8b6914; border-radius: 50% !important; }
.theme-paper .portal-right-rail :deep(.el-button:hover) { color: #c4903c; box-shadow: 0 4px 12px rgba(74, 64, 48, 0.1); }
.theme-paper .portal-bottom-dock { background: #fffdf9; border: none; border-radius: 24px; }
.theme-paper .portal-hamburger { color: #8b6914; }
.theme-paper .portal-sidebar__close { color: #c4b596; }
.theme-paper .portal-context-menu { background: #fffdf9; border: 2px solid #e0d5c5; border-radius: 14px; }
.theme-paper .portal-context-menu__item { color: #3a3220; }
.theme-paper .portal-context-menu__item:hover { color: #c4903c; background: #faf5ec; }

/* --- paper DARK --- */
html.dark .theme-paper .portal-main { background: #1a1710; }
html.dark .theme-paper .portal-content { background: #1a1710; }
html.dark .theme-paper .portal-searchbar { background: #242018; border-color: #3a3220; min-height: 72px; }
html.dark .theme-paper .portal-search-shell { background: #1e1a14; border: 2px solid #c4903c; border-radius: 20px; }
html.dark .theme-paper .portal-search-field { color: #e8dcc8; }
html.dark .theme-paper .portal-search-field::placeholder { color: #5a5040; }
html.dark .theme-paper .portal-searchbar__input { color: #e8dcc8; }
html.dark .theme-paper .portal-searchbar__input::placeholder { color: #5a5040; }
html.dark .theme-paper .portal-sidebar { background: #141210; box-shadow: 4px 0 16px rgba(0,0,0,0.3); }
html.dark .theme-paper .portal-sidebar__logo { border-color: #2a2620; }
html.dark .theme-paper .portal-sidebar__logo-title { color: #e8dcc8; }
html.dark .theme-paper .portal-sidebar__logo-title-full { color: #e8dcc8; }
html.dark .theme-paper .portal-sidebar__link { color: #8b7e66; border-radius: 10px; }
html.dark .theme-paper .portal-sidebar__link:hover { color: #e8dcc8; background: #242018; border-radius: 10px; }
html.dark .theme-paper .portal-sidebar__link.is-active { color: #d4a04c; background: rgba(196, 144, 60, 0.12); border-radius: 10px; }
html.dark .theme-paper .portal-sidebar__link.is-active::before { background: #d4a04c; }
html.dark .theme-paper .portal-sidebar__child-link { color: #aa9a73; border-radius: 8px; }
html.dark .theme-paper .portal-sidebar__child-link:hover { color: #e8dcc8; background: #242018; border-radius: 8px; }
html.dark .theme-paper .portal-sidebar__child-link.is-active { color: #d4a04c; background: rgba(196, 144, 60, 0.12); border-radius: 8px; }
html.dark .theme-paper .portal-section { background: #242018; border: 2px solid #3a3220; border-radius: 14px; box-shadow: 0 3px 16px rgba(0,0,0,0.2); }
html.dark .theme-paper .portal-section__head { border-bottom: 2px solid #3a3220; }
html.dark .theme-paper .portal-subsection__head { border-bottom: 1px dashed #3a3220; }
html.dark .theme-paper .portal-section__title h2 { color: #e8dcc8; font-family: Georgia, 'Times New Roman', 'Noto Serif SC', serif; }
html.dark .theme-paper .portal-subsection__title span { color: #e8dcc8; font-family: Georgia, 'Times New Roman', 'Noto Serif SC', serif; }
html.dark .theme-paper .portal-subsection__title { border-left-color: #c4903c; border-left-width: 4px; padding-left: 14px; }
html.dark .theme-paper .portal-card { background: #2a2620; border: 2px solid #3a3220; border-radius: 14px; }
html.dark .theme-paper .portal-card:hover { background: #302c24; border-color: #8b6914; box-shadow: 0 6px 20px rgba(0,0,0,0.2), 0 2px 4px rgba(196, 144, 60, 0.1); }
html.dark .theme-paper .portal-card__icon { background: #3a3220; color: #c4903c; width: 40px; height: 40px; border-radius: 10px; }
html.dark .theme-paper .portal-card__copy strong { color: #e8dcc8; }
html.dark .theme-paper .portal-card__copy p { color: #6b5e47; }
html.dark .theme-paper .portal-engine-menu { background: #242018; border: 2px solid #3a3220; box-shadow: 0 8px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-paper .portal-engine-menu__item { color: #8b7e66; }
html.dark .theme-paper .portal-engine-menu__item:hover { color: #d4a04c; background: #2a2620; }
html.dark .theme-paper .portal-suggest { background: #242018; border: 2px solid #3a3220; box-shadow: 0 8px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-paper .portal-suggest__item:hover,
html.dark .theme-paper .portal-suggest__item.is-active { background: #2a2620; }
html.dark .theme-paper .portal-suggest__title { color: #e8dcc8; }
html.dark .theme-paper .portal-suggest__category { color: #6b5e47; }
html.dark .theme-paper .portal-footer { color: #5a5040; border-color: #3a3220; }
html.dark .theme-paper .portal-right-rail :deep(.el-button) { background: #242018; border: none; color: #8b7e66; border-radius: 50% !important; }
html.dark .theme-paper .portal-right-rail :deep(.el-button:hover) { color: #d4a04c; }
html.dark .theme-paper .portal-bottom-dock { background: #242018; border: none; border-radius: 24px; box-shadow: 0 4px 20px rgba(0,0,0,0.4); }
html.dark .theme-paper .portal-hamburger { color: #8b7e66; }
html.dark .theme-paper .portal-sidebar__close { color: #8b7e66; }
html.dark .theme-paper .portal-context-menu { background: #242018; border: 2px solid #3a3220; box-shadow: 0 6px 24px rgba(0,0,0,0.5); border-radius: 14px; }
html.dark .theme-paper .portal-context-menu__item { color: #e8dcc8; }
html.dark .theme-paper .portal-context-menu__item:hover { color: #d4a04c; background: #2a2620; }
html.dark .theme-paper .portal-context-menu__divider { background: #3a3220; }

@media (max-width: 760px) {
  .portal-page .portal-main,
  .portal-page .portal-content,
  .portal-page .portal-section,
  .portal-page .portal-grid,
  .portal-page .portal-card {
    min-width: 0;
    max-width: 100%;
  }

  .portal-page .portal-grid {
    grid-template-columns: minmax(0, 1fr) !important;
  }

  .portal-page .portal-card {
    width: 100%;
    box-sizing: border-box;
  }
}
</style>
