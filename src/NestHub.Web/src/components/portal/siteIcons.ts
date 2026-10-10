import axios from 'axios'

const discoveries = new Map<string, Promise<string[]>>()
const waiting: Array<() => void> = []
let active = 0

// Fetch remote icons through NestHub so HTTP authentication cannot open browser dialogs.
export function safeIconSource(source: string): string {
  if (/^data:image\/(?:png|jpeg|gif|webp|svg\+xml|x-icon|avif)[;,]/i.test(source)) return source
  try {
    const icon = new URL(source, window.location.href)
    if (!['http:', 'https:'].includes(icon.protocol) || icon.username || icon.password) return ''
    if (icon.origin === window.location.origin) return icon.href
    return `${import.meta.env.VITE_API_BASE_URL || '/api'}/site-icons/image?url=${encodeURIComponent(icon.href)}`
  } catch { return '' }
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
      new URL('favicon.ico', directory).href,
      new URL('/favicon.ico', site).href,
      new URL('/favicon.svg', site).href,
      new URL('/favicon.png', site).href,
      new URL('/apple-touch-icon.png', site).href,
    ])]
  } catch { return [] }
}
