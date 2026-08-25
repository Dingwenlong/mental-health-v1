// @vitest-environment jsdom

import { flushPromises, mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import AuditView from "../features/audit/AuditView.vue";
import type { AuditRecord, AuditService } from "../features/audit/auditService";

describe("audit view", () => {
  it("shows only the allowed audit fields", async () => {
    const record = {
      occurredAt: "2026-08-24T07:30:00Z",
      actorUserId: "operator-1",
      action: "DemoDataDeleted",
      resourceId: "subject-1",
      reason: "用户确认清除数据",
      messageText: "conversation-marker",
      mediaUrl: "https://private.example/media",
      secret: "private-secret",
    } as unknown as AuditRecord;
    const service: AuditService = {
      list: vi.fn().mockResolvedValue([record]),
    };

    const wrapper = mount(AuditView, { props: { service } });
    await flushPromises();

    expect(wrapper.text()).toContain("数据已清除");
    expect(wrapper.text()).not.toContain("演示");
    expect(wrapper.text()).toContain("operator-1");
    expect(wrapper.text()).toContain("subject-1");
    expect(wrapper.text()).toContain("用户确认清除数据");
    expect(wrapper.text()).not.toContain("conversation-marker");
    expect(wrapper.text()).not.toContain("private.example");
    expect(wrapper.text()).not.toContain("private-secret");
  });

  it("uses the shared empty copy when there are no records", async () => {
    const service: AuditService = {
      list: vi.fn().mockResolvedValue([]),
    };

    const wrapper = mount(AuditView, { props: { service } });
    await flushPromises();

    expect(wrapper.text()).toContain("没有记录");
  });
});
