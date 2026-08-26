<script setup lang="ts">
import emptyRoomUrl from '../assets/empty-room.png'

defineProps<{ message: string }>()
</script>

<template>
  <div class="empty-state">
    <img
      data-test="empty-state-illustration"
      :src="emptyRoomUrl"
      alt=""
      aria-hidden="true"
    />
    <p class="empty">{{ message }}</p>
  </div>
</template>
