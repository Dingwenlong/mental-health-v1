<script setup lang="ts">
import { c } from "./careService";
defineProps<{ page: number; total: number; size: number; busy?: boolean }>();
defineEmits<{ change: [page: number] }>();
</script>
<template>
  <div class="care-pagination">
    <button
      type="button"
      class="secondary"
      :disabled="busy || page <= 1"
      @click="$emit('change', page - 1)"
    >
      {{ c("care.previous") }}
    </button>
    <span>{{ page }} / {{ Math.max(1, Math.ceil(total / size)) }}</span>
    <button
      type="button"
      class="secondary"
      :disabled="busy || page * size >= total"
      @click="$emit('change', page + 1)"
    >
      {{ c("care.next") }}
    </button>
  </div>
</template>
