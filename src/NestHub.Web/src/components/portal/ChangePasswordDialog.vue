<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed, ref } from 'vue'
import { changePassword, requestPasswordReset } from '@/api/auth'
import { useAuthStore } from '@/stores/auth'

const props = defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const authStore = useAuthStore()
const mode = ref<'password' | 'email'>('password')

const userEmail = computed(() => authStore.user?.email || '')

// password mode
const pwLoading = ref(false)
const pwForm = ref({ currentPassword: '', newPassword: '', confirmPassword: '' })

async function submitByPassword() {
  if (pwForm.value.newPassword.length < 6) {
    ElMessage.warning('新密码至少 6 位。')
    return
  }
  if (pwForm.value.newPassword !== pwForm.value.confirmPassword) {
    ElMessage.warning('两次输入的新密码不一致。')
    return
  }
  pwLoading.value = true
  try {
    await changePassword(pwForm.value.currentPassword, pwForm.value.newPassword)
    ElMessage.success('密码已修改。')
    pwForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
    emit('update:modelValue', false)
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '密码修改失败。')
  } finally {
    pwLoading.value = false
  }
}

// email mode
const emailSending = ref(false)
const emailSent = ref(false)

async function submitByEmail() {
  if (!userEmail.value) {
    ElMessage.error('无法获取当前用户邮箱。')
    return
  }
  emailSending.value = true
  try {
    await requestPasswordReset(userEmail.value)
    emailSent.value = true
    ElMessage.success('验证邮件已发送。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '发送验证邮件失败。')
  } finally {
    emailSending.value = false
  }
}

function handleClose() {
  mode.value = 'password'
  pwForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
  emailSent.value = false
  emit('update:modelValue', false)
}
</script>

<template>
  <el-dialog :model-value="modelValue" title="修改密码" width="440px" destroy-on-close @close="handleClose">
    <el-tabs v-model="mode">
      <el-tab-pane label="当前密码验证" name="password">
        <el-form label-position="top" @submit.prevent="submitByPassword">
          <el-form-item label="当前密码">
            <el-input v-model="pwForm.currentPassword" type="password" show-password @keyup.enter="submitByPassword" />
          </el-form-item>
          <el-form-item label="新密码">
            <el-input v-model="pwForm.newPassword" type="password" show-password placeholder="至少 6 位" @keyup.enter="submitByPassword" />
          </el-form-item>
          <el-form-item label="确认新密码">
            <el-input v-model="pwForm.confirmPassword" type="password" show-password @keyup.enter="submitByPassword" />
          </el-form-item>
          <el-button type="primary" :loading="pwLoading" style="width: 100%;" @click="submitByPassword">确认修改</el-button>
        </el-form>
      </el-tab-pane>

      <el-tab-pane label="邮箱验证" name="email">
        <template v-if="!emailSent">
          <p class="email-hint">
            点击下方按钮，系统将向您的邮箱 <strong>{{ userEmail }}</strong> 发送一封验证邮件，请通过邮件中的链接设置新密码。
          </p>
          <el-button type="primary" :loading="emailSending" style="width: 100%;" @click="submitByEmail">
            发送验证邮件
          </el-button>
        </template>
        <template v-else>
          <div class="email-success">
            <i class="fa fa-check-circle" style="font-size: 40px; color: #52c41a;"></i>
            <p>验证邮件已发送到 <strong>{{ userEmail }}</strong>，请在 30 分钟内点击邮件中的链接重置密码。</p>
            <p class="email-sub">如果没有收到，请检查垃圾邮件文件夹。</p>
          </div>
        </template>
      </el-tab-pane>
    </el-tabs>

    <template #footer>
      <el-button @click="handleClose">{{ emailSent ? '关闭' : '取消' }}</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.email-hint {
  margin: 0 0 16px;
  color: #666;
  font-size: 14px;
  line-height: 1.6;
}

.email-success {
  text-align: center;
  padding: 12px 0;
}

.email-success p {
  margin: 16px 0 0;
  color: #555;
  font-size: 14px;
  line-height: 1.6;
}

.email-sub {
  color: #999;
  font-size: 13px;
}
</style>
