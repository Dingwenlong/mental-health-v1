<script setup lang="ts">
import { reactive, ref } from "vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import {
  riskCaseService as defaultRiskCaseService,
  type RiskCaseService,
  type RiskLevel,
} from "./riskCaseService";

const props = withDefaults(
  defineProps<{
    caseId: string;
    currentLevel: RiskLevel;
    service?: RiskCaseService;
  }>(),
  { service: () => defaultRiskCaseService },
);
const emit = defineEmits<{ saved: [] }>();
const form = reactive({ level: props.currentLevel, reason: "" });
const reasonError = ref(false);
const saveFailed = ref(false);
const isSaving = ref(false);

async function submit(): Promise<void> {
  reasonError.value = !form.reason.trim();
  saveFailed.value = false;
  if (reasonError.value) return;
  isSaving.value = true;
  try {
    await props.service.reviewRisk(props.caseId, {
      reviewedLevel: form.level,
      reason: form.reason.trim(),
    });
    form.reason = "";
    emit("saved");
  } catch {
    saveFailed.value = true;
  } finally {
    isSaving.value = false;
  }
}
</script>

<template>
  <form class="clinical-form" @submit.prevent="submit">
    <h3>{{ getUiCopy("risk.review.title") }}</h3>
    <label>
      {{ getUiCopy("risk.review.level") }}
      <select v-model="form.level" data-test="level">
        <option value="L1">{{ getUiCopy("risk.level.l1") }}</option>
        <option value="L2">{{ getUiCopy("risk.level.l2") }}</option>
        <option value="L3">{{ getUiCopy("risk.level.l3") }}</option>
        <option value="Crisis">{{ getUiCopy("risk.level.crisis") }}</option>
      </select>
    </label>
    <label>
      {{ getUiCopy("risk.review.reason") }}
      <textarea
        v-model="form.reason"
        data-test="reason"
        maxlength="1000"
        rows="4"
        :aria-invalid="reasonError"
        @input="reasonError = false"
      />
    </label>
    <p v-if="reasonError" class="field-error" role="alert">
      {{ getUiCopy("risk.review.reasonRequired") }}
    </p>
    <p v-if="saveFailed" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <button type="submit" :disabled="isSaving">
      {{ getUiCopy(isSaving ? "common.saving" : "risk.review.submit") }}
    </button>
  </form>
</template>
