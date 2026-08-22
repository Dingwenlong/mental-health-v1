import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiProblemError, apiClient, tokenStore } from '../api/client'

type LoginResponse = { accessToken: string; expiresAt: string }
type MfaSetupResponse = { manualKey: string; provisioningUri: string; enabled: boolean }

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(tokenStore.read())
  const isBusy = ref(false)
  const needsMfaCode = ref(false)
  const needsMfaSetup = ref(false)
  const mfaManualKey = ref('')
  const errorCopyKey = ref<string | null>(null)
  const serverMessage = ref<string | null>(null)
  let setupToken: string | null = null
  let lastEmail = ''
  let lastPassword = ''

  const isAuthenticated = computed(() => accessToken.value !== null)

  async function login(email: string, password: string, totpCode?: string): Promise<boolean> {
    lastEmail = email.trim()
    lastPassword = password
    return runLogin(totpCode)
  }

  async function completeMfaSetup(totpCode: string): Promise<boolean> {
    if (!setupToken) return false
    isBusy.value = true
    clearError()
    try {
      await apiClient.post<MfaSetupResponse>('auth/mfa/setup', { totpCode }, setupToken)
      needsMfaSetup.value = false
      return await runLogin(totpCode, false)
    } catch (error) {
      setError(error)
      return false
    } finally {
      isBusy.value = false
    }
  }

  function logout(): void {
    tokenStore.clear()
    accessToken.value = null
    needsMfaCode.value = false
    needsMfaSetup.value = false
    lastPassword = ''
  }

  async function runLogin(totpCode?: string, manageBusy = true): Promise<boolean> {
    if (manageBusy) {
      isBusy.value = true
      clearError()
    }
    try {
      const response = await apiClient.post<LoginResponse>('auth/login', {
        email: lastEmail,
        password: lastPassword,
        totpCode: totpCode ?? null,
      })
      tokenStore.write(response.accessToken)
      accessToken.value = response.accessToken
      needsMfaCode.value = false
      needsMfaSetup.value = false
      lastPassword = ''
      return true
    } catch (error) {
      if (error instanceof ApiProblemError && error.problem.code === 'MFA_REQUIRED') {
        const token = error.problem.setupToken
        if (typeof token === 'string' && token.length > 0) {
          setupToken = token
          try {
            const setup = await apiClient.post<MfaSetupResponse>(
              'auth/mfa/setup',
              { totpCode: null },
              token,
            )
            mfaManualKey.value = setup.manualKey
            needsMfaSetup.value = true
            needsMfaCode.value = false
          } catch (setupError) {
            setError(setupError)
            return false
          }
        } else {
          needsMfaCode.value = true
          needsMfaSetup.value = false
        }
      }
      setError(error)
      return false
    } finally {
      if (manageBusy) isBusy.value = false
    }
  }

  function setError(error: unknown): void {
    if (!(error instanceof ApiProblemError)) {
      errorCopyKey.value = 'error.retry'
      serverMessage.value = null
      return
    }
    errorCopyKey.value =
      {
        INVALID_CREDENTIALS: 'auth.invalidCredentials',
        INVALID_MFA_CODE: 'auth.invalidMfaCode',
        MFA_REQUIRED: 'auth.mfaRequired',
        FORBIDDEN_RESOURCE: 'error.forbidden',
      }[error.problem.code] ?? 'error.retry'
    serverMessage.value = error.message
  }

  function clearError(): void {
    errorCopyKey.value = null
    serverMessage.value = null
  }

  return {
    isAuthenticated,
    isBusy,
    needsMfaCode,
    needsMfaSetup,
    mfaManualKey,
    errorCopyKey,
    serverMessage,
    login,
    completeMfaSetup,
    logout,
  }
})
