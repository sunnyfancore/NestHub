<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import { getAdminCategories, importBookmarks } from '@/api/admin'
import type { AdminCategoryItem, BookmarkImportResult } from '@/types/models'

const loading = ref(false)
const categoryId = ref('')
const categories = ref<AdminCategoryItem[]>([])
const file = ref<File | null>(null)
const result = ref<BookmarkImportResult | null>(null)

async function loadCategories() {
  categories.value = await getAdminCategories()
}

function handleFileChange(uploadFile: any) {
  file.value = uploadFile.raw || null
}

async function submit() {
  if (!file.value) {
    ElMessage.warning('请选择书签 HTML 文件。')
    return
  }

  loading.value = true

  try {
    result.value = await importBookmarks(categoryId.value || undefined, file.value)
    ElMessage.success('书签导入完成。')
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '书签导入失败。')
  } finally {
    loading.value = false
  }
}

loadCategories()
</script>

<template>
  <div class="admin-page">
    <div class="import-card">
      <h3>导入浏览器书签</h3>
      <p>支持导入浏览器导出的 HTML 书签文件。不选分类时，将自动根据书签文件中的文件夹结构创建分类。</p>

      <el-form label-position="top">
        <el-form-item label="目标分类（可选）">
          <el-select v-model="categoryId" clearable placeholder="不选则自动创建分类" style="width: 320px">
            <el-option
              v-for="item in categories"
              :key="item.id"
              :label="item.parentName ? `${item.parentName} / ${item.name}` : item.name"
              :value="item.id"
            />
          </el-select>
        </el-form-item>

        <el-form-item label="书签文件">
          <el-upload
            drag
            action="#"
            :auto-upload="false"
            :show-file-list="true"
            :limit="1"
            accept=".html,.htm"
            @change="handleFileChange"
          >
            <div class="el-upload__text">
              将 HTML 书签文件拖到这里，或点击选择文件
            </div>
          </el-upload>
        </el-form-item>

        <el-button type="primary" :loading="loading" @click="submit">开始导入</el-button>
      </el-form>
    </div>

    <div v-if="result" class="import-result">
      <div class="result-item">
        <span>总数</span>
        <strong>{{ result.total }}</strong>
      </div>
      <div class="result-item">
        <span>成功导入</span>
        <strong>{{ result.imported }}</strong>
      </div>
      <div class="result-item">
        <span>失败</span>
        <strong>{{ result.failed }}</strong>
      </div>
    </div>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 18px;
}

.import-card,
.import-result {
  padding: 20px;
  background: #fff;
  border-radius: 10px;
  border: 1px solid #e8ecf0;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.import-card h3 {
  margin: 0;
  font-size: 22px;
}

.import-card p {
  margin: 8px 0 20px;
  color: #7b8798;
}

.import-result {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 14px;
}

.result-item {
  padding: 16px;
  border-radius: 10px;
  background: #f8f9fb;
}

.result-item span {
  display: block;
  color: #7b8798;
  margin-bottom: 8px;
}

.result-item strong {
  font-size: 24px;
}
</style>
