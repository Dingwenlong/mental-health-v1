// @vitest-environment jsdom

import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, describe, expect, it, vi } from 'vitest'
import CatalogView from '../features/catalog/CatalogView.vue'
import { useAuthStore } from '../stores/auth'

afterEach(() => {
  sessionStorage.clear()
  vi.unstubAllGlobals()
})

describe('catalog administration', () => {
  it('rejects AI video plan before sending request', async () => {
    const createPlan = vi.fn()
    const wrapper = mount(CatalogView, {
      props: {
        catalogService: {
          listPlans: vi.fn().mockResolvedValue([]),
          createPlan,
        },
      },
    })
    await wrapper.get('[data-test=name]').setValue('不支持的测试套餐')
    await wrapper.get('[data-test=kind]').setValue('Ai')
    await wrapper.get('[data-test=channel]').setValue('Video')
    await wrapper.get('[data-test=payment-mode]').setValue('Free')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.text()).toContain('当前版本不能创建 AI 视频套餐')
    expect(createPlan).not.toHaveBeenCalled()
  })

  it('shows MFA input state from the stable problem code', async () => {
    setActivePinia(createPinia())
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            status: 401,
            code: 'MFA_REQUIRED',
            title: 'Text can change without changing behavior',
          }),
          { status: 401, headers: { 'Content-Type': 'application/json' } },
        ),
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({ accessToken: 'api-token', expiresAt: '2030-01-01T00:00:00Z' }),
          { status: 200, headers: { 'Content-Type': 'application/json' } },
        ),
      )
    vi.stubGlobal('fetch', fetch)
    const auth = useAuthStore()

    expect(await auth.login('doctor@example.test', 'local-password')).toBe(false)
    expect(auth.needsMfaCode).toBe(true)
    expect(auth.errorCopyKey).toBe('auth.mfaRequired')

    expect(await auth.login('doctor@example.test', 'local-password', '123456')).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
    expect(fetch).toHaveBeenCalledTimes(2)
  })
})
