<script setup lang="ts">
import { ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { PortalSite, SaveSiteSettingsRequest } from '@/types/models'

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
})

const rules: FormRules<SaveSiteSettingsRequest> = {
  title: [{ required: true, message: '请输入站点标题。', trigger: 'blur' }],
}

watch(
  () => [props.modelValue, props.site],
  () => {
    if (!props.modelValue) {
      return
    }

    form.value = {
      title: props.site.title,
      subtitle: props.site.subtitle,
      description: props.site.description,
      logoText: props.site.logoText,
      logoUrl: props.site.logoUrl ?? '',
      searchPlaceholder: props.site.searchPlaceholder,
      footerText: props.site.footerText,
      themeName: props.site.themeName,
      mobileThemeName: props.site.mobileThemeName,
      logoMode: props.site.logoMode ?? 'compact',
    }
  },
  { immediate: true },
)

async function handleSubmit() {
  await formRef.value?.validate()
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
  })
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="站点设置"
    width="680px"
    @close="emit('update:modelValue', false)"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
      <el-row :gutter="16">
        <el-col :span="16">
          <el-form-item label="站点标题" prop="title">
            <el-input v-model="form.title" maxlength="120" show-word-limit />
          </el-form-item>
        </el-col>
        <el-col :span="8">
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
        <el-col :span="12">
          <el-form-item label="搜索框提示词">
            <el-input v-model="form.searchPlaceholder" maxlength="120" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="主题名称">
            <el-input v-model="form.themeName" maxlength="40" />
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="页脚文案">
        <el-input v-model="form.footerText" maxlength="255" show-word-limit />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">保存</el-button>
    </template>
  </el-dialog>
</template>
