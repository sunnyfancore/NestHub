<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { getSmtpConfig, testSmtp, updateSmtpConfig } from '@/api/security'

const loading = ref(false)
const saving = ref(false)
const sending = ref(false)
const testEmail = ref('')

const form = ref({
  host: '',
  port: 465,
  useSsl: true,
  username: '',
  password: '',
  fromEmail: '',
  fromName: 'NestHub',
})

let hasExistingPassword = false

async function load() {
  loading.value = true
  try {
    const config = await getSmtpConfig()
    form.value.host = config.host || ''
    form.value.port = config.port || 465
    form.value.useSsl = config.useSsl ?? true
    form.value.username = config.username || ''
    form.value.password = ''
    form.value.fromEmail = config.fromEmail || ''
    form.value.fromName = config.fromName || 'NestHub'
    hasExistingPassword = config.hasPassword
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取 SMTP 配置失败。')
  } finally {
    loading.value = false
  }
}

async function handleSave() {
  if (!form.value.host.trim()) {
    ElMessage.warning('请填写 SMTP 服务器地址。')
    return
  }
  saving.value = true
  try {
    await updateSmtpConfig({
      host: form.value.host,
      port: form.value.port,
      useSsl: form.value.useSsl,
      username: form.value.username,
      password: form.value.password || undefined,
      fromEmail: form.value.fromEmail,
      fromName: form.value.fromName,
    })
    ElMessage.success('SMTP 配置已保存。')
    form.value.password = ''
    hasExistingPassword = true
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '保存 SMTP 配置失败。')
  } finally {
    saving.value = false
  }
}

async function handleTest() {
  if (!testEmail.value.trim()) {
    ElMessage.warning('请输入测试邮箱地址。')
    return
  }
  sending.value = true
  try {
    await testSmtp(testEmail.value.trim())
    ElMessage.success('测试邮件已发送，请查收。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '发送测试邮件失败。')
  } finally {
    sending.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="admin-page" v-loading="loading">
    <div class="form-card">
      <h3 class="card-title">
        <i class="fa fa-envelope"></i>
        邮件服务 (SMTP)
      </h3>
      <p class="card-desc">配置 SMTP 邮件服务后，用户可以通过邮箱找回密码。</p>

      <el-form label-position="top" class="smtp-form">
        <el-form-item label="SMTP 服务器">
          <el-input v-model="form.host" placeholder="例如 smtp.qq.com" />
        </el-form-item>

        <div class="smtp-row">
          <el-form-item label="端口" class="smtp-row__port">
            <el-input-number v-model="form.port" :min="1" :max="65535" controls-position="right" />
          </el-form-item>
          <el-form-item label="使用 SSL" class="smtp-row__ssl">
            <el-switch v-model="form.useSsl" />
          </el-form-item>
        </div>

        <el-form-item label="用户名">
          <el-input v-model="form.username" placeholder="SMTP 登录用户名" />
        </el-form-item>

        <el-form-item :label="hasExistingPassword ? '密码（留空则不修改）' : '密码'">
          <el-input v-model="form.password" type="password" show-password :placeholder="hasExistingPassword ? '留空则保持原密码不变' : '请输入密码'" />
        </el-form-item>

        <el-form-item label="发件人邮箱">
          <el-input v-model="form.fromEmail" placeholder="留空则使用用户名作为发件人" />
        </el-form-item>

        <el-form-item label="发件人名称">
          <el-input v-model="form.fromName" placeholder="NestHub" />
        </el-form-item>

        <el-button type="primary" :loading="saving" @click="handleSave">保存配置</el-button>
      </el-form>
    </div>

    <div class="form-card">
      <h3 class="card-title">
        <i class="fa fa-paper-plane"></i>
        发送测试邮件
      </h3>
      <p class="card-desc">输入邮箱地址以测试 SMTP 邮件发送是否正常。</p>
      <div class="test-email-row">
        <el-input
          v-model="testEmail"
          placeholder="请输入邮箱地址"
          style="flex: 1;"
          @keyup.enter="handleTest"
        />
        <el-button type="primary" :loading="sending" @click="handleTest">
          发送测试邮件
        </el-button>
      </div>
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

.card-title {
  margin: 0 0 8px;
  font-size: 17px;
  font-weight: 700;
  color: #1a1a2e;
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-desc {
  margin: 0 0 20px;
  color: #8896a6;
  font-size: 14px;
  line-height: 1.6;
}

.smtp-form {
  display: grid;
  gap: 4px;
}

.smtp-row {
  display: flex;
  gap: 16px;
}

.smtp-row__port {
  flex: 0 0 180px;
}

.smtp-row__ssl {
  flex: 1;
  display: flex;
  align-items: flex-end;
  padding-bottom: 18px;
}

.test-email-row {
  display: flex;
  align-items: center;
  gap: 12px;
}
</style>
