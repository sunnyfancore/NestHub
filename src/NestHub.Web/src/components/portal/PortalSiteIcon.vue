<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { directSiteIcons, discoverSiteIcons, safeIconSource } from './siteIcons'

const props = defineProps<{ url: string; iconUrl?: string | null; title: string }>()
const discovered = ref<string[]>([])
const failed = ref(new Set<string>())
let generation = 0
const candidates = computed(() => [...new Set([
  ...(props.iconUrl ? [props.iconUrl] : []),
  ...discovered.value,
  ...directSiteIcons(props.url),
].map(safeIconSource).filter(Boolean))])
const source = computed(() => candidates.value.find(candidate => !failed.value.has(candidate)))
const letter = computed(() => Array.from(props.title.trim())[0]?.toLocaleUpperCase() || '?')

watch(() => [props.url, props.iconUrl], async () => {
  const current = ++generation
  discovered.value = []
  failed.value = new Set()
  // Explicit custom icons always win and don't need discovery.
  if (props.iconUrl || !directSiteIcons(props.url).length) return
  const icons = await discoverSiteIcons(props.url)
  if (current === generation) discovered.value = icons
}, { immediate: true })

function onError(event: Event) {
  const img = event.target as HTMLImageElement
  const attempted = img.getAttribute('src')
  if (attempted) failed.value = new Set([...failed.value, attempted])
  // A stale custom icon can still fall back to the website's own declaration.
  if (props.iconUrl && attempted === safeIconSource(props.iconUrl)) {
    const current = generation
    discoverSiteIcons(props.url).then(icons => { if (current === generation) discovered.value = icons })
  }
}
</script>

<template>
  <img v-if="source" :key="source" :src="source" :alt="title" referrerpolicy="no-referrer" @error="onError">
  <span v-else class="portal-card__fallback">{{ letter }}</span>
</template>
