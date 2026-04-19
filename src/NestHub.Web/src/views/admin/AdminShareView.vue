<script setup lang="ts">
import { Delete, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { getAdminCategories } from '@/api/admin'
import { createShareLink, deleteShareLink, getShareLinks, type ShareLinkItem } from '@/api/share'
import type { AdminCategoryItem } from '@/types/models'

const loading = ref(false)
const saving = ref(false)
const shares = ref<ShareLinkItem[]>([])
const categories = ref<AdminCategoryItem[]>([])
const dialogVisible = ref(false)

const form = ref({
  categoryId: '',
  password: '',
  note: '',
  expireAt: '',
})

const categoryOptions = computed(() => categories.value)

async function load() {
  loading.value = true

  try {
    categories.value = await getAdminCategories()
    shares.value = await getShareLinks()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取分享列表失败。')
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!form.value.categoryId) {
    ElMessage.warning('请选择要分享的分类。')
    return
  }

  saving.value = true

  try {
    await createShareLink({
      categoryId: form.value.categoryId,
      password: form.value.password || undefined,
      note: form.value.note || undefined,
      expireAt: form.value.expireAt || undefined,
    })
    ElMessage.success('分享链接已创建。')
    dialogVisible.value = false
    form.value = {
      categoryId: '',
      password: '',
      note: '',
      expireAt: '',
    }
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '创建分享失败。')
  } finally {
    saving.value = false
  }
}

async function remove(item: ShareLinkItem) {
  await ElMessageBox.confirm(`确认删除分享「${item.shareCode}」吗？`, '删除分享', {
    type: 'warning',
  })

  try {
    await deleteShareLink(item.id)
    ElMessage.success('分享记录已删除。')
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '删除分享失败。')
  }
}

async function copy(item: ShareLinkItem) {
  const shareUrl = `${window.location.origin}/#/share/${item.shareCode}`
  const text = item.password
    ? `分享链接：${shareUrl} 密码：${item.password}`
    : `分享链接：${shareUrl}`

  await navigator.clipboard.writeText(text)
  ElMessage.success('分享链接已复制。')
}

onMounted(load)
</script>

<template>
  <div class="admin-page">
    <div class="admin-toolbar">
      <el-button type="primary" @click="dialogVisible = true">
        <el-icon><Plus /></el-icon>
        创建分享
      </el-button>
      <el-button @click="load">
        <el-icon><Refresh /></el-icon>
        刷新
      </el-button>
    </div>

    <el-table v-loading="loading" :data="shares">
      <el-table-column prop="shareCode" label="分享码" width="140" />
      <el-table-column prop="categoryName" label="分类名称" min-width="180" />
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">
          {{ new Date(row.createdAt).toLocaleString() }}
        </template>
      </el-table-column>
      <el-table-column prop="expireAt" label="过期时间" width="180">
        <template #default="{ row }">
          {{ row.expireAt ? new Date(row.expireAt).toLocaleString() : '不过期' }}
        </template>
      </el-table-column>
      <el-table-column prop="password" label="密码" width="120" />
      <el-table-column prop="note" label="备注" min-width="180" />
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button text @click="copy(row)">复制</el-button>
          <el-button text type="danger" @click="remove(row)">
            <el-icon><Delete /></el-icon>
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="创建分享" width="520px">
      <el-form label-position="top">
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
        <el-form-item label="过期时间">
          <el-date-picker
            v-model="form.expireAt"
            type="datetime"
            placeholder="可选"
            style="width: 100%"
            value-format="YYYY-MM-DDTHH:mm:ss"
          />
        </el-form-item>
        <el-form-item label="访问密码">
          <el-input v-model="form.password" placeholder="留空表示公开分享" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.note" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submit">创建</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
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
