<script setup lang="ts">
import AdminLoadNotice from '@/components/admin/AdminLoadNotice.vue'
import { Delete, Edit, Link, Plus, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getAdminCategories, getAdminLinks } from '@/api/admin'
import { createLink, deleteLink, updateLink } from '@/api/links'
import PortalLinkDialog from '@/components/portal/PortalLinkDialog.vue'
import type {
  AdminCategoryItem,
  AdminLinkItem,
  PortalCategoryOption,
  PortalLink,
  SavePortalLinkRequest,
} from '@/types/models'

const route = useRoute()
const targetTenantId = computed(() => (route.meta?.targetTenantId as string) || undefined)

const loading = ref(false)
const saving = ref(false)
const keyword = ref('')
const categoryId = ref('')
const categories = ref<AdminCategoryItem[]>([])
const allLinks = ref<AdminLinkItem[]>([])
const dialogVisible = ref(false)
const editingLink = ref<PortalLink | null>(null)

const page = ref(1)
const pageSize = ref(20)

const categoryOptions = computed<PortalCategoryOption[]>(() =>
  categories.value.map((item) => ({
    id: item.id,
    parentId: item.parentId,
    name: item.parentName ? `${item.parentName} / ${item.name}` : item.name,
    level: item.parentId ? 2 : 1,
  })),
)

const total = computed(() => allLinks.value.length)

const pagedLinks = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return allLinks.value.slice(start, start + pageSize.value)
})

const categoriesError = ref('')
const linksError = ref('')
const categoriesReady = ref(false)
const categoriesLoading = ref(false)
let categoryRequestId = 0
async function loadCategories() {
  const requestId = ++categoryRequestId
  categoriesLoading.value = true
  categoriesError.value = ''
  try {
    const result = await getAdminCategories(targetTenantId.value)
    if (requestId !== categoryRequestId) return
    categories.value = result
    categoriesReady.value = true
  } catch (error: any) {
    if (requestId === categoryRequestId) categoriesError.value = error?.response?.data?.message || '分类读取失败，可重新加载；链接列表仍可查看。'
  } finally {
    if (requestId === categoryRequestId) categoriesLoading.value = false
  }
}

let linksRequestId = 0
onBeforeUnmount(() => { ++linksRequestId; ++categoryRequestId })
async function loadLinks() {
  const requestId = ++linksRequestId
  loading.value = true
  linksError.value = ''

  try {
    const result = await getAdminLinks({
      keyword: keyword.value || undefined,
      categoryId: categoryId.value || undefined,
      targetTenantId: targetTenantId.value,
    })
    if (requestId !== linksRequestId) return
    allLinks.value = result
    page.value = 1
  } catch (error: any) {
    if (requestId === linksRequestId) linksError.value = error?.response?.data?.message || '链接读取失败，请重新加载。'
  } finally {
    if (requestId === linksRequestId) loading.value = false
  }
}

function handlePageChange(val: number) {
  page.value = val
}

function handleSizeChange(val: number) {
  pageSize.value = val
  page.value = 1
}

function openCreate() {
  editingLink.value = null
  dialogVisible.value = true
}

function openEdit(item: AdminLinkItem) {
  editingLink.value = {
    id: item.id,
    categoryId: item.categoryId,
    categoryName: item.categoryName,
    title: item.title,
    url: item.url,
    standbyUrl: item.standbyUrl,
    description: item.description,
    tags: item.tags,
    iconUrl: item.iconUrl,
    fontIcon: '',
    isPinned: item.isPinned,
    sortOrder: item.sortOrder,
    clickCount: item.clickCount,
    checkStatus: 0,
    lastCheckedAt: null,
    lastVisitedAt: null,
  }
  dialogVisible.value = true
}

async function submit(payload: SavePortalLinkRequest) {
  if (saving.value || !categoriesReady.value) return
  saving.value = true

  try {
    if (editingLink.value) {
      await updateLink(editingLink.value.id, payload, targetTenantId.value)
      ElMessage.success('链接已更新。')
    } else {
      await createLink(payload, targetTenantId.value)
      ElMessage.success('链接已创建。')
    }

    dialogVisible.value = false
    await loadLinks()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '链接保存失败。')
  } finally {
    saving.value = false
  }
}

async function remove(item: AdminLinkItem) {
  const confirmed = await ElMessageBox.confirm(`确认删除链接「${item.title}」吗？`, '删除链接', {
    type: 'warning',
  }).then(() => true, () => false)
  if (!confirmed) return

  try {
    await deleteLink(item.id, targetTenantId.value)
    ElMessage.success('链接已删除。')
    await loadLinks()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '链接删除失败。')
  }
}

watch(() => route.path, async () => {
  categoriesReady.value = false
  categories.value = []
  allLinks.value = []
  await Promise.all([loadCategories(), loadLinks()])
})

onMounted(async () => {
  await Promise.all([loadCategories(), loadLinks()])
})
</script>

<template>
  <div class="admin-page">
    <AdminLoadNotice :message="categoriesError" :loading="categoriesLoading" @retry="loadCategories" />
    <AdminLoadNotice :message="linksError" :loading="loading" @retry="loadLinks" />
    <div class="admin-toolbar">
      <div class="admin-toolbar__left">
        <el-input v-model="keyword" placeholder="搜索标题或链接" aria-label="搜索标题或链接" clearable @clear="loadLinks" style="width: 220px" @keyup.enter="loadLinks">
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <el-select v-model="categoryId" clearable placeholder="筛选分类" style="width: 220px" @change="loadLinks">
          <el-option
            v-for="item in categories"
            :key="item.id"
            :label="item.parentName ? `${item.parentName} / ${item.name}` : item.name"
            :value="item.id"
          />
        </el-select>
        <el-button type="primary" plain @click="loadLinks">查询</el-button>
      </div>

      <div class="admin-toolbar__left">
        <el-button type="primary" :disabled="!categoriesReady" @click="openCreate">
          <el-icon><Plus /></el-icon>
          添加链接
        </el-button>
        <RouterLink to="/admin/import">
          <el-button plain>
            <el-icon><Link /></el-icon>
            书签导入
          </el-button>
        </RouterLink>
        <el-button @click="loadLinks">
          <el-icon><Refresh /></el-icon>
          刷新
        </el-button>
      </div>
    </div>

    <el-table v-loading="loading" :data="pagedLinks" row-class-name="link-table__row" height="100%">
      <template #empty><span>{{ linksError ? '链接读取未完成，请重新加载。' : keyword || categoryId ? '没有符合筛选条件的链接。' : '暂无链接，添加一个常用网址吧。' }}</span></template>
      <el-table-column prop="title" label="标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="categoryName" label="分类" min-width="120" show-overflow-tooltip />
      <el-table-column prop="url" label="主链接" min-width="240" show-overflow-tooltip />
      <el-table-column label="置顶" width="70">
        <template #default="{ row }">
          {{ row.isPinned ? '是' : '否' }}
        </template>
      </el-table-column>
      <el-table-column prop="clickCount" label="点击数" width="80" />
      <el-table-column prop="sortOrder" label="排序值" width="90" />
      <el-table-column label="更新时间" width="180">
        <template #default="{ row }">
          {{ new Date(row.updatedAt).toLocaleString() }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button text aria-label="编辑链接" title="编辑链接" :disabled="!categoriesReady" @click="openEdit(row)">
            <el-icon><Edit /></el-icon>
          </el-button>
          <el-button text type="danger" aria-label="删除链接" title="删除链接" @click="remove(row)">
            <el-icon><Delete /></el-icon>
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="admin-pagination">
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :total="total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next, jumper"
        background
        @current-change="handlePageChange"
        @size-change="handleSizeChange"
      />
    </div>

    <PortalLinkDialog
      v-model="dialogVisible"
      :link="editingLink"
      :category-options="categoryOptions"
      :submitting="saving"
      @submit="submit"
    />
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

@media (max-width: 760px) {
  .admin-page {
    height: auto;
    min-height: calc(100dvh - 108px);
    overflow: visible;
  }

  .admin-page .el-table { flex: none; height: 380px !important; }

  .admin-toolbar__left .el-input {
    width: 100% !important;
  }

  .admin-toolbar__left .el-select {
    width: 100% !important;
  }
}

.admin-pagination {
  display: flex;
  justify-content: flex-end;
}
</style>
