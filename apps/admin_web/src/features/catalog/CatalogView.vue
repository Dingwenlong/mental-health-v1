<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ApiProblemError } from '../../api/client'
import EmptyState from '../../components/EmptyState.vue'
import { getUiCopy, type UiCopyKey } from '../../generated/uiCopy.generated'
import {
  catalogService as defaultCatalogService,
  type CatalogService,
  type ServicePlan,
  type ServicePlanInput,
} from './catalogService'

const props = withDefaults(defineProps<{ catalogService?: CatalogService }>(), {
  catalogService: () => defaultCatalogService,
})

const plans = ref<ServicePlan[]>([])
const isSaving = ref(false)
const errorKey = ref<UiCopyKey | null>(null)
const saved = ref(false)
const form = reactive<ServicePlanInput>({
  name: '',
  kind: 'Human',
  channel: 'Chat',
  paymentMode: 'Free',
  priceInMinorUnits: 0,
  currency: 'CNY',
  durationMinutes: 30,
})

onMounted(load)

async function load(): Promise<void> {
  try {
    plans.value = await props.catalogService.listPlans()
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  }
}

async function submit(): Promise<void> {
  saved.value = false
  errorKey.value = null
  if (form.kind === 'Ai' && form.channel === 'Video') {
    errorKey.value = 'admin.plan.unsupportedAiVideo'
    return
  }
  isSaving.value = true
  try {
    const input: ServicePlanInput = {
      ...form,
      priceInMinorUnits: form.paymentMode === 'Free' ? 0 : Number(form.priceInMinorUnits),
      durationMinutes: Number(form.durationMinutes),
    }
    const created = await props.catalogService.createPlan(input)
    plans.value.push(created)
    saved.value = true
    form.name = ''
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  } finally {
    isSaving.value = false
  }
}

async function deactivate(id: string): Promise<void> {
  if (!props.catalogService.deactivatePlan) return
  try {
    await props.catalogService.deactivatePlan(id)
    plans.value = plans.value.filter((plan) => plan.id !== id)
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  }
}

function copyKeyFor(error: unknown): UiCopyKey {
  return error instanceof ApiProblemError && error.problem.code === 'FORBIDDEN_RESOURCE'
    ? 'error.forbidden'
    : 'error.retry'
}

function planLabel(plan: ServicePlan): string {
  const key = `plan.${plan.kind === 'Ai' ? 'ai' : 'human'}.${plan.channel.toLowerCase()}`
  return getUiCopy(key as UiCopyKey)
}
</script>

<template>
  <section class="workspace-panel">
    <header class="section-header">
      <h2>{{ getUiCopy('admin.catalog') }}</h2>
      <button type="button" class="secondary" @click="load">
        {{ getUiCopy('common.refresh') }}
      </button>
    </header>

    <form class="editor-form" @submit.prevent="submit">
      <h3>{{ getUiCopy('admin.plan.new') }}</h3>
      <label>
        {{ getUiCopy('admin.plan.name') }}
        <input data-test="name" v-model.trim="form.name" required />
      </label>
      <label>
        {{ getUiCopy('admin.plan.kind') }}
        <select data-test="kind" v-model="form.kind">
          <option value="Human">{{ getUiCopy('option.human') }}</option>
          <option value="Ai">{{ getUiCopy('option.ai') }}</option>
        </select>
      </label>
      <label>
        {{ getUiCopy('admin.plan.channel') }}
        <select data-test="channel" v-model="form.channel">
          <option value="Chat">{{ getUiCopy('option.chat') }}</option>
          <option value="Video">{{ getUiCopy('option.video') }}</option>
        </select>
      </label>
      <label>
        {{ getUiCopy('admin.plan.paymentMode') }}
        <select data-test="payment-mode" v-model="form.paymentMode">
          <option value="Free">{{ getUiCopy('option.free') }}</option>
          <option value="DemoPaid">{{ getUiCopy('option.demoPaid') }}</option>
        </select>
      </label>
      <label v-if="form.paymentMode === 'DemoPaid'">
        {{ getUiCopy('admin.plan.price') }}
        <input v-model.number="form.priceInMinorUnits" type="number" min="1" required />
      </label>
      <label>
        {{ getUiCopy('admin.plan.duration') }}
        <input v-model.number="form.durationMinutes" type="number" min="10" max="180" required />
      </label>
      <button :disabled="isSaving" type="submit">
        {{ getUiCopy(isSaving ? 'common.saving' : 'common.save') }}
      </button>
    </form>

    <p v-if="errorKey" class="error" role="alert">{{ getUiCopy(errorKey) }}</p>
    <p v-if="saved" class="success" role="status">{{ getUiCopy('admin.plan.saved') }}</p>

    <EmptyState v-if="plans.length === 0" :message="getUiCopy('empty.records')" />
    <ul v-else class="record-list">
      <li v-for="plan in plans" :key="plan.id">
        <div>
          <strong>{{ plan.name }}</strong>
          <span>{{ planLabel(plan) }}</span>
          <span>{{ plan.paymentMode === 'Free' ? getUiCopy('catalog.free') : getUiCopy('order.demoPaid') }}</span>
        </div>
        <button v-if="props.catalogService.deactivatePlan" type="button" class="danger" @click="deactivate(plan.id)">
          {{ getUiCopy('common.disable') }}
        </button>
      </li>
    </ul>
  </section>
</template>
