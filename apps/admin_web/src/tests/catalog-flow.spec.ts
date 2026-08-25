// @vitest-environment jsdom

import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import CatalogView from '../features/catalog/CatalogView.vue'

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
})
