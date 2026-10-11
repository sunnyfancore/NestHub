<script setup lang="ts">
defineProps<{ message: string; loading?: boolean }>()
defineEmits<{ retry: [] }>()
</script>

<template>
  <div v-if="message" class="admin-load-notice" role="alert">
    <div><strong>读取未完成</strong><p>{{ message }}</p></div>
    <el-button :loading="loading" @click="$emit('retry')">重新加载</el-button>
  </div>
</template>

<style scoped>
.admin-load-notice { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 16px; border: 1px solid #e8d6b1; border-radius: 12px; background: #fffaf0; color: #705526; flex-shrink: 0; }
.admin-load-notice strong { font-size: 14px; }
.admin-load-notice p { margin: 4px 0 0; font-size: 13px; line-height: 1.6; overflow-wrap: anywhere; }
.admin-load-notice .el-button { flex-shrink: 0; }
@media(max-width:600px) { .admin-load-notice { align-items: flex-start; flex-direction: column; gap: 12px; } }
</style>
