import { apiClient } from "../../api/client";

export type RiskLevel = "L1" | "L2" | "L3" | "Crisis";

export type RiskQueueFilter = {
  level?: RiskLevel;
  status?: string;
  assignedToMe?: boolean;
};

export type AssessmentEvidence = {
  code: string;
  modality: string;
  contribution: number;
  sourceRange: string;
  quality: number;
};

export type RiskAssessment = {
  id: string;
  sessionId: string;
  transcriptRevision: number | null;
  ruleSetVersion: string;
  score: number;
  availableWeight: number;
  confidence: number;
  level: RiskLevel;
  isCrisis: boolean;
  crisisRuleId: string | null;
  missing: string[];
  evidence: AssessmentEvidence[];
  createdAt: string;
  notice: string;
};

export type FollowUpTask = {
  id: string;
  assessmentId: string;
  status: string;
  assigneeId: string | null;
  availabilitySlotId: string | null;
  dueAt: string | null;
  deadline: string | null;
  conflictCode: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
};

export type ClinicalReview = {
  id: string;
  assessmentId: string;
  reviewerId: string;
  reviewedLevel: RiskLevel;
  reason: string;
  reviewedAt: string;
};

export type RiskCase = {
  id: string;
  assessmentId: string;
  sessionId: string;
  subjectId: string;
  consultationKind: string;
  originalLevel: RiskLevel;
  currentLevel: RiskLevel;
  status: string;
  followUpTaskId: string | null;
  createdAt: string;
  assessment: RiskAssessment;
  reviews: ClinicalReview[];
  followUp: FollowUpTask | null;
};

export interface RiskCaseService {
  listCases(filter?: RiskQueueFilter): Promise<RiskCase[]>;
  getCase(caseId: string): Promise<RiskCase>;
  reviewRisk(
    caseId: string,
    input: { reviewedLevel: RiskLevel; reason: string },
  ): Promise<void>;
  listFollowUps(): Promise<FollowUpTask[]>;
  rescheduleFollowUp(
    taskId: string,
    availabilitySlotId: string,
    reason: string,
  ): Promise<FollowUpTask>;
  reassignFollowUp(
    taskId: string,
    availabilitySlotId: string,
    reason: string,
  ): Promise<FollowUpTask>;
  completeFollowUp(taskId: string, reason: string): Promise<FollowUpTask>;
  cancelFollowUp(taskId: string, reason: string): Promise<FollowUpTask>;
}

function queryFor(filter?: RiskQueueFilter): string {
  const query = new URLSearchParams();
  if (filter?.level) query.set("level", filter.level);
  if (filter?.status) query.set("status", filter.status);
  if (filter?.assignedToMe) query.set("assignedToMe", "true");
  const suffix = query.toString();
  return suffix ? `risk-cases?${suffix}` : "risk-cases";
}

export const riskCaseService: RiskCaseService = {
  listCases: (filter) => apiClient.get<RiskCase[]>(queryFor(filter)),
  getCase: (caseId) => apiClient.get<RiskCase>(`risk-cases/${caseId}`),
  reviewRisk: async (caseId, input) => {
    await apiClient.post(`risk-cases/${caseId}/reviews`, input);
  },
  listFollowUps: () => apiClient.get<FollowUpTask[]>("follow-ups"),
  rescheduleFollowUp: (taskId, availabilitySlotId, reason) =>
    apiClient.post<FollowUpTask>(`follow-ups/${taskId}/reschedule`, {
      availabilitySlotId,
      reason,
    }),
  reassignFollowUp: (taskId, availabilitySlotId, reason) =>
    apiClient.post<FollowUpTask>(`follow-ups/${taskId}/reassign`, {
      availabilitySlotId,
      reason,
    }),
  completeFollowUp: (taskId, reason) =>
    apiClient.post<FollowUpTask>(`follow-ups/${taskId}/complete`, { reason }),
  cancelFollowUp: (taskId, reason) =>
    apiClient.post<FollowUpTask>(`follow-ups/${taskId}/cancel`, { reason }),
};
