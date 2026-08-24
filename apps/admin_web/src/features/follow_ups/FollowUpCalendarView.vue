<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import type { ClinicalNotificationSource } from "../notifications/notificationConnection";
import {
  riskCaseService as defaultRiskCaseService,
  type FollowUpTask,
  type RiskCaseService,
} from "../risk_cases/riskCaseService";
import FollowUpEditor from "./FollowUpEditor.vue";

const props = withDefaults(
  defineProps<{
    service?: RiskCaseService;
    notifications?: ClinicalNotificationSource;
  }>(),
  { service: () => defaultRiskCaseService },
);
const tasks = ref<FollowUpTask[]>([]);
const selected = ref<FollowUpTask | null>(null);
const loading = ref(true);
const failed = ref(false);

let stopNotifications: (() => void) | undefined;

onMounted(() => {
  void load();
  stopNotifications = props.notifications?.subscribe((notification) => {
    if (notification.kind === "follow-up") void load();
  });
});

onBeforeUnmount(() => stopNotifications?.());

async function load(): Promise<void> {
  loading.value = true;
  failed.value = false;
  try {
    tasks.value = await props.service.listFollowUps();
    selected.value = selected.value
      ? (tasks.value.find((task) => task.id === selected.value?.id) ??
        tasks.value[0] ??
        null)
      : (tasks.value[0] ?? null);
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

function format(value: string | null): string {
  return value
    ? new Date(value).toLocaleString("zh-CN", { hour12: false })
    : getUiCopy("followUp.status.proposed");
}

function statusLabel(task: FollowUpTask): string {
  if (task.conflictCode === "NO_QUALIFIED_SLOT_BEFORE_SLA")
    return getUiCopy("followUp.manualQueue");
  const labels: Record<string, string> = {
    Proposed: getUiCopy("followUp.status.proposed"),
    Scheduled: getUiCopy("followUp.status.scheduled"),
    Due: getUiCopy("followUp.status.due"),
    Overdue: getUiCopy("followUp.status.overdue"),
    Completed: getUiCopy("followUp.status.completed"),
    Cancelled: getUiCopy("followUp.status.cancelled"),
  };
  return labels[task.status] ?? task.status;
}
</script>

<template>
  <section class="clinical-workspace">
    <header class="clinical-header">
      <h1>{{ getUiCopy("followUp.calendar.title") }}</h1>
      <button type="button" class="secondary" :disabled="loading" @click="load">
        {{ getUiCopy("common.refresh") }}
      </button>
    </header>
    <p v-if="failed" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <p v-if="loading" class="empty">{{ getUiCopy("app.loading") }}</p>
    <div v-else class="follow-up-columns">
      <table class="queue-table">
        <thead>
          <tr>
            <th>{{ getUiCopy("followUp.calendar.time") }}</th>
            <th>{{ getUiCopy("followUp.calendar.status") }}</th>
            <th>{{ getUiCopy("followUp.calendar.deadline") }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="task in tasks"
            :key="task.id"
            :class="{ selected: selected?.id === task.id }"
            tabindex="0"
            @click="selected = task"
            @keydown.enter="selected = task"
          >
            <td>{{ format(task.dueAt) }}</td>
            <td>{{ statusLabel(task) }}</td>
            <td>{{ format(task.deadline) }}</td>
          </tr>
        </tbody>
      </table>
      <FollowUpEditor
        v-if="selected"
        :task="selected"
        :service="service"
        @saved="load"
      />
    </div>
  </section>
</template>
