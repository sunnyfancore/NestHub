<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { confirmPasswordReset } from '@/api/auth'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const done = ref(false)
const error = ref('')

const form = ref({
  password: '',
  confirmPassword: '',
})

const token = ref('')

onMounted(() => {
  token.value = (route.query.token as string) || ''
  if (!token.value) {
    error.value = '无效的重置链接，请重新申请密码重置。'
  }
})

async function submit() {
  if (form.value.password.length < 6) {
    ElMessage.warning('密码至少 6 位。')
    return
  }
  if (form.value.password !== form.value.confirmPassword) {
    ElMessage.warning('两次输入的密码不一致。')
    return
  }
  loading.value = true
  try {
    await confirmPasswordReset(token.value, form.value.password)
    done.value = true
    ElMessage.success('密码已重置。')
  } catch (err: any) {
    ElMessage.error(err?.response?.data?.message || '密码重置失败。')
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
      <h1>重置密码</h1>
      <p>设置您的新密码。</p>
    </section>

    <section class="auth-panel panel-card">
      <div class="auth-panel__header">
        <div>
          <h2>设置新密码</h2>
          <p>请输入新密码完成重置。</p>
        </div>
      </div>

      <template v-if="error">
        <div class="reset-error">
          <i class="fa fa-exclamation-circle" style="font-size: 48px; color: #ff4d4f;"></i>
          <p>{{ error }}</p>
        </div>
      </template>
      <template v-else-if="!done">
        <el-form label-position="top" class="auth-form" @submit.prevent="submit">
          <el-form-item label="新密码">
            <el-input v-model="form.password" type="password" show-password placeholder="至少 6 位" @keyup.enter="submit" />
          </el-form-item>
          <el-form-item label="确认新密码">
            <el-input v-model="form.confirmPassword" type="password" show-password placeholder="再次输入新密码" @keyup.enter="submit" />
          </el-form-item>

          <el-button type="primary" class="auth-submit" :loading="loading" @click="submit">
            重置密码
          </el-button>
        </el-form>
      </template>
      <template v-else>
        <div class="reset-success">
          <i class="fa fa-check-circle" style="font-size: 48px; color: #52c41a;"></i>
          <p>密码重置成功！请使用新密码登录。</p>
        </div>
      </template>

      <div class="auth-links" style="margin-top: 8px;">
        <el-button text @click="router.push('/login')">返回登录</el-button>
        <el-button text @click="router.push('/')">返回门户首页</el-button>
      </div>
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

.reset-success,
.reset-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 16px;
  padding: 24px 0;
}

.reset-success p,
.reset-error p {
  color: #555;
  font-size: 15px;
  line-height: 1.6;
  margin: 0;
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
