<script setup lang="ts">
type Tone = "neutral" | "info" | "success" | "warning" | "danger";

const props = withDefaults(
  defineProps<{ label: string; tone?: Tone }>(),
  { tone: "neutral" },
);
</script>

<template>
  <span class="status-badge" :class="`status-${props.tone}`">{{ props.label }}</span>
</template>
