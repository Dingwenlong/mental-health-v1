<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { ApiProblemError } from "../../api/client";
import {
  c,
  careApi,
  dateText,
  type CareApi,
  type CarePlan,
  type ClinicalSubject,
  type Page,
  type SubjectView,
  type Summary,
} from "./careService";
import CarePlanEditor from "./CarePlanEditor.vue";
import PageControls from "./PageControls.vue";
const props = defineProps<{
  mode: "overview" | "subjects" | "plans";
  service?: CareApi;
}>();
const api = props.service ?? careApi;
const page = ref(1);
const size = 10;
const summary = ref<Summary | null>(null);
const subjects = ref<Page<ClinicalSubject> | null>(null);
const detail = ref<SubjectView | null>(null);
const plans = ref<Page<CarePlan> | null>(null);
const selected = ref<string | null>(null);
const editor = ref<{ followUpId: string; plan?: CarePlan } | null>(null);
const busy = ref(false);
const error = ref("");
let generation = 0;
const shownPlans = computed(
  () => detail.value?.plans.items ?? plans.value?.items ?? [],
);
const title = computed(() =>
  c(
    props.mode === "overview"
      ? "care.overview"
      : props.mode === "subjects"
        ? "care.subjects"
        : "care.plans",
  ),
);
async function load(): Promise<void> {
  const request = ++generation;
  busy.value = true;
  error.value = "";
  detail.value = null;
  summary.value = null;
  subjects.value = null;
  plans.value = null;
  try {
    if (selected.value) {
      const value = await api.get<SubjectView>(
        `clinical/subjects/${selected.value}?page=${page.value}&pageSize=${size}`,
      );
      if (request === generation) detail.value = value;
    } else if (props.mode === "overview") {
      const value = await api.get<Summary>("workspace/summary");
      if (request === generation) summary.value = value;
    } else if (props.mode === "subjects") {
      const value = await api.get<Page<ClinicalSubject>>(
        `clinical/subjects?page=${page.value}&pageSize=${size}`,
      );
      if (request === generation) subjects.value = value;
    } else {
      const value = await api.get<Page<CarePlan>>(
        `care-plans?page=${page.value}&pageSize=${size}`,
      );
      if (request === generation) plans.value = value;
    }
  } catch {
    if (request === generation) error.value = c("care.retry");
  } finally {
    if (request === generation) busy.value = false;
  }
}
watch(
  () => props.mode,
  () => {
    selected.value = null;
    editor.value = null;
    page.value = 1;
    void load();
  },
  { immediate: true },
);
onBeforeUnmount(() => {
  generation++;
});
function openSubject(id: string): void {
  selected.value = id;
  page.value = 1;
  editor.value = null;
  void load();
}
function goBack(): void {
  selected.value = null;
  page.value = 1;
  editor.value = null;
  void load();
}
function changePage(value: number): void {
  page.value = value;
  void load();
}
async function transition(
  plan: CarePlan,
  action: "publish" | "cancel",
): Promise<void> {
  if (busy.value) return;
  busy.value = true;
  error.value = "";
  try {
    await api.post(`care-plans/${plan.id}/${action}`);
    await load();
  } catch (failure) {
    error.value = c(
      failure instanceof ApiProblemError && failure.problem.status === 409
        ? "care.conflict"
        : "care.retry",
    );
  } finally {
    busy.value = false;
  }
}
async function saved(): Promise<void> {
  editor.value = null;
  await load();
}
</script>
<template>
  <section class="workspace-panel care-workspace">
    <div class="section-header">
      <h2>{{ selected ? c("care.file") : title }}</h2>
      <div class="care-actions">
        <button v-if="selected" type="button" class="secondary" @click="goBack">
          {{ c("care.back") }}</button
        ><button
          type="button"
          class="secondary"
          data-test="refresh-care"
          :disabled="busy"
          @click="
            editor = null;
            load();
          "
        >
          {{ c("care.refresh") }}
        </button>
      </div>
    </div>
    <p v-if="busy" role="status">{{ c("care.loading") }}</p>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <dl v-if="summary" class="care-metrics">
      <div v-if="summary.role !== 'Doctor'">
        <dt>{{ c("care.consultationCount") }}</dt>
        <dd>{{ summary.consultationCount }}</dd>
      </div>
      <template v-if="summary.role !== 'Counselor'"
        ><div>
          <dt>{{ c("care.pendingFollowUps") }}</dt>
          <dd>{{ summary.pendingFollowUps }}</dd>
        </div>
        <div>
          <dt>{{ c("care.overdueFollowUps") }}</dt>
          <dd>{{ summary.overdueFollowUps }}</dd>
        </div></template
      >
      <template v-if="summary.role === 'Doctor'"
        ><div>
          <dt>{{ c("care.activePlans") }}</dt>
          <dd>{{ summary.activePlans }}</dd>
        </div>
        <div>
          <dt>{{ c("care.completedTasks") }}</dt>
          <dd>{{ summary.completedPlanTasks }} / {{ summary.planTasks }}</dd>
        </div></template
      >
    </dl>
    <template v-if="subjects">
      <p v-if="!subjects.items.length">{{ c("care.noSubjects") }}</p>
      <div
        v-for="subject in subjects.items"
        :key="subject.subjectId"
        class="care-list-row"
      >
        <div>
          <h3>{{ c("care.subject") }} {{ subject.subjectId.slice(0, 6) }}</h3>
          <p>
            {{ c("care.nextFollowUp") }} ·
            {{ dateText(subject.nextFollowUpAt) }}
          </p>
        </div>
        <button
          type="button"
          data-test="open-subject"
          @click="openSubject(subject.subjectId)"
        >
          {{ c("care.openFile") }}
        </button>
      </div>
      <PageControls
        :page="page"
        :total="subjects.total"
        :size="size"
        :busy="busy"
        @change="changePage"
      />
    </template>
    <template v-if="detail">
      <p class="care-notice" data-test="sharing-status">
        {{ c(detail.sharingActive ? "care.shared" : "care.privateNotice") }}
      </p>
      <section v-if="detail.sharingActive" data-test="shared-records">
        <h3>{{ c("care.trends") }}</h3>
        <p>{{ c("care.sharedTrendNotice") }}</p>
        <div class="care-table-scroll">
          <table>
            <thead>
              <tr>
                <th>{{ c("care.date") }}</th>
                <th>{{ c("care.mood") }}</th>
                <th>{{ c("care.sleepShort") }}</th>
                <th>{{ c("care.exerciseCount") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="day in detail.trends" :key="day.date">
                <td>{{ day.date }}</td>
                <td>
                  {{
                    day.mood === null
                      ? c("care.noValue")
                      : c(`care.mood.${day.mood}`)
                  }}
                </td>
                <td>{{ day.sleepHours ?? c("care.noValue") }}</td>
                <td>{{ day.exerciseCount }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p
          v-for="entry in detail.checkIns.filter((item) => item.note)"
          :key="entry.date"
          class="care-note"
        >
          {{ entry.date }} · {{ entry.note }}
        </p>
      </section>
      <h3>{{ c("care.assessment") }}</h3>
      <article
        v-for="record in detail.records.items"
        :key="record.followUpId"
        class="care-record"
      >
        <div class="care-list-row">
          <div>
            <strong>{{ dateText(record.dueAt) }}</strong>
            <p>
              {{ c(`care.status.${record.followUpStatus}`) }} ·
              {{ record.score }} · {{ record.level }}
            </p>
          </div>
          <button
            v-if="!['Completed', 'Cancelled'].includes(record.followUpStatus)"
            type="button"
            :disabled="busy"
            @click="editor = { followUpId: record.followUpId }"
          >
            {{ c("care.newPlan") }}
          </button>
        </div>
        <p>{{ record.notice }}</p>
        <h4 v-if="record.reviews.length">{{ c("care.review") }}</h4>
        <p v-for="review in record.reviews" :key="review.reviewedAt">
          {{ dateText(review.reviewedAt) }} · {{ review.level }} ·
          {{ review.reason }}
        </p>
      </article>
      <PageControls
        :page="page"
        :total="Math.max(detail.records.total, detail.plans.total)"
        :size="size"
        :busy="busy"
        @change="changePage"
      />
    </template>
    <CarePlanEditor
      v-if="editor"
      :key="editor.plan?.id ?? editor.followUpId"
      :follow-up-id="editor.followUpId"
      :plan="editor.plan"
      :service="api"
      @saved="saved"
      @cancel="editor = null"
    />
    <template v-if="plans || detail">
      <h3>{{ c("care.plans") }}</h3>
      <p>{{ c("care.followUpSeparate") }}</p>
      <p v-if="!shownPlans.length">{{ c("care.noPlans") }}</p>
      <article v-for="plan in shownPlans" :key="plan.id" class="care-record">
        <div class="care-list-row">
          <div>
            <h3>{{ plan.title }}</h3>
            <span>{{ c(`care.status.${plan.status}`) }}</span>
          </div>
          <div class="care-actions">
            <template v-if="plan.status === 'Draft'"
              ><button
                type="button"
                :disabled="busy"
                @click="editor = { followUpId: plan.followUpId, plan }"
              >
                {{ c("care.edit") }}</button
              ><button
                type="button"
                :disabled="busy"
                @click="transition(plan, 'publish')"
              >
                {{ c("care.publish") }}
              </button></template
            ><button
              v-if="['Draft', 'Active'].includes(plan.status)"
              type="button"
              class="secondary"
              :disabled="busy"
              @click="transition(plan, 'cancel')"
            >
              {{ c("care.cancelPlan") }}
            </button>
          </div>
        </div>
        <ul class="care-task-list">
          <li v-for="task in plan.tasks" :key="task.id">
            <strong
              >{{ task.dueDate }} · {{ c(`care.task.${task.kind}`) }}</strong
            >
            <span v-if="task.exerciseId">{{
              c(
                task.exerciseId === "grounding"
                  ? "care.exercise.grounding"
                  : task.exerciseId === "pause"
                    ? "care.exercise.pause"
                    : "care.exercise.smallStep",
              )
            }}</span>
            <span>{{ c(`care.status.${task.status}`) }}</span>
            <p v-if="task.feedback">{{ task.feedback }}</p>
          </li>
        </ul>
      </article>
      <PageControls
        v-if="plans"
        :page="page"
        :total="plans.total"
        :size="size"
        :busy="busy"
        @change="changePage"
      />
    </template>
  </section>
</template>
