<script setup lang="ts">
import StatePanel from "./StatePanel.vue";

defineProps<{ message: string }>();
</script>

<template><StatePanel :message="message" /></template>
