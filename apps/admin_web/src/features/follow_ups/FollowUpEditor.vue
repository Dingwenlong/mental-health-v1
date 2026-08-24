<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import {
  riskCaseService as defaultRiskCaseService,
  type FollowUpTask,
  type RiskCaseService,
} from "../risk_cases/riskCaseService";

type FollowUpAction = "reschedule" | "reassign" | "complete" | "cancel";

const props = withDefaults(
  defineProps<{
    task: FollowUpTask;
    service?: RiskCaseService;
  }>(),
  { service: () => defaultRiskCaseService },
);
const emit = defineEmits<{ saved: [task: FollowUpTask] }>();
const form = reactive<{
  action: FollowUpAction;
  availabilitySlotId: string;
  reason: string;
}>({ action: "reschedule", availabilitySlotId: "", reason: "" });
const reasonError = ref(false);
const slotError = ref(false);
const saveFailed = ref(false);
const isSaving = ref(false);
const needsSlot = computed(
  () => form.action === "reschedule" || form.action === "reassign",
);

async function submit(): Promise<void> {
  reasonError.value = !form.reason.trim();
  slotError.value = needsSlot.value && !form.availabilitySlotId.trim();
  saveFailed.value = false;
  if (reasonError.value || slotError.value) return;

  isSaving.value = true;
  try {
    const reason = form.reason.trim();
    const task = await runAction(reason);
    form.reason = "";
    emit("saved", task);
  } catch {
    saveFailed.value = true;
  } finally {
    isSaving.value = false;
  }
}

function runAction(reason: string): Promise<FollowUpTask> {
  if (form.action === "reschedule") {
    return props.service.rescheduleFollowUp(
      props.task.id,
      form.availabilitySlotId.trim(),
      reason,
    );
  }
  if (form.action === "reassign") {
    return props.service.reassignFollowUp(
      props.task.id,
      form.availabilitySlotId.trim(),
      reason,
    );
  }
  if (form.action === "complete") {
    return props.service.completeFollowUp(props.task.id, reason);
  }
  return props.service.cancelFollowUp(props.task.id, reason);
}
</script>

<template>
  <form class="clinical-form" @submit.prevent="submit">
    <h3>{{ getUiCopy("followUp.action.title") }}</h3>
    <label>
      {{ getUiCopy("followUp.action.type") }}
      <select v-model="form.action" data-test="action">
        <option value="reschedule">
          {{ getUiCopy("followUp.action.reschedule") }}
        </option>
        <option value="reassign">
          {{ getUiCopy("followUp.action.reassign") }}
        </option>
        <option value="complete">
          {{ getUiCopy("followUp.action.complete") }}
        </option>
        <option value="cancel">
          {{ getUiCopy("followUp.action.cancel") }}
        </option>
      </select>
    </label>
    <label v-if="needsSlot">
      {{ getUiCopy("followUp.action.slot") }}
      <input
        v-model="form.availabilitySlotId"
        data-test="slot"
        :aria-invalid="slotError"
        @input="slotError = false"
      />
    </label>
    <label>
      {{ getUiCopy("followUp.action.reason") }}
      <textarea
        v-model="form.reason"
        data-test="reason"
        maxlength="1000"
        rows="3"
        :aria-invalid="reasonError"
        @input="reasonError = false"
      />
    </label>
    <p v-if="reasonError" class="field-error" role="alert">
      {{ getUiCopy("followUp.action.reasonRequired") }}
    </p>
    <p v-if="slotError" class="field-error" role="alert">
      {{ getUiCopy("followUp.action.slotRequired") }}
    </p>
    <p v-if="saveFailed" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <button type="submit" :disabled="isSaving">
      {{ getUiCopy(isSaving ? "common.saving" : "followUp.action.submit") }}
    </button>
  </form>
</template>
