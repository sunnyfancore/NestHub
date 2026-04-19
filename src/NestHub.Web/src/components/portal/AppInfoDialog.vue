<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref, watch } from 'vue'
import { getAppInfo, type AppInfo } from '@/api/app'

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const loading = ref(false)
const appInfo = ref<AppInfo | null>(null)

async function loadInfo() {
  loading.value = true

  try {
    appInfo.value = await getAppInfo()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取系统信息失败。')
  } finally {
    loading.value = false
  }
}

watch(
  () => props.modelValue,
  (value) => {
    if (value) {
      void loadInfo()
    }
  },
)

onMounted(() => {
  if (props.modelValue) {
    void loadInfo()
  }
})
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    title="系统信息"
    width="520px"
    @close="emit('update:modelValue', false)"
  >
    <div v-loading="loading" class="app-info-grid">
      <div class="app-info-item">
        <span>当前租户</span>
        <strong>{{ appInfo?.tenantName || '--' }}</strong>
      </div>
      <div class="app-info-item">
        <span>运行时版本</span>
        <strong>{{ appInfo?.runtimeVersion || '--' }}</strong>
      </div>
      <div class="app-info-item">
        <span>应用版本</span>
        <strong>{{ appInfo?.appVersion || '--' }}</strong>
      </div>
      <div class="app-info-item">
        <span>分类数量</span>
        <strong>{{ appInfo?.categoryCount ?? 0 }}</strong>
      </div>
      <div class="app-info-item">
        <span>链接数量</span>
        <strong>{{ appInfo?.linkCount ?? 0 }}</strong>
      </div>
    </div>

    <template #footer>
      <el-button @click="emit('update:modelValue', false)">关闭</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.app-info-grid {
  display: grid;
  gap: 14px;
}

.app-info-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  padding: 14px 16px;
  border-radius: 14px;
  background: #f8f9fb;
  border: 1px solid #e8ecf0;
}

.app-info-item span {
  color: #7b8798;
}

.app-info-item strong {
  color: #182333;
}
</style>
