<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const chromeStoreUrl = 'https://chromewebstore.google.com/detail/nesthub/anlhoeladccnfaehoinbofpjiakgiiff'
const siteAddress = window.location.origin

async function copyAddress(value: string) {
  try {
    await navigator.clipboard.writeText(value)
    ElMessage.success('地址已复制。')
  } catch {
    ElMessage.error('无法复制，请手动复制页面中的地址。')
  }
}

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
    <div class="api-card plugin-card">
      <div class="plugin-heading">
        <img src="/favicon.svg" alt="" class="plugin-logo">
        <div><span class="plugin-eyebrow">NestHub 浏览器扩展</span><h3>好网页，随手收藏</h3></div>
      </div>
      <p class="plugin-intro">自动带入网页标题和地址，按分类保存到你的导航。支持 Chrome、Edge、Brave，也可以开启网页贴边收藏按钮。</p>
      <div class="installation-methods">
        <section class="installation-method">
          <div class="method-heading"><h4>商店安装</h4><span class="method-badge">自动更新</span></div>
          <p>打开 Chrome 商店详情页，点击「添加至 Chrome」，再将插件固定到工具栏。</p>
          <a class="plugin-download store-link" :href="chromeStoreUrl" target="_blank" rel="noopener noreferrer">前往 Chrome 商店 <span aria-hidden="true">↗</span></a>
          <small>此入口指向公开详情页。若插件尚在审核或商店暂不可访问，可使用手动安装方式。</small>
        </section>
        <section class="installation-method">
          <div class="method-heading"><h4>手动安装</h4><span class="method-badge neutral">本站发布包</span></div>
          <p>下载插件压缩包，通过浏览器开发者模式加载。压缩包随本站发布自动更新。</p>
          <a class="plugin-download manual-link" href="/downloads/nesthub-extension.zip" download="nesthub-extension.zip">下载插件 ZIP <span aria-hidden="true">↓</span></a>
          <ol class="plugin-guide manual-guide">
            <li><strong>解压到固定目录。</strong>不要直接选择 ZIP，也不要安装后删除解压目录。</li>
            <li><strong>打开扩展管理页。</strong>复制下方地址，粘贴到浏览器地址栏并回车。
              <div class="copy-row"><code>chrome://extensions</code><button type="button" @click="copyAddress('chrome://extensions')">复制</button></div>
              <div class="copy-row"><code>edge://extensions</code><button type="button" @click="copyAddress('edge://extensions')">复制</button></div>
            </li>
            <li><strong>加载插件。</strong>开启「开发者模式」，点击「加载已解压的扩展程序」，选择包含 <code>manifest.json</code> 的目录。</li>
            <li><strong>更新版本。</strong>关闭插件收藏窗口，将新版解压覆盖到原目录，点击扩展管理页的「重新加载」，并刷新已打开的网页。</li>
          </ol>
        </section>
      </div>
      <section class="connection-guide">
        <h4>安装后，连接你的 NestHub</h4>
        <div class="plugin-actions">
          <div class="site-address"><span>本站地址</span><code>{{ siteAddress }}</code></div>
          <el-button @click="copyAddress(siteAddress)">复制站点地址</el-button>
          <el-button type="primary" @click="copyToken">复制 API 令牌</el-button>
        </div>
        <ol class="plugin-guide">
          <li>点击工具栏中的「NestHub 快捷收藏」，粘贴本站地址和 API 令牌，点击「验证并连接」，允许访问本站。</li>
          <li>打开要收藏的网页，确认标题和地址，选择分类并点击「添加到 NestHub」。描述、标签在「添加描述和标签」中选填。</li>
          <li>如需贴边按钮，展开插件中的「插件设置」，勾选「在网页上显示贴边收藏按钮」并授权。按钮可拖动到左右边缘，位置会自动保存。</li>
        </ol>
        <details class="plugin-faq"><summary>快捷键与常见问题</summary>
          <p>快捷键未生效？在 <code>chrome://extensions/shortcuts</code> 或 <code>edge://extensions/shortcuts</code> 中手动绑定 <code>Alt+Shift+B</code>，有冲突时换一个组合。</p>
          <p>令牌无效或过期？在此重新复制并替换。插件无需邮箱和密码。没有分类？先在本站创建分类，再在插件中刷新。</p>
          <p>浮窗未显示？确认已开启并允许访问网页，更新插件后刷新网页。浏览器内部页和扩展商店禁止注入浮窗。</p>
        </details>
      </section>
    </div>
    <div class="api-card">
      <h3>API 调用说明</h3>
      <p>API 使用 Bearer JWT 鉴权。复制当前令牌，在请求中添加 <code>Authorization: Bearer ...</code> 即可使用。</p>

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

.plugin-card { padding: 26px; }
.plugin-heading { display: flex; align-items: center; gap: 13px; }
.plugin-logo { width: 46px; height: 46px; border-radius: 13px; }
.plugin-eyebrow { color: #9097a3; font-size: 12px; }
.plugin-heading h3 { margin-top: 4px; }
.api-card .plugin-intro { margin-top: 16px; color: #737c8b; line-height: 1.8; }
.installation-methods { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); align-items: start; gap: 18px; margin: 20px 0; }
.installation-method { padding: 21px; border: 1px solid #eceef2; border-radius: 15px; background: #fafbfc; }
.method-heading { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.plugin-card h4 { font-size: 16px; margin: 0; color: #263043; }
.method-badge { font-size: 11px; padding: 4px 8px; color: #b64957; background: #ffeaed; border-radius: 6px; }
.method-badge.neutral { color: #768090; background: #edf0f5; }
.installation-method p { font-size: 13px; line-height: 1.8; margin: 12px 0 16px; }
.installation-method small { display: block; margin-top: 13px; color: #9097a3; font-size: 12px; line-height: 1.8; }
.plugin-download.store-link { background: #ee5261; }
.plugin-download.manual-link { background: white; color: #4e596b; border: 1px solid #dde2ea; }
.plugin-download { display: inline-flex; align-items: center; gap: 20px; }
.copy-row { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 6px 9px; background: #fff; border: 1px solid #eceef2; border-radius: 7px; margin-top: 7px; }
.copy-row code { font-size: 12px; word-break: break-all; }
.copy-row button { border: 0; background: transparent; color: #ce4c5b; cursor: pointer; flex-shrink: 0; }
.manual-guide { margin-bottom: 0; font-size: 13px; }
.connection-guide { border-top: 1px solid #eceef2; padding-top: 22px; }
.connection-guide .plugin-actions { margin-top: 15px; }
.site-address { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; padding: 8px 12px; border: 1px solid #eceef2; border-radius: 8px; font-size: 12px; }
.site-address span { color: #9097a3; }.site-address code { word-break: break-all; }
.plugin-faq { border-top: 1px solid #eceef2; padding-top: 14px; color: #738092; }
.plugin-faq summary { cursor: pointer; font-size: 13px; }
.plugin-faq p { font-size: 13px; line-height: 1.9; margin-top: 12px; }
@media (max-width: 800px) { .installation-methods { grid-template-columns: 1fr; }.plugin-card { padding: 18px; } }

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
