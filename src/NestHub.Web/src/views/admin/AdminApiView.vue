<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()

const apiExamples = computed(() => {
  const tenantId = authStore.tenant?.id || '<tenant-id>'
  return [
    {
      title: '获取门户数据',
      method: 'GET',
      url: '/api/portal',
    },
    {
      title: '获取分类列表',
      method: 'GET',
      url: '/api/admin/categories',
    },
    {
      title: '获取链接列表',
      method: 'GET',
      url: '/api/admin/links',
    },
    {
      title: '切换租户访问',
      method: 'Header',
      url: `X-Tenant-Id: ${tenantId}`,
    },
  ]
})

async function copyToken() {
  if (!authStore.token) {
    ElMessage.warning('当前没有可用的登录令牌。')
    return
  }

  await navigator.clipboard.writeText(authStore.token)
  ElMessage.success('当前登录令牌已复制。')
}
</script>

<template>
  <div class="admin-page">
    <div class="api-card">
      <h3>API 调用说明</h3>
      <p>当前后台直接使用 JWT 鉴权，你可以在调试时复制当前登录令牌配合 `Authorization: Bearer ...` 头使用。</p>

      <el-button type="primary" @click="copyToken">复制当前 JWT</el-button>

      <div class="api-list">
        <article v-for="item in apiExamples" :key="item.title" class="api-item">
          <span class="api-item__method">{{ item.method }}</span>
          <div>
            <strong>{{ item.title }}</strong>
            <code>{{ item.url }}</code>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 18px;
}

.api-card {
  padding: 20px;
  background: #fff;
  border-radius: 10px;
  border: 1px solid #e8ecf0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.api-card h3 {
  margin: 0;
  font-size: 22px;
}

.api-card p {
  margin: 8px 0 18px;
  color: #7b8798;
}

.api-list {
  margin-top: 20px;
  display: grid;
  gap: 12px;
}

.api-item {
  display: flex;
  gap: 14px;
  align-items: start;
  padding: 14px 16px;
  border-radius: 10px;
  background: #f8f9fb;
}

.api-item__method {
  min-width: 68px;
  display: inline-flex;
  justify-content: center;
  padding: 6px 10px;
  border-radius: 999px;
  background: #5b9aff;
  color: white;
  font-size: 12px;
  font-weight: 700;
}

.api-item strong {
  display: block;
  margin-bottom: 6px;
}

.api-item code {
  color: #41526b;
}
</style>
