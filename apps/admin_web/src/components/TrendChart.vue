<script setup lang="ts">
import { computed } from "vue";

type TrendDay = Record<string, string | number | null>;

const props = defineProps<{
  title: string;
  days: TrendDay[];
  valueKey: string;
  maxValue: number;
}>();

const width = 600;
const height = 160;
const inset = 14;

const segments = computed(() => {
  const result: string[] = [];
  let current: string[] = [];
  props.days.forEach((day, index) => {
    const raw = day[props.valueKey];
    if (typeof raw !== "number") {
      if (current.length) result.push(current.join(" "));
      current = [];
      return;
    }
    const x =
      inset +
      (index * (width - inset * 2)) / Math.max(1, props.days.length - 1);
    const value = Math.min(props.maxValue, Math.max(0, raw));
    const y = inset + ((props.maxValue - value) * (height - inset * 2)) / props.maxValue;
    current.push(`${current.length ? "L" : "M"}${x.toFixed(1)},${y.toFixed(1)}`);
  });
  if (current.length) result.push(current.join(" "));
  return result;
});
</script>

<template>
  <figure class="trend-chart">
    <figcaption>{{ title }}</figcaption>
    <svg
      viewBox="0 0 600 160"
      preserveAspectRatio="none"
      role="img"
      :aria-label="title"
    >
      <line v-for="row in [14, 80, 146]" :key="row" x1="14" :y1="row" x2="586" :y2="row" class="trend-grid" />
      <path v-for="path in segments" :key="path" :d="path" class="trend-line" />
    </svg>
  </figure>
</template>
