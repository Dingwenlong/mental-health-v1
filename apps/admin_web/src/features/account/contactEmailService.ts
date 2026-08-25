import { apiClient } from '../../api/client'

export type ContactEmailService = { get: () => Promise<string | null>; put: (email: string | null) => Promise<void> }

export const contactEmailService: ContactEmailService = {
  async get(): Promise<string | null> {
    return (await apiClient.get<{ email: string | null }>('account/contact-email')).email
  },
  put(email: string | null): Promise<void> { return apiClient.put('account/contact-email', { email }) },
}
