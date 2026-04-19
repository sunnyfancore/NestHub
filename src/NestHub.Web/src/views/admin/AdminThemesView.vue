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
          <img v-if="theme.screenshotUrl" :src="theme.screenshotUrl" :alt="theme.title" />
          <div v-else class="theme-card__placeholder">
            <span>{{ theme.title }}</span>
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
  border-radius: 10px;
  background: #f0f2f5;
  cursor: pointer;
  transition: opacity 0.15s;
}

.theme-card__thumb:hover {
  opacity: 0.85;
}

.theme-card__placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  font-size: 18px;
  font-weight: 700;
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
