<script setup lang="ts">
import axios from 'axios'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'

interface Info { categoryName: string; requiresPassword: boolean; expireAt: string | null }
interface Content { categories: { name: string; links: { title: string; url: string; description?: string }[] }[] }
const route = useRoute()
// Public password failures must not trigger the application's login/token renewal interceptor.
const client = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL || '/api', timeout: 15000 })
const info = ref<Info | null>(null), content = ref<Content | null>(null)
const password = ref(''), error = ref(''), loading = ref(false), keyword = ref('')
const retrySeconds = ref(0)
let retryTimer: ReturnType<typeof setInterval> | undefined
let controller: AbortController | null = null
const code = computed(() => String(route.params.code || ''))
const groups = computed(() => content.value?.categories.map(group => ({ ...group, links: group.links.filter(link =>
  `${link.title} ${link.description || ''} ${link.url}`.toLocaleLowerCase().includes(keyword.value.trim().toLocaleLowerCase()),
) })).filter(group => group.links.length) || [])
const count = computed(() => content.value?.categories.reduce((total, group) => total + group.links.length, 0) || 0)
const matchedCount = computed(() => groups.value.reduce((total, group) => total + group.links.length, 0))
function safeUrl(url: string) { try { const parsed = new URL(url); return ['http:', 'https:'].includes(parsed.protocol) && !parsed.username && !parsed.password ? parsed.href : undefined } catch { return undefined } }
function expiry(value: string) { return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z').toLocaleString() }
function clearRetry() { if (retryTimer) clearInterval(retryTimer); retryTimer = undefined; retrySeconds.value = 0 }
function handleFailure(e: any, fallback: string) {
  error.value = e.response?.data?.message || fallback
  if ([403, 404].includes(e.response?.status)) content.value = null
  if (e.response?.status === 404) { info.value = null; password.value = '' }
  if (e.response?.status === 429) {
    clearRetry()
    const delay = Number(e.response.headers?.['retry-after'])
    retrySeconds.value = Number.isFinite(delay) && delay > 0 ? Math.min(Math.ceil(delay), 300) : 60
    retryTimer = setInterval(() => { if (--retrySeconds.value <= 0) clearRetry() }, 1000)
  }
  if (e.response?.status === 403) nextTick(() => document.querySelector<HTMLInputElement>('#share-password')?.focus())
}

async function read() {
  if (loading.value || retrySeconds.value) return
  controller?.abort()
  const request = new AbortController(); controller = request
  loading.value = true; error.value = ''
  try {
    const response = await client.post<Content>(`/shares/public/${encodeURIComponent(code.value)}/access`, { password: password.value }, { signal: request.signal })
    if (!request.signal.aborted) content.value = response.data
  } catch (e: any) {
    if (request.signal.aborted) return
    handleFailure(e, '读取失败，请检查网络后重试。已显示的内容可能不是最新版本。')
  } finally { if (!request.signal.aborted) loading.value = false }
}
async function load() {
  clearRetry()
  controller?.abort()
  const request = new AbortController(); controller = request
  loading.value = true; error.value = ''; info.value = null; content.value = null; password.value = ''; keyword.value = ''
  try { const response = await client.get<Info>(`/shares/public/${encodeURIComponent(code.value)}`, { signal: request.signal }); if (!request.signal.aborted) info.value = response.data }
  catch (e: any) { if (!request.signal.aborted) handleFailure(e, '无法读取分享，请检查网络后重试。') }
  finally { if (!request.signal.aborted) loading.value = false }
  if (!request.signal.aborted && info.value && !info.value.requiresPassword) await read()
}
let previousReferrer: string | null = null, referrerMeta: HTMLMetaElement | null = null, addedReferrer = false
let robotsMeta: HTMLMetaElement | null = null
onMounted(() => {
  referrerMeta = document.querySelector('meta[name="referrer"]')
  previousReferrer = referrerMeta?.content ?? null
  if (!referrerMeta) { referrerMeta = document.createElement('meta'); referrerMeta.name = 'referrer'; document.head.append(referrerMeta); addedReferrer = true }
  referrerMeta.content = 'no-referrer'
  robotsMeta = document.createElement('meta'); robotsMeta.name = 'robots'; robotsMeta.content = 'noindex,nofollow'; document.head.append(robotsMeta)
})
onBeforeUnmount(() => { controller?.abort(); clearRetry(); password.value = ''; robotsMeta?.remove(); if (addedReferrer) referrerMeta?.remove(); else if (referrerMeta && previousReferrer !== null) referrerMeta.content = previousReferrer })
watch(code, load, { immediate: true })
</script>

<template>
  <main class="share-page">
    <header class="share-heading"><span class="share-brand">NestHub · 书签分享</span><h1>{{ info?.categoryName || '查看分享' }}</h1><p v-if="info">{{ info.expireAt ? `有效期至 ${expiry(info.expireAt)}` : '分享长期有效' }}</p></header>
    <section v-if="error" class="share-notice" role="alert"><p>{{ error }}</p><button :disabled="loading || retrySeconds > 0" @click="info ? read() : load()">{{ retrySeconds ? `${retrySeconds} 秒后可重试` : '重新尝试' }}</button></section>
    <section v-if="info?.requiresPassword && !content" class="share-unlock">
      <h2>请输入访问密码</h2><p>此分享受密码保护，无需登录 NestHub。</p>
      <form @submit.prevent="read"><label for="share-password">访问密码</label><input id="share-password" v-model="password" type="password" maxlength="32" autocomplete="off" :disabled="loading" required /><button :disabled="loading || retrySeconds > 0">{{ loading ? '正在验证…' : '查看书签' }}</button></form>
    </section>
    <p v-if="loading" class="share-status" role="status">正在读取分享…</p>
    <template v-if="content">
      <div class="share-toolbar"><label class="share-filter">搜索分享内的书签<input v-model="keyword" type="search" placeholder="搜索标题、描述或网址" /></label><button :disabled="loading || retrySeconds > 0" @click="read">{{ loading ? '正在刷新…' : '刷新内容' }}</button></div>
      <p class="share-status" aria-live="polite">{{ keyword.trim() ? `找到 ${matchedCount} / ${count} 个书签` : `共 ${count} 个书签` }} · 分享内容随原分类更新</p>
      <section v-for="(group, index) in groups" :key="index" class="share-group"><h2>{{ group.name }}</h2><div class="share-grid"><a v-for="(link, i) in group.links" :key="i" :href="safeUrl(link.url)" target="_blank" rel="noopener noreferrer" referrerpolicy="no-referrer"><strong>{{ link.title }}</strong><p v-if="link.description">{{ link.description }}</p><small>{{ link.url }}</small></a></div></section>
      <p v-if="!groups.length" class="share-empty">{{ keyword ? '没有匹配的书签，试试其他关键词。' : '这个分类暂时没有可分享的书签。' }}</p>
    </template>
    <footer>仅展示分享者选定分类及其子分类 · 不包含后台管理权限</footer>
  </main>
</template>

<style scoped>
.share-page { min-height:100dvh;padding:40px max(20px,calc((100vw - 1120px)/2));background:#f6f8f7;color:#243a32; }
.share-heading { margin-bottom:28px; }.share-brand { font-size:13px;color:#168575;font-weight:600; }h1 { margin:12px 0 8px;font-size:30px;overflow-wrap:anywhere; }h2 { font-size:18px;overflow-wrap:anywhere; }.share-heading p,.share-status,footer { color:#64796d;font-size:13px;line-height:1.7; }
.share-unlock,.share-notice { max-width:480px;padding:24px;border:1px solid #dfe8e2;border-radius:16px;background:white;margin-bottom:20px; }.share-unlock p { font-size:13px;color:#64796d; }.share-unlock form { display:flex;flex-direction:column;gap:12px; }.share-unlock label,.share-filter { font-size:13px; }input { width:100%;min-height:44px;border:1px solid #cad9d0;border-radius:9px;padding:10px 12px;font-size:16px;background:white;color:#243a32; }button { min-height:44px;padding:10px 16px;border:1px solid #168575;border-radius:9px;background:#168575;color:white;cursor:pointer; }button:disabled { opacity:.6;cursor:wait; }button:focus-visible,input:focus-visible,a:focus-visible { outline:2px solid #168575;outline-offset:3px; }
.share-notice { background:#fffaf0;border-color:#e8d6b1;color:#705526; }.share-notice p { overflow-wrap:anywhere; }.share-toolbar { display:flex;align-items:end;gap:12px;justify-content:space-between; }.share-filter { display:flex;flex-direction:column;gap:8px;max-width:480px;flex:1; }.share-group { margin-top:28px; }.share-grid { display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px; }.share-grid a { display:block;min-width:0;padding:20px;background:white;border:1px solid #dfe8e2;border-radius:12px;overflow-wrap:anywhere; }.share-grid a:hover { border-color:#168575; }.share-grid strong { font-size:15px; }.share-grid p { font-size:13px;color:#64796d;line-height:1.7; }.share-grid small { display:block;margin-top:12px;font-size:12px;color:#64796d; }.share-empty { padding:36px;text-align:center;color:#64796d; }footer { margin-top:36px;padding-top:20px;border-top:1px solid #dfe8e2; }
@media(max-width:760px) { .share-page { padding:24px 16px; }.share-grid { grid-template-columns:1fr; }h1 { font-size:24px; }.share-toolbar { flex-wrap:wrap; }.share-unlock,.share-notice { padding:20px; } }
</style>
