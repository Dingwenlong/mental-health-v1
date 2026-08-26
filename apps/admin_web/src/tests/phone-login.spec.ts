// @vitest-environment jsdom

import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiClient, ApiProblemError } from '../api/client'
import PhoneLoginForm, {
  type PhoneLoginService,
} from '../features/auth/PhoneLoginForm.vue'

const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0))

function createPhoneLoginService(): PhoneLoginService {
  return {
    bootstrap: vi.fn().mockResolvedValue({
      preChallengeToken: 'pre-challenge-1',
      prefix: 'xfkdn8',
      encryptedSceneId: 'encrypted-scene-1',
      expiresAt: '2030-01-01T00:00:00.000Z',
    }),
    createChallenge: vi.fn().mockResolvedValue({
      challengeId: 'challenge-1',
      challengeToken: 'challenge-token-1',
      expiresAt: '2030-01-01T00:05:00.000Z',
      resendAt: '2030-01-01T00:01:00.000Z',
    }),
    verify: vi.fn().mockResolvedValue(undefined),
  }
}

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
  delete window.AliyunCaptchaConfig
  delete window.initAliyunCaptcha
  document.querySelectorAll('script[src="https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js"]')
    .forEach((script) => script.remove())
})

describe('phone login form', () => {
  it('separates the management identity from the phone login task', () => {
    const wrapper = mount(PhoneLoginForm, {
      props: { phoneLogin: createPhoneLoginService() },
    })

    const introduction = wrapper.find('[data-test=login-introduction]')
    expect(introduction.exists()).toBe(true)
    expect(introduction.find('h1').text()).toBe('心理健康管理端')
    expect(wrapper.find('form h2').text()).toBe('登录')
    expect(wrapper.find('form h1').exists()).toBe(false)
  })

  it('starts with only a mainland mobile field and a send-code action', () => {
    const wrapper = mount(PhoneLoginForm, {
      props: { phoneLogin: createPhoneLoginService() },
    })

    expect(wrapper.get('[data-test=login-phone]').attributes()).toMatchObject({
      type: 'tel',
      inputmode: 'numeric',
      autocomplete: 'tel-national',
      maxlength: '11',
    })
    expect(wrapper.find('[data-test=login-send-code]').exists()).toBe(true)
    expect(wrapper.find('[data-test=login-sms-code]').exists()).toBe(false)
    expect(wrapper.find('[data-test=login-submit]').exists()).toBe(false)
    expect(wrapper.findAll('input[type=password], input[type=email]').length).toBe(0)
    expect(wrapper.find('#admin-captcha-element').exists()).toBe(true)
    expect(wrapper.find('#admin-captcha-button').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('+86')
    expect(wrapper.text()).not.toContain('换个手机号')
  })

  it('lets a failed Aliyun script load be retried without retaining the failed node', async () => {
    vi.resetModules()
    const { runAliyunCaptcha } = await import('../features/auth/aliyunCaptcha')
    const bootstrap = {
      preChallengeToken: 'pre-challenge-1',
      prefix: 'xfkdn8',
      encryptedSceneId: 'encrypted-scene-1',
      expiresAt: '2030-01-01T00:00:00.000Z',
    }

    const first = runAliyunCaptcha(bootstrap)
    const failedScript = document.querySelector('script[src="https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js"]') as HTMLScriptElement
    failedScript.dispatchEvent(new Event('error'))
    await expect(first).rejects.toThrow('Aliyun captcha script failed to load')
    expect(failedScript.isConnected).toBe(false)

    const retry = runAliyunCaptcha(bootstrap)
    const retryScript = document.querySelector('script[src="https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js"]') as HTMLScriptElement
    expect(retryScript).not.toBe(failedScript)
    retryScript.dispatchEvent(new Event('error'))
    await expect(retry).rejects.toThrow('Aliyun captcha script failed to load')
  })

  it('uses the V3 success and instance hooks for encrypted traceless verification', async () => {
    vi.useFakeTimers()
    vi.resetModules()
    let capturedOptions: Record<string, unknown> | undefined
    const startTracelessVerification = vi.fn()
    window.initAliyunCaptcha = vi.fn((options) => {
      capturedOptions = options as unknown as Record<string, unknown>
      const getInstance = capturedOptions.getInstance as ((instance: unknown) => void) | undefined
      getInstance?.({ startTracelessVerification })
    })
    const { runAliyunCaptcha } = await import('../features/auth/aliyunCaptcha')
    const pending = runAliyunCaptcha({
      preChallengeToken: 'pre-challenge-1',
      prefix: 'xfkdn8',
      encryptedSceneId: 'encrypted-scene-1',
      expiresAt: '2030-01-01T00:00:00.000Z',
    })
    const script = document.querySelector('script[src="https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js"]') as HTMLScriptElement
    script.dispatchEvent(new Event('load'))

    await vi.runAllTicks()
    expect(capturedOptions).toBeDefined()
    expect(vi.mocked(window.initAliyunCaptcha!).mock.calls[0]).toHaveLength(1)
    expect(Object.keys(capturedOptions!).sort()).toEqual([
      'EncryptedSceneId',
      'SceneId',
      'button',
      'delayBeforeSuccess',
      'element',
      'fail',
      'getInstance',
      'language',
      'mode',
      'onClose',
      'onError',
      'slideStyle',
      'success',
    ])
    expect(capturedOptions).toMatchObject({
      SceneId: '1lae8yfm',
      EncryptedSceneId: 'encrypted-scene-1',
      element: '#admin-captcha-element',
      button: '#admin-captcha-button',
    })

    await vi.advanceTimersByTimeAsync(2100)
    expect(startTracelessVerification).toHaveBeenCalledOnce()
    const success = capturedOptions!.success as (value: string) => void
    success('captcha-pass')
    await expect(pending).resolves.toBe('captcha-pass')
  })

  it('parses an integer Retry-After response into an API problem', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ status: 429, code: 'SMS_RATE_LIMITED' }),
      { status: 429, headers: { 'Content-Type': 'application/json', 'Retry-After': '17' } },
    )))
    const client = new ApiClient('https://api.example.test/')

    let thrown: unknown
    try { await client.post('auth/sms/challenges', {}) }
    catch (error) { thrown = error }

    expect(thrown).toBeInstanceOf(ApiProblemError)
    expect((thrown as ApiProblemError & { retryAfterSeconds?: number }).retryAfterSeconds).toBe(17)
  })

  it('bootstraps, runs captcha, then creates a challenge before locking the phone', async () => {
    const order: string[] = []
    const phoneLogin = createPhoneLoginService()
    phoneLogin.bootstrap = vi.fn(async () => {
      order.push('bootstrap')
      return {
        preChallengeToken: 'pre-challenge-1',
        prefix: 'xfkdn8',
        encryptedSceneId: 'encrypted-scene-1',
        expiresAt: '2030-01-01T00:00:00.000Z',
      }
    })
    phoneLogin.createChallenge = vi.fn(async () => {
      order.push('challenge')
      return {
        challengeId: 'challenge-1',
        challengeToken: 'challenge-token-1',
        expiresAt: '2030-01-01T00:05:00.000Z',
        resendAt: '2030-01-01T00:01:00.000Z',
      }
    })
    const captchaRunner = vi.fn(async () => {
      order.push('captcha runner')
      return 'captcha-pass'
    })
    const wrapper = mount(PhoneLoginForm, { props: { phoneLogin, captchaRunner } })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await flush()

    expect(order).toEqual(['bootstrap', 'captcha runner', 'challenge'])
    expect(phoneLogin.createChallenge).toHaveBeenCalledWith('pre-challenge-1', 'captcha-pass')
    expect(wrapper.get('[data-test=login-phone]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-test=login-sms-code]').attributes()).toMatchObject({
      inputmode: 'numeric',
      autocomplete: 'one-time-code',
      maxlength: '6',
    })
    expect(wrapper.find('[data-test=login-submit]').exists()).toBe(true)
    expect(wrapper.text()).toContain('如果该手机号已登记，你会收到验证码')
  })

  it('keeps resend disabled for sixty seconds, then bootstraps again', async () => {
    vi.useFakeTimers()
    const phoneLogin = createPhoneLoginService()
    const captchaRunner = vi.fn().mockResolvedValue('captcha-pass')
    const wrapper = mount(PhoneLoginForm, { props: { phoneLogin, captchaRunner } })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await vi.runAllTicks()
    expect(wrapper.get('[data-test=login-send-code]').attributes('disabled')).toBeDefined()

    await vi.advanceTimersByTimeAsync(60_000)
    expect(wrapper.get('[data-test=login-send-code]').attributes('disabled')).toBeUndefined()
    await wrapper.get('[data-test=login-send-code]').trigger('click')
    await vi.runAllTicks()

    expect(phoneLogin.bootstrap).toHaveBeenCalledTimes(2)
    expect(captchaRunner).toHaveBeenCalledTimes(2)
  })

  it('uses the server retry-after seconds on the send button when SMS is rate limited', async () => {
    const phoneLogin = createPhoneLoginService()
    const rateLimited = Object.assign(
      new ApiProblemError({ code: 'SMS_RATE_LIMITED' }),
      { retryAfterSeconds: 17 },
    )
    phoneLogin.bootstrap = vi.fn().mockRejectedValue(rateLimited)
    const wrapper = mount(PhoneLoginForm, { props: { phoneLogin } })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await flush()

    expect(wrapper.get('[data-test=login-send-code]').text()).toBe('17 秒后重新获取')
    expect(wrapper.get('[data-test=login-send-code]').attributes('disabled')).toBeDefined()
    wrapper.unmount()
  })

  it('uses explicit empty-value validation and moves focus to the invalid field', async () => {
    const wrapper = mount(PhoneLoginForm, {
      attachTo: document.body,
      props: { phoneLogin: createPhoneLoginService() },
    })

    expect(wrapper.get('form').attributes('novalidate')).toBeDefined()
    await wrapper.get('form').trigger('submit')
    await flush()

    expect(wrapper.get('[role=alert]').text()).toBe('请输入 11 位手机号')
    expect(document.activeElement).toBe(wrapper.get('[data-test=login-phone]').element)
    wrapper.unmount()
  })

  it('moves focus to an empty SMS-code field after the phone is locked', async () => {
    const wrapper = mount(PhoneLoginForm, {
      attachTo: document.body,
      props: { phoneLogin: createPhoneLoginService(), captchaRunner: vi.fn().mockResolvedValue('captcha-pass') },
    })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await flush()
    await wrapper.get('form').trigger('submit')
    await flush()

    expect(wrapper.get('[role=alert]').text()).toBe('验证码无效或已过期')
    expect(document.activeElement).toBe(wrapper.get('[data-test=login-sms-code]').element)
    wrapper.unmount()
  })

  it('marks the form busy while sending a code', async () => {
    let resolveBootstrap: ((value: Awaited<ReturnType<PhoneLoginService['bootstrap']>>) => void) | undefined
    const phoneLogin = createPhoneLoginService()
    phoneLogin.bootstrap = vi.fn(() => new Promise<Awaited<ReturnType<PhoneLoginService['bootstrap']>>>((resolve) => { resolveBootstrap = resolve }))
    const wrapper = mount(PhoneLoginForm, { props: { phoneLogin } })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('[data-test=login-send-code]').trigger('click')
    await wrapper.vm.$nextTick()
    expect(wrapper.get('form').attributes('aria-busy')).toBe('true')

    resolveBootstrap?.({
      preChallengeToken: 'pre-challenge-1', prefix: 'xfkdn8', encryptedSceneId: 'encrypted-scene-1', expiresAt: '2030-01-01T00:00:00.000Z',
    })
    wrapper.unmount()
  })

  it('shows the confirmed short message for an invalid SMS code', async () => {
    const phoneLogin = createPhoneLoginService()
    phoneLogin.verify = vi.fn().mockRejectedValue({ code: 'INVALID_SMS_CODE' })
    const wrapper = mount(PhoneLoginForm, {
      attachTo: document.body,
      props: { phoneLogin, captchaRunner: vi.fn().mockResolvedValue('captcha-pass') },
    })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await flush()
    await wrapper.get('[data-test=login-sms-code]').setValue('123456')
    await wrapper.get('form').trigger('submit')
    await flush()
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[role=alert]').text()).toBe('验证码无效或已过期')
    await vi.waitFor(() => {
      expect(document.activeElement).toBe(wrapper.get('[data-test=login-sms-code]').element)
    })
    wrapper.unmount()
  })
})
