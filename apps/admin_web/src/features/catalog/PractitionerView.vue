<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ApiProblemError } from '../../api/client'
import { getUiCopy, type UiCopyKey } from '../../generated/uiCopy.generated'
import {
  practitionerService as defaultPractitionerService,
  type Practitioner,
  type PractitionerService,
} from './catalogService'

const props = withDefaults(defineProps<{ practitionerService?: PractitionerService }>(), {
  practitionerService: () => defaultPractitionerService,
})
const practitioners = ref<Practitioner[]>([])
const errorKey = ref<UiCopyKey | null>(null)
const saved = ref(false)
const isSaving = ref(false)
const form = reactive({ displayName: '', role: 'Counselor' })

onMounted(load)

async function load(): Promise<void> {
  try {
    practitioners.value = await props.practitionerService.listPractitioners()
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  }
}

async function submit(): Promise<void> {
  isSaving.value = true
  saved.value = false
  errorKey.value = null
  try {
    const created = await props.practitionerService.createPractitioner(form)
    practitioners.value.push(created)
    form.displayName = ''
    saved.value = true
  } catch (error) {
    errorKey.value = copyKeyFor(error)
  } finally {
    isSaving.value = false
  }
}

async function deactivate(id: string): Promise<void> {
  if (!props.practitionerService.deactivatePractitioner) return
  try {
    await props.practitionerService.deactivatePractitioner(id)
    practitioners.value = practitioners.value.filter((item) => item.id !== id)
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
      <h2>{{ getUiCopy('admin.practitioners') }}</h2>
      <button type="button" class="secondary" @click="load">{{ getUiCopy('common.refresh') }}</button>
    </header>
    <form class="editor-form" @submit.prevent="submit">
      <h3>{{ getUiCopy('admin.practitioner.new') }}</h3>
      <label>
        {{ getUiCopy('admin.practitioner.name') }}
        <input v-model.trim="form.displayName" required />
      </label>
      <label>
        {{ getUiCopy('admin.practitioner.role') }}
        <select v-model="form.role">
          <option value="Counselor">{{ getUiCopy('option.counselor') }}</option>
          <option value="Doctor">{{ getUiCopy('option.doctor') }}</option>
        </select>
      </label>
      <button :disabled="isSaving" type="submit">{{ getUiCopy(isSaving ? 'common.saving' : 'common.save') }}</button>
    </form>
    <p v-if="errorKey" class="error" role="alert">{{ getUiCopy(errorKey) }}</p>
    <p v-if="saved" class="success" role="status">{{ getUiCopy('admin.practitioner.saved') }}</p>
    <p v-if="practitioners.length === 0" class="empty">{{ getUiCopy('empty.records') }}</p>
    <ul v-else class="record-list">
      <li v-for="item in practitioners" :key="item.id">
        <div><strong>{{ item.displayName }}</strong><span>{{ getUiCopy(item.role === 'Doctor' ? 'option.doctor' : 'option.counselor') }}</span></div>
        <button v-if="props.practitionerService.deactivatePractitioner" type="button" class="danger" @click="deactivate(item.id)">{{ getUiCopy('common.disable') }}</button>
      </li>
    </ul>
  </section>
</template>
