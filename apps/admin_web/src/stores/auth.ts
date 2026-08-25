import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { apiClient, tokenStore } from '../api/client'
import type { CaptchaBootstrap } from '../features/auth/aliyunCaptcha'
import type { SmsChallenge } from '../features/auth/PhoneLoginForm.vue'

type LoginResponse = { accessToken: string; expiresAt: string }

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(tokenStore.read())
  const isAuthenticated = computed(() => accessToken.value !== null)

  function bootstrap(phoneNumber: string): Promise<CaptchaBootstrap> {
    return apiClient.post('auth/captcha/bootstrap', { phoneNumber, client: 'admin' })
  }

  function createChallenge(preChallengeToken: string, captchaVerifyParam: string): Promise<SmsChallenge> {
    return apiClient.post('auth/sms/challenges', { preChallengeToken, captchaVerifyParam })
  }

  async function verify(challengeToken: string, code: string): Promise<void> {
    const response = await apiClient.post<LoginResponse>('auth/sms/verify', { challengeToken, code })
    tokenStore.write(response.accessToken)
    accessToken.value = response.accessToken
  }

  function logout(): void {
    tokenStore.clear()
    accessToken.value = null
  }

  return { isAuthenticated, bootstrap, createChallenge, verify, logout }
})
