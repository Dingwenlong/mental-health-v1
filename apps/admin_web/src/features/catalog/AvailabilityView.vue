<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ApiProblemError } from '../../api/client'
import { getUiCopy, type UiCopyKey } from '../../generated/uiCopy.generated'
import {
  availabilityService as defaultAvailabilityService,
  type AvailabilityService,
  type Practitioner,
} from './catalogService'

const props = withDefaults(defineProps<{ availabilityService?: AvailabilityService }>(), {
  availabilityService: () => defaultAvailabilityService,
})
const practitioners = ref<Practitioner[]>([])
const errorKey = ref<UiCopyKey | null>(null)
const saved = ref(false)
const isSaving = ref(false)
const form = reactive({ practitionerId: '', startAt: '', endAt: '' })

onMounted(load)

async function load(): Promise<void> {
  try {
    practitioners.value = await props.availabilityService.listPractitioners()
    if (!form.practitionerId && practitioners.value.length > 0) {
      form.practitionerId = practitioners.value[0]?.id ?? ''
    }
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  }
}

async function submit(): Promise<void> {
  isSaving.value = true
  saved.value = false
  errorKey.value = null
  try {
    await props.availabilityService.createSlot(form.practitionerId, {
      startAt: new Date(form.startAt).toISOString(),
      endAt: new Date(form.endAt).toISOString(),
    })
    saved.value = true
    await load()
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  } finally {
    isSaving.value = false
  }
}

async function deactivate(practitionerId: string, slotId: string): Promise<void> {
  if (!props.availabilityService.deactivateSlot) return
  try {
    await props.availabilityService.deactivateSlot(practitionerId, slotId)
    await load()
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  }
}

function copyKeyFor(error: unknown): UiCopyKey {
  return error instanceof ApiProblemError && error.problem.code === 'FORBIDDEN_RESOURCE'
    ? 'error.forbidden'
    : 'error.retry'
}
</script>

<template>
  <section class="workspace-panel">
    <header class="section-header">
      <h2>{{ getUiCopy('admin.availability') }}</h2>
      <button type="button" class="secondary" @click="load">{{ getUiCopy('common.refresh') }}</button>
    </header>
    <form class="editor-form" @submit.prevent="submit">
      <h3>{{ getUiCopy('admin.availability.new') }}</h3>
      <label>
        {{ getUiCopy('admin.availability.practitioner') }}
        <select v-model="form.practitionerId" required>
          <option v-for="item in practitioners" :key="item.id" :value="item.id">{{ item.displayName }}</option>
        </select>
      </label>
      <label>{{ getUiCopy('admin.availability.start') }}<input v-model="form.startAt" type="datetime-local" required /></label>
      <label>{{ getUiCopy('admin.availability.end') }}<input v-model="form.endAt" type="datetime-local" required /></label>
      <button :disabled="isSaving" type="submit">{{ getUiCopy(isSaving ? 'common.saving' : 'common.save') }}</button>
    </form>
    <p v-if="errorKey" class="error" role="alert">{{ getUiCopy(errorKey) }}</p>
    <p v-if="saved" class="success" role="status">{{ getUiCopy('admin.availability.saved') }}</p>
    <div v-for="item in practitioners" :key="item.id" class="slot-group">
      <h3>{{ item.displayName }}</h3>
      <p v-if="item.availabilitySlots.length === 0" class="empty">{{ getUiCopy('empty.records') }}</p>
      <ul v-else class="record-list">
        <li v-for="slot in item.availabilitySlots" :key="slot.id">
          <div><span>{{ new Date(slot.startAt).toLocaleString() }}</span><span>{{ new Date(slot.endAt).toLocaleString() }}</span></div>
          <button v-if="props.availabilityService.deactivateSlot" type="button" class="danger" @click="deactivate(item.id, slot.id)">{{ getUiCopy('common.disable') }}</button>
        </li>
      </ul>
    </div>
  </section>
</template>
