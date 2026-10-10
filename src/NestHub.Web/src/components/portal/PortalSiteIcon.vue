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

let discoveryStarted = false
watch(() => [props.url, props.iconUrl], () => {
  ++generation
  discovered.value = []
  failed.value = new Set()
  discoveryStarted = false
}, { immediate: true })

function onError(event: Event) {
  const attempted = (event.target as HTMLImageElement).getAttribute('src')
  if (attempted) failed.value = new Set([...failed.value, attempted])
  // Try the configured icon or standard favicon first; discover only on failure.
  if (!discoveryStarted && directSiteIcons(props.url).length) {
    discoveryStarted = true
    const current = generation
    discoverSiteIcons(props.url).then(icons => {
      if (current === generation) discovered.value = icons
    })
  }
}

</script>

<template>
  <img v-if="source" :key="source" :src="source" :alt="title" loading="lazy" decoding="async" referrerpolicy="no-referrer" @error="onError">
  <span v-else class="portal-card__fallback">{{ letter }}</span>
</template>
