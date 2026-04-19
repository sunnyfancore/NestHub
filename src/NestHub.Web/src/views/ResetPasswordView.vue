<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { requestPasswordReset } from '@/api/auth'

const router = useRouter()
const loading = ref(false)
const email = ref('')
const sent = ref(false)

async function submit() {
  if (!email.value.trim()) {
    ElMessage.warning('请输入邮箱地址。')
    return
  }
  loading.value = true
  try {
    await requestPasswordReset(email.value.trim())
    sent.value = true
    ElMessage.success('重置邮件已发送，请查收邮箱。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '发送重置邮件失败。')
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
      <h1>忘记密码？</h1>
      <p>输入您的注册邮箱，我们将发送密码重置链接到您的邮箱。</p>
    </section>

    <section class="auth-panel panel-card">
      <div class="auth-panel__header">
        <div>
          <h2>找回密码</h2>
          <p>通过邮箱验证重置您的密码。</p>
        </div>
      </div>

      <template v-if="!sent">
        <el-form label-position="top" class="auth-form">
          <el-form-item label="邮箱地址">
            <el-input v-model="email" placeholder="请输入注册时使用的邮箱" @keyup.enter="submit" />
          </el-form-item>

          <el-button type="primary" class="auth-submit" :loading="loading" @click="submit">
            发送重置邮件
          </el-button>
        </el-form>
      </template>
      <template v-else>
        <div class="reset-success">
          <i class="fa fa-check-circle" style="font-size: 48px; color: #52c41a;"></i>
          <p>重置邮件已发送到 <strong>{{ email }}</strong>，请在 30 分钟内点击邮件中的链接重置密码。</p>
          <p style="color: #999; font-size: 13px;">如果没有收到，请检查垃圾邮件文件夹。</p>
        </div>
      </template>

      <div class="auth-links">
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

.auth-links {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}

.reset-success {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 16px;
  padding: 24px 0;
}

.reset-success p {
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
