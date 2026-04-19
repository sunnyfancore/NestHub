<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getSiteSettings, updateSiteSettings, uploadLogo } from '@/api/site'
import type { SaveSiteSettingsRequest } from '@/types/models'

const route = useRoute()
const targetTenantId = computed(() => (route.meta?.targetTenantId as string) || undefined)

const CACHE_KEY = 'nesthub-portal-cache'

const loading = ref(false)
const saving = ref(false)
const form = ref<SaveSiteSettingsRequest>({
  title: '',
  subtitle: '',
  description: '',
  logoText: '',
  logoUrl: '',
  searchPlaceholder: '',
  footerText: '',
  themeName: 'default2',
  mobileThemeName: 'default2',
  logoMode: 'compact',
  showBottomDock: true,
})

async function load() {
  loading.value = true

  try {
    form.value = await getSiteSettings(targetTenantId.value)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取站点设置失败。')
  } finally {
    loading.value = false
  }
}

async function submit() {
  saving.value = true

  try {
    form.value = await updateSiteSettings(form.value, targetTenantId.value)
    localStorage.removeItem(CACHE_KEY)
    ElMessage.success('站点设置已保存。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '站点设置保存失败。')
  } finally {
    saving.value = false
  }
}

async function handleLogoUpload(options: any) {
  try {
    const url = await uploadLogo(options.file, targetTenantId.value)
    form.value.logoUrl = url
    localStorage.removeItem(CACHE_KEY)
    ElMessage.success('Logo 上传成功。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || 'Logo 上传失败。')
  }
}

function removeLogo() {
  form.value.logoUrl = ''
}

watch(targetTenantId, load, { immediate: true })
</script>

<template>
  <div class="admin-page" v-loading="loading">
    <div class="form-card">
      <el-form label-position="top">
        <el-form-item label="站点标题">
          <el-input v-model="form.title" />
        </el-form-item>
        <el-form-item label="副标题">
          <el-input v-model="form.subtitle" placeholder="留空则不显示副标题" />
        </el-form-item>
        <el-form-item label="站点描述">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="Logo 图片">
          <div style="display: flex; align-items: center; gap: 12px; flex-wrap: wrap;">
            <el-upload
              :show-file-list="false"
              :auto-upload="true"
              :http-request="handleLogoUpload"
              accept=".png,.jpg,.jpeg,.gif,.svg,.webp"
            >
              <el-button>上传 Logo</el-button>
            </el-upload>
            <el-button v-if="form.logoUrl" type="danger" plain @click="removeLogo">删除 Logo</el-button>
          </div>
          <div v-if="form.logoUrl" style="margin-top: 12px;">
            <img :src="form.logoUrl" alt="Logo 预览" style="max-height: 80px; max-width: 240px; border-radius: 6px; border: 1px solid #e8ecf0;" />
          </div>
        </el-form-item>
        <el-form-item label="Logo 备用文字">
          <el-input v-model="form.logoText" />
        </el-form-item>
        <el-form-item label="Logo 显示模式">
          <el-radio-group v-model="form.logoMode">
            <el-radio value="compact">紧凑模式（图标+标题）</el-radio>
            <el-radio value="full">全幅模式（Logo 填充整个区域）</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="搜索提示词">
          <el-input v-model="form.searchPlaceholder" />
        </el-form-item>
        <el-form-item label="页脚文案">
          <el-input v-model="form.footerText" />
        </el-form-item>
        <el-form-item label="底部按钮区">
          <el-switch
            v-model="form.showBottomDock"
            inline-prompt
            active-text="显示"
            inactive-text="隐藏"
          />
        </el-form-item>
        <el-button type="primary" :loading="saving" @click="submit">保存站点设置</el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 18px;
}

.form-card {
  padding: 20px;
  background: #fff;
  border-radius: 10px;
  border: 1px solid #e8ecf0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}
</style>
