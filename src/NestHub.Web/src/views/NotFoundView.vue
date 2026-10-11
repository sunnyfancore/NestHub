<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<template>
  <main class="not-found">
    <section>
      <span class="not-found__code">404</span>
      <h1>这个页面暂时找不到了</h1>
      <p>地址可能已变更，请检查链接，或回到导航首页继续浏览。</p>
      <el-button type="primary" @click="router.push('/')">返回导航首页</el-button>
    </section>
  </main>
</template>

<style scoped>
.not-found{display:grid;place-items:center;min-height:100dvh;padding:24px;background:#f5f7f6;color:#263e3b}
section{width:min(100%,480px);padding:40px 28px;text-align:center;border:1px solid #e2eae6;border-radius:20px;background:#fff;box-shadow:0 8px 32px #25443206}
.not-found__code{font-size:52px;line-height:1.2;font-weight:700;color:#168575;font-variant-numeric:tabular-nums}
h1{font-size:22px;margin:18px 0 12px}p{font-size:14px;line-height:1.8;color:#64796d;margin-bottom:24px}
</style>
