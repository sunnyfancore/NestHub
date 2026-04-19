<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { getTransitionSetting, updateTransitionSetting, type TransitionSetting } from '@/api/transition'

const loading = ref(false)
const saving = ref(false)
const form = ref<TransitionSetting>({
  isEnabled: false,
  visitorStaySeconds: 5,
  adminStaySeconds: 1,
  adScript1: '',
  adScript2: '',
})

async function load() {
  loading.value = true

  try {
    form.value = await getTransitionSetting()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取过滤页设置失败。')
  } finally {
    loading.value = false
  }
}

async function submit() {
  saving.value = true

  try {
    form.value = await updateTransitionSetting(form.value)
    ElMessage.success('过滤页设置已保存。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '过滤页设置保存失败。')
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="admin-page" v-loading="loading">
    <div class="form-card">
      <div class="form-note">
        过滤页用于在访客点击外部链接时先展示一个停留页，你可以控制访客与管理员的停留时间，并配置两段扩展广告脚本。
      </div>

      <el-form label-position="top">
        <el-form-item label="过滤页开关">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="访客停留时间（秒）">
          <el-input-number v-model="form.visitorStaySeconds" :min="0" :max="86400" />
        </el-form-item>
        <el-form-item label="管理员停留时间（秒）">
          <el-input-number v-model="form.adminStaySeconds" :min="0" :max="86400" />
        </el-form-item>
        <el-form-item label="广告脚本 1">
          <el-input v-model="form.adScript1" type="textarea" :rows="4" />
        </el-form-item>
        <el-form-item label="广告脚本 2">
          <el-input v-model="form.adScript2" type="textarea" :rows="4" />
        </el-form-item>
        <el-button type="primary" :loading="saving" @click="submit">保存过滤页设置</el-button>
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

.form-note {
  margin-bottom: 16px;
  padding: 14px 16px;
  border-radius: 10px;
  background: #f0f5ff;
  color: #5b8ab5;
}
</style>
