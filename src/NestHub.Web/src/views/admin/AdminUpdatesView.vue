<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '@/api/http'

interface UpdateStatus {
  currentRevision: string; latestRevision: string; publishedAt: string | null
  hasUpdate: boolean; updateEnabled: boolean; workflowUrl: string; disabledReason?: string | null
  updateState?: string | null; updateStage?: string | null; updateMessage?: string | null
  updateStartedAt?: string | null; downloadedBytes?: number | null; totalBytes?: number | null; updateTaskUpdatedAt?: string | null
}
const info = ref<UpdateStatus | null>(null)
const checking = ref(false)
const updating = ref(false)
const uncertain = ref(false)
const disconnected = ref(false)
const checkError = ref('')
const hint = ref('')
const now = ref(Date.now())
const startedAt = ref(0)
const finishedAt = ref(0)
const localFailure = ref(false)
const stageNames = ['启动任务', '下载应用包', '校验与切换', '健康检查', '完成']
const activeStates = ['pulling', 'replacing', 'checking', 'rollingBack']
const stage = computed(() => ({ pulling: 1, replacing: 2, checking: 3, rollingBack: 3, success: 4 }[info.value?.updateStage || info.value?.updateState || ''] ?? 0))
const elapsed = computed(() => startedAt.value ? Math.max(0, Math.floor(((finishedAt.value || now.value) - startedAt.value) / 1000)) : 0)
const failed = computed(() => localFailure.value || info.value?.updateState === 'failed')
const completed = computed(() => info.value?.updateState === 'success' && !updating.value && !info.value?.hasUpdate)
const statusTitle = computed(() => uncertain.value ? '等待确认任务状态' : updating.value ? disconnected.value ? '应用正在重新连接' : info.value?.updateState === 'rollingBack' ? '正在恢复原版本' : '更新正在进行' : failed.value ? '更新未完成' : completed.value ? '更新已完成' : info.value?.updateState === 'unchanged' ? '发布包尚未就绪' : info.value?.hasUpdate ? '有新版本可用' : info.value?.latestRevision ? '当前版本已就绪' : checkError.value ? '版本信息待确认' : checking.value ? '正在获取版本信息' : '尚未发布应用包')
function size(bytes?: number | null) { return ((bytes || 0) / 1024 / 1024).toFixed(1) + ' MB' }
const elapsedTimer = setInterval(() => { now.value = Date.now() }, 1000)
let timer: ReturnType<typeof setTimeout> | undefined
let disposed = false
let baseline = ''
let requestedAt = 0
function revision(value?: string) { return value && value !== 'local' ? value.slice(0, 7) : '本地构建' }
function reloadPage() { window.location.reload() }
function schedule() { if (!disposed) timer = setTimeout(poll, 2000) }
function mergeProgress(result: UpdateStatus) {
  info.value = { ...info.value!, ...result, latestRevision: info.value?.latestRevision || '', publishedAt: info.value?.publishedAt || null, workflowUrl: info.value?.workflowUrl || '', hasUpdate: info.value?.hasUpdate || false }
  if (completed.value) info.value.hasUpdate = info.value.currentRevision !== info.value.latestRevision
}
function consume(result: UpdateStatus) {
  const identity = result.updateStartedAt || result.updateTaskUpdatedAt || ''
  if (!identity || identity === baseline) return false
  mergeProgress(result)
  const serverStart = Date.parse(result.updateStartedAt || '')
  if (Number.isFinite(serverStart)) startedAt.value = serverStart
  hint.value = result.updateMessage || '更新任务正在执行。'
  if (['failed', 'unchanged', 'success'].includes(result.updateState || '')) {
    updating.value = false
    uncertain.value = false
    finishedAt.value = Date.parse(result.updateTaskUpdatedAt || '') || Date.now()
    if (result.updateState === 'success') {
      info.value!.hasUpdate = info.value!.currentRevision !== info.value!.latestRevision
      hint.value = '更新完成，请刷新页面使用新版。'
      ElMessage.success('应用更新完成。')
    }
    return true
  }
  return false
}
async function check(force = false) {
  if (checking.value) return
  checking.value = true
  checkError.value = ''
  try {
    const result = (await http.get<UpdateStatus>('/system-updates', { params: { force }, timeout: 35000 })).data
    if (disposed) return
    info.value = result
    if (!updating.value && activeStates.includes(result.updateState || '')) resume()
  } catch (error: any) {
    if (disposed) return
    checkError.value = error?.response?.data?.message || '版本查询失败，请检查网络后重试。'
    // Local task status remains available even when GitHub cannot be reached.
    try {
      const progress = (await http.get<UpdateStatus>('/system-updates/progress', { timeout: 5000 })).data
      if (!disposed) { mergeProgress(progress); if (activeStates.includes(progress.updateState || '')) resume() }
    } catch { /* Keep the last visible state and show the retry action. */ }
  } finally { checking.value = false }
}
function resume() {
  if (timer) clearTimeout(timer)
  baseline = ''
  requestedAt = Date.now()
  startedAt.value = Date.parse(info.value?.updateStartedAt || '') || requestedAt
  finishedAt.value = 0
  uncertain.value = false
  updating.value = true
  hint.value = info.value?.updateMessage || '正在恢复更新进度。'
  schedule()
}
async function poll() {
  if (disposed) return
  try {
    const result = (await http.get<UpdateStatus>('/system-updates/progress', { timeout: 5000 })).data
    if (disposed) return
    disconnected.value = false
    if (consume(result)) return
  } catch (error: any) {
    if (disposed) return
    if ([401, 403].includes(error?.response?.status)) {
      updating.value = false
      uncertain.value = true
      hint.value = '登录状态已失效，请重新登录后查看更新进度；后台任务会继续执行。'
      return
    }
    disconnected.value = true
  }
  if (Date.now() - requestedAt > 20 * 60 * 1000) {
    updating.value = false
    uncertain.value = true
    hint.value = '暂未确认任务结束。请继续查看进度或检查应用日志，避免重复发起更新。'
    return
  }
  if (Date.now() - requestedAt > 60000 && !info.value?.updateState) hint.value = '尚未收到任务进度，正在继续查询。请检查应用日志，勿重复发起。'
  schedule()
}
async function apply() {
  if (!info.value?.updateEnabled || !info.value.hasUpdate || updating.value || uncertain.value) return
  try { await ElMessageBox.confirm('更新期间应用会短暂重启，数据库与上传文件保留。建议先完成数据备份。', '更新到最新版', { confirmButtonText: '开始更新', cancelButtonText: '取消', type: 'warning' }) }
  catch { return }
  baseline = info.value.updateStartedAt || info.value.updateTaskUpdatedAt || ''
  requestedAt = Date.now()
  updating.value = true
  uncertain.value = false
  disconnected.value = false
  localFailure.value = false
  startedAt.value = requestedAt
  finishedAt.value = 0
  hint.value = '正在启动更新任务。离开页面后，任务仍会继续执行。'
  info.value = { ...info.value, updateState: undefined, updateStage: undefined, downloadedBytes: null, totalBytes: null }
  try { await http.post('/system-updates', undefined, { timeout: 60000 }) }
  catch (error: any) {
    if (disposed) return
    if (error.response) {
      updating.value = false
      localFailure.value = true
      finishedAt.value = Date.now()
      hint.value = error.response.data?.message || '更新请求失败，请检查部署配置。'
      return
    }
    disconnected.value = true
    hint.value = '暂未收到启动确认，正在查询后台任务状态。'
  }
  schedule()
}
onMounted(() => check())
onBeforeUnmount(() => { disposed = true; if (timer) clearTimeout(timer); clearInterval(elapsedTimer) })
</script>

<template>
  <div class="updates-page">
    <section class="update-card">
      <div class="update-heading"><div><span class="update-eyebrow">版本与维护</span><h2>应用更新</h2><p>查看版本、跟踪更新进度，保持导航稳定运行。</p></div><span class="update-status" :class="{ 'is-running': updating, 'is-error': failed, 'is-complete': completed }">{{ statusTitle }}</span></div>
      <el-alert v-if="checkError" :title="checkError" type="warning" :closable="false" show-icon />
      <div class="version-grid">
        <div><span>当前版本</span><strong>{{ revision(info?.currentRevision) }}</strong></div>
        <div><span>最新版本</span><strong>{{ info?.latestRevision ? revision(info.latestRevision) : '待检查' }}</strong></div>
      </div>
      <p v-if="info?.publishedAt" class="published">最近发布：{{ new Date(info.publishedAt).toLocaleString('zh-CN') }}</p>
      <el-alert v-if="info && !info.updateEnabled" title="后台更新尚未启用" type="warning" :closable="false" show-icon
        :description="info.disabledReason || '当前运行容器缺少更新配置。请按下方说明补齐 Compose 并重新创建容器，单独下载应用包不会启用此功能。'" />
      <div class="update-actions">
        <el-button :loading="checking" :disabled="updating" @click="check(true)">检查更新</el-button>
        <el-button type="primary" :loading="updating" :disabled="!info?.updateEnabled || !info?.hasUpdate || uncertain || checking" @click="apply">{{ info && !info.updateEnabled ? '需先配置更新服务' : updating ? '更新中' : '更新到最新版' }}</el-button>
        <el-button v-if="uncertain" @click="resume">继续查看进度</el-button>
        <el-button v-if="hint === '更新完成，请刷新页面使用新版。'" @click="reloadPage">刷新页面</el-button>
      </div>
      <div v-if="updating || uncertain || localFailure || info?.updateState" class="update-progress" role="status" aria-live="polite">
        <div class="progress-caption">{{ updating ? '当前更新任务' : '最近一次更新' }}</div>
        <ol class="update-stages"><li v-for="(label, index) in stageNames" :key="label" :class="{ active: updating && stage === index, done: stage > index, failed: failed && stage === index }">{{ label }}</li></ol>
        <p v-if="startedAt">已用时 {{ Math.floor(elapsed / 60) }} 分 {{ elapsed % 60 }} 秒<span v-if="info?.updateState === 'pulling' && info.downloadedBytes"> · 已下载 {{ size(info.downloadedBytes) }}<template v-if="info.totalBytes"> / 应用包总大小 {{ size(info.totalBytes) }}</template></span></p>
        <p v-if="updating && elapsed > 60 && info?.updateState === 'pulling'">当前耗时在应用包下载，应用仍可正常访问；代理速度和应用包大小会影响下载时间。</p>
        <p v-if="updating && info?.updateState === 'replacing'">正在重启应用，短暂断开属正常现象，恢复后继续获取进度。</p>
      </div>
      <p v-if="hint || info?.updateMessage" class="update-hint" :class="{ 'is-error': failed }" role="status">{{ hint || info?.updateMessage }}</p>
      <p v-else-if="info && !info.hasUpdate">{{ info.currentRevision && info.currentRevision !== 'local' ? (info.latestRevision ? '当前无需更新。' : '应用包尚未发布，首次发布完成后即可检测更新。') : '本地构建请先使用正式 Docker 镜像。' }}</p>
    </section>
    <details class="setup-card" :open="!!info && !info.updateEnabled"><summary>部署配置与更新说明</summary>
      <h3>首次启用后台更新</h3>
      <p>后台下载应用包、校验并重启应用。数据库、上传文件和配置保持在挂载目录中，新版本启动失败会自动恢复旧版本。</p>
      <pre>environment:
  Updates__Enabled: "true"
  # 可选：访问 GitHub 的代理
  # Updates__ProxyUrl: http://代理地址:端口
volumes:
  - ./data/app:/app/AppData</pre>
      <p>旧版首次需要更新一次基础镜像，安装应用启动器。之后日常更新无需 Docker socket、Watchtower 或额外更新容器。</p>
      <p>挂载目录需要允许容器用户（UID 1654）写入。应用版本保存在 AppData/updates，重建容器后仍会使用已更新版本。运行环境大版本升级时仍需更新基础镜像。</p>
      <p><a href="https://github.com/sunnyfancore/NestHub/blob/main/docs/docker-deployment.md" target="_blank" rel="noopener noreferrer">查看完整部署说明</a></p>
    </details>
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
.update-progress{margin-top:22px;padding:16px;background:#f4f6fa;border-radius:12px}
.update-stages{display:flex;flex-wrap:wrap;gap:10px;list-style:none;margin:0;padding:0}
.update-stages li{font-size:12px;padding:6px 10px;border-radius:8px;color:#66758a;background:#e9edf3}
.update-stages .active{background:#e0e9ff;color:#304fa1;font-weight:600}.update-stages .done{background:#e2f3ed;color:#21705b}
.progress-caption{font-size:12px;font-weight:600;color:#516461;margin-bottom:12px}.updates-page{max-width:1040px}.update-card{background:#fff;border-color:var(--el-border-color-light);box-shadow:0 4px 24px #25443205}
.update-heading{display:flex;align-items:flex-start;justify-content:space-between;gap:20px}.update-heading h2{color:#263e3b;font-size:24px}.update-heading p{margin-bottom:0}
.update-eyebrow{color:#57796e;letter-spacing:.08em}.update-status{display:inline-flex;align-items:center;gap:7px;padding:8px 12px;border:1px solid #dfe8e5;border-radius:99px;background:#f5f8f7;color:#516461;font-size:12px;white-space:nowrap}
.update-status::before{content:'';width:6px;height:6px;border-radius:50%;background:currentColor}.update-status.is-running{color:#127565;background:#edf7f4}.update-status.is-error{color:#a53d3d;background:#fff2f2;border-color:#efd4d4}.update-status.is-complete{color:#21705b;background:#eaf6ef}
.version-grid strong{font-family:ui-monospace,Consolas,monospace;font-size:24px;color:#263e3b}.version-grid div{border:1px solid #e6eeea;background:#f8faf9}.published{font-size:12px}
.update-progress{background:#f7faf8;border:1px solid #e2ece6}.update-stages{gap:8px}.update-stages li{flex:1;text-align:center;background:#edf2ef;white-space:nowrap}.update-stages .active{background:#e0f2eb;color:#127565}.update-stages .failed{color:#a53d3d;background:#fce8e8}
.update-hint{padding:10px 12px;border-left:3px solid #168575;background:#f4f9f6;border-radius:0 8px 8px 0;color:#516461;font-size:13px;overflow-wrap:anywhere}.update-hint.is-error{border-color:#b44747;background:#fff5f5;color:#9c3838}
.setup-card{padding:20px 24px}.setup-card summary{cursor:pointer;color:#36564b;font-size:14px;font-weight:600}.setup-card summary:focus-visible{outline:2px solid #168575;outline-offset:5px}.setup-card h3{margin-top:22px;font-size:15px}.setup-card pre{white-space:pre-wrap;overflow-wrap:anywhere}
@media(max-width:600px){.update-card{padding:20px 16px}.update-heading{flex-direction:column;gap:12px}.update-heading h2{font-size:22px}.version-grid{gap:10px;margin-top:22px}.version-grid div{padding:14px 12px}.version-grid strong{font-size:20px}.update-stages{display:grid;grid-template-columns:repeat(2,minmax(0,1fr))}.update-actions{gap:8px}.update-actions .el-button{margin-left:0}.setup-card{padding:16px}.update-heading p{font-size:13px}}
</style>
