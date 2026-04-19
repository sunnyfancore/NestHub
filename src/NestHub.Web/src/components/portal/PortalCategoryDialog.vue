<script setup lang="ts">
import { ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { PortalCategory, PortalCategoryOption, SavePortalCategoryRequest } from '@/types/models'

const props = defineProps<{
  modelValue: boolean
  category?: PortalCategory | null
  parentOptions: PortalCategoryOption[]
  defaultParentId?: string | null
  submitting?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  submit: [payload: SavePortalCategoryRequest]
}>()

const formRef = ref<FormInstance>()
const form = ref<SavePortalCategoryRequest>({
  name: '',
  description: '',
  icon: '',
  parentId: undefined,
  sortOrder: 100,
})

const rules: FormRules<SavePortalCategoryRequest> = {
  name: [{ required: true, message: '请输入分类名称。', trigger: 'blur' }],
}

const iconPickerVisible = ref(false)
const iconSearch = ref('')

const iconGroups = [
  { label: '常用', icons: ['fa-star', 'fa-heart', 'fa-home', 'fa-bookmark', 'fa-folder', 'fa-folder-open', 'fa-tag', 'fa-flag', 'fa-bolt', 'fa-fire', 'fa-gift', 'fa-trophy', 'fa-thumbs-up', 'fa-bell', 'fa-cog', 'fa-wrench', 'fa-search', 'fa-globe', 'fa-link', 'fa-external-link'] },
  { label: '技术', icons: ['fa-code', 'fa-terminal', 'fa-database', 'fa-server', 'fa-cloud', 'fa-desktop', 'fa-laptop', 'fa-mobile', 'fa-cube', 'fa-cubes', 'fa-git', 'fa-github', 'fa-linux', 'fa-windows', 'fa-android', 'fa-html5', 'fa-css3', 'fa-jsfiddle'] },
  { label: '工具', icons: ['fa-wrench', 'fa-key', 'fa-lock', 'fa-unlock', 'fa-shield', 'fa-book', 'fa-pencil', 'fa-paint-brush', 'fa-camera', 'fa-image', 'fa-file', 'fa-file-text', 'fa-download', 'fa-upload', 'fa-print', 'fa-copy', 'fa-paste', 'fa-trash', 'fa-archive'] },
  { label: '箭头/方向', icons: ['fa-arrow-up', 'fa-arrow-down', 'fa-arrow-left', 'fa-arrow-right', 'fa-angle-up', 'fa-angle-down', 'fa-angle-left', 'fa-angle-right', 'fa-chevron-up', 'fa-chevron-down', 'fa-chevron-left', 'fa-chevron-right', 'fa-exchange', 'fa-refresh', 'fa-repeat', 'fa-expand', 'fa-compress'] },
  { label: '商业', icons: ['fa-briefcase', 'fa-building', 'fa-users', 'fa-user', 'fa-envelope', 'fa-phone', 'fa-bar-chart', 'fa-line-chart', 'fa-pie-chart', 'fa-dollar', 'fa-shopping-cart', 'fa-credit-card', 'fa-money', 'fa-calculator', 'fa-calendar', 'fa-clock', 'fa-task'] },
  { label: '媒体', icons: ['fa-play', 'fa-pause', 'fa-stop', 'fa-music', 'fa-film', 'fa-video-camera', 'fa-headphones', 'fa-microphone', 'fa-youtube', 'fa-youtube-play', 'fa-spotify', 'fa-soundcloud'] },
  { label: '交通/位置', icons: ['fa-map', 'fa-map-marker', 'fa-map-pin', 'fa-location-arrow', 'fa-car', 'fa-plane', 'fa-ship', 'fa-train', 'fa-bicycle', 'fa-bus', 'fa-compass', 'fa-anchor'] },
  { label: '其他', icons: ['fa-magic', 'fa-coffee', 'fa-gamepad', 'fa-puzzle-piece', 'fa-lightbulb', 'fa-rocket', 'fa-space-shuttle', 'fa-eye', 'fa-eye-slash', 'fa-bug', 'fa-sitemap', 'fa-columns', 'fa-table', 'fa-list', 'fa-th', 'fa-th-large', 'fa-filter', 'fa-sort', 'fa-ellipsis-h', 'fa-circle', 'fa-square'] },
]

const filteredGroups = ref(iconGroups)

watch(iconSearch, (kw) => {
  const keyword = kw.trim().toLowerCase()
  if (!keyword) {
    filteredGroups.value = iconGroups
    return
  }
  filteredGroups.value = iconGroups
    .map((g) => ({
      ...g,
      icons: g.icons.filter((ic) => ic.replace('fa-', '').includes(keyword)),
    }))
    .filter((g) => g.icons.length > 0)
})

function selectIcon(icon: string) {
  form.value.icon = icon
  iconPickerVisible.value = false
  iconSearch.value = ''
}

function clearIcon() {
  form.value.icon = ''
}

watch(
  () => [props.modelValue, props.category, props.defaultParentId],
  () => {
    if (!props.modelValue) {
      return
    }

    form.value = props.category
      ? {
          name: props.category.name,
          description: props.category.description || '',
          icon: props.category.icon || '',
          parentId: props.category.parentId || undefined,
          sortOrder: props.category.sortOrder,
        }
      : {
          name: '',
          description: '',
          icon: '',
          parentId: props.defaultParentId || undefined,
          sortOrder: 100,
        }
  },
  { immediate: true },
)

async function handleSubmit() {
  await formRef.value?.validate()
  emit('submit', {
    ...form.value,
    description: form.value.description?.trim() || '',
    icon: form.value.icon?.trim() || '',
    parentId: form.value.parentId || undefined,
  })
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="category ? '编辑分类' : '新建分类'"
    width="560px"
    @close="emit('update:modelValue', false)"
  >
    <el-form ref="formRef" :model="form" :rules="rules" label-position="top">
      <el-form-item label="分类名称" prop="name">
        <el-input v-model="form.name" maxlength="80" show-word-limit />
      </el-form-item>

      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="父级分类">
            <el-select v-model="form.parentId" clearable placeholder="顶级分类">
              <el-option
                v-for="option in parentOptions"
                :key="option.id"
                :label="option.name"
                :value="option.id"
              />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="图标">
            <div class="icon-field">
              <div class="icon-field__preview" @click="iconPickerVisible = !iconPickerVisible">
                <i v-if="form.icon" :class="['fa', form.icon]"></i>
                <span v-else class="icon-field__placeholder">选择图标</span>
              </div>
              <button v-if="form.icon" type="button" class="icon-field__clear" @click="clearIcon">
                <i class="fa fa-times"></i>
              </button>
            </div>

            <div v-if="iconPickerVisible" class="icon-picker">
              <input
                v-model="iconSearch"
                class="icon-picker__search"
                type="text"
                placeholder="搜索图标..."
              />
              <div class="icon-picker__grid">
                <div v-for="group in filteredGroups" :key="group.label" class="icon-picker__group">
                  <div class="icon-picker__label">{{ group.label }}</div>
                  <div class="icon-picker__icons">
                    <button
                      v-for="icon in group.icons"
                      :key="icon"
                      type="button"
                      class="icon-picker__item"
                      :class="{ 'is-active': form.icon === icon }"
                      :title="icon"
                      @click="selectIcon(icon)"
                    >
                      <i :class="['fa', icon]"></i>
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="分类描述">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="3"
          maxlength="255"
          show-word-limit
        />
      </el-form-item>

      <el-form-item label="排序值">
        <el-input-number v-model="form.sortOrder" :min="0" :max="9999" style="width: 100%" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="emit('update:modelValue', false)">取消</el-button>
      <el-button type="primary" :loading="submitting" @click="handleSubmit">保存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.icon-field {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  position: relative;
}

.icon-field__preview {
  flex: 1;
  height: 32px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 16px;
  color: #667eea;
  transition: border-color 0.2s;
  background: #fff;
}
.icon-field__preview:hover {
  border-color: #667eea;
}

.icon-field__placeholder {
  font-size: 13px;
  color: #c0c4cc;
}

.icon-field__clear {
  border: 0;
  background: transparent;
  color: #c0c4cc;
  cursor: pointer;
  padding: 4px;
  font-size: 12px;
}
.icon-field__clear:hover {
  color: #f56c6c;
}

.icon-picker {
  position: absolute;
  left: 0;
  right: 0;
  top: 100%;
  margin-top: 6px;
  background: #fff;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.1);
  z-index: 30;
  max-height: 320px;
  overflow-y: auto;
  padding: 12px;
}

.icon-picker__search {
  width: 100%;
  height: 32px;
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 0 10px;
  font-size: 13px;
  outline: none;
  margin-bottom: 10px;
}
.icon-picker__search:focus {
  border-color: #667eea;
}

.icon-picker__grid {
  display: grid;
  gap: 10px;
}

.icon-picker__label {
  font-size: 12px;
  color: #999;
  margin-bottom: 4px;
  font-weight: 600;
}

.icon-picker__icons {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.icon-picker__item {
  width: 32px;
  height: 32px;
  border: 1px solid transparent;
  border-radius: 4px;
  background: transparent;
  color: #666;
  font-size: 14px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: all 0.1s;
}
.icon-picker__item:hover {
  background: #f0edff;
  color: #667eea;
  border-color: #c5cae9;
}
.icon-picker__item.is-active {
  background: #667eea;
  color: #fff;
  border-color: #667eea;
}
</style>
