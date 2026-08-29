// @vitest-environment jsdom
import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import CareWorkspace from "../features/care/CareWorkspace.vue";
import CarePlanEditor from "../features/care/CarePlanEditor.vue";
import ConsultationList from "../features/care/ConsultationList.vue";
import TrendChart from "../components/TrendChart.vue";
import { menusForRoles } from "../features/care/roleMenu";
import type { CareApi, SubjectView } from "../features/care/careService";

const page = <T>(items: T[]) => ({
  items,
  total: items.length,
  pageNumber: 1,
  pageSize: 10,
});
const subject: SubjectView = {
  subjectId: "subject-1",
  sharingActive: true,
  records: page([]),
  checkIns: [
    { date: "2026-08-27", mood: 3, sleepHours: 7, note: "用户主动分享的记录" },
  ],
  trends: [{ date: "2026-08-27", mood: 3, sleepHours: 7, exerciseCount: 0 }],
  plans: page([]),
};
function service(get: (path: string) => Promise<unknown>): CareApi {
  return {
    get: vi.fn(get) as CareApi["get"],
    post: vi.fn(async () => ({})) as CareApi["post"],
    put: vi.fn(async () => ({})) as CareApi["put"],
  };
}

describe("care workspace", () => {
  it("breaks the trend line across missing dates", () => {
    const wrapper = mount(TrendChart, {
      props: {
        title: "心情变化",
        valueKey: "mood",
        maxValue: 5,
        days: [
          { date: "2026-08-26", mood: 3 },
          { date: "2026-08-27", mood: null },
          { date: "2026-08-28", mood: 4 },
          { date: "2026-08-29", mood: 4 },
        ],
      },
    });
    expect(wrapper.findAll("path.trend-line")).toHaveLength(2);
    expect(wrapper.get("svg").attributes("aria-label")).toBe("心情变化");
  });
  it("keeps clinical menus out of operations and consultation menus out of doctors", () => {
    expect(
      menusForRoles(["OperationsAdmin"]).map((item) => item.view),
    ).not.toContain("subjects");
    expect(
      menusForRoles(["OperationsAdmin"]).map((item) => item.view),
    ).not.toContain("plans");
    expect(menusForRoles(["Doctor"]).map((item) => item.view)).not.toContain(
      "consultations",
    );
    expect(menusForRoles(["Counselor"]).map((item) => item.view)).not.toContain(
      "subjects",
    );
    expect(menusForRoles(["User"]).map((item) => item.view)).toEqual([
      "account",
    ]);
  });
  it("removes previously shared notes after revocation and refresh", async () => {
    let allowed = true;
    const api = service(async (path) =>
      path.startsWith("clinical/subjects?")
        ? page([
            { subjectId: "subject-1", nextFollowUpAt: null, followUpCount: 1 },
          ])
        : {
            ...subject,
            sharingActive: allowed,
            checkIns: allowed ? subject.checkIns : [],
            trends: allowed ? subject.trends : [],
          },
    );
    const wrapper = mount(CareWorkspace, {
      props: { mode: "subjects", service: api },
    });
    await flushPromises();
    await wrapper.get("[data-test=open-subject]").trigger("click");
    await flushPromises();
    expect(wrapper.text()).toContain("用户主动分享的记录");
    expect(wrapper.findAll("figure.trend-chart")).toHaveLength(2);
    allowed = false;
    await wrapper.get("[data-test=refresh-care]").trigger("click");
    await flushPromises();
    expect(wrapper.find("[data-test=shared-records]").exists()).toBe(false);
    expect(wrapper.findAll("figure.trend-chart")).toHaveLength(0);
    expect(wrapper.text()).not.toContain("用户主动分享的记录");
    expect(wrapper.text()).toContain("日常资料未获授权");
  });
  it("clears clinical content on a failed refresh rather than leaving a stale private view", async () => {
    let fail = false;
    const api = service(async (path) => {
      if (fail) throw Error("offline");
      return path.startsWith("clinical/subjects?")
        ? page([
            { subjectId: "subject-1", nextFollowUpAt: null, followUpCount: 1 },
          ])
        : subject;
    });
    const wrapper = mount(CareWorkspace, {
      props: { mode: "subjects", service: api },
    });
    await flushPromises();
    await wrapper.get("[data-test=open-subject]").trigger("click");
    await flushPromises();
    fail = true;
    await wrapper.get("[data-test=refresh-care]").trigger("click");
    await flushPromises();
    expect(wrapper.text()).not.toContain("用户主动分享的记录");
    expect(wrapper.text()).toContain("没能加载，请重试");
  });
  it("renders an empty list and recovers after retry", async () => {
    let fail = true;
    const api = service(async () => {
      if (fail) throw Error("offline");
      return page([]);
    });
    const wrapper = mount(CareWorkspace, {
      props: { mode: "subjects", service: api },
    });
    await flushPromises();
    expect(wrapper.text()).toContain("没能加载");
    fail = false;
    await wrapper.get("[data-test=refresh-care]").trigger("click");
    await flushPromises();
    expect(wrapper.text()).toContain("还没有分配给你的回访");
  });
  it("saves a draft with an idempotency key without publishing it", async () => {
    const api = service(async () => []);
    const wrapper = mount(CarePlanEditor, {
      props: { followUpId: "follow-1", service: api },
    });
    await flushPromises();
    await wrapper.get("[data-test=plan-title]").setValue("散步与记录");
    await wrapper.get("form").trigger("submit");
    await flushPromises();
    expect(api.post).toHaveBeenCalledWith(
      "care-plans",
      expect.objectContaining({
        followUpId: "follow-1",
        title: "散步与记录",
        idempotencyKey: expect.any(String),
        tasks: [expect.objectContaining({ kind: "CheckIn", exerciseId: null })],
      }),
    );
    expect(api.post).toHaveBeenCalledTimes(1);
    expect(wrapper.emitted("saved")).toHaveLength(1);
  });
  it("opens the selected report directly from its consultation row", async () => {
    const session = {
      id: "session-7",
      orderId: "order-7",
      kind: "Human",
      channel: "Chat",
      status: "Completed",
      scheduledAt: null,
      completedAt: null,
      practitionerName: "咨询师",
      analysisStatus: "Completed",
    };
    const api = service(async () => page([session]));
    const wrapper = mount(ConsultationList, { props: { service: api } });
    await flushPromises();
    const button = wrapper
      .findAll("button")
      .find((item) => item.text() === "查看报告")!;
    await button.trigger("click");
    expect(wrapper.emitted("open")).toEqual([[session, "analysisJobs"]]);
    expect(wrapper.find("input[data-testid=session-id]").exists()).toBe(false);
  });
});
