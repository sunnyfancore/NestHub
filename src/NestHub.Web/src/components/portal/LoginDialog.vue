<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{
  modelValue: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  success: []
}>()

const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)

const loginForm = ref({
  email: '',
  password: '',
})

async function submit() {
  loading.value = true

  try {
    await authStore.login(loginForm.value)
    ElMessage.success('登录成功。')
    emit('update:modelValue', false)
    emit('success')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '登录失败，请检查账号密码。')
  } finally {
    loading.value = false
  }
}

function goResetPassword() {
  emit('update:modelValue', false)
  router.push('/reset-password')
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    width="420px"
    :show-close="false"
    destroy-on-close
    @close="emit('update:modelValue', false)"
  >
    <template #header>
      <div>
        <h3>登录</h3>
        <p>登录后可管理您的导航分类和链接。</p>
      </div>
    </template>

    <el-form label-position="top" class="login-dialog__form" @submit.prevent="submit">
      <el-form-item label="邮箱">
        <el-input v-model="loginForm.email" placeholder="请输入邮箱" @keyup.enter="submit" />
      </el-form-item>
      <el-form-item label="密码">
        <el-input v-model="loginForm.password" type="password" show-password placeholder="请输入密码" @keyup.enter="submit" />
      </el-form-item>
      <el-form-item>
        <el-button text type="primary" style="padding: 0; font-size: 13px;" @click="goResetPassword">忘记密码？</el-button>
      </el-form-item>
    </el-form>

    <template #footer>
      <div class="login-dialog__footer">
        <el-button @click="emit('update:modelValue', false)">取消</el-button>
        <el-button type="primary" :loading="loading" @click="submit">登录</el-button>
      </div>
    </template>
  </el-dialog>
</template>

<style scoped>
h3 {
  margin: 0;
  font-size: 20px;
  color: #1a1a2e;
}

p {
  margin: 6px 0 0;
  color: #7a8797;
  font-size: 14px;
}

.login-dialog__form {
  display: grid;
  gap: 6px;
}

.login-dialog__footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
</style>
