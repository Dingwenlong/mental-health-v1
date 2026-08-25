// @vitest-environment jsdom

import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import ReviewForm from "../features/risk_cases/ReviewForm.vue";
import RiskQueueView from "../features/risk_cases/RiskQueueView.vue";
import RiskRuleVersionsView from "../features/risk_cases/RiskRuleVersionsView.vue";
import FollowUpEditor from "../features/follow_ups/FollowUpEditor.vue";
import FollowUpCalendarView from "../features/follow_ups/FollowUpCalendarView.vue";
import type { ClinicalNotification } from "../features/notifications/notificationConnection";
import type {
  FollowUpTask,
  RiskCase,
  RiskCaseService,
} from "../features/risk_cases/riskCaseService";
import type { RiskRuleService } from "../features/risk_cases/riskRuleService";

describe("risk review flow", () => {
  it("does not submit a reviewed level without a reason", async () => {
    const reviewRisk = vi.fn();
    const wrapper = mount(ReviewForm, {
      props: {
        caseId: "case-1",
        currentLevel: "L3",
        service: serviceWith({ reviewRisk }),
      },
    });

    await wrapper.get("[data-test=level]").setValue("L1");
    await wrapper.get("form").trigger("submit");

    expect(wrapper.text()).toContain("请填写调整原因");
    expect(reviewRisk).not.toHaveBeenCalled();
  });

  it("sends the doctor reason with the reviewed level", async () => {
    const reviewRisk = vi.fn().mockResolvedValue(undefined);
    const wrapper = mount(ReviewForm, {
      props: {
        caseId: "case-1",
        currentLevel: "L3",
        service: serviceWith({ reviewRisk }),
      },
    });

    await wrapper.get("[data-test=level]").setValue("L2");
    await wrapper
      .get("[data-test=reason]")
      .setValue("本次会谈中情况稳定，改为三天内回访");
    await wrapper.get("form").trigger("submit");

    expect(reviewRisk).toHaveBeenCalledWith("case-1", {
      reviewedLevel: "L2",
      reason: "本次会谈中情况稳定，改为三天内回访",
    });
  });

  it("keeps a follow-up action local until a reason is provided", async () => {
    const completeFollowUp = vi.fn();
    const wrapper = mount(FollowUpEditor, {
      props: {
        task: followUp,
        service: serviceWith({ completeFollowUp }),
      },
    });

    await wrapper.get("[data-test=action]").setValue("complete");
    await wrapper.get("form").trigger("submit");

    expect(wrapper.text()).toContain("请填写操作原因");
    expect(completeFollowUp).not.toHaveBeenCalled();
  });

  it("filters the observation queue and opens the selected case", async () => {
    const listCases = vi.fn().mockResolvedValue([riskCase]);
    const service = serviceWith({ listCases });
    const wrapper = mount(RiskQueueView, { props: { service } });
    await flushPromises();

    expect(wrapper.text()).toContain("用户 7f2a");
    expect(wrapper.text()).toContain("78");
    await wrapper.get("[data-test=assigned-to-me]").setValue(true);
    await flushPromises();
    expect(listCases).toHaveBeenLastCalledWith({ assignedToMe: true });

    await wrapper.get("[data-test=risk-case-row]").trigger("click");
    await flushPromises();
    expect(wrapper.text()).toContain("规则版本");
    expect(wrapper.text()).toContain("v1");
    expect(wrapper.text()).toContain("缺少：录像表情");
    expect(wrapper.text()).toContain("原始对话内容");
  });

  it("refreshes the observation queue after a clinical notification", async () => {
    let notify: ((notification: ClinicalNotification) => void) | undefined;
    const notifications = {
      subscribe: (listener: (notification: ClinicalNotification) => void) => {
        notify = listener;
        return () => undefined;
      },
    };
    const listCases = vi.fn().mockResolvedValue([]);
    mount(RiskQueueView, {
      props: { service: serviceWith({ listCases }), notifications },
    });
    await flushPromises();

    notify?.({
      kind: "risk-case",
      caseId: "case-1",
      currentLevel: "L3",
      status: "Open",
    });
    await flushPromises();

    expect(listCases).toHaveBeenCalledTimes(2);
  });

  it("shows a manual queue conflict in the follow-up calendar", async () => {
    const task = {
      ...followUp,
      status: "Proposed",
      dueAt: null,
      conflictCode: "NO_QUALIFIED_SLOT_BEFORE_SLA",
    };
    const wrapper = mount(FollowUpCalendarView, {
      props: {
        service: serviceWith({
          listFollowUps: vi.fn().mockResolvedValue([task]),
        }),
      },
    });
    await flushPromises();

    expect(wrapper.text()).toContain("需人工安排：时限内没有合适号源");
    expect(wrapper.text()).toContain("改期");
  });

  it("creates a future-only rule version with crisis rules kept on", async () => {
    const create = vi.fn().mockResolvedValue(undefined);
    const service: RiskRuleService = {
      list: vi.fn().mockResolvedValue([]),
      create,
      activate: vi.fn(),
    };
    const wrapper = mount(RiskRuleVersionsView, { props: { service } });
    await flushPromises();

    expect(wrapper.text()).toContain(
      "新设置只用于以后生成的结果，旧结果不会改变",
    );
    expect(
      wrapper.find("[data-test=crisis-enabled]").attributes("disabled"),
    ).toBeDefined();
    await wrapper.get("[data-test=rule-version]").setValue("v2");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(create).toHaveBeenCalledWith({
      version: "v2",
      scaleWeight: 0.45,
      textWeight: 0.25,
      audioWeight: 0.15,
      videoWeight: 0.05,
      trendWeight: 0.1,
      thresholds: [25, 50, 75],
      crisisRulesEnabled: true,
    });
  });
});

const followUp: FollowUpTask = {
  id: "follow-up-1",
  assessmentId: "assessment-1",
  status: "Scheduled",
  assigneeId: "doctor-1",
  availabilitySlotId: "slot-1",
  dueAt: "2026-08-25T06:30:00Z",
  deadline: "2026-08-25T08:00:00Z",
  conflictCode: null,
  completedAt: null,
  cancelledAt: null,
};

const riskCase: RiskCase = {
  id: "case-1",
  assessmentId: "assessment-1",
  sessionId: "session-1",
  subjectId: "7f2a0000-0000-0000-0000-000000000000",
  consultationKind: "AiVirtual",
  originalLevel: "L3",
  currentLevel: "L3",
  status: "Open",
  followUpTaskId: "follow-up-1",
  createdAt: "2026-08-24T06:00:00Z",
  assessment: {
    id: "assessment-1",
    sessionId: "session-1",
    transcriptRevision: 1,
    ruleSetVersion: "v1",
    score: 78,
    availableWeight: 0.95,
    confidence: 0.82,
    level: "L3",
    isCrisis: false,
    crisisRuleId: null,
    missing: ["Video"],
    evidence: [
      {
        code: "questionnaire_total",
        modality: "Scale",
        contribution: 35.1,
        sourceRange: "questionnaire",
        quality: 1,
      },
    ],
    createdAt: "2026-08-24T06:00:00Z",
    notice: "此结果不能替代诊断",
  },
  reviews: [],
  followUp,
};

function serviceWith(overrides: Partial<RiskCaseService>): RiskCaseService {
  return {
    listCases: vi.fn().mockResolvedValue([]),
    getCase: vi.fn(),
    reviewRisk: vi.fn(),
    listFollowUps: vi.fn().mockResolvedValue([]),
    rescheduleFollowUp: vi.fn(),
    reassignFollowUp: vi.fn(),
    completeFollowUp: vi.fn(),
    cancelFollowUp: vi.fn(),
    ...overrides,
  };
}
