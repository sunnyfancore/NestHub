<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '@/api/http'

interface UpdateStatus {
  currentRevision: string; latestRevision: string; publishedAt: string | null
  hasUpdate: boolean; updateEnabled: boolean; workflowUrl: string; disabledReason?: string | null; updateState?: string | null; updateMessage?: string | null; updateTaskUpdatedAt?: string | null
}
const info = ref<UpdateStatus | null>(null)
const checking = ref(false)
const updating = ref(false)
const hint = ref('')
let timer: ReturnType<typeof setTimeout> | undefined
let disposed = false
function revision(value?: string) { return value && value !== 'local' ? value.slice(0, 7) : '本地构建' }
function reloadPage() { window.location.reload() }
async function check() {
  checking.value = true
  try { info.value = (await http.get<UpdateStatus>('/system-updates')).data }
  catch (error: any) { ElMessage.error(error?.response?.data?.message || '检查更新失败，请稍后重试。') }
  finally { checking.value = false }
}
async function waitForUpdate(previous: string, started: number) {
  if (disposed) return
  try {
    const result = (await http.get<UpdateStatus>('/system-updates')).data
    if (disposed) return
    info.value = result
    if (result.updateTaskUpdatedAt && Date.parse(result.updateTaskUpdatedAt) >= started && ['pulling', 'replacing'].includes(result.updateState || '')) hint.value = result.updateMessage || '更新任务正在执行。'
    if (result.updateTaskUpdatedAt && Date.parse(result.updateTaskUpdatedAt) >= started && ['failed', 'unchanged'].includes(result.updateState || '')) {
      updating.value = false
      hint.value = result.updateMessage || '更新任务已结束，请检查部署配置。'
      return
    }
    if (result.currentRevision !== previous) {
      updating.value = false
      hint.value = '更新完成，请刷新页面使用新版。'
      ElMessage.success('应用更新完成。')
      return
    }
  } catch { /* A short interruption is expected during container replacement. */ }
  if (disposed) return
  if (Date.now() - started > 20 * 60 * 1000) {
    updating.value = false
    hint.value = '尚未确认新版启动，请检查更新服务日志后再试。'
    return
  }
  timer = setTimeout(() => waitForUpdate(previous, started), 5000)
}
async function apply() {
  if (!info.value?.updateEnabled || updating.value) return
  try {
    await ElMessageBox.confirm('更新会短暂重启应用，数据库和上传文件保留。请先完成备份。', '更新到最新版', { confirmButtonText: '开始更新', cancelButtonText: '取消', type: 'warning' })
  } catch { return }
  const previous = info.value.currentRevision
  updating.value = true
  hint.value = '正在启动内置更新任务，请保持页面打开。'
  const started = Date.now()
  try {
    await http.post('/system-updates')
  } catch (error: any) {
    if (error.response) {
      updating.value = false
      hint.value = error.response.data?.message || '更新请求失败。'
      ElMessage.error(hint.value)
      return
    }
    // The old container may have stopped before it could return the acknowledgement.
  }
  timer = setTimeout(() => waitForUpdate(previous, started), 5000)
}
onMounted(check)
onBeforeUnmount(() => { disposed = true; if (timer) clearTimeout(timer) })
</script>

<template>
  <div class="updates-page">
    <section class="update-card">
      <span class="update-eyebrow">NESTHUB</span>
      <h2>让导航保持新鲜</h2>
      <p>检查新版本，在这里完成更新。</p>
      <div class="version-grid">
        <div><span>当前版本</span><strong>{{ revision(info?.currentRevision) }}</strong></div>
        <div><span>最新版本</span><strong>{{ info?.latestRevision ? revision(info.latestRevision) : '待检查' }}</strong></div>
      </div>
      <p v-if="info?.publishedAt" class="published">最近发布：{{ new Date(info.publishedAt).toLocaleString('zh-CN') }}</p>
      <el-alert v-if="info && !info.updateEnabled" title="后台更新尚未启用" type="warning" :closable="false" show-icon
        :description="info.disabledReason || '当前运行容器缺少更新配置。请按下方说明补齐 Compose 并重新创建容器，单独拉取镜像不会启用此功能。'" />
      <div class="update-actions">
        <el-button :loading="checking" :disabled="updating" @click="check">检查更新</el-button>
        <el-button type="primary" :loading="updating" :disabled="!info?.updateEnabled || !info?.hasUpdate" @click="apply">{{ info && !info.updateEnabled ? '需先配置更新服务' : '更新到最新版' }}</el-button>
        <el-button v-if="hint === '更新完成，请刷新页面使用新版。'" @click="reloadPage">刷新页面</el-button>
      </div>
      <p v-if="hint" class="update-hint" role="status">{{ hint }}</p>
      <p v-else-if="info && !info.hasUpdate">{{ info.currentRevision && info.currentRevision !== 'local' ? '当前已是最新版。' : '本地构建请先使用正式 Docker 镜像。' }}</p>
    </section>
    <section v-if="info && !info.updateEnabled" class="setup-card">
      <h3>首次启用后台更新</h3>
      <p>新版 SQLite 部署只运行一个 NestHub 容器。内置更新通过 Docker socket 管理当前应用；更新期间启动临时任务容器，完成后自动删除。</p>
      <pre>container_name: nesthub
user: "0:0"
environment:
  Updates__Enabled: "true"
  Updates__ContainerName: nesthub
volumes:
  - ./data/app:/app/AppData
  - /var/run/docker.sock:/var/run/docker.sock</pre>
      <p>保留原有数据库、端口、JWT 和目录挂载，配置容器名称与 Docker socket。此功能让应用具有管理 Docker 的权限，仅超级管理员可触发更新。</p>
      <p><a href="https://github.com/sunnyfancore/NestHub/blob/main/docs/docker-deployment.md" target="_blank" rel="noopener noreferrer">查看完整 Compose 和目录挂载说明</a></p>
      <pre>docker compose up -d --force-recreate nesthub</pre>
      <p>从旧方案升级时移除 Compose 中的 updater 服务，再使用 docker compose up -d --remove-orphans 清理旧更新容器。新版不需要 Watchtower 或 UPDATE_API_TOKEN。</p>
    </section>
  </div>
</template>

<style scoped>
.updates-page{display:grid;gap:20px;max-width:900px}
.update-card,.setup-card{padding:30px;border:1px solid #e5e9ef;border-radius:18px;background:#fff}
.update-card{background:radial-gradient(ellipse at 100% 0,#edf2ff,transparent 65%),#fff}
.update-eyebrow{font-size:11px;letter-spacing:.18em;color:#687e9e}
h2{margin:12px 0;font-size:26px;color:#243247}p{font-size:14px;color:#68778c;line-height:1.8}
.version-grid{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin:28px 0 12px}
.version-grid div{display:grid;gap:10px;padding:20px;background:#f4f6fa;border-radius:12px}
.version-grid span{font-size:12px;color:#718096}.version-grid strong{font-size:22px;color:#2c3b51}
.update-actions{display:flex;flex-wrap:wrap;gap:12px;margin-top:24px}.update-actions :deep(.el-button){margin-left:0}
.update-hint{color:#5265d8}h3{margin-top:0;color:#2c3b51}pre{white-space:pre-wrap;overflow-wrap:anywhere;font-size:12px;padding:16px;background:#f4f6fa;border-radius:10px;color:#465670}
@media(max-width:640px){.update-card,.setup-card{padding:20px}.version-grid{gap:10px}}
</style>
