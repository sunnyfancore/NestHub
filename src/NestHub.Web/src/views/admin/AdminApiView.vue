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

  try {
    await navigator.clipboard.writeText(authStore.token)
    ElMessage.success('当前 API 令牌已复制，可粘贴到插件中。')
  } catch {
    ElMessage.error('无法复制，请允许浏览器访问剪贴板后重试。')
  }
}
</script>

<template>
  <div class="admin-page">
    <div class="api-card">
      <h3>浏览器快捷收藏插件</h3>
      <p>适用于 Chrome、Edge、Brave 等 Chromium 浏览器。当前插件压缩包随本站每次发布自动更新，内含安装说明。</p>
      <div class="plugin-actions">
        <a class="plugin-download" href="/downloads/nesthub-extension.zip" download="nesthub-extension.zip">下载插件压缩包</a>
        <el-button @click="copyToken">复制 API 令牌</el-button>
      </div>
      <ol class="plugin-guide">
        <li>下载并解压压缩包，保留解压目录，后续浏览器会从这里加载插件。</li>
        <li>Chrome 打开 <code>chrome://extensions</code>，Edge 打开 <code>edge://extensions</code>，开启「开发者模式」，点击「加载已解压的扩展程序」，选择包含 <code>manifest.json</code> 的目录。</li>
        <li>将「NestHub 快捷收藏」固定到工具栏。打开要收藏的网页，点击插件图标，填写本站根地址和上方复制的 API 令牌，点击「验证并连接」并允许访问本站。</li>
        <li>确认自动填入的标题和地址，选择分类，点击「添加到 NestHub」，等待收藏成功提示。</li>
        <li>想用贴边浮窗，可在插件中勾选「在网页上显示贴边收藏按钮」并授权。按钮可拖动至左右边缘并记住位置，点击「收藏」打开窗口；× 在当前页隐藏，取消勾选可全局关闭。</li>
        <li>如需快捷键，Chrome 打开 <code>chrome://extensions/shortcuts</code>，Edge 打开 <code>edge://extensions/shortcuts</code>，找到本插件手动绑定 <code>Alt+Shift+B</code>。有冲突时换用其他组合。</li>
        <li>更新时重新下载并解压到原目录，再到扩展管理页点击「重新加载」；无需重新配置连接。</li>
      </ol>
      <p>API 令牌当前为有有效期的 JWT，失效后在此重新复制并替换。插件不需要邮箱或密码。暂无分类时，先在本站创建分类，再在插件中刷新。</p>
    </div>
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

.plugin-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
}

.plugin-download {
  display: inline-block;
  padding: 8px 16px;
  border-radius: 7px;
  background: #2563eb;
  color: white;
  text-decoration: none;
}

.plugin-guide {
  padding-left: 24px;
  line-height: 1.9;
  color: #41526b;
}

.plugin-guide li { margin: 10px 0; }
.plugin-guide code { overflow-wrap: anywhere; }

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
