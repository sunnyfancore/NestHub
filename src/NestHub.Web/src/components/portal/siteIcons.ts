import axios from 'axios'

const discoveries = new Map<string, Promise<string[]>>()
const waiting: Array<() => void> = []
let active = 0
const SUCCESS_KEY = 'nesthub.icon-success.v2'
const successes = new Map<string, { source: string; expires: number }>()
try {
  const saved = JSON.parse(localStorage.getItem(SUCCESS_KEY) || '[]')
  if (Array.isArray(saved)) for (const item of saved.slice(-256)) {
    if (Array.isArray(item) && typeof item[0] === 'string' && typeof item[1]?.source === 'string' && item[1].expires > Date.now()) successes.set(item[0], item[1])
  }
} catch { /* Storage is optional; icon loading still works. */ }
export function rememberedIcon(key: string): string {
  const entry = successes.get(key)
  if (!entry || entry.expires <= Date.now()) { successes.delete(key); return '' }
  return entry.source
}
export function rememberIcon(key: string, source: string) {
  if (!source || source.startsWith('data:')) return
  successes.delete(key)
  successes.set(key, { source, expires: Date.now() + 12 * 60 * 60 * 1000 })
  if (successes.size > 256) successes.delete(successes.keys().next().value!)
  try { localStorage.setItem(SUCCESS_KEY, JSON.stringify([...successes])) } catch { /* Quota/private mode: keep the memory cache. */ }
}

// Fetch remote icons through NestHub so HTTP authentication cannot open browser dialogs.
export function safeIconSource(source: string): string {
  if (/^data:image\/(?:png|jpeg|gif|webp|svg\+xml|x-icon|avif)[;,]/i.test(source)) return source
  try {
    const icon = new URL(source, window.location.href)
    if (!['http:', 'https:'].includes(icon.protocol) || icon.username || icon.password) return ''
    if (icon.origin === window.location.origin) return icon.href
    // Known public favicon services are safe to load directly; never visit the
    // bookmarked site's authentication endpoint to obtain its default icon.
    if (icon.protocol === 'https:' && !icon.port && (
      (icon.hostname === 'favicon.png.pub' && icon.pathname.startsWith('/v1/')) ||
      (icon.hostname === 't0.gstatic.cn' && icon.pathname === '/faviconV2') ||
      (icon.hostname === 'favicon.im') ||
      (icon.hostname === 'icons.duckduckgo.com' && icon.pathname.startsWith('/ip3/'))
    )) return icon.href
    return `${import.meta.env.VITE_API_BASE_URL || '/api'}/site-icons/image?url=${encodeURIComponent(icon.href)}`
  } catch { return '' }
}

export function serviceSiteIcons(url: string): string[] {
  try {
    const site = new URL(url)
    if (!['http:', 'https:'].includes(site.protocol) || site.username || site.password) return []
    return [
      `https://favicon.png.pub/v1/${btoa(site.host)}`,
      `https://t0.gstatic.cn/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&size=32&url=${encodeURIComponent(site.origin)}`,
      `https://favicon.im/${encodeURIComponent(site.hostname)}?larger=true`,
      `https://icons.duckduckgo.com/ip3/${site.hostname}.ico`,
    ]
  } catch { return [] }
}

function pump() {
  while (active < 4 && waiting.length) waiting.shift()?.()
}

export function discoverSiteIcons(url: string): Promise<string[]> {
  const cached = discoveries.get(url)
  if (cached) return cached
  const promise = new Promise<string[]>(resolve => {
    waiting.push(() => {
      active++
      // Anonymous discovery must not attach the NestHub login token to outbound websites.
      axios.get<string[]>(`${import.meta.env.VITE_API_BASE_URL || '/api'}/site-icons`, {
        params: { url }, timeout: 6500,
      }).then(({ data }) => resolve(Array.isArray(data) ? data.filter(icon => /^https?:\/\//i.test(icon)).slice(0, 8) : []))
        .catch(() => resolve([]))
        .finally(() => { active--; pump() })
    })
    pump()
  })
  if (discoveries.size >= 512) discoveries.delete(discoveries.keys().next().value!)
  discoveries.set(url, promise)
  return promise
}

export function directSiteIcons(url: string): string[] {
  try {
    const site = new URL(url)
    if (!['http:', 'https:'].includes(site.protocol) || site.username || site.password) return []
    const directory = new URL('.', site)
    return [...new Set([
      new URL('/favicon.ico', site).href,
      new URL('favicon.ico', directory).href,
      new URL('/favicon.svg', site).href,
      new URL('/favicon.png', site).href,
      new URL('/apple-touch-icon.png', site).href,
    ])]
  } catch { return [] }
}
