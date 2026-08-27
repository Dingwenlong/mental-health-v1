import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.e2e.ts",
  workers: 1,
  timeout: 60_000,
  reporter: "line",
  outputDir: process.env.CARE_ARTIFACTS ?? "output/playwright/care",
  use: {
    browserName: "chromium",
    headless: true,
    screenshot: "only-on-failure",
    trace: "off",
  },
});
