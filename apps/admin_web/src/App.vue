<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useAuthStore } from './stores/auth'
import { getUiCopy, type UiCopyKey } from './generated/uiCopy.generated'
import CatalogView from './features/catalog/CatalogView.vue'
import PractitionerView from './features/catalog/PractitionerView.vue'
import AvailabilityView from './features/catalog/AvailabilityView.vue'

const auth = useAuthStore()
const activeView = ref<'catalog' | 'practitioners' | 'availability'>('catalog')
const credentials = reactive({ email: '', password: '', totpCode: '' })

async function submitLogin(): Promise<void> {
  if (auth.needsMfaSetup) {
    await auth.completeMfaSetup(credentials.totpCode)
    return
  }
  await auth.login(
    credentials.email,
    credentials.password,
    auth.needsMfaCode ? credentials.totpCode : undefined,
  )
}

function label(key: string): string {
  return getUiCopy(key as UiCopyKey)
}
</script>

<template>
  <main id="app-shell">
    <section v-if="!auth.isAuthenticated" class="login-shell">
      <form class="login-card" @submit.prevent="submitLogin">
        <h1>{{ getUiCopy('admin.title') }}</h1>
        <label>{{ getUiCopy('auth.email') }}<input v-model.trim="credentials.email" type="email" required /></label>
        <label>{{ getUiCopy('auth.password') }}<input v-model="credentials.password" type="password" required /></label>
        <template v-if="auth.needsMfaSetup || auth.needsMfaCode">
          <p v-if="auth.needsMfaSetup">{{ getUiCopy('auth.mfaSetup') }}</p>
          <code v-if="auth.needsMfaSetup">{{ auth.mfaManualKey }}</code>
          <label>{{ getUiCopy('auth.mfaCode') }}<input v-model.trim="credentials.totpCode" inputmode="numeric" maxlength="6" required /></label>
        </template>
        <p v-if="auth.errorCopyKey" class="error" role="alert">{{ label(auth.errorCopyKey) }}</p>
        <button type="submit" :disabled="auth.isBusy">
          {{ getUiCopy(auth.isBusy ? 'auth.loggingIn' : auth.needsMfaSetup ? 'auth.enableMfa' : 'auth.login') }}
        </button>
      </form>
    </section>

    <template v-else>
      <header class="topbar">
        <h1>{{ getUiCopy('admin.title') }}</h1>
        <button type="button" class="secondary" @click="auth.logout">{{ getUiCopy('auth.logout') }}</button>
      </header>
      <nav class="section-nav" :aria-label="getUiCopy('admin.navigation')">
        <button type="button" :class="{ active: activeView === 'catalog' }" @click="activeView = 'catalog'">{{ getUiCopy('admin.catalog') }}</button>
        <button type="button" :class="{ active: activeView === 'practitioners' }" @click="activeView = 'practitioners'">{{ getUiCopy('admin.practitioners') }}</button>
        <button type="button" :class="{ active: activeView === 'availability' }" @click="activeView = 'availability'">{{ getUiCopy('admin.availability') }}</button>
      </nav>
      <CatalogView v-if="activeView === 'catalog'" />
      <PractitionerView v-else-if="activeView === 'practitioners'" />
      <AvailabilityView v-else />
    </template>
  </main>
</template>
