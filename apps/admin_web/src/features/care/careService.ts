import { apiClient } from "../../api/client";
import { getUiCopy, type UiCopyKey } from "../../generated/uiCopy.generated";

export const c = (key: string): string => getUiCopy(key as UiCopyKey) ?? key;
export const dateText = (value: string | null): string =>
  value
    ? new Date(value).toLocaleString("zh-CN", { timeZone: "Asia/Shanghai" })
    : c("care.noValue");
export const today = (): string =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Shanghai" }).format(
    new Date(),
  );
export type Page<T> = {
  items: T[];
  total: number;
  pageNumber: number;
  pageSize: number;
};
export type CareTask = {
  id: string;
  kind: "CheckIn" | "Exercise";
  exerciseId: string | null;
  dueDate: string;
  status: string;
  feedback: string | null;
};
export type CarePlan = {
  id: string;
  followUpId: string;
  title: string;
  status: string;
  version: number;
  createdAt: string;
  tasks: CareTask[];
};
export type Exercise = {
  id: string;
  title: string;
  instruction: string;
  durationSeconds: number;
};
export type ClinicalRecord = {
  followUpId: string;
  followUpStatus: string;
  dueAt: string | null;
  sessionId: string;
  assessmentId: string;
  score: number;
  level: string;
  notice: string;
  reviews: { level: string; reason: string; reviewedAt: string }[];
};
export type SubjectView = {
  subjectId: string;
  sharingActive: boolean;
  records: Page<ClinicalRecord>;
  checkIns: {
    date: string;
    mood: number;
    sleepHours: number;
    note: string | null;
  }[];
  trends: {
    date: string;
    mood: number | null;
    sleepHours: number | null;
    exerciseCount: number;
  }[];
  plans: Page<CarePlan>;
};
export type ClinicalSubject = {
  subjectId: string;
  nextFollowUpAt: string | null;
  followUpCount: number;
};
export type Summary = {
  role: string;
  consultationCount: number;
  pendingFollowUps: number;
  overdueFollowUps: number;
  activePlans: number;
  completedPlanTasks: number;
  planTasks: number;
};
export type Consultation = {
  id: string;
  orderId: string | null;
  kind: string;
  channel: string;
  status: string;
  practitionerName: string | null;
  scheduledAt: string | null;
  completedAt: string | null;
  analysisStatus: string;
};
export type CareApi = Pick<typeof apiClient, "get" | "post" | "put">;
export const careApi: CareApi = apiClient;
