<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { PortalSite, SaveSiteSettingsRequest } from '@/types/models'
import { themeOptions, resolveTheme } from '@/themes'

const props = defineProps<{
  modelValue: boolean
  site: PortalSite
  submitting?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  submit: [payload: SaveSiteSettingsRequest]
}>()

const formRef = ref<FormInstance>()
const form = ref<SaveSiteSettingsRequest>({
  title: '',
  subtitle: '',
  description: '',
  logoText: '',
  logoUrl: '',
  searchPlaceholder: '',
  footerText: '',
  themeName: '',
  mobileThemeName: '',
  logoMode: 'compact',
  showBottomDock: true,
})

const rules: FormRules<SaveSiteSettingsRequest> = {
  title: [{ required: true, message: '请输入站点标题。', trigger: 'blur' }],
}

let draftRevision = 0
watch(
  () => [props.modelValue, props.site],
  () => {
    ++draftRevision
    if (!props.modelValue) {
      return
    }

    void nextTick(() => formRef.value?.clearValidate())
    form.value = {
      title: props.site.title,
      subtitle: props.site.subtitle,
      description: props.site.description,
      logoText: props.site.logoText,
      logoUrl: props.site.logoUrl ?? '',
      searchPlaceholder: props.site.searchPlaceholder,
      footerText: props.site.footerText,
      themeName: resolveTheme(props.site.themeName),
      mobileThemeName: resolveTheme(props.site.mobileThemeName),
      logoMode: props.site.logoMode ?? 'compact',
      showBottomDock: props.site.showBottomDock ?? true,
    }
  },
  { immediate: true },
)

const validating = ref(false)
function beforeClose(done: () => void) {
  if (!props.submitting && !validating.value) done()
}
async function handleSubmit() {
  if (props.submitting || validating.value || !formRef.value) return
  const revision = draftRevision
  validating.value = true
  try {
    const valid = await formRef.value.validate().catch(() => false)
    if (!valid || revision !== draftRevision || !props.modelValue || props.submitting) return
    emit('submit', {
      ...form.value,
      subtitle: form.value.subtitle?.trim() || '',
      description: form.value.description?.trim() || '',
      logoText: form.value.logoText?.trim() || '',
      logoUrl: form.value.logoUrl?.trim() || '',
      searchPlaceholder: form.value.searchPlaceholder?.trim() || '',
      footerText: form.value.footerText?.trim() || '',
      themeName: form.value.themeName?.trim() || '',
      mobileThemeName: form.value.mobileThemeName?.trim() || '',
      logoMode: form.value.logoMode?.trim() || 'compact',
      showBottomDock: form.value.showBottomDock ?? true,
    })
  } finally {
    validating.value = false
  }
}
</script>

<template>
  <el-dialog
    class="nesthub-editor-dialog"
    destroy-on-close
    :before-close="beforeClose"
    :close-on-click-modal="false"
    :close-on-press-escape="!submitting && !validating"
    :show-close="!submitting && !validating"
    :model-value="modelValue"
    title="站点设置"
    width="680px"
    @close="emit('update:modelValue', false)"
  >
    <el-form :disabled="submitting || validating" ref="formRef" :model="form" :rules="rules" label-position="top">
      <el-row :gutter="16">
        <el-col :xs="24" :sm="16">
          <el-form-item label="站点标题" prop="title">
            <el-input v-model="form.title" maxlength="120" show-word-limit />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="8">
          <el-form-item label="Logo 文字">
            <el-input v-model="form.logoText" maxlength="32" />
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="副标题">
        <el-input v-model="form.subtitle" maxlength="255" show-word-limit />
      </el-form-item>

      <el-form-item label="站点描述">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="3"
          maxlength="500"
          show-word-limit
        />
      </el-form-item>

      <el-row :gutter="16">
        <el-col :xs="24" :sm="12">
          <el-form-item label="搜索框提示词">
            <el-input v-model="form.searchPlaceholder" maxlength="120" />
          </el-form-item>
        </el-col>
        <el-col :xs="24" :sm="12">
          <el-form-item label="桌面主题">
            <el-select v-model="form.themeName" style="width: 100%">
              <el-option v-for="theme in themeOptions" :key="theme.name" :label="theme.title" :value="theme.name" />
            </el-select>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="手机主题">
        <el-select v-model="form.mobileThemeName" style="width: 100%">
          <el-option v-for="theme in themeOptions" :key="theme.name" :label="theme.title" :value="theme.name" />
        </el-select>
      </el-form-item>

      <el-form-item label="页脚文案">
        <el-input v-model="form.footerText" maxlength="255" show-word-limit />
      </el-form-item>
      <el-form-item label="底部按钮区">
        <el-switch
          v-model="form.showBottomDock"
          inline-prompt
          active-text="显示"
          inactive-text="隐藏"
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button :disabled="submitting || validating" @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="submitting || validating" @click="handleSubmit">保存</el-button>
    </template>
  </el-dialog>
</template>
