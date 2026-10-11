<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import AdminLoadNotice from '@/components/admin/AdminLoadNotice.vue'
import { createTenant, getTenants, resetTenantPassword, toggleTenantActive, updateTenant } from '@/api/super'
import type { AdminTenant } from '@/types/models'

const loading = ref(false)
const tenants = ref<AdminTenant[]>([])
const loadError = ref('')
let loadSequence = 0
const keyword = ref('')
const filteredTenants = computed(() => {
  const query = keyword.value.trim().toLocaleLowerCase()
  return tenants.value.filter(tenant => !query || `${tenant.name} ${tenant.email || ''} ${tenant.displayName || ''}`.toLocaleLowerCase().includes(query))
})
const activeCount = computed(() => tenants.value.filter(tenant => tenant.isActive).length)
const linkCount = computed(() => tenants.value.reduce((count, tenant) => count + tenant.linkCount, 0))

// ── Create tenant dialog ──
const createDialogVisible = ref(false)
const creating = ref(false)
const createForm = ref({ name: '', email: '', displayName: '', password: '' })

// ── Edit tenant dialog ──
const editDialogVisible = ref(false)
const editing = ref(false)
const editTarget = ref<AdminTenant | null>(null)
const editForm = ref({ name: '', displayName: '' })

// ── Reset password dialog ──
const resetDialogVisible = ref(false)
const resetting = ref(false)
const resetTarget = ref<AdminTenant | null>(null)
const newPassword = ref('')

async function load() {
  const sequence = ++loadSequence
  loading.value = true
  loadError.value = ''
  try {
    const result = await getTenants()
    if (sequence === loadSequence) tenants.value = result
  } catch (error: any) {
    if (sequence === loadSequence) loadError.value = error?.response?.data?.message || '租户读取失败，请重新加载。'
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}

function openCreate() {
  createForm.value = { name: '', email: '', displayName: '', password: '' }
  createDialogVisible.value = true
}

async function submitCreate() {
  const { name, email, password } = createForm.value
  if (!name.trim() || !email.trim() || !password) {
    ElMessage.warning('请填写所有必填项。')
    return
  }
  creating.value = true
  try {
    await createTenant({
      name: name.trim(),
      email: email.trim(),
      displayName: createForm.value.displayName.trim() || undefined,
      password,
    })
    ElMessage.success('租户已创建。')
    createDialogVisible.value = false
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '创建租户失败。')
  } finally {
    creating.value = false
  }
}

function openEdit(tenant: AdminTenant) {
  editTarget.value = tenant
  editForm.value = { name: tenant.name, displayName: tenant.displayName || '' }
  editDialogVisible.value = true
}

async function submitEdit() {
  if (!editForm.value.name.trim()) {
    ElMessage.warning('租户名称不能为空。')
    return
  }
  editing.value = true
  try {
    await updateTenant(editTarget.value!.id, {
      name: editForm.value.name.trim(),
      displayName: editForm.value.displayName.trim() || undefined,
    })
    ElMessage.success('租户已更新。')
    editDialogVisible.value = false
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '更新租户失败。')
  } finally {
    editing.value = false
  }
}

async function handleToggleActive(tenant: AdminTenant) {
  if (tenant.isSuperAdmin) {
    ElMessage.warning('不能禁用超级管理员。')
    return
  }
  const action = tenant.isActive ? '停用' : '启用'
  await ElMessageBox.confirm(`确认${action}租户「${tenant.name}」吗？`, `${action}租户`, { type: 'warning' })
  try {
    await toggleTenantActive(tenant.id)
    ElMessage.success(`租户已${action}。`)
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || `${action}租户失败。`)
  }
}

function openResetPassword(tenant: AdminTenant) {
  resetTarget.value = tenant
  newPassword.value = ''
  resetDialogVisible.value = true
}

async function submitResetPassword() {
  if (!newPassword.value) {
    ElMessage.warning('请输入新密码。')
    return
  }
  resetting.value = true
  try {
    await resetTenantPassword(resetTarget.value!.id, newPassword.value)
    ElMessage.success('密码已重置。')
    resetDialogVisible.value = false
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '重置密码失败。')
  } finally {
    resetting.value = false
  }
}

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleString()
}

onMounted(load)
onBeforeUnmount(() => { ++loadSequence })
</script>

<template>
  <div class="admin-page tenant-page">
    <AdminLoadNotice :message="loadError" :loading="loading" @retry="load" />
    <div class="tenant-overview">
      <div class="tenant-stat"><span>租户总数</span><strong>{{ tenants.length }}</strong><small>独立的导航空间</small></div>
      <div class="tenant-stat"><span>启用中</span><strong>{{ activeCount }}</strong><small>可正常访问的空间</small></div>
      <div class="tenant-stat"><span>收藏链接</span><strong>{{ linkCount }}</strong><small>所有租户的链接总数</small></div>
    </div>
    <section class="tenant-directory">
    <div class="tenant-directory__heading"><div><h2>租户列表</h2><p>管理导航空间、访问状态与登录账号。</p></div><span>{{ filteredTenants.length }} 个空间</span></div>
    <div class="admin-toolbar">
      <div class="admin-toolbar__left">
        <el-input v-model="keyword" clearable placeholder="搜索名称或邮箱" aria-label="搜索租户" style="width: 240px" />
        <el-button type="primary" @click="openCreate">
          <i class="fa fa-plus" aria-hidden="true"></i>
          创建租户
        </el-button>
      </div>
      <el-button @click="load">
        <i class="fa fa-refresh" aria-hidden="true"></i>
        刷新
      </el-button>
    </div>

    <div class="admin-mobile-records" v-loading="loading">
      <article v-for="item in filteredTenants" :key="item.id" class="admin-record">
        <div class="admin-record__heading"><h3>{{ item.name }}</h3><el-tag :type="item.isActive ? 'success' : 'danger'">{{ item.isActive ? '启用' : '停用' }}</el-tag></div>
        <p class="admin-record__url">{{ item.email || '未设置邮箱' }}</p>
        <dl class="admin-record__details"><div><dt>角色</dt><dd>{{ item.isSuperAdmin ? '超级管理员' : '普通租户' }}</dd></div><div><dt>链接</dt><dd>{{ item.linkCount }}</dd></div><div><dt>创建</dt><dd>{{ formatDate(item.createdAt) }}</dd></div></dl>
        <div class="admin-record__actions"><el-button @click="openEdit(item)">编辑</el-button><el-button v-if="!item.isSuperAdmin" :type="item.isActive ? 'warning' : 'success'" plain @click="handleToggleActive(item)">{{ item.isActive ? '停用' : '启用' }}</el-button><el-button v-if="item.email" @click="openResetPassword(item)">重置密码</el-button></div>
      </article>
      <el-empty v-if="!loading && !filteredTenants.length" description="暂无符合条件的租户" :image-size="64" />
    </div>
    <el-table class="admin-desktop-records"
      v-loading="loading"
      :data="filteredTenants"
      row-key="id"
      :max-height="560"
    >
      <el-table-column prop="name" label="租户名称" min-width="140" show-overflow-tooltip />
      <el-table-column prop="email" label="邮箱" min-width="180" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.email || '-' }}
        </template>
      </el-table-column>
      <el-table-column label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'danger'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="超管" width="70">
        <template #default="{ row }">
          <el-tag v-if="row.isSuperAdmin" type="warning" size="small">是</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="linkCount" label="链接数" width="80" />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">
          {{ formatDate(row.createdAt) }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button text type="primary" size="small" @click="openEdit(row)">
            <i class="fa fa-edit" aria-hidden="true"></i>
            编辑
          </el-button>
          <el-button
            v-if="!row.isSuperAdmin"
            text
            :type="row.isActive ? 'warning' : 'success'"
            size="small"
            @click="handleToggleActive(row)"
          >
            <i class="fa" :class="row.isActive ? 'fa-ban' : 'fa-check'" aria-hidden="true"></i>
            {{ row.isActive ? '停用' : '启用' }}
          </el-button>
          <el-button
            v-if="row.email"
            text
            type="primary"
            size="small"
            @click="openResetPassword(row)"
          >
            <i class="fa fa-key" aria-hidden="true"></i>
            重置密码
          </el-button>
        </template>
      </el-table-column>
    </el-table>
    </section>

    <!-- Create tenant dialog -->
    <el-dialog v-model="createDialogVisible" title="创建租户" width="480" :close-on-click-modal="false">
      <el-form label-position="top">
        <el-form-item label="租户名称" required>
          <el-input v-model="createForm.name" placeholder="请输入租户名称" />
        </el-form-item>
        <el-form-item label="邮箱" required>
          <el-input v-model="createForm.email" placeholder="请输入登录邮箱" />
        </el-form-item>
        <el-form-item label="显示名称">
          <el-input v-model="createForm.displayName" placeholder="可选，默认与租户名称相同" />
        </el-form-item>
        <el-form-item label="密码" required>
          <el-input v-model="createForm.password" type="password" show-password placeholder="请输入密码" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="creating" @click="submitCreate">确认创建</el-button>
      </template>
    </el-dialog>

    <!-- Edit tenant dialog -->
    <el-dialog v-model="editDialogVisible" title="编辑租户" width="420" :close-on-click-modal="false">
      <p class="edit-target-info">
        租户：<strong>{{ editTarget?.name }}</strong>
        <template v-if="editTarget?.email">（{{ editTarget?.email }}）</template>
      </p>
      <el-form label-position="top">
        <el-form-item label="租户名称" required>
          <el-input v-model="editForm.name" placeholder="请输入租户名称" />
        </el-form-item>
        <el-form-item label="显示名称">
          <el-input v-model="editForm.displayName" placeholder="可选，用于前端显示" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="editing" @click="submitEdit">保存</el-button>
      </template>
    </el-dialog>

    <!-- Reset password dialog -->
    <el-dialog v-model="resetDialogVisible" title="重置密码" width="420" :close-on-click-modal="false">
      <p class="reset-target-info">
        正在为租户 <strong>{{ resetTarget?.name }}</strong>
        <template v-if="resetTarget?.email">（{{ resetTarget?.email }}）</template>
        重置密码。
      </p>
      <el-form label-position="top">
        <el-form-item label="新密码" required>
          <el-input v-model="newPassword" type="password" show-password placeholder="请输入新密码" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="resetting" @click="submitResetPassword">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.admin-page {
  display: flex;
  flex-direction: column;
  height: auto;
  gap: 16px;
  overflow: visible;
  min-width: 0;
}

.admin-page .el-table {
  flex: none;
  min-height: 0;
}

.admin-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
  min-width: 0;
}

.admin-toolbar__left {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
}

.edit-target-info,
.reset-target-info {
  margin-bottom: 16px;
  font-size: 14px;
  color: #666;
}

@media (max-width: 760px) {
  .admin-page {
    height: auto;
  }
}
</style>
