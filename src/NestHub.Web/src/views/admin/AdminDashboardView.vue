<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { getAppInfo, type AppInfo } from '@/api/app'

const loading = ref(false)
const appInfo = ref<AppInfo | null>(null)

async function load() {
  loading.value = true

  try {
    appInfo.value = await getAppInfo()
  } catch (error: any) {
    ElMessage.error(error?.response?.data?.message || '获取后台概览失败。')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="admin-page" v-loading="loading">
    <section class="welcome-bar">
      <div class="welcome-bar__text">
        <h2>欢迎使用 NestHub 后台</h2>
        <p>在这里管理你的导航站分类、链接和系统设置。</p>
      </div>
    </section>

    <section class="stats-grid">
      <article class="stat-card stat-card--blue">
        <div class="stat-card__icon"><i class="fa fa-cloud"></i></div>
        <div class="stat-card__body">
          <span>当前租户</span>
          <strong>{{ appInfo?.tenantName || '--' }}</strong>
        </div>
      </article>
      <article class="stat-card stat-card--green">
        <div class="stat-card__icon"><i class="fa fa-folder"></i></div>
        <div class="stat-card__body">
          <span>分类数量</span>
          <strong>{{ appInfo?.categoryCount ?? 0 }}</strong>
        </div>
      </article>
      <article class="stat-card stat-card--orange">
        <div class="stat-card__icon"><i class="fa fa-link"></i></div>
        <div class="stat-card__body">
          <span>链接数量</span>
          <strong>{{ appInfo?.linkCount ?? 0 }}</strong>
        </div>
      </article>
      <article class="stat-card stat-card--purple">
        <div class="stat-card__icon"><i class="fa fa-code-fork"></i></div>
        <div class="stat-card__body">
          <span>应用版本</span>
          <strong>{{ appInfo?.appVersion || '--' }}</strong>
        </div>
      </article>
    </section>

    <section class="tips-grid">
      <div class="tip-card">
        <div class="tip-card__head">
          <i class="fa fa-lightbulb-o"></i>
          <h4>快速上手</h4>
        </div>
        <ul>
          <li>在「分类列表」中创建一级和二级分类</li>
          <li>在「我的链接」中添加书签链接</li>
          <li>在前台可直接拖动排序分类和链接</li>
          <li>右键点击链接可打开、复制或编辑</li>
        </ul>
      </div>
      <div class="tip-card">
        <div class="tip-card__head">
          <i class="fa fa-shield"></i>
          <h4>数据管理</h4>
        </div>
        <ul>
          <li>定期在「数据备份」中导出 JSON 备份</li>
          <li>在「书签导入」中批量导入浏览器书签</li>
          <li>在「书签分享」中创建临时分享链接</li>
          <li>在「获取 API」中查看接口文档</li>
        </ul>
      </div>
    </section>
  </div>
</template>

<style scoped>
.admin-page {
  display: grid;
  gap: 20px;
}

.welcome-bar {
  background: linear-gradient(135deg, #5b9aff 0%, #3d7be0 100%);
  border-radius: 12px;
  padding: 24px 28px;
  color: #fff;
}

.welcome-bar h2 {
  margin: 0;
  font-size: 20px;
  font-weight: 700;
}

.welcome-bar p {
  margin: 6px 0 0;
  opacity: 0.85;
  font-size: 14px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.stat-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.stat-card__icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  flex-shrink: 0;
}

.stat-card--blue .stat-card__icon {
  background: #eef4ff;
  color: #5b9aff;
}

.stat-card--green .stat-card__icon {
  background: #edfff4;
  color: #52c41a;
}

.stat-card--orange .stat-card__icon {
  background: #fff7e6;
  color: #fa8c16;
}

.stat-card--purple .stat-card__icon {
  background: #f3e8ff;
  color: #9254de;
}

.stat-card__body span {
  display: block;
  color: #8896a6;
  font-size: 13px;
  margin-bottom: 4px;
}

.stat-card__body strong {
  font-size: 22px;
  color: #1a1a2e;
  font-weight: 700;
}

.tips-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.tip-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
}

.tip-card__head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
  color: #5b9aff;
  font-size: 15px;
}

.tip-card__head i {
  font-size: 16px;
}

.tip-card__head h4 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
  color: #1a1a2e;
}

.tip-card ul {
  margin: 0;
  padding-left: 16px;
  display: grid;
  gap: 8px;
}

.tip-card li {
  color: #666;
  font-size: 13px;
  line-height: 1.6;
}

@media (max-width: 900px) {
  .stats-grid {
    grid-template-columns: 1fr 1fr;
  }

  .tips-grid {
    grid-template-columns: 1fr;
  }
}
</style>
