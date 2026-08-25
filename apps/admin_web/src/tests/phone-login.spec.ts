// @vitest-environment jsdom

import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
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
})

describe('phone login form', () => {
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
    expect(wrapper.text()).not.toContain('+86')
    expect(wrapper.text()).not.toContain('换个手机号')
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

  it('shows the confirmed short message for an invalid SMS code', async () => {
    const phoneLogin = createPhoneLoginService()
    phoneLogin.verify = vi.fn().mockRejectedValue({ code: 'INVALID_SMS_CODE' })
    const wrapper = mount(PhoneLoginForm, {
      props: { phoneLogin, captchaRunner: vi.fn().mockResolvedValue('captcha-pass') },
    })

    await wrapper.get('[data-test=login-phone]').setValue('13800138001')
    await wrapper.get('form').trigger('submit')
    await flush()
    await wrapper.get('[data-test=login-sms-code]').setValue('123456')
    await wrapper.get('form').trigger('submit')
    await flush()

    expect(wrapper.get('[role=alert]').text()).toBe('验证码无效或已过期')
  })
})
