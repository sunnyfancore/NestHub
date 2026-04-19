<script setup lang="ts">
import { Delete, Edit, Plus, Refresh, Sort } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import Sortable from 'sortablejs'
import { computed, nextTick, onMounted, onBeforeUnmount, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getAdminCategories } from '@/api/admin'
import { deleteCategory, updateCategory, createCategory, sortCategories } from '@/api/categories'
import PortalCategoryDialog from '@/components/portal/PortalCategoryDialog.vue'
import type {
  AdminCategoryItem,
  PortalCategory,
  PortalCategoryOption,
  SavePortalCategoryRequest,
} from '@/types/models'

interface TreeNode {
  item: AdminCategoryItem
  children: AdminCategoryItem[]
}

const route = useRoute()
const targetTenantId = computed(() => (route.meta?.targetTenantId as string) || undefined)

const loading = ref(false)
const saving = ref(false)
const items = ref<AdminCategoryItem[]>([])
const dialogVisible = ref(false)
const editingCategory = ref<PortalCategory | null>(null)
const treeRef = ref<HTMLElement | null>(null)
const childRefs = ref<Record<string, HTMLElement | null>>({})
const sortables = ref<any[]>([])
const expandedNodes = ref<Record<string, boolean>>({})

const tree = computed<TreeNode[]>(() => {
  const parents = items.value.filter((i) => !i.parentId)
  return parents.map((p) => ({
    item: p,
    children: items.value.filter((i) => i.parentId === p.id),
  }))
})

function isExpanded(id: string) {
  return !!expandedNodes.value[id]
}

function toggleAll(expand: boolean) {
  for (const node of tree.value) {
    expandedNodes.value[node.item.id] = expand
  }
}

function toggleExpand(node: TreeNode) {
  expandedNodes.value[node.item.id] = !expandedNodes.value[node.item.id]
}

const parentOptions = computed<PortalCategoryOption[]>(() =>
  items.value
    .filter((item) => !item.parentId)
    .map((item) => ({
      id: item.id,
      parentId: item.parentId,
      name: item.name,
      level: 1,
    })),
)

async function load() {
  loading.value = true
  try {
    items.value = await getAdminCategories(targetTenantId.value)
    await nextTick()
    initSortables()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取分类列表失败。')
  } finally {
    loading.value = false
  }
}

function openCreate(parentId?: string) {
  editingCategory.value = null
  dialogVisible.value = true
  _defaultParentId.value = parentId || null
}

const _defaultParentId = ref<string | null>(null)

function openEdit(item: AdminCategoryItem) {
  editingCategory.value = {
    id: item.id,
    parentId: item.parentId,
    name: item.name,
    description: item.description,
    icon: item.icon,
    sortOrder: item.sortOrder,
    links: [],
    children: [],
  }
  dialogVisible.value = true
}

async function submit(payload: SavePortalCategoryRequest) {
  saving.value = true
  try {
    if (editingCategory.value) {
      await updateCategory(editingCategory.value.id, payload)
      ElMessage.success('分类已更新。')
    } else {
      await createCategory(payload, targetTenantId.value)
      ElMessage.success('分类已创建。')
    }
    dialogVisible.value = false
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '分类保存失败。')
  } finally {
    saving.value = false
  }
}

async function remove(item: AdminCategoryItem) {
  const hasChildren = items.value.some((i) => i.parentId === item.id)
  if (hasChildren) {
    ElMessage.warning('该分类下有子分类，请先删除或移动子分类。')
    return
  }
  await ElMessageBox.confirm(`确认删除分类「${item.name}」吗？`, '删除分类', { type: 'warning' })
  try {
    await deleteCategory(item.id)
    ElMessage.success('分类已删除。')
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '分类删除失败。')
  }
}

function destroySortables() {
  for (const s of sortables.value) s.destroy()
  sortables.value = []
}

function initSortables() {
  destroySortables()

  if (!treeRef.value) return

  // Parent level drag
  sortables.value.push(
    Sortable.create(treeRef.value, {
      animation: 160,
      draggable: '.cat-tree__parent',
      ghostClass: 'cat-tree__parent--ghost',
      onEnd: async () => {
        if (!treeRef.value) return
        const orderedIds = Array.from(treeRef.value.querySelectorAll<HTMLElement>('.cat-tree__parent'))
          .map((el) => el.dataset.id || '')
          .filter(Boolean)
        try {
          await sortCategories(orderedIds)
          ElMessage.success('分类排序已更新。')
          await load()
        } catch (error: any) {
          ElMessage.error(error?.response?.data?.message || '排序失败。')
        }
      },
    }),
  )

  // Children level drag
  for (const node of tree.value) {
    const container = childRefs.value[node.item.id]
    if (!container || node.children.length <= 1) continue

    sortables.value.push(
      Sortable.create(container, {
        animation: 160,
        draggable: '.cat-tree__child',
        ghostClass: 'cat-tree__child--ghost',
        onEnd: async () => {
          const orderedIds = Array.from(container.querySelectorAll<HTMLElement>('.cat-tree__child'))
            .map((el) => el.dataset.id || '')
            .filter(Boolean)
          try {
            await sortCategories(orderedIds)
            ElMessage.success('子分类排序已更新。')
            await load()
          } catch (error: any) {
            ElMessage.error(error?.response?.data?.message || '排序失败。')
          }
        },
      }),
    )
  }
}

async function resetSortOrder() {
  await ElMessageBox.confirm('将按当前显示顺序重新分配排序值（步长10），确认继续？', '重置排序', { type: 'info' })
  try {
    // Must send same-level groups separately
    const parentIds = tree.value.map((n) => n.item.id)
    if (parentIds.length) {
      await sortCategories(parentIds)
    }
    for (const node of tree.value) {
      if (node.children.length) {
        await sortCategories(node.children.map((c) => c.id))
      }
    }
    ElMessage.success('排序已重置。')
    await load()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '重置失败。')
  }
}

watch(() => route.path, load)

onMounted(load)
onBeforeUnmount(destroySortables)
</script>

<template>
  <div class="admin-page">
    <div class="admin-toolbar">
      <div class="admin-toolbar__left">
        <el-button type="primary" @click="openCreate()">
          <el-icon><Plus /></el-icon>
          添加分类
        </el-button>
        <el-button @click="resetSortOrder">
          <el-icon><Sort /></el-icon>
          重置排序
        </el-button>
        <el-button @click="toggleAll(true)">全部展开</el-button>
        <el-button @click="toggleAll(false)">全部折叠</el-button>
      </div>
      <el-button @click="load">
        <el-icon><Refresh /></el-icon>
        刷新
      </el-button>
    </div>

    <div v-loading="loading" ref="treeRef" class="cat-tree">
      <div
        v-for="node in tree"
        :key="node.item.id"
        class="cat-tree__parent"
        :data-id="node.item.id"
      >
        <div class="cat-tree__row">
          <button class="cat-tree__drag" type="button" title="拖拽排序">
            <i class="fa fa-grip-vertical"></i>
          </button>
          <button v-if="node.children.length" class="cat-tree__toggle" type="button" @click="toggleExpand(node)">
            <i class="fa" :class="isExpanded(node.item.id) ? 'fa-caret-down' : 'fa-caret-right'"></i>
          </button>
          <span v-else class="cat-tree__toggle-placeholder"></span>
          <i v-if="node.item.icon" :class="['fa', node.item.icon, 'cat-tree__icon']"></i>
          <i v-else class="fa fa-folder cat-tree__icon"></i>
          <span class="cat-tree__name">{{ node.item.name }}</span>
          <span class="cat-tree__count">{{ node.item.linkCount }} 链接</span>
          <div class="cat-tree__actions">
            <el-button text size="small" @click="openCreate(node.item.id)">
              <el-icon><Plus /></el-icon>
            </el-button>
            <el-button text size="small" @click="openEdit(node.item)">
              <el-icon><Edit /></el-icon>
            </el-button>
            <el-button text size="small" type="danger" @click="remove(node.item)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
        </div>

        <transition name="cat-slide">
          <div
            v-if="isExpanded(node.item.id) && node.children.length"
            class="cat-tree__children"
            :ref="(el: any) => { childRefs[node.item.id] = el as HTMLElement | null }"
          >
            <div
              v-for="child in node.children"
              :key="child.id"
              class="cat-tree__child"
              :data-id="child.id"
            >
              <div class="cat-tree__row cat-tree__row--child">
                <button class="cat-tree__drag" type="button" title="拖拽排序">
                  <i class="fa fa-grip-vertical"></i>
                </button>
                <span class="cat-tree__indent"></span>
                <i v-if="child.icon" :class="['fa', child.icon, 'cat-tree__icon']"></i>
                <i v-else class="fa fa-file-o cat-tree__icon"></i>
                <span class="cat-tree__name">{{ child.name }}</span>
                <span class="cat-tree__count">{{ child.linkCount }} 链接</span>
                <div class="cat-tree__actions">
                  <el-button text size="small" @click="openEdit(child)">
                    <el-icon><Edit /></el-icon>
                  </el-button>
                  <el-button text size="small" type="danger" @click="remove(child)">
                    <el-icon><Delete /></el-icon>
                  </el-button>
                </div>
              </div>
            </div>
          </div>
        </transition>
      </div>

      <div v-if="!tree.length && !loading" class="cat-tree__empty">暂无分类，点击上方按钮添加。</div>
    </div>

    <PortalCategoryDialog
      v-model="dialogVisible"
      :category="editingCategory"
      :parent-options="parentOptions"
      :default-parent-id="_defaultParentId"
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

.cat-tree {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
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
  flex-wrap: wrap;
}

@media (max-width: 760px) {
  .admin-page {
    height: calc(100vh - 72px);
  }
}

/* ── tree ── */
.cat-tree {
  background: #fff;
  border: 1px solid #e8eaed;
  border-radius: 8px;
  overflow: hidden;
}

.cat-tree__parent {
  border-bottom: 1px solid #f0f0f0;
}
.cat-tree__parent:last-child {
  border-bottom: none;
}
.cat-tree__parent--ghost {
  opacity: 0.3;
}

.cat-tree__row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  min-height: 48px;
  transition: background 0.15s;
  user-select: none;
}
.cat-tree__row:hover {
  background: #f9fafb;
}

.cat-tree__row--child {
  background: #fafbfc;
  border-top: 1px solid #f2f3f5;
}

.cat-tree__drag {
  border: 0;
  background: transparent;
  color: #ccc;
  cursor: grab;
  padding: 4px 8px;
  font-size: 16px;
  flex-shrink: 0;
  border-radius: 4px;
  transition: all 0.15s;
}
.cat-tree__drag:hover {
  color: #667eea;
  background: #f0edff;
}
.cat-tree__drag:active {
  cursor: grabbing;
}

.cat-tree__toggle {
  border: 0;
  background: transparent;
  color: #999;
  cursor: pointer;
  padding: 4px;
  font-size: 18px;
  width: 28px;
  height: 28px;
  text-align: center;
  flex-shrink: 0;
  border-radius: 4px;
  transition: all 0.15s;
}
.cat-tree__toggle:hover {
  color: #333;
  background: #f0f0f0;
}
.cat-tree__toggle-placeholder {
  width: 28px;
  flex-shrink: 0;
}

.cat-tree__icon {
  width: 18px;
  text-align: center;
  font-size: 14px;
  color: #667eea;
  flex-shrink: 0;
}

.cat-tree__name {
  flex: 1;
  min-width: 0;
  font-size: 14px;
  font-weight: 600;
  color: #1a1a2e;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cat-tree__row--child .cat-tree__name {
  font-weight: 500;
}

.cat-tree__tag {
  flex-shrink: 0;
}

.cat-tree__count {
  color: #999;
  font-size: 12px;
  flex-shrink: 0;
}

.cat-tree__indent {
  width: 20px;
  flex-shrink: 0;
}

.cat-tree__actions {
  display: flex;
  gap: 2px;
  flex-shrink: 0;
}

.cat-tree__actions :deep(.el-button) {
  font-size: 18px !important;
  padding: 8px !important;
}

.cat-tree__children {
  overflow: hidden;
}
.cat-tree__child--ghost {
  opacity: 0.3;
}

.cat-tree__empty {
  padding: 40px;
  text-align: center;
  color: #aaa;
  font-size: 14px;
}

.cat-slide-enter-active,
.cat-slide-leave-active {
  transition: max-height 0.25s ease, opacity 0.2s ease;
}
.cat-slide-enter-from,
.cat-slide-leave-to {
  max-height: 0;
  opacity: 0;
}
.cat-slide-enter-to,
.cat-slide-leave-from {
  max-height: 600px;
  opacity: 1;
}
</style>
