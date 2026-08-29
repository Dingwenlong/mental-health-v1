<script setup lang="ts">
import emptyRoomUrl from "../assets/empty-room.webp";

withDefaults(
  defineProps<{ message: string; compact?: boolean; showIllustration?: boolean }>(),
  { compact: false, showIllustration: true },
);
</script>

<template>
  <div class="state-panel" :class="{ compact }">
    <img
      v-if="showIllustration"
      data-test="empty-state-illustration"
      :src="emptyRoomUrl"
      alt=""
      aria-hidden="true"
    />
    <p>{{ message }}</p>
    <slot />
  </div>
</template>
