<script setup lang="ts">
import { onMounted, ref } from "vue";
import { ApiProblemError } from "../../api/client";
import {
  c,
  careApi,
  today,
  type CareApi,
  type CarePlan,
  type Exercise,
} from "./careService";
const props = defineProps<{
  followUpId: string;
  plan?: CarePlan;
  service?: CareApi;
}>();
const emit = defineEmits<{ saved: []; cancel: [] }>();
const api = props.service ?? careApi;
const title = ref(props.plan?.title ?? c("care.planTitleDefault"));
const tasks = ref(
  props.plan?.tasks.map((task) => ({
    kind: task.kind,
    exerciseId: task.exerciseId,
    dueDate: task.dueDate,
  })) ?? [
    {
      kind: "CheckIn" as "CheckIn" | "Exercise",
      exerciseId: null as string | null,
      dueDate: today(),
    },
  ],
);
const exercises = ref<Exercise[]>([]);
const busy = ref(false);
const error = ref("");
const key = crypto.randomUUID();
onMounted(async () => {
  try {
    exercises.value = await api.get<Exercise[]>("exercises");
  } catch {
    error.value = c("care.retry");
  }
});
async function save(): Promise<void> {
  if (busy.value) return;
  busy.value = true;
  error.value = "";
  try {
    const body = {
      title: title.value,
      tasks: tasks.value.map((task) => ({
        ...task,
        exerciseId: task.kind === "CheckIn" ? null : task.exerciseId,
      })),
    };
    if (props.plan)
      await api.put(`care-plans/${props.plan.id}`, {
        ...body,
        version: props.plan.version,
      });
    else
      await api.post("care-plans", {
        ...body,
        followUpId: props.followUpId,
        idempotencyKey: key,
      });
    emit("saved");
  } catch (failure) {
    error.value = c(
      failure instanceof ApiProblemError && failure.problem.status === 409
        ? "care.conflict"
        : "care.invalid",
    );
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <form class="care-editor" @submit.prevent="save">
    <h3>{{ c("care.newPlan") }}</h3>
    <p>{{ c("care.draftNotice") }}</p>
    <label
      >{{ c("care.planTitle")
      }}<input
        v-model.trim="title"
        data-test="plan-title"
        maxlength="120"
        required
        :disabled="busy"
    /></label>
    <div v-for="(task, index) in tasks" :key="index" class="care-task-form">
      <label
        >{{ c("care.taskKind")
        }}<select v-model="task.kind" :disabled="busy">
          <option value="CheckIn">{{ c("care.task.CheckIn") }}</option>
          <option value="Exercise">{{ c("care.task.Exercise") }}</option>
        </select></label
      >
      <label v-if="task.kind === 'Exercise'"
        >{{ c("care.exercises")
        }}<select v-model="task.exerciseId" required :disabled="busy">
          <option
            v-for="exercise in exercises"
            :key="exercise.id"
            :value="exercise.id"
          >
            {{ exercise.title }}
          </option>
        </select></label
      >
      <label
        >{{ c("care.dueDate")
        }}<input
          v-model="task.dueDate"
          type="date"
          :min="today()"
          required
          :disabled="busy"
      /></label>
      <button
        v-if="tasks.length > 1"
        type="button"
        class="secondary"
        :disabled="busy"
        @click="tasks.splice(index, 1)"
      >
        {{ c("care.removeTask") }}
      </button>
    </div>
    <div class="care-actions">
      <button
        type="button"
        class="secondary"
        :disabled="busy || tasks.length >= 30"
        @click="
          tasks.push({ kind: 'CheckIn', exerciseId: null, dueDate: today() })
        "
      >
        {{ c("care.addTask") }}</button
      ><button type="submit" :disabled="busy">{{ c("care.save") }}</button
      ><button
        type="button"
        class="secondary"
        :disabled="busy"
        @click="emit('cancel')"
      >
        {{ c("care.cancel") }}
      </button>
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
  </form>
</template>
