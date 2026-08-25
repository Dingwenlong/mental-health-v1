// @vitest-environment jsdom

import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import ContactEmailView, {
  type ContactEmailService,
} from '../features/account/ContactEmailView.vue'

const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0))

describe('contact email settings', () => {
  it('loads the current email and saves only the contact address', async () => {
    const contactEmail: ContactEmailService = {
      get: vi.fn().mockResolvedValue('care@example.test'),
      put: vi.fn().mockResolvedValue(undefined),
    }
    const wrapper = mount(ContactEmailView, { props: { contactEmail } })
    await flush()

    expect(wrapper.get('[data-test=contact-email]').element).toHaveProperty('value', 'care@example.test')
    expect(wrapper.text()).toContain('联系邮箱不能用于登录')
    expect(wrapper.find('[data-test=contact-email-save]').exists()).toBe(true)
    expect(wrapper.find('[data-test=contact-email-clear]').exists()).toBe(true)

    await wrapper.get('[data-test=contact-email]').setValue('office@example.test')
    await wrapper.get('form').trigger('submit')
    await flush()
    expect(contactEmail.put).toHaveBeenLastCalledWith('office@example.test')
  })

  it('clears the address with a null PUT without adding a separate workflow', async () => {
    const contactEmail: ContactEmailService = {
      get: vi.fn().mockResolvedValue('care@example.test'),
      put: vi.fn().mockResolvedValue(undefined),
    }
    const wrapper = mount(ContactEmailView, { props: { contactEmail } })
    await flush()

    await wrapper.get('[data-test=contact-email-clear]').trigger('click')
    await flush()

    expect(contactEmail.put).toHaveBeenCalledWith(null)
    expect(wrapper.get('[data-test=contact-email]').element).toHaveProperty('value', '')
    expect(wrapper.text()).not.toContain('验证码')
    expect(wrapper.text()).not.toContain('找回密码')
  })
})
