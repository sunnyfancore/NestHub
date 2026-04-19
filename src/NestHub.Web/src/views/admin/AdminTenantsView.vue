<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { onMounted, ref } from 'vue'
import { createTenant, getTenants, resetTenantPassword, toggleTenantActive, updateTenant } from '@/api/super'
import type { AdminTenant } from '@/types/models'

const loading = ref(false)
const tenants = ref<AdminTenant[]>([])

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
  loading.value = true
  try {
    tenants.value = await getTenants()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取数据失败。')
  } finally {
    loading.value = false
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
</script>

<template>
  <div class="admin-page">
    <div class="admin-toolbar">
      <div class="admin-toolbar__left">
        <el-button type="primary" @click="openCreate">
          <i class="fa fa-plus"></i>
          创建租户
        </el-button>
      </div>
      <el-button @click="load">
        <i class="fa fa-refresh"></i>
        刷新
      </el-button>
    </div>

    <el-table
      v-loading="loading"
      :data="tenants"
      row-key="id"
      height="100%"
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
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button text type="primary" size="small" @click="openEdit(row)">
            <i class="fa fa-edit"></i>
            编辑
          </el-button>
          <el-button
            v-if="!row.isSuperAdmin"
            text
            :type="row.isActive ? 'warning' : 'success'"
            size="small"
            @click="handleToggleActive(row)"
          >
            <i class="fa" :class="row.isActive ? 'fa-ban' : 'fa-check'"></i>
            {{ row.isActive ? '停用' : '启用' }}
          </el-button>
          <el-button
            v-if="row.email"
            text
            type="primary"
            size="small"
            @click="openResetPassword(row)"
          >
            <i class="fa fa-key"></i>
            重置密码
          </el-button>
        </template>
      </el-table-column>
    </el-table>

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
  height: calc(100vh - 108px);
  gap: 16px;
  overflow: hidden;
  min-width: 0;
}

.admin-page .el-table {
  flex: 1;
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
    height: calc(100vh - 72px);
  }
}
</style>
