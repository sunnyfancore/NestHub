<script setup lang="ts">
import { Delete, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { getAdminCategories } from '@/api/admin'
import { createShareLink, deleteShareLink, getShareLinks, type ShareLinkItem } from '@/api/share'
import AdminLoadNotice from '@/components/admin/AdminLoadNotice.vue'
import type { AdminCategoryItem } from '@/types/models'

const loading = ref(false)
const saving = ref(false)
const shares = ref<ShareLinkItem[]>([])
const categories = ref<AdminCategoryItem[]>([])
const dialogVisible = ref(false)
const ready = ref(false)
const loadError = ref('')
const removing = ref<string[]>([])
const now = ref(Date.now())
let loadSequence = 0
let expiryTimer: ReturnType<typeof setInterval> | undefined

const form = ref({
  categoryId: '',
  password: '',
  note: '',
  expireAt: new Date(Date.now() + 7 * 86400000).toISOString(),
  neverExpires: false,
})

const categoryOptions = computed(() => categories.value)

async function load() {
  const sequence = ++loadSequence
  loading.value = true
  loadError.value = ''

  try {
    const [folders, records] = await Promise.all([getAdminCategories(), getShareLinks()])
    if (sequence !== loadSequence) return
    categories.value = folders
    shares.value = records
    ready.value = true
  } catch (error: any) {
    if (sequence !== loadSequence) return
    loadError.value = error?.response?.data?.message || '获取分享列表失败。'
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

async function submit() {
  if (saving.value || !ready.value) return
  if (!form.value.categoryId) {
    ElMessage.warning('请选择要分享的分类。')
    return
  }

  if (form.value.password.trim() && form.value.password.trim().length < 6) { ElMessage.warning('访问密码至少 6 位。'); return }
  if (!form.value.neverExpires && (!form.value.expireAt || !Number.isFinite(new Date(form.value.expireAt).getTime()) || new Date(form.value.expireAt).getTime() <= Date.now())) { ElMessage.warning('请选择未来的过期时间。'); return }
  saving.value = true

  try {
    await createShareLink({
      categoryId: form.value.categoryId,
      password: form.value.password || undefined,
      note: form.value.note || undefined,
      expireAt: form.value.neverExpires ? undefined : new Date(form.value.expireAt).toISOString(),
      neverExpires: form.value.neverExpires,
    })
    ElMessage.success('分享链接已创建。')
    dialogVisible.value = false
    form.value = {
      categoryId: '',
      password: '',
      note: '',
      expireAt: new Date(Date.now() + 7 * 86400000).toISOString(),
      neverExpires: false,
    }
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '创建分享失败。')
  } finally {
    saving.value = false
  }
}

async function remove(item: ShareLinkItem) {
  if (removing.value.includes(item.id)) return
  removing.value.push(item.id)
  try {
  const confirmed = await ElMessageBox.confirm('撤销后，此链接将无法继续访问。确认撤销？', '撤销分享', { type: 'warning' }).then(() => true, () => false)
  if (!confirmed) return

  try {
    await deleteShareLink(item.id)
    ++loadSequence
    loading.value = false
    shares.value = shares.value.filter(share => share.id !== item.id)
    ElMessage.success('分享已撤销。')
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '删除分享失败。')
  }
  } finally { removing.value = removing.value.filter(id => id !== item.id) }
}

async function copy(item: ShareLinkItem) {
  now.value = Date.now()
  if (expired(item)) { ElMessage.warning('分享已过期，请重新创建。'); return }
  const text = `${window.location.origin}/share/${item.shareCode}`
  try {
    if (navigator.clipboard && window.isSecureContext) await navigator.clipboard.writeText(text)
    else {
      const input = document.createElement('textarea'); input.value = text; input.style.position = 'fixed'; input.style.opacity = '0'; document.body.append(input); input.select()
      try { if (!document.execCommand('copy')) throw new Error('copy') } finally { input.remove() }
    }
    ElMessage.success(item.hasPassword ? '链接已复制，请单独告知接收者访问密码。' : '分享链接已复制。')
  } catch { ElMessage.warning('无法自动复制，请从分享地址中手动复制。') }
}
function beforeClose(done: () => void) { if (!saving.value) done() }
function shareUrl(item: ShareLinkItem) { return `${window.location.origin}/share/${item.shareCode}` }
function date(value: string) { return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z') }
function expired(item: ShareLinkItem) { return item.expireAt ? date(item.expireAt).getTime() <= now.value : false }
function openCreate() {
  form.value = { categoryId: '', password: '', note: '', expireAt: new Date(Date.now() + 7 * 86400000).toISOString(), neverExpires: false }
  dialogVisible.value = true
}

onMounted(() => { load(); expiryTimer = setInterval(() => { now.value = Date.now() }, 1000) })
onBeforeUnmount(() => { ++loadSequence; if (expiryTimer) clearInterval(expiryTimer) })
</script>

<template>
  <div class="admin-page">
    <AdminLoadNotice :message="loadError" :loading="loading" @retry="load" />
    <p class="share-help">分享选定分类及其子分类，内容随原分类更新。默认 7 天有效；受保护的分享需单独告知访问密码，密码不会再次显示。</p>
    <div class="admin-toolbar">
      <el-button type="primary" :disabled="!ready || !categories.length || saving" @click="openCreate">
        <el-icon><Plus /></el-icon>
        创建分享
      </el-button>
      <el-button :loading="loading" @click="load">
        <el-icon><Refresh /></el-icon>
        刷新
      </el-button>
    </div>

    <p v-if="ready && !categories.length" class="share-help">还没有可分享的分类，请先在分类管理中创建分类。</p>
    <div class="share-mobile-list" v-loading="loading">
      <article v-for="item in shares" :key="item.id" class="share-mobile-card">
        <div class="share-card-heading"><h2>{{ item.categoryName || '分类已删除' }}</h2><el-tag :type="expired(item) ? 'info' : 'success'">{{ expired(item) ? '已过期' : '有效' }}</el-tag></div>
        <p class="share-card-access">{{ item.hasPassword ? '密码保护' : '公开链接' }} · {{ item.expireAt ? `到期 ${date(item.expireAt).toLocaleString()}` : '长期有效' }}</p>
        <a class="share-card-url" :href="shareUrl(item)" target="_blank" rel="noopener noreferrer">{{ shareUrl(item) }}</a>
        <p v-if="item.note" class="share-card-note">{{ item.note }}</p>
        <div class="share-card-actions"><el-button :disabled="expired(item) || removing.includes(item.id)" @click="copy(item)">复制链接</el-button><el-button type="danger" plain :loading="removing.includes(item.id)" @click="remove(item)">撤销分享</el-button></div>
      </article>
      <el-empty v-if="ready && !shares.length && !loading" description="暂无分享，创建后即可发送给朋友" :image-size="64" />
    </div>
    <el-table class="share-desktop-table" v-loading="loading" :data="shares">
      <el-table-column label="分享地址" min-width="220" show-overflow-tooltip><template #default="{ row }"><a :href="shareUrl(row)" target="_blank" rel="noopener noreferrer">{{ shareUrl(row) }}</a></template></el-table-column>
      <el-table-column prop="categoryName" label="分类名称" min-width="180" />
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">
          {{ new Date(row.createdAt).toLocaleString() }}
        </template>
      </el-table-column>
      <el-table-column prop="expireAt" label="过期时间" width="180">
        <template #default="{ row }">
          {{ row.expireAt ? date(row.expireAt).toLocaleString() : '不过期' }}
        </template>
      </el-table-column>
      <el-table-column label="访问方式" width="110"><template #default="{ row }">{{ row.hasPassword ? '密码保护' : '公开链接' }}</template></el-table-column>
      <el-table-column label="状态" width="90"><template #default="{ row }"><el-tag :type="expired(row) ? 'info' : 'success'">{{ expired(row) ? '已过期' : '有效' }}</el-tag></template></el-table-column>
      <el-table-column prop="note" label="备注" min-width="180" />
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button text :disabled="expired(row)" @click="copy(row)">复制</el-button>
          <el-button text type="danger" :loading="removing.includes(row.id)" aria-label="撤销分享" title="撤销分享" @click="remove(row)">
            <el-icon><Delete /></el-icon>
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" class="nesthub-editor-dialog" :before-close="beforeClose" title="创建分享" width="520px" :close-on-click-modal="false" :close-on-press-escape="!saving" :show-close="!saving">
      <el-form label-position="top" :disabled="saving">
        <el-form-item label="分享分类">
          <el-select v-model="form.categoryId" placeholder="请选择分类" style="width: 100%">
            <el-option
              v-for="item in categoryOptions"
              :key="item.id"
              :label="item.parentName ? `${item.parentName} / ${item.name}` : item.name"
              :value="item.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="长期分享"><el-checkbox v-model="form.neverExpires">不过期（主动撤销前一直有效）</el-checkbox></el-form-item>
        <el-form-item v-if="!form.neverExpires" label="过期时间">
          <el-date-picker
            v-model="form.expireAt"
            type="datetime"
            placeholder="默认 7 天有效"
            style="width: 100%"

          />
        </el-form-item>
        <el-form-item label="访问密码">
          <el-input v-model="form.password" type="password" show-password maxlength="32" autocomplete="new-password" placeholder="6—32 位；留空表示公开分享" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.note" type="textarea" :rows="3" maxlength="255" placeholder="仅自己可见，不会展示给访问者" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button :disabled="saving" @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submit">创建</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.share-help { margin:0;color:#64796d;font-size:13px;line-height:1.8; }
.share-mobile-list { display:none; }
.share-mobile-card { padding:16px;border:1px solid #dfe8e2;border-radius:14px;background:#fff;min-width:0; }
.share-card-heading { display:flex;align-items:center;justify-content:space-between;gap:12px; }
.share-card-heading h2 { margin:0;font-size:16px;overflow-wrap:anywhere; }
.share-card-access,.share-card-note { font-size:12px;color:#64796d;line-height:1.7;overflow-wrap:anywhere; }
.share-card-url { display:block;padding:10px 12px;background:#f5f8f6;border-radius:8px;font-size:12px;overflow-wrap:anywhere;color:#127565; }
.share-card-actions { display:flex;gap:8px;margin-top:14px; }
.share-card-actions :deep(.el-button) { flex:1;margin:0; }
@media(max-width:760px) { .share-mobile-list { display:grid;gap:12px; }.share-desktop-table { display:none; } }
.admin-page {
  display: grid;
  gap: 16px;
}

.admin-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
}
</style>
