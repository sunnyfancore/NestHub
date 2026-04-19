<script setup lang="ts">
import { ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { PortalCategoryOption, PortalLink, SavePortalLinkRequest } from '@/types/models'

const props = defineProps<{
  modelValue: boolean
  link?: PortalLink | null
  categoryOptions: PortalCategoryOption[]
  defaultCategoryId?: string | null
  submitting?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  submit: [payload: SavePortalLinkRequest]
}>()

const formRef = ref<FormInstance>()
const form = ref<SavePortalLinkRequest>({
  categoryId: '',
  title: '',
  url: '',
  standbyUrl: '',
  description: '',
  tags: '',
  iconUrl: '',
  fontIcon: '',
  isPinned: false,
  sortOrder: 100,
})

const rules: FormRules<SavePortalLinkRequest> = {
  categoryId: [{ required: true, message: '请选择分类。', trigger: 'change' }],
  title: [{ required: true, message: '请输入标题。', trigger: 'blur' }],
  url: [
    { required: true, message: '请输入链接地址。', trigger: 'blur' },
    { type: 'url', message: '请输入有效的 URL。', trigger: 'blur' },
  ],
}

watch(
  () => [props.modelValue, props.link, props.defaultCategoryId],
  () => {
    if (!props.modelValue) {
      return
    }

    form.value = props.link
      ? {
          categoryId: props.link.categoryId,
          title: props.link.title,
          url: props.link.url,
          standbyUrl: props.link.standbyUrl || '',
          description: props.link.description || '',
          tags: props.link.tags || '',
          iconUrl: props.link.iconUrl || '',
          fontIcon: props.link.fontIcon || '',
          isPinned: props.link.isPinned,
          sortOrder: props.link.sortOrder,
        }
      : {
          categoryId: props.defaultCategoryId || '',
          title: '',
          url: '',
          standbyUrl: '',
          description: '',
          tags: '',
          iconUrl: '',
          fontIcon: '',
          isPinned: false,
          sortOrder: 100,
        }
  },
  { immediate: true },
)

async function handleSubmit() {
  await formRef.value?.validate()
  emit('submit', {
    ...form.value,
    standbyUrl: form.value.standbyUrl?.trim() || '',
    description: form.value.description?.trim() || '',
    tags: form.value.tags?.trim() || '',
    iconUrl: form.value.iconUrl?.trim() || '',
    fontIcon: form.value.fontIcon?.trim() || '',
  })
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="link ? '编辑链接' : '新建链接'"
    width="700px"
    @close="emit('update:modelValue', false)"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
      <el-form-item label="所属分类" prop="categoryId">
        <el-select v-model="form.categoryId" placeholder="请选择分类">
          <el-option
            v-for="option in categoryOptions"
            :key="option.id"
            :label="`${option.level === 2 ? '↳ ' : ''}${option.name}`"
            :value="option.id"
          />
        </el-select>
      </el-form-item>

      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="标题" prop="title">
            <el-input v-model="form.title" maxlength="120" show-word-limit />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="主链接" prop="url">
            <el-input v-model="form.url" maxlength="1024" placeholder="https://example.com" />
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="备用链接">
        <el-input v-model="form.standbyUrl" maxlength="1024" placeholder="可选，适合内外网双地址场景" />
      </el-form-item>

      <el-form-item label="描述">
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
          <el-form-item label="标签">
            <el-input v-model="form.tags" placeholder="例如：设计、文档、私有" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="图标地址">
            <el-input v-model="form.iconUrl" placeholder="可选，填写 favicon 或图标地址" />
          </el-form-item>
        </el-col>
      </el-row>

      <el-row :gutter="16">
        <el-col :span="8">
          <el-form-item label="字体图标">
            <el-input v-model="form.fontIcon" placeholder="例如：star / terminal / shield" />
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="排序值">
            <el-input-number v-model="form.sortOrder" :min="0" :max="9999" style="width: 100%" />
          </el-form-item>
        </el-col>
        <el-col :span="8">
          <el-form-item label="置顶">
            <el-switch v-model="form.isPinned" />
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>

    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">保存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.switch-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
</style>
