<script setup lang="ts">
import { onMounted, ref } from "vue";
import EmptyState from "../../components/EmptyState.vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import { auditActionLabel } from "./auditLabels";
import {
  auditService as defaultAuditService,
  type AuditRecord,
  type AuditService,
} from "./auditService";

const props = withDefaults(defineProps<{ service?: AuditService }>(), {
  service: () => defaultAuditService,
});
const records = ref<AuditRecord[]>([]);
const loading = ref(true);
const failed = ref(false);

onMounted(() => void load());

async function load(): Promise<void> {
  loading.value = true;
  failed.value = false;
  try {
    records.value = await props.service.list();
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

function formatTime(value: string): string {
  return new Date(value).toLocaleString("zh-CN", { hour12: false });
}
</script>

<template>
  <section class="clinical-workspace">
    <header class="clinical-header">
      <h1>{{ getUiCopy("audit.title") }}</h1>
      <button type="button" class="secondary" :disabled="loading" @click="load">
        {{ getUiCopy("common.refresh") }}
      </button>
    </header>
    <p v-if="failed" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <p v-if="loading" class="empty">{{ getUiCopy("app.loading") }}</p>
    <EmptyState
      v-else-if="records.length === 0"
      :message="getUiCopy('empty.records')"
    />
    <table v-else class="queue-table">
      <thead>
        <tr>
          <th>{{ getUiCopy("audit.time") }}</th>
          <th>{{ getUiCopy("audit.actor") }}</th>
          <th>{{ getUiCopy("audit.action") }}</th>
          <th>{{ getUiCopy("audit.record") }}</th>
          <th>{{ getUiCopy("audit.reason") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="record in records"
          :key="`${record.occurredAt}-${record.resourceId}`"
        >
          <td>{{ formatTime(record.occurredAt) }}</td>
          <td>{{ record.actorUserId }}</td>
          <td>{{ auditActionLabel(record.action) }}</td>
          <td>{{ record.resourceId }}</td>
          <td>{{ record.reason ?? "" }}</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
