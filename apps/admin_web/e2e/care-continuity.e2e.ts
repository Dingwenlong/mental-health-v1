import { expect, test } from "@playwright/test";

test("doctor publishes and reads back a user care plan with live permission checks", async ({
  page,
  request,
}) => {
  const web = process.env.CARE_WEB_URL;
  const api = process.env.CARE_API_URL;
  const doctor = process.env.CARE_DOCTOR_TOKEN;
  const user = process.env.CARE_USER_TOKEN;
  const operations = process.env.CARE_OPERATIONS_TOKEN;
  const phase = process.env.CARE_PHASE ?? "publish";
  const subject = "10000000-0000-0000-0000-000000000001";
  expect(
    web && api && doctor && user && operations,
    "dedicated local test settings are required",
  ).toBeTruthy();
  expect(new URL(web!).hostname).toBe("127.0.0.1");
  expect(new URL(api!).hostname).toBe("127.0.0.1");
  const headers = (token: string) => ({ Authorization: `Bearer ${token}` });
  const url = (path: string) => new URL(path, api!).toString();
  const profile = await request.get(url(`clinical/subjects/${subject}`), {
    headers: headers(doctor!),
  });
  expect(profile.ok()).toBeTruthy();
  expect((await profile.json()).sharingActive).toBe(true);
  await page.addInitScript(
    (token) => sessionStorage.setItem("mh_access_token", token),
    doctor!,
  );
  await page.goto(web!);
  await expect(
    page
      .getByRole("navigation")
      .getByRole("button", { name: "回访用户", exact: true }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("navigation")
      .getByRole("button", { name: "服务目录", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("navigation")
    .getByRole("button", { name: "回访用户", exact: true })
    .click();
  await page.locator('[data-test="open-subject"]').first().click();
  await expect(page.locator('[data-test="shared-records"]')).toContainText(
    "合成日常记录：今天散步十分钟。",
  );
  if (phase === "publish") {
    await page.getByRole("button", { name: "制定计划", exact: true }).click();
    await page.locator('[data-test="plan-title"]').fill("本周跟进安排");
    await page.getByRole("button", { name: "增加任务", exact: true }).click();
    const exercise = page.locator(".care-task-form").nth(1);
    await exercise.locator("select").first().selectOption("Exercise");
    await exercise.locator("select").nth(1).selectOption("grounding");
    await page.getByRole("button", { name: "保存", exact: true }).click();
    await expect(
      page.getByRole("button", { name: "发布计划", exact: true }),
    ).toBeVisible();
    const drafts = await request.get(url("care-plans"), {
      headers: headers(user!),
    });
    expect((await drafts.json()).items).toHaveLength(0);
    await page.getByRole("button", { name: "发布计划", exact: true }).click();
    await expect(
      page.getByRole("button", { name: "发布计划", exact: true }),
    ).toHaveCount(0);
    const plans = await request.get(url("care-plans"), {
      headers: headers(user!),
    });
    const active = (await plans.json()).items[0];
    expect(active.status).toBe("Active");
    expect(active.tasks).toHaveLength(2);
  } else {
    expect(phase).toBe("readback");
    await expect(
      page.getByText("合成任务反馈：已完成记录。", { exact: true }),
    ).toBeVisible();
    await expect(
      page.getByText("合成任务反馈：今天先休息。", { exact: true }),
    ).toBeVisible();
    const grants = await request.get(url("me/sharing-grants"), {
      headers: headers(user!),
    });
    const grant = (await grants.json()).items.find(
      (item: { active: boolean }) => item.active,
    );
    expect(grant).toBeTruthy();
    const revoked = await request.delete(url(`me/sharing-grants/${grant.id}`), {
      headers: headers(user!),
    });
    expect(revoked.status()).toBe(204);
    await page.locator('[data-test="refresh-care"]').click();
    await expect(page.locator('[data-test="sharing-status"]')).toBeVisible();
    await expect(page.locator('[data-test="shared-records"]')).toHaveCount(0);
    await expect(
      page.getByText("合成任务反馈：已完成记录。", { exact: true }),
    ).toBeVisible();
    const after = await request.get(url(`clinical/subjects/${subject}`), {
      headers: headers(doctor!),
    });
    expect((await after.json()).checkIns).toHaveLength(0);
  }
  for (const path of [
    `clinical/subjects/${subject}`,
    "me/check-ins",
    "me/trends",
    "me/exercise-completions",
    "care-plans",
  ]) {
    const denied = await request.get(url(path), {
      headers: headers(operations!),
    });
    expect(denied.status(), path).toBe(403);
  }
  const summary = await request.get(url("workspace/summary"), {
    headers: headers(operations!),
  });
  expect(summary.ok()).toBeTruthy();
  expect(await summary.text()).not.toMatch(/合成|feedback|subjectId|score/);
  await page.evaluate(() => window.scrollTo(0, 0));
  if (process.env.CARE_ARTIFACTS)
    await page.screenshot({
      path: `${process.env.CARE_ARTIFACTS}/${phase}.png`,
      fullPage: true,
    });
});
