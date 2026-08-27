<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { ApiProblemError, apiClient } from "../../api/client";
import { getUiCopy } from "../../generated/uiCopy.generated";
import type { ClinicalNotificationSource } from "../notifications/notificationConnection";
import type { RiskAssessment } from "../risk_cases/riskCaseService";

const props = defineProps<{
  notifications?: ClinicalNotificationSource;
  initialSessionId?: string;
}>();

const sessionId = ref(props.initialSessionId ?? "");
const result = ref<RiskAssessment | null>(null);
const status = ref<"idle" | "loading" | "pending" | "completed" | "failed">(
  "idle",
);
let stopNotifications: (() => void) | undefined;

onMounted(() => {
  if (props.initialSessionId) void load();
  stopNotifications = props.notifications?.subscribe((notification) => {
    if (
      notification.kind === "analysis" &&
      notification.sessionId === sessionId.value.trim()
    ) {
      void load();
    }
  });
});

onBeforeUnmount(() => stopNotifications?.());

async function load(): Promise<void> {
  status.value = "loading";
  result.value = null;
  try {
    result.value = await apiClient.get<RiskAssessment>(
      `results/${sessionId.value.trim()}`,
    );
    status.value = "completed";
  } catch (error) {
    status.value =
      error instanceof ApiProblemError &&
      error.problem.code === "RESULT_NOT_FOUND"
        ? "pending"
        : "failed";
  }
}
</script>

<template>
  <section class="clinical-workspace analysis-job-view">
    <header class="clinical-header">
      <h1>{{ getUiCopy("analysis.admin.title") }}</h1>
    </header>
    <form class="lookup-form" @submit.prevent="load">
      <label>
        {{ getUiCopy("chat.sessionId") }}
        <input v-model.trim="sessionId" required />
      </label>
      <button type="submit" :disabled="status === 'loading'">
        {{ getUiCopy("analysis.admin.lookup") }}
      </button>
    </form>
    <p v-if="status === 'pending'">{{ getUiCopy("analysis.processing") }}</p>
    <p v-else-if="status === 'failed'" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <dl v-else-if="result" class="assessment-readout">
      <div>
        <dt>{{ getUiCopy("risk.detail.score") }}</dt>
        <dd>{{ Math.round(result.score) }}</dd>
      </div>
      <div>
        <dt>{{ getUiCopy("risk.detail.ruleVersion") }}</dt>
        <dd>{{ result.ruleSetVersion }}</dd>
      </div>
      <div>
        <dt>{{ getUiCopy("risk.queue.level") }}</dt>
        <dd>{{ result.level }}</dd>
      </div>
    </dl>
  </section>
</template>
