<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { getAdminThemes } from '@/api/admin'
import { getSiteSettings, updateSiteSettings } from '@/api/site'
import type { AdminThemeOption, SaveSiteSettingsRequest } from '@/types/models'

const loading = ref(false)
const saving = ref(false)
const CACHE_KEY = 'nesthub-portal-cache'
const themes = ref<AdminThemeOption[]>([])
const form = ref<SaveSiteSettingsRequest>({
  title: '',
  subtitle: '',
  description: '',
  logoText: '',
  searchPlaceholder: '',
  footerText: '',
  themeName: 'default2',
  mobileThemeName: 'default2',
})

function previewTheme(name: string) {
  window.open(`/?preview=${name}`, '_blank')
}

async function load() {
  loading.value = true

  try {
    themes.value = await getAdminThemes()
    form.value = await getSiteSettings()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取主题信息失败。')
  } finally {
    loading.value = false
  }
}

async function submit() {
  saving.value = true

  try {
    form.value = await updateSiteSettings(form.value)
    localStorage.removeItem(CACHE_KEY)
    ElMessage.success('主题设置已保存。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '主题设置保存失败。')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="admin-page" v-loading="loading">
    <div class="theme-config">
      <el-form inline>
        <el-form-item label="PC 主题">
          <el-select v-model="form.themeName" style="width: 220px">
            <el-option
              v-for="theme in themes"
              :key="theme.name"
              :label="theme.title"
              :value="theme.name"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="手机主题">
          <el-select v-model="form.mobileThemeName" style="width: 220px">
            <el-option
              v-for="theme in themes"
              :key="`${theme.name}-mobile`"
              :label="theme.title"
              :value="theme.name"
            />
          </el-select>
        </el-form-item>
        <el-button type="primary" :loading="saving" @click="submit">保存</el-button>
      </el-form>
    </div>

    <div class="theme-grid">
      <article v-for="theme in themes" :key="theme.name" class="theme-card">
        <div class="theme-card__thumb" @click="previewTheme(theme.name)">
          <div class="theme-preview" :class="`theme-preview--${theme.name}`">
            <aside class="theme-preview__sidebar">
              <div class="theme-preview__brand">
                <i></i>
                <b></b>
              </div>
              <div class="theme-preview__nav-list">
                <div class="theme-preview__nav is-active"></div>
                <div class="theme-preview__nav"></div>
                <div class="theme-preview__nav short"></div>
                <div class="theme-preview__nav"></div>
                <div class="theme-preview__nav short"></div>
              </div>
            </aside>
            <main class="theme-preview__main">
              <div class="theme-preview__top">
                <div class="theme-preview__search"></div>
                <div class="theme-preview__switch"></div>
              </div>
              <div class="theme-preview__content">
                <section class="theme-preview__section">
                  <div class="theme-preview__heading"></div>
                  <div class="theme-preview__grid">
                    <span v-for="item in 18" :key="item" class="theme-preview__link">
                      <i></i>
                      <b></b>
                    </span>
                  </div>
                </section>
                <section class="theme-preview__section lower">
                  <div class="theme-preview__heading"></div>
                  <div class="theme-preview__grid">
                    <span v-for="item in 12" :key="item" class="theme-preview__link">
                      <i></i>
                      <b></b>
                    </span>
                  </div>
                </section>
              </div>
            </main>
          </div>
        </div>
        <div class="theme-card__body">
          <h3>{{ theme.title }} <span class="theme-card__version">v{{ theme.version }}</span></h3>
          <p>{{ theme.description }}</p>
          <div class="theme-card__actions">
            <el-button size="small" @click="previewTheme(theme.name)">预览</el-button>
            <el-button size="small" type="primary" plain @click="form.themeName = theme.name">应用为 PC 主题</el-button>
          </div>
        </div>
      </article>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 18px;
}

.theme-config,
.theme-card {
  padding: 20px;
  background: #fff;
  border-radius: 10px;
  border: 1px solid #e8ecf0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.theme-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.theme-card__thumb {
  aspect-ratio: 16 / 9;
  overflow: hidden;
  border-radius: 8px;
  background: #f0f2f5;
  cursor: pointer;
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.theme-card__thumb:hover {
  transform: translateY(-2px);
  box-shadow: 0 16px 36px rgba(20, 31, 48, 0.12);
}

.theme-preview {
  --preview-bg: #eef0f4;
  --preview-sidebar: rgba(255, 255, 255, 0.78);
  --preview-panel: #fff;
  --preview-card: #f6f7f9;
  --preview-accent: #ff4d55;
  --preview-muted: #dfe4ec;
  --preview-line: #c9d1dd;
  --preview-gap: 10px;
  width: 100%;
  height: 100%;
  display: grid;
  grid-template-columns: 86px var(--preview-gap) minmax(0, 1fr);
  padding: 14px;
  background:
    radial-gradient(circle at 78% 12%, rgba(255, 255, 255, 0.36), transparent 28%),
    var(--preview-bg);
}

.theme-preview__sidebar {
  display: grid;
  align-content: start;
  gap: 16px;
  grid-column: 1;
  min-height: 100%;
  padding: 12px 9px;
  border-radius: 6px;
  background: var(--preview-sidebar);
  box-shadow: 0 8px 22px rgba(15, 23, 42, 0.08);
}

.theme-preview__brand {
  display: grid;
  grid-template-columns: 18px 1fr;
  align-items: center;
  gap: 7px;
}

.theme-preview__brand i {
  width: 18px;
  height: 18px;
  border-radius: 5px;
  background: var(--preview-accent);
}

.theme-preview__brand b {
  width: 42px;
  height: 8px;
  border-radius: 4px;
  background: var(--preview-line);
}

.theme-preview__nav-list {
  display: grid;
  gap: 8px;
}

.theme-preview__nav {
  width: 74px;
  height: 11px;
  border-radius: 4px;
  background: var(--preview-muted);
}

.theme-preview__nav.short {
  width: 54px;
}

.theme-preview__nav.is-active {
  width: 76px;
  height: 18px;
  background: color-mix(in srgb, var(--preview-accent) 18%, transparent);
}

.theme-preview__main {
  grid-column: 3;
  display: grid;
  grid-template-rows: 48px minmax(0, 1fr);
  gap: 10px;
  min-width: 0;
}

.theme-preview__top {
  position: relative;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  min-width: 0;
}

.theme-preview__search {
  width: 62%;
  height: 26px;
  border-radius: 6px;
  background: var(--preview-panel);
  box-shadow: 0 12px 24px rgba(15, 23, 42, 0.08);
}

.theme-preview__switch {
  position: absolute;
  right: 0;
  bottom: 0;
  width: 54px;
  height: 14px;
  border-radius: 5px;
  background: var(--preview-panel);
  box-shadow: 0 6px 16px rgba(15, 23, 42, 0.08);
}

.theme-preview__switch::before {
  content: '';
  display: block;
  width: 27px;
  height: 10px;
  margin: 2px;
  border-radius: 4px;
  background: var(--preview-accent);
}

.theme-preview__content {
  display: grid;
  gap: 10px;
  min-width: 0;
}

.theme-preview__section {
  min-width: 0;
  padding: 9px;
  border-radius: 6px;
  background: var(--preview-panel);
}

.theme-preview__section.lower {
  opacity: 0.92;
}

.theme-preview__heading {
  width: 64px;
  height: 8px;
  margin-bottom: 8px;
  border-radius: 4px;
  background: var(--preview-line);
}

.theme-preview__grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 7px 8px;
}

.theme-preview__link {
  display: grid;
  grid-template-columns: 13px 1fr;
  align-items: center;
  gap: 5px;
  height: 22px;
  min-width: 0;
  padding: 0 6px;
  border-radius: 5px;
  background: var(--preview-card);
}

.theme-preview__link i {
  width: 11px;
  height: 11px;
  border-radius: 50%;
  background: var(--preview-accent);
}

.theme-preview__link b {
  display: block;
  width: 70%;
  height: 5px;
  border-radius: 4px;
  background: var(--preview-line);
}

.theme-preview--ocean {
  --preview-bg: #f0f4f8;
  --preview-sidebar: #0a2540;
  --preview-panel: #fff;
  --preview-card: #f8fbff;
  --preview-accent: #3b82f6;
  --preview-muted: #2d4f7a;
  --preview-line: #9cb6d0;
}

.theme-preview--nord {
  --preview-bg: #eceff4;
  --preview-sidebar: #3b4252;
  --preview-panel: #e5e9f0;
  --preview-card: #edf1f6;
  --preview-accent: #88c0d0;
  --preview-muted: #4c566a;
  --preview-line: #9aa8bb;
}

.theme-preview--glass {
  --preview-bg: linear-gradient(135deg, #ede7f6 0%, #e3f2fd 50%, #e0f2f1 100%);
  --preview-sidebar: rgba(255, 255, 255, 0.5);
  --preview-panel: rgba(255, 255, 255, 0.56);
  --preview-card: rgba(255, 255, 255, 0.68);
  --preview-accent: #7c4dff;
  --preview-muted: rgba(124, 77, 255, 0.22);
  --preview-line: rgba(74, 85, 104, 0.38);
}

.theme-preview--neon {
  --preview-bg: #0a0618;
  --preview-sidebar: #080416;
  --preview-panel: #110d24;
  --preview-card: #17102f;
  --preview-accent: #00f5ff;
  --preview-muted: #1e1550;
  --preview-line: #7b61ff;
}

.theme-preview--paper {
  --preview-bg: #f5f0e8;
  --preview-sidebar: #3a3220;
  --preview-panel: #faf7f2;
  --preview-card: #fffdf9;
  --preview-accent: #c4903c;
  --preview-muted: #4a4030;
  --preview-line: #cbb998;
}

.theme-card__thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.theme-card__body h3 {
  margin: 16px 0 8px;
}

.theme-card__version {
  font-size: 12px;
  color: #999;
  font-weight: 400;
}

.theme-card__body p {
  margin: 0 0 12px;
  color: #7b8798;
  line-height: 1.6;
  font-size: 13px;
}

.theme-card__actions {
  display: flex;
  gap: 8px;
}

@media (max-width: 1100px) {
  .theme-grid {
    grid-template-columns: 1fr;
  }
}
</style>
