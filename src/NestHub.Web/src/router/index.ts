import { createRouter, createWebHistory } from 'vue-router'
import { AUTH_TOKEN_KEY } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'portal',
      component: () => import('@/views/PortalView.vue'),
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: {
        public: true,
      },
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: {
        public: true,
      },
    },
    {
      path: '/reset-password/confirm',
      name: 'confirm-reset-password',
      component: () => import('@/views/ConfirmResetPasswordView.vue'),
      meta: {
        public: true,
      },
    },
    {
      path: '/admin',
      component: () => import('@/layouts/admin/AdminLayout.vue'),
      meta: {
        requiresAuth: true,
      },
      children: [
        {
          path: '',
          name: 'admin-dashboard',
          component: () => import('@/views/admin/AdminDashboardView.vue'),
        },
        {
          path: 'categories',
          name: 'admin-categories',
          component: () => import('@/views/admin/AdminCategoriesView.vue'),
        },
        {
          path: 'links',
          name: 'admin-links',
          component: () => import('@/views/admin/AdminLinksView.vue'),
        },
        {
          path: 'import',
          name: 'admin-import',
          component: () => import('@/views/admin/AdminImportView.vue'),
        },
        {
          path: 'site',
          name: 'admin-site',
          component: () => import('@/views/admin/AdminSiteView.vue'),
        },
        {
          path: 'themes',
          name: 'admin-themes',
          component: () => import('@/views/admin/AdminThemesView.vue'),
        },
        {
          path: 'share',
          name: 'admin-share',
          component: () => import('@/views/admin/AdminShareView.vue'),
        },
        {
          path: 'transition',
          name: 'admin-transition',
          component: () => import('@/views/admin/AdminTransitionView.vue'),
        },
        {
          path: 'backup',
          name: 'admin-backup',
          component: () => import('@/views/admin/AdminBackupView.vue'),
        },
        {
          path: 'api',
          name: 'admin-api',
          component: () => import('@/views/admin/AdminApiView.vue'),
        },
        {
          path: 'tenants',
          name: 'admin-tenants',
          component: () => import('@/views/admin/AdminTenantsView.vue'),
        },
        {
          path: 'security',
          name: 'admin-security',
          component: () => import('@/views/admin/AdminSecurityView.vue'),
        },
        {
          path: 'public-categories',
          name: 'admin-public-categories',
          component: () => import('@/views/admin/AdminCategoriesView.vue'),
          meta: { targetTenantId: '00000000-0000-0000-0000-000000000001' },
        },
        {
          path: 'public-links',
          name: 'admin-public-links',
          component: () => import('@/views/admin/AdminLinksView.vue'),
          meta: { targetTenantId: '00000000-0000-0000-0000-000000000001' },
        },
        {
          path: 'public-site',
          name: 'admin-public-site',
          component: () => import('@/views/admin/AdminSiteView.vue'),
          meta: { targetTenantId: '00000000-0000-0000-0000-000000000001' },
        },
      ],
    },
  ],
})

router.beforeEach((to) => {
  const isAuthenticated = Boolean(localStorage.getItem(AUTH_TOKEN_KEY))

  if (to.meta.requiresAuth && !isAuthenticated) {
    return { name: 'login' }
  }

  if (to.name === 'login' && isAuthenticated) {
    return { name: 'portal' }
  }

  return true
})

export { router }
