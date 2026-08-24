import { apiClient } from "../../api/client";

export interface AuditRecord {
  occurredAt: string;
  actorUserId: string;
  action: string;
  resourceId: string;
  reason: string | null;
}

export interface AuditService {
  list(): Promise<AuditRecord[]>;
}

export const auditService: AuditService = {
  list: () => apiClient.get<AuditRecord[]>("data-rights/audit?limit=100"),
};
