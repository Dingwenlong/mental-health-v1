<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from "vue";
import {
  c,
  careApi,
  dateText,
  type CareApi,
  type Consultation,
  type Page,
} from "./careService";
import PageControls from "./PageControls.vue";
const props = defineProps<{ service?: CareApi }>();
const emit = defineEmits<{
  open: [session: Consultation, mode: "chat" | "video" | "analysisJobs"];
}>();
const api = props.service ?? careApi;
const page = ref(1);
const status = ref("");
const from = ref("");
const to = ref("");
const result = ref<Page<Consultation> | null>(null);
const busy = ref(false);
const error = ref("");
let generation = 0;
async function load(): Promise<void> {
  const request = ++generation;
  busy.value = true;
  error.value = "";
  result.value = null;
  const query = new URLSearchParams({
    page: String(page.value),
    pageSize: "15",
  });
  if (status.value) query.set("status", status.value);
  if (from.value) query.set("from", `${from.value}T00:00:00+08:00`);
  if (to.value) query.set("to", `${to.value}T23:59:59+08:00`);
  try {
    const value = await api.get<Page<Consultation>>(`consultations?${query}`);
    if (request === generation) result.value = value;
  } catch {
    if (request === generation) error.value = c("care.retry");
  } finally {
    if (request === generation) busy.value = false;
  }
}
watch(page, () => void load(), { immediate: true });
onBeforeUnmount(() => {
  generation++;
});
function filter(): void {
  if (page.value !== 1) page.value = 1;
  else void load();
}
</script>
<template>
  <section class="workspace-panel">
    <div class="section-header">
      <h2>{{ c("care.consultations") }}</h2>
      <button type="button" class="secondary" :disabled="busy" @click="load">
        {{ c("care.refresh") }}
      </button>
    </div>
    <form class="care-filters" @submit.prevent="filter">
      <label
        >{{ c("care.all")
        }}<select v-model="status">
          <option value="">{{ c("care.all") }}</option>
          <option
            v-for="state in [
              'Scheduled',
              'InProgress',
              'Completed',
              'Cancelled',
            ]"
            :key="state"
            :value="state"
          >
            {{ c(`care.status.${state}`) }}
          </option>
        </select></label
      ><label>{{ c("care.from") }}<input v-model="from" type="date" /></label
      ><label>{{ c("care.to") }}<input v-model="to" type="date" /></label
      ><button :disabled="busy">{{ c("care.filter") }}</button>
    </form>
    <p v-if="busy" role="status">{{ c("care.loading") }}</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <template v-if="result"
      ><p v-if="!result.items.length">{{ c("care.noConsultations") }}</p>
      <article
        v-for="session in result.items"
        :key="session.id"
        class="care-list-row"
      >
        <div>
          <h3>{{ dateText(session.scheduledAt) }}</h3>
          <p>
            {{ session.practitionerName }} ·
            {{ c(`care.status.${session.status}`) }}
          </p>
        </div>
        <div class="care-actions">
          <button
            v-if="['Scheduled', 'InProgress'].includes(session.status)"
            type="button"
            @click="
              emit(
                'open',
                session,
                session.channel === 'Video' ? 'video' : 'chat',
              )
            "
          >
            {{ c("care.openConsultation") }}</button
          ><button
            v-if="session.status === 'Completed'"
            type="button"
            @click="emit('open', session, 'analysisJobs')"
          >
            {{ c("care.openReport") }}
          </button>
        </div>
      </article>
      <PageControls
        :page="page"
        :total="result.total"
        :size="15"
        :busy="busy"
        @change="page = $event"
    /></template>
  </section>
</template>
