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
    class="login-dialog"
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

:global(.login-dialog) {
  border-radius: 18px;
}

:global(.login-dialog .el-dialog__header) {
  padding: 22px 24px 10px;
}

:global(.login-dialog .el-dialog__body) {
  padding: 12px 24px 4px;
}

:global(.login-dialog .el-dialog__footer) {
  padding: 12px 24px 22px;
}

:global(.login-dialog .el-input__wrapper) {
  border-radius: 10px;
}

@media (max-width: 760px) {
  :global(.el-overlay-dialog) {
    padding: 0 10px;
  }

  :global(.login-dialog) {
    width: 100% !important;
    max-width: 420px;
    margin: 12dvh auto 0 !important;
    border-radius: 22px;
    overflow: hidden;
    box-shadow: 0 24px 60px rgba(15, 23, 42, 0.2);
  }

  :global(.login-dialog.el-dialog .el-dialog__header) {
    padding: 24px 18px 8px;
    margin: 0;
  }

  :global(.login-dialog.el-dialog .el-dialog__body) {
    max-height: calc(100dvh - 250px);
    padding: 12px 18px 0;
    overflow-y: auto;
  }

  :global(.login-dialog.el-dialog .el-dialog__footer) {
    padding: 14px 18px 20px;
  }

  h3 {
    font-size: 22px;
    line-height: 28px;
  }

  p {
    max-width: 260px;
    font-size: 13px;
    line-height: 20px;
  }

  .login-dialog__form {
    gap: 8px;
  }

  :global(.login-dialog .el-form-item) {
    margin-bottom: 14px;
  }

  :global(.login-dialog .el-form-item__label) {
    padding-bottom: 7px;
    color: #475569;
    font-weight: 700;
  }

  :global(.login-dialog .el-input__wrapper) {
    min-height: 46px;
    border-radius: 13px;
    box-shadow: 0 0 0 1px #e5eaf1 inset;
  }

  :global(.login-dialog .el-input__inner) {
    font-size: 15px;
  }

  .login-dialog__footer {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 10px;
  }

  :global(.login-dialog .login-dialog__footer .el-button) {
    width: 100%;
    height: 44px;
    margin: 0;
    border-radius: 13px;
    font-weight: 700;
  }
}
</style>
