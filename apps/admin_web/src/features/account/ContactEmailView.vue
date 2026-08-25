<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiProblemError } from '../../api/client'
import { getUiCopy } from '../../generated/uiCopy.generated'
import type { ContactEmailService } from './contactEmailService'

export type { ContactEmailService } from './contactEmailService'
const props = defineProps<{ contactEmail: ContactEmailService }>()
const email = ref('')
const isBusy = ref(false)
const error = ref('')

async function load(): Promise<void> {
  isBusy.value = true
  try { email.value = (await props.contactEmail.get()) ?? '' }
  catch { error.value = getUiCopy('error.retry') }
  finally { isBusy.value = false }
}
async function save(value: string | null): Promise<void> {
  isBusy.value = true
  error.value = ''
  try { await props.contactEmail.put(value); email.value = value ?? '' }
  catch (reason) {
    error.value = reason instanceof ApiProblemError && reason.problem.code === 'CONTACT_EMAIL_INVALID'
      ? getUiCopy('account.contactEmail.invalid') : getUiCopy('error.retry')
  } finally { isBusy.value = false }
}
function submit(): Promise<void> { const value = email.value.trim(); return save(value.length > 0 ? value : null) }
function clear(): Promise<void> { return save(null) }
onMounted(load)
</script>

<template>
  <section class="account-settings" aria-labelledby="contact-email-title">
    <h2 id="contact-email-title">{{ getUiCopy('account.contactEmail.title') }}</h2>
    <p class="hint">{{ getUiCopy('account.contactEmail.help') }}</p>
    <form @submit.prevent="submit">
      <label>{{ getUiCopy('account.contactEmail.label') }}
        <input v-model="email" data-test="contact-email" type="text" autocomplete="email" :disabled="isBusy" />
      </label>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <div class="form-actions">
        <button data-test="contact-email-save" type="submit" :disabled="isBusy">{{ getUiCopy(isBusy ? 'common.saving' : 'common.save') }}</button>
        <button data-test="contact-email-clear" type="button" class="secondary" :disabled="isBusy" @click="clear">{{ getUiCopy('account.contactEmail.clear') }}</button>
      </div>
    </form>
  </section>
</template>
