<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

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
    ElMessage.success('欢迎回来。')
    await router.push('/')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '请求失败，请稍后重试。')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-shell">
    <section class="auth-copy">
      <span class="auth-badge">
        <span class="brand-dot" />
        NestHub 导航门户
      </span>
      <h1>把导航站当成首页，而不是只做一个书签表。</h1>
      <p>
        登录后可以管理您的导航分类和链接，打造属于自己的导航门户。
      </p>
    </section>

    <section class="auth-panel panel-card">
      <div class="auth-panel__header">
        <div>
          <h2>登录</h2>
          <p>使用已有账号进入导航站。</p>
        </div>
      </div>

      <el-form label-position="top" class="auth-form" @submit.prevent="submit">
        <el-form-item label="邮箱">
          <el-input v-model="loginForm.email" @keyup.enter="submit" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="loginForm.password" type="password" show-password @keyup.enter="submit" />
        </el-form-item>

        <el-button type="primary" class="auth-submit" :loading="loading" @click="submit">
          登录
        </el-button>

        <div class="auth-links">
          <el-button text @click="router.push('/reset-password')">忘记密码？</el-button>
          <el-button text @click="router.push('/')">返回门户首页</el-button>
        </div>
      </el-form>
    </section>
  </div>
</template>

<style scoped>
.auth-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: 1.15fr 0.85fr;
  align-items: stretch;
}

.auth-copy {
  padding: 64px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.auth-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  align-self: flex-start;
  padding: 10px 14px;
  border-radius: 999px;
  background: rgba(91, 154, 255, 0.12);
  color: #5b9aff;
  font-weight: 700;
}

.auth-copy h1 {
  margin: 24px 0 0;
  font-size: clamp(36px, 4vw, 62px);
  line-height: 1.02;
  letter-spacing: -0.05em;
}

.auth-copy p {
  margin: 20px 0 0;
  max-width: 640px;
  color: var(--text-muted);
  font-size: 18px;
}

.auth-panel {
  margin: 24px;
  padding: 32px;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.auth-panel__header {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  align-items: start;
  margin-bottom: 24px;
}

.auth-panel__header h2 {
  margin: 0;
  font-size: 28px;
}

.auth-panel__header p {
  margin: 8px 0 0;
  color: var(--text-muted);
}

.auth-form {
  display: grid;
  gap: 8px;
}

.auth-submit {
  width: 100%;
  height: 48px;
}

.auth-links {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

@media (max-width: 960px) {
  .auth-shell {
    grid-template-columns: 1fr;
  }

  .auth-copy {
    padding: 32px 24px 8px;
  }

  .auth-panel {
    margin: 16px;
    padding: 24px;
  }
}
</style>
