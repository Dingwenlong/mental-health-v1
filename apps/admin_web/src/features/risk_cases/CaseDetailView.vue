<script setup lang="ts">
import { getUiCopy } from "../../generated/uiCopy.generated";
import FollowUpEditor from "../follow_ups/FollowUpEditor.vue";
import ReviewForm from "./ReviewForm.vue";
import type { RiskCase, RiskCaseService } from "./riskCaseService";

defineProps<{ riskCase: RiskCase; service: RiskCaseService }>();
const emit = defineEmits<{ changed: [] }>();

function modalityLabel(value: string): string {
  if (value === "Scale") return getUiCopy("modality.scale");
  if (value === "Text") return getUiCopy("modality.text");
  if (value === "Audio") return getUiCopy("modality.audio");
  if (value === "Video") return getUiCopy("modality.videoExpression");
  if (value === "Trend") return getUiCopy("modality.trend");
  return value;
}

function qualityLabel(value: number): string {
  if (value >= 0.8) return getUiCopy("quality.high");
  if (value >= 0.5) return getUiCopy("quality.medium");
  return getUiCopy("quality.low");
}
</script>

<template>
  <article class="case-detail">
    <header class="case-summary">
      <div>
        <h2>
          {{
            `${getUiCopy("risk.queue.subject")} ${riskCase.subjectId.slice(0, 4)}`
          }}
        </h2>
        <span>{{
          `${getUiCopy("risk.detail.levelPrefix")}${riskCase.currentLevel}`
        }}</span>
      </div>
      <dl class="assessment-readout">
        <div>
          <dt>{{ getUiCopy("risk.detail.score") }}</dt>
          <dd>{{ Math.round(riskCase.assessment.score) }}</dd>
        </div>
        <div>
          <dt>{{ getUiCopy("risk.detail.ruleVersion") }}</dt>
          <dd>{{ riskCase.assessment.ruleSetVersion }}</dd>
        </div>
        <div>
          <dt>{{ getUiCopy("risk.detail.confidence") }}</dt>
          <dd>{{ Math.round(riskCase.assessment.confidence * 100) }}%</dd>
        </div>
      </dl>
    </header>

    <p v-if="riskCase.assessment.missing.length" class="missing-line">
      {{ getUiCopy("risk.detail.missing")
      }}{{
        riskCase.assessment.missing
          .map(modalityLabel)
          .join(getUiCopy("common.listSeparator"))
      }}
    </p>

    <table class="evidence-table">
      <thead>
        <tr>
          <th>{{ getUiCopy("risk.detail.source") }}</th>
          <th>{{ getUiCopy("risk.detail.quality") }}</th>
          <th>{{ getUiCopy("risk.detail.contribution") }}</th>
          <th>{{ getUiCopy("risk.detail.range") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="evidence in riskCase.assessment.evidence"
          :key="evidence.code"
        >
          <td>{{ modalityLabel(evidence.modality) }}</td>
          <td>{{ qualityLabel(evidence.quality) }}</td>
          <td>{{ evidence.contribution.toFixed(1) }}</td>
          <td>
            <code>{{ evidence.sourceRange }}</code>
          </td>
        </tr>
      </tbody>
    </table>

    <details class="source-disclosure">
      <summary>{{ getUiCopy("risk.detail.originalText") }}</summary>
      <p>{{ getUiCopy("risk.detail.originalTextUnavailable") }}</p>
    </details>

    <div class="case-actions">
      <ReviewForm
        :case-id="riskCase.id"
        :current-level="riskCase.currentLevel"
        :service="service"
        @saved="emit('changed')"
      />
      <div class="follow-up-pane">
        <p
          v-if="
            riskCase.followUp?.conflictCode === 'NO_QUALIFIED_SLOT_BEFORE_SLA'
          "
          class="manual-queue"
        >
          {{ getUiCopy("followUp.manualQueue") }}
        </p>
        <FollowUpEditor
          v-if="riskCase.followUp"
          :task="riskCase.followUp"
          :service="service"
          @saved="emit('changed')"
        />
        <p v-else class="empty">{{ getUiCopy("followUp.none") }}</p>
      </div>
    </div>
  </article>
</template>
