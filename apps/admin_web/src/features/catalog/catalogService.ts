import { apiClient } from '../../api/client'

export type ServicePlan = {
  id: string
  name: string
  kind: 'Human' | 'Ai'
  channel: 'Chat' | 'Video'
  paymentMode: 'Free' | 'DemoPaid'
  priceInMinorUnits: number
  currency: string
  durationMinutes: number
  active: boolean
}

export type ServicePlanInput = Omit<ServicePlan, 'id' | 'active'>

export type AvailabilitySlot = {
  id: string
  practitionerId: string
  startAt: string
  endAt: string
  active: boolean
}

export type Practitioner = {
  id: string
  displayName: string
  role: 'Counselor' | 'Doctor'
  active: boolean
  availabilitySlots: AvailabilitySlot[]
}

export interface CatalogService {
  listPlans(): Promise<ServicePlan[]>
  createPlan(input: ServicePlanInput): Promise<ServicePlan>
  deactivatePlan?(id: string): Promise<void>
}

export interface PractitionerService {
  listPractitioners(): Promise<Practitioner[]>
  createPractitioner(input: { displayName: string; role: string }): Promise<Practitioner>
  deactivatePractitioner?(id: string): Promise<void>
}

export interface AvailabilityService {
  listPractitioners(): Promise<Practitioner[]>
  createSlot(practitionerId: string, input: { startAt: string; endAt: string }): Promise<AvailabilitySlot>
  deactivateSlot?(practitionerId: string, slotId: string): Promise<void>
}

export const catalogService: CatalogService = {
  listPlans: () => apiClient.get<ServicePlan[]>('catalog/plans'),
  createPlan: (input) => apiClient.post<ServicePlan>('admin/catalog/plans', input),
  deactivatePlan: (id) => apiClient.delete(`admin/catalog/plans/${id}`),
}

export const practitionerService: PractitionerService = {
  listPractitioners: () => apiClient.get<Practitioner[]>('catalog/practitioners'),
  createPractitioner: (input) =>
    apiClient.post<Practitioner>('admin/catalog/practitioners', input),
  deactivatePractitioner: (id) =>
    apiClient.delete(`admin/catalog/practitioners/${id}`),
}

export const availabilityService: AvailabilityService = {
  listPractitioners: () => apiClient.get<Practitioner[]>('catalog/practitioners'),
  createSlot: (practitionerId, input) =>
    apiClient.post<AvailabilitySlot>(
      `admin/catalog/practitioners/${practitionerId}/slots`,
      input,
    ),
  deactivateSlot: (practitionerId, slotId) =>
    apiClient.delete(`admin/catalog/practitioners/${practitionerId}/slots/${slotId}`),
}
