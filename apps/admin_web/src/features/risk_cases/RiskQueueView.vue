<script setup lang="ts">
import { onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import type { ClinicalNotificationSource } from "../notifications/notificationConnection";
import CaseDetailView from "./CaseDetailView.vue";
import {
  riskCaseService as defaultRiskCaseService,
  type RiskCase,
  type RiskCaseService,
  type RiskLevel,
} from "./riskCaseService";

const props = withDefaults(
  defineProps<{
    service?: RiskCaseService;
    notifications?: ClinicalNotificationSource;
  }>(),
  {
    service: () => defaultRiskCaseService,
  },
);
const filter = reactive<{
  level: "" | RiskLevel;
  status: string;
  assignedToMe: boolean;
}>({
  level: "",
  status: "",
  assignedToMe: false,
});
const cases = ref<RiskCase[]>([]);
const selected = ref<RiskCase | null>(null);
const loading = ref(true);
const failed = ref(false);

let stopNotifications: (() => void) | undefined;

onMounted(() => {
  void load();
  stopNotifications = props.notifications?.subscribe(() => {
    void load();
  });
});

onBeforeUnmount(() => stopNotifications?.());

async function load(): Promise<void> {
  loading.value = true;
  failed.value = false;
  try {
    const request = {
      ...(filter.level ? { level: filter.level } : {}),
      ...(filter.status ? { status: filter.status } : {}),
      ...(filter.assignedToMe ? { assignedToMe: true } : {}),
    };
    cases.value = await props.service.listCases(request);
    if (selected.value) {
      selected.value =
        cases.value.find((item) => item.id === selected.value?.id) ?? null;
    }
  } catch {
    failed.value = true;
  } finally {
    loading.value = false;
  }
}

async function refreshSelected(): Promise<void> {
  if (!selected.value) return;
  selected.value = await props.service.getCase(selected.value.id);
  await load();
}

function selectCase(item: RiskCase): void {
  selected.value = item;
}

function relativeTime(value: string): string {
  const minutes = Math.max(
    0,
    Math.floor((Date.now() - Date.parse(value)) / 60_000),
  );
  if (minutes < 60) return `${minutes}${getUiCopy("time.minutesAgo")}`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}${getUiCopy("time.hoursAgo")}`;
  return `${Math.floor(hours / 24)}${getUiCopy("time.daysAgo")}`;
}

function followUpLabel(item: RiskCase): string {
  if (item.followUp?.conflictCode === "NO_QUALIFIED_SLOT_BEFORE_SLA")
    return getUiCopy("followUp.manualShort");
  const labels: Record<string, string> = {
    Proposed: getUiCopy("followUp.status.proposed"),
    Scheduled: getUiCopy("followUp.status.scheduled"),
    Due: getUiCopy("followUp.status.due"),
    Overdue: getUiCopy("followUp.status.overdue"),
    Completed: getUiCopy("followUp.status.completed"),
    Cancelled: getUiCopy("followUp.status.cancelled"),
  };
  return item.followUp
    ? (labels[item.followUp.status] ?? item.followUp.status)
    : getUiCopy("followUp.status.none");
}
</script>

<template>
  <section class="clinical-workspace risk-workspace">
    <header class="clinical-header">
      <h1>{{ getUiCopy("risk.queue.title") }}</h1>
      <button type="button" class="secondary" :disabled="loading" @click="load">
        {{ getUiCopy("common.refresh") }}
      </button>
    </header>
    <div class="queue-filters">
      <label>
        {{ getUiCopy("risk.queue.level") }}
        <select v-model="filter.level" data-test="level-filter" @change="load">
          <option value="">{{ getUiCopy("risk.queue.all") }}</option>
          <option value="L1">L1</option>
          <option value="L2">L2</option>
          <option value="L3">L3</option>
          <option value="Crisis">Crisis</option>
        </select>
      </label>
      <label>
        {{ getUiCopy("risk.queue.status") }}
        <select
          v-model="filter.status"
          data-test="status-filter"
          @change="load"
        >
          <option value="">{{ getUiCopy("risk.queue.all") }}</option>
          <option value="Open">{{ getUiCopy("risk.queue.open") }}</option>
          <option value="Closed">{{ getUiCopy("risk.queue.closed") }}</option>
        </select>
      </label>
      <label class="checkbox-label">
        <input
          v-model="filter.assignedToMe"
          data-test="assigned-to-me"
          type="checkbox"
          @change="load"
        />
        {{ getUiCopy("risk.queue.assignedToMe") }}
      </label>
    </div>

    <p v-if="failed" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <div class="risk-columns">
      <div class="queue-table-wrap">
        <p v-if="loading" class="empty">{{ getUiCopy("app.loading") }}</p>
        <p v-else-if="cases.length === 0" class="empty">
          {{ getUiCopy("empty.records") }}
        </p>
        <table v-else class="queue-table">
          <thead>
            <tr>
              <th>{{ getUiCopy("risk.queue.subject") }}</th>
              <th>{{ getUiCopy("risk.queue.level") }}</th>
              <th>{{ getUiCopy("risk.queue.updated") }}</th>
              <th>{{ getUiCopy("risk.queue.followUp") }}</th>
              <th>{{ getUiCopy("risk.detail.score") }}</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in cases"
              :key="item.id"
              data-test="risk-case-row"
              :class="{ selected: selected?.id === item.id }"
              tabindex="0"
              @click="selectCase(item)"
              @keydown.enter="selectCase(item)"
            >
              <td>
                {{
                  `${getUiCopy("risk.queue.subject")} ${item.subjectId.slice(0, 4)}`
                }}
              </td>
              <td>{{ item.currentLevel }}</td>
              <td>{{ relativeTime(item.createdAt) }}</td>
              <td>{{ followUpLabel(item) }}</td>
              <td>{{ Math.round(item.assessment.score) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <CaseDetailView
        v-if="selected"
        :risk-case="selected"
        :service="service"
        @changed="refreshSelected"
      />
      <div v-else class="case-empty">{{ getUiCopy("risk.queue.select") }}</div>
    </div>
  </section>
</template>
