import { getUiCopy, type UiCopyKey } from "../../generated/uiCopy.generated";

export const auditLabelKeys = {
  ConsentGranted: "audit.consentGranted",
  ConsentWithdrawn: "audit.consentWithdrawn",
  RecordViewed: "audit.recordViewed",
  RiskReviewed: "audit.riskReviewed",
  FollowUpRescheduled: "audit.followUpRescheduled",
  DemoDataDeleted: "audit.demoDataDeleted",
} as const;

export function auditActionLabel(action: string): string {
  const key = auditLabelKeys[action as keyof typeof auditLabelKeys];
  return key ? getUiCopy(key as UiCopyKey) : action;
}
