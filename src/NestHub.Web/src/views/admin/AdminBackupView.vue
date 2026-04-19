<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { exportBackup, exportBookmarkHtml } from '@/api/admin'

const loadingJson = ref(false)
const loadingHtml = ref(false)

async function handleExportJson() {
  loadingJson.value = true
  try {
    const payload = await exportBackup()
    const blob = new Blob([JSON.stringify(payload, null, 2)], {
      type: 'application/json;charset=utf-8',
    })
    downloadBlob(blob, `nesthub-backup-${dateStr()}.json`)
    ElMessage.success('JSON 备份已导出。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '导出失败。')
  } finally {
    loadingJson.value = false
  }
}

async function handleExportHtml() {
  loadingHtml.value = true
  try {
    const blob = await exportBookmarkHtml()
    downloadBlob(blob, `bookmarks_${dateStr()}.html`)
    ElMessage.success('书签文件已导出，可直接导入 Chrome 书签管理器。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '导出失败。')
  } finally {
    loadingHtml.value = false
  }
}

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}

function dateStr() {
  return new Date().toISOString().slice(0, 10)
}
</script>

<template>
  <div class="admin-page">
    <div class="backup-card">
      <div class="backup-card__icon"><i class="fa fa-bookmark"></i></div>
      <h3>导出书签 (HTML)</h3>
      <p>导出为 Chrome 书签管理器兼容的 Netscape HTML 格式，可直接导入 Chrome / Edge / Firefox 等浏览器。</p>
      <el-button type="primary" :loading="loadingHtml" @click="handleExportHtml">
        <i class="fa fa-chrome" style="margin-right: 6px;"></i>
        导出书签 HTML
      </el-button>
    </div>

    <div class="backup-card">
      <div class="backup-card__icon"><i class="fa fa-download"></i></div>
      <h3>导出完整数据 (JSON)</h3>
      <p>导出当前租户的站点设置、分类和链接为 JSON 文件，用于手动备份或数据迁移。</p>
      <el-button :loading="loadingJson" @click="handleExportJson">
        <i class="fa fa-file-code-o" style="margin-right: 6px;"></i>
        导出 JSON 备份
      </el-button>
    </div>

    <div class="backup-note">
      <i class="fa fa-info-circle"></i>
      <span>书签 HTML 格式与 Chrome 书签管理器完全兼容，支持分类层级和图标信息。</span>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 16px;
}

.backup-card {
  padding: 24px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
  display: grid;
  gap: 8px;
}

.backup-card__icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: #f0f5ff;
  color: #5b9aff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
}

.backup-card h3 {
  margin: 0;
  font-size: 17px;
  font-weight: 700;
  color: #1a1a2e;
}

.backup-card p {
  margin: 0;
  color: #8896a6;
  font-size: 14px;
  line-height: 1.6;
}

.backup-note {
  padding: 14px 18px;
  background: #f0f5ff;
  border-radius: 10px;
  color: #5b8ab5;
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
