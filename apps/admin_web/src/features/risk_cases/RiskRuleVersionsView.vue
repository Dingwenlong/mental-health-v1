<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { getUiCopy } from "../../generated/uiCopy.generated";
import {
  riskRuleService as defaultRiskRuleService,
  type RiskRuleInput,
  type RiskRuleService,
  type RiskRuleVersion,
} from "./riskRuleService";

const props = withDefaults(defineProps<{ service?: RiskRuleService }>(), {
  service: () => defaultRiskRuleService,
});
const versions = ref<RiskRuleVersion[]>([]);
const loading = ref(true);
const saving = ref(false);
const error = ref(false);
const form = reactive({
  version: "",
  scaleWeight: 0.45,
  textWeight: 0.25,
  audioWeight: 0.15,
  videoWeight: 0.05,
  trendWeight: 0.1,
  l1: 25,
  l2: 50,
  l3: 75,
});

onMounted(load);

async function load(): Promise<void> {
  loading.value = true;
  error.value = false;
  try {
    versions.value = await props.service.list();
  } catch {
    error.value = true;
  } finally {
    loading.value = false;
  }
}

async function create(): Promise<void> {
  saving.value = true;
  error.value = false;
  const input: RiskRuleInput = {
    version: form.version.trim(),
    scaleWeight: Number(form.scaleWeight),
    textWeight: Number(form.textWeight),
    audioWeight: Number(form.audioWeight),
    videoWeight: Number(form.videoWeight),
    trendWeight: Number(form.trendWeight),
    thresholds: [Number(form.l1), Number(form.l2), Number(form.l3)],
    crisisRulesEnabled: true,
  };
  try {
    await props.service.create(input);
    form.version = "";
    await load();
  } catch {
    error.value = true;
  } finally {
    saving.value = false;
  }
}

async function activate(version: string): Promise<void> {
  saving.value = true;
  error.value = false;
  try {
    await props.service.activate(version);
    await load();
  } catch {
    error.value = true;
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <section class="clinical-workspace rule-workspace">
    <header class="clinical-header">
      <div>
        <h1>{{ getUiCopy("risk.rules.title") }}</h1>
        <p>{{ getUiCopy("risk.rules.immutableNotice") }}</p>
      </div>
      <button type="button" class="secondary" :disabled="loading" @click="load">
        {{ getUiCopy("common.refresh") }}
      </button>
    </header>

    <form class="rule-form" @submit.prevent="create">
      <label>
        {{ getUiCopy("risk.rules.version") }}
        <input v-model="form.version" data-test="rule-version" required />
      </label>
      <fieldset>
        <legend>{{ getUiCopy("risk.rules.weights") }}</legend>
        <label
          >{{ getUiCopy("modality.scale")
          }}<input
            v-model.number="form.scaleWeight"
            type="number"
            step="0.01"
            min="0.01"
            max="1"
        /></label>
        <label
          >{{ getUiCopy("modality.text")
          }}<input
            v-model.number="form.textWeight"
            type="number"
            step="0.01"
            min="0.01"
            max="1"
        /></label>
        <label
          >{{ getUiCopy("modality.audio")
          }}<input
            v-model.number="form.audioWeight"
            type="number"
            step="0.01"
            min="0.01"
            max="1"
        /></label>
        <label
          >{{ getUiCopy("modality.video")
          }}<input
            v-model.number="form.videoWeight"
            type="number"
            step="0.01"
            min="0.01"
            max="1"
        /></label>
        <label
          >{{ getUiCopy("modality.trend")
          }}<input
            v-model.number="form.trendWeight"
            type="number"
            step="0.01"
            min="0.01"
            max="1"
        /></label>
      </fieldset>
      <fieldset>
        <legend>{{ getUiCopy("risk.rules.thresholds") }}</legend>
        <label
          >L1<input v-model.number="form.l1" type="number" min="1" max="99"
        /></label>
        <label
          >L2<input v-model.number="form.l2" type="number" min="1" max="99"
        /></label>
        <label
          >L3<input v-model.number="form.l3" type="number" min="1" max="99"
        /></label>
      </fieldset>
      <label class="checkbox-label">
        <input data-test="crisis-enabled" type="checkbox" checked disabled />
        {{ getUiCopy("risk.rules.crisisRequired") }}
      </label>
      <button type="submit" :disabled="saving">
        {{ getUiCopy(saving ? "common.saving" : "common.save") }}
      </button>
    </form>

    <p v-if="error" class="error" role="alert">
      {{ getUiCopy("error.retry") }}
    </p>
    <p v-if="loading" class="empty">{{ getUiCopy("app.loading") }}</p>
    <table v-else class="queue-table">
      <thead>
        <tr>
          <th>{{ getUiCopy("risk.rules.version") }}</th>
          <th>{{ getUiCopy("risk.rules.weights") }}</th>
          <th>{{ getUiCopy("risk.rules.thresholds") }}</th>
          <th>{{ getUiCopy("risk.rules.status") }}</th>
          <th>{{ getUiCopy("risk.rules.action") }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="version in versions" :key="version.id">
          <td>{{ version.version }}</td>
          <td>{{ Object.values(version.weights).join(" / ") }}</td>
          <td>{{ version.thresholds.join(" / ") }}</td>
          <td>
            {{
              getUiCopy(
                version.active ? "risk.rules.active" : "risk.rules.inactive",
              )
            }}
          </td>
          <td>
            <button
              v-if="!version.active"
              type="button"
              class="secondary"
              :disabled="saving"
              @click="activate(version.version)"
            >
              {{ getUiCopy("risk.rules.activate") }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </section>
</template>
