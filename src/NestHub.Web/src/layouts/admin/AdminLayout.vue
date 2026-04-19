<script setup lang="ts">
import {
  Collection,
  Download,
  House,
  Link,
  PictureFilled,
  Setting,
  UploadFilled,
  Key,
  Share,
  Filter,
  OfficeBuilding,
  Lock,
  Grid,
} from '@element-plus/icons-vue'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ChangePasswordDialog from '@/components/portal/ChangePasswordDialog.vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const sidebarOpen = ref(false)
const changePasswordVisible = ref(false)

const allSections = [
  {
    title: '分类管理',
    children: [
      { label: '分类列表', to: '/admin/categories', icon: Collection },
    ],
  },
  {
    title: '链接管理',
    children: [
      { label: '我的链接', to: '/admin/links', icon: Link },
      { label: '书签导入', to: '/admin/import', icon: UploadFilled },
      { label: '书签分享', to: '/admin/share', icon: Share },
    ],
  },
  {
    title: '系统设置',
    children: [
      { label: '站点设置', to: '/admin/site', icon: Setting },
      { label: '主题设置', to: '/admin/themes', icon: PictureFilled },
      { label: '过滤页面', to: '/admin/transition', icon: Filter },
      { label: '数据备份', to: '/admin/backup', icon: Download },
      { label: '获取 API', to: '/admin/api', icon: Key },
    ],
  },
  {
    title: '超管',
    superAdminOnly: true,
    children: [
      { label: '租户管理', to: '/admin/tenants', icon: OfficeBuilding },
      { label: '公共分类', to: '/admin/public-categories', icon: Grid },
      { label: '公共链接', to: '/admin/public-links', icon: Link },
      { label: '安全设置', to: '/admin/security', icon: Lock },
    ],
  },
]

const sections = computed(() => {
  if (authStore.isSuperAdmin) return allSections
  return allSections.filter(s => !s.superAdminOnly)
})

const currentTitle = computed(() => {
  for (const section of allSections) {
    const current = section.children.find((item) => item.to === route.path)
    if (current) {
      return current.label
    }
  }

  return '后台首页'
})

function goPortal() {
  router.push('/')
}

function handleUserCommand(command: string) {
  if (command === 'password') {
    changePasswordVisible.value = true
  } else if (command === 'logout') {
    logout()
  }
}

function logout() {
  authStore.clear()
  localStorage.removeItem('nesthub-portal-cache')
  router.push('/login')
}
</script>

<template>
  <div class="admin-shell">
    <div v-if="sidebarOpen" class="admin-overlay" @click="sidebarOpen = false"></div>
    <aside class="admin-sidebar" :class="{ 'is-open': sidebarOpen }">
      <div class="admin-brand">
        <div class="admin-brand__icon">N</div>
        <h1>后台管理</h1>
      </div>

      <div class="admin-nav">
        <section v-for="section in sections" :key="section.title" class="admin-nav__section">
          <div class="admin-nav__title">{{ section.title }}</div>
          <RouterLink
            v-for="item in section.children"
            :key="item.to"
            :to="item.to"
            class="admin-nav__item"
            :class="{ 'is-active': route.path === item.to }"
          >
            <el-icon><component :is="item.icon" /></el-icon>
            <span>{{ item.label }}</span>
          </RouterLink>
        </section>
      </div>
    </aside>

    <main class="admin-main">
      <header class="admin-topbar">
        <div class="admin-topbar__left">
          <button class="admin-hamburger" type="button" @click="sidebarOpen = !sidebarOpen">
            <i class="fa fa-bars"></i>
          </button>
          <span class="admin-topbar__title">{{ currentTitle }}</span>
        </div>

        <div class="admin-topbar__right">
          <el-button text size="small" @click="goPortal">
            <el-icon><House /></el-icon>
            前台首页
          </el-button>
          <el-dropdown trigger="click" @command="handleUserCommand">
            <div class="admin-avatar">
              <i class="fa fa-user-circle"></i>
            </div>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item disabled>
                  <div class="admin-dropdown-info">
                    <strong>{{ authStore.user?.displayName || '管理员' }}</strong>
                    <span>{{ authStore.tenant?.name || '当前租户' }}</span>
                  </div>
                </el-dropdown-item>
                <el-dropdown-item divided command="password">
                  <i class="fa fa-key"></i>修改密码
                </el-dropdown-item>
                <el-dropdown-item command="logout">
                  <i class="fa fa-sign-out"></i>退出登录
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </header>

      <div class="admin-content">
        <RouterView />
      </div>
    </main>

    <ChangePasswordDialog v-model="changePasswordVisible" />
  </div>
</template>

<style scoped>
.admin-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: 230px 1fr;
  background: #f0f2f5;
  overflow-x: hidden;
}

.admin-sidebar {
  background: linear-gradient(180deg, #1e2a3a 0%, #263345 100%);
  color: #dfe5ec;
  display: flex;
  flex-direction: column;
  position: sticky;
  top: 0;
  align-self: start;
  min-height: 100vh;
  overflow-y: auto;
}

.admin-sidebar::-webkit-scrollbar {
  width: 4px;
}

.admin-sidebar::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.12);
  border-radius: 2px;
}

.admin-brand {
  padding: 20px 18px 22px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  display: flex;
  align-items: center;
  gap: 12px;
}

.admin-brand__icon {
  width: 36px;
  height: 36px;
  border-radius: 10px;
  background: linear-gradient(135deg, #5b9aff 0%, #3d7be0 100%);
  box-shadow: 0 4px 12px rgba(59, 125, 233, 0.3);
  color: #fff;
  font-size: 18px;
  font-weight: 800;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.admin-brand h1 {
  margin: 0;
  font-size: 17px;
  font-weight: 700;
  color: #fff;
}

.admin-nav {
  padding: 10px 0;
  flex: 1;
}

.admin-nav__section + .admin-nav__section {
  margin-top: 6px;
}

.admin-nav__title {
  padding: 14px 20px 8px;
  color: rgba(255, 255, 255, 0.4);
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.admin-nav__item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 20px;
  margin: 1px 8px;
  border-radius: 8px;
  color: rgba(255, 255, 255, 0.65);
  font-size: 14px;
  transition: all 0.15s ease;
}

.admin-nav__item:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #fff;
}

.admin-nav__item.is-active {
  background: rgba(91, 154, 255, 0.18);
  color: #8ec5ff;
  font-weight: 600;
}

.admin-main {
  min-width: 0;
  display: grid;
  grid-template-rows: auto 1fr;
}

.admin-topbar {
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid #e8ecf0;
  padding: 0 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.admin-topbar__left,
.admin-topbar__right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.admin-topbar__title {
  font-size: 16px;
  font-weight: 700;
  color: #1a1a2e;
}

.admin-user {
  display: grid;
  justify-items: end;
}

.admin-user strong {
  font-size: 14px;
  color: #333;
}

.admin-user span {
  font-size: 12px;
  color: #999;
}

.admin-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: box-shadow 0.2s;
}

.admin-avatar:hover {
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.25);
}

.admin-avatar i {
  font-size: 20px;
  color: #fff;
}

.admin-dropdown-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  line-height: 1.4;
}

.admin-dropdown-info strong {
  font-size: 13px;
  color: #333;
}

.admin-dropdown-info span {
  font-size: 12px;
  color: #999;
}

.admin-content {
  padding: 24px;
  overflow: hidden;
  min-width: 0;
}

.admin-content :deep(.el-button) {
  font-size: 14px;
}

.admin-content :deep(.el-table .el-button) {
  font-size: 16px;
  padding: 6px;
}

.admin-content :deep(.el-table .el-button--text) {
  font-size: 16px;
}

.admin-hamburger {
  display: none;
  border: 0;
  background: transparent;
  font-size: 18px;
  color: #555;
  cursor: pointer;
  padding: 4px 8px;
}

.admin-overlay {
  display: none;
}

@media (max-width: 980px) {
  .admin-hamburger {
    display: block;
  }

  .admin-overlay {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.3);
    z-index: 99;
  }

  .admin-content {
    padding: 12px;
    overflow: auto;
  }

  .admin-topbar {
    padding: 0 12px;
    min-height: 48px;
    height: auto;
    flex-wrap: wrap;
  }

  .admin-topbar__left {
    gap: 8px;
    min-width: 0;
  }

  .admin-topbar__title {
    font-size: 14px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .admin-topbar__right {
    flex-shrink: 0;
  }
}

@media (max-width: 980px) {
  .admin-shell {
    grid-template-columns: 1fr;
  }

  .admin-sidebar {
    position: fixed;
    top: 0;
    left: 0;
    bottom: 0;
    width: 240px;
    z-index: 100;
    transform: translateX(-100%);
    transition: transform 0.25s ease;
  }

  .admin-sidebar.is-open {
    transform: translateX(0);
  }
}
</style>
