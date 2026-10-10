<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '@/api/http'

interface UpdateStatus {
  currentRevision: string; latestRevision: string; publishedAt: string | null
  hasUpdate: boolean; updateEnabled: boolean; workflowUrl: string
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
    if (result.currentRevision !== previous) {
      updating.value = false
      hint.value = '更新完成，请刷新页面使用新版。'
      ElMessage.success('应用更新完成。')
      return
    }
  } catch { /* A short interruption is expected during container replacement. */ }
  if (disposed) return
  if (Date.now() - started > 6 * 60 * 1000) {
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
  hint.value = '正在拉取镜像并重启应用，请保持页面打开。'
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
  timer = setTimeout(() => waitForUpdate(previous, Date.now()), 5000)
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
      <div class="update-actions">
        <el-button :loading="checking" :disabled="updating" @click="check">检查更新</el-button>
        <el-button type="primary" :loading="updating" :disabled="!info?.updateEnabled || !info?.hasUpdate" @click="apply">更新到最新版</el-button>
        <el-button v-if="hint === '更新完成，请刷新页面使用新版。'" @click="reloadPage">刷新页面</el-button>
      </div>
      <p v-if="hint" class="update-hint" role="status">{{ hint }}</p>
      <p v-else-if="info && !info.hasUpdate">{{ info.currentRevision && info.currentRevision !== 'local' ? '当前已是最新版。' : '本地构建请先使用正式 Docker 镜像。' }}</p>
    </section>
    <section v-if="!info?.updateEnabled" class="setup-card">
      <h3>首次启用后台更新</h3>
      <p>新版 Compose 已内置更新服务，只需一个部署文件。旧部署请保留数据目录与 .env，替换新版配置，并在 .env 中补充随机 UPDATE_API_TOKEN。</p>
      <pre>docker compose up -d</pre>
      <p>MySQL 部署使用 docker compose -f docker-compose.mysql.yml up -d。完成一次配置后，后续直接点击上方更新按钮。</p>
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
