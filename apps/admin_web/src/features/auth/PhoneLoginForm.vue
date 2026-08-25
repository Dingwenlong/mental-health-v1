<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { ApiProblemError } from '../../api/client'
import { getUiCopy, type UiCopyKey } from '../../generated/uiCopy.generated'
import { runAliyunCaptcha, type CaptchaBootstrap } from './aliyunCaptcha'

export type SmsChallenge = { challengeId: string; challengeToken: string; expiresAt: string; resendAt: string }
export type PhoneLoginService = {
  bootstrap: (phoneNumber: string) => Promise<CaptchaBootstrap>
  createChallenge: (preChallengeToken: string, captchaVerifyParam: string) => Promise<SmsChallenge>
  verify: (challengeToken: string, code: string) => Promise<void>
}
export type CaptchaRunner = (bootstrap: CaptchaBootstrap) => Promise<string>

const props = defineProps<{ phoneLogin: PhoneLoginService; captchaRunner?: CaptchaRunner }>()
const phoneNumber = ref('')
const smsCode = ref('')
const challengeToken = ref<string | null>(null)
const isBusy = ref(false)
const secondsUntilResend = ref(0)
const errorCopyKey = ref<UiCopyKey | null>(null)
let countdown: ReturnType<typeof setInterval> | null = null

const hasChallenge = computed(() => challengeToken.value !== null)
const sendLabel = computed(() => {
  if (isBusy.value && !hasChallenge.value) return getUiCopy('auth.sendingCode')
  return secondsUntilResend.value > 0
    ? `${secondsUntilResend.value}${getUiCopy('auth.resendCodeSuffix')}`
    : getUiCopy('auth.sendCode')
})

function mapError(error: unknown): UiCopyKey {
  const code = error instanceof ApiProblemError
    ? error.problem.code
    : typeof error === 'object' && error !== null && 'code' in error ? String(error.code) : ''
  const errorKeys: Record<string, UiCopyKey> = {
    INVALID_PHONE_NUMBER: 'auth.phoneRequired', CAPTCHA_FAILED: 'auth.captchaFailed',
    LOGIN_CHALLENGE_INVALID: 'auth.challengeInvalid', SMS_RATE_LIMITED: 'auth.smsRateLimited',
    INVALID_SMS_CODE: 'auth.invalidSmsCode', AUTH_PROVIDER_UNAVAILABLE: 'auth.providerUnavailable',
  }
  return errorKeys[code] ?? 'error.retry'
}

function startCountdown(): void {
  if (countdown) clearInterval(countdown)
  secondsUntilResend.value = 60
  countdown = setInterval(() => {
    secondsUntilResend.value = Math.max(0, secondsUntilResend.value - 1)
    if (secondsUntilResend.value === 0 && countdown) { clearInterval(countdown); countdown = null }
  }, 1000)
}

async function sendCode(): Promise<void> {
  if (isBusy.value || secondsUntilResend.value > 0) return
  if (!/^1\d{10}$/.test(phoneNumber.value)) { errorCopyKey.value = 'auth.phoneRequired'; return }
  isBusy.value = true
  errorCopyKey.value = null
  try {
    const bootstrap = await props.phoneLogin.bootstrap(phoneNumber.value)
    const captchaVerifyParam = await (props.captchaRunner ?? runAliyunCaptcha)(bootstrap)
    const challenge = await props.phoneLogin.createChallenge(bootstrap.preChallengeToken, captchaVerifyParam)
    challengeToken.value = challenge.challengeToken
    smsCode.value = ''
    startCountdown()
  } catch (error) {
    errorCopyKey.value = mapError(error)
  } finally {
    isBusy.value = false
  }
}

async function submit(): Promise<void> {
  if (!hasChallenge.value) return sendCode()
  if (isBusy.value || !/^\d{6}$/.test(smsCode.value)) {
    if (!isBusy.value) errorCopyKey.value = 'auth.invalidSmsCode'
    return
  }
  isBusy.value = true
  errorCopyKey.value = null
  try { await props.phoneLogin.verify(challengeToken.value!, smsCode.value) }
  catch (error) { errorCopyKey.value = mapError(error) }
  finally { isBusy.value = false }
}

onBeforeUnmount(() => { if (countdown) clearInterval(countdown) })
</script>

<template>
  <form class="login-card" @submit.prevent="submit">
    <h1>{{ getUiCopy('admin.title') }}</h1>
    <label>{{ getUiCopy('auth.phone') }}
      <input v-model.trim="phoneNumber" data-test="login-phone" type="tel" inputmode="numeric" autocomplete="tel-national" maxlength="11" :disabled="hasChallenge || isBusy" :aria-invalid="errorCopyKey === 'auth.phoneRequired'" required />
    </label>
    <label v-if="hasChallenge">{{ getUiCopy('auth.smsCode') }}
      <input v-model.trim="smsCode" data-test="login-sms-code" inputmode="numeric" autocomplete="one-time-code" maxlength="6" :disabled="isBusy" :aria-invalid="errorCopyKey === 'auth.invalidSmsCode'" required />
    </label>
    <p v-if="hasChallenge" class="hint">{{ getUiCopy('auth.sendHint') }}</p>
    <p v-if="errorCopyKey" class="error" role="alert" aria-live="assertive">{{ getUiCopy(errorCopyKey) }}</p>
    <button v-if="hasChallenge" data-test="login-submit" type="submit" :disabled="isBusy">{{ getUiCopy(isBusy ? 'auth.loggingIn' : 'auth.login') }}</button>
    <button data-test="login-send-code" type="button" :disabled="isBusy || secondsUntilResend > 0" @click="sendCode">{{ sendLabel }}</button>
  </form>
</template>
