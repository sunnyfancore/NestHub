<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { serviceSiteIcons, safeIconSource, rememberedIcon, rememberIcon } from './siteIcons'

const props = defineProps<{ url: string; iconUrl?: string | null; title: string }>()
const failed = ref(new Set<string>())
const cacheKey = computed(() => JSON.stringify([props.url, props.iconUrl || '']))
const remembered = ref('')
const candidates = computed(() => [...new Set([
  remembered.value,
  ...(props.iconUrl ? [props.iconUrl] : []),
  ...serviceSiteIcons(props.url),
].map(safeIconSource).filter(Boolean))])
const source = computed(() => candidates.value.find(candidate => !failed.value.has(candidate)))
const letter = computed(() => Array.from(props.title.trim())[0]?.toLocaleUpperCase() || '?')

watch(() => [props.url, props.iconUrl], () => {
  failed.value = new Set()
  remembered.value = rememberedIcon(cacheKey.value)
}, { immediate: true })

function onError(event: Event) {
  const attempted = (event.target as HTMLImageElement).getAttribute('src')
  if (attempted) failed.value = new Set([...failed.value, attempted])
}

function onLoad(event: Event) {
  const loaded = (event.target as HTMLImageElement).getAttribute('src')
  if (loaded && loaded === source.value) rememberIcon(cacheKey.value, loaded)
}

</script>

<template>
  <img v-if="source" :key="source" :src="source" :alt="title" loading="lazy" decoding="async" referrerpolicy="no-referrer" @error="onError" @load="onLoad">
  <span v-else class="portal-card__fallback">{{ letter }}</span>
</template>
