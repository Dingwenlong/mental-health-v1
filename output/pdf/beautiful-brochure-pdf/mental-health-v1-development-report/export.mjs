import { chromium } from '../../../../apps/admin_web/node_modules/playwright/index.mjs';
import path from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const html = pathToFileURL(path.join(here, 'index.html')).href;
const output = path.resolve(here, '../../mental-health-v1-development-process-report.pdf');
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1200, height: 1600 } });
await page.goto(html, { waitUntil: 'networkidle' });
await page.emulateMedia({ media: 'print', colorScheme: 'light' });
await page.pdf({ path: output, format: 'A4', printBackground: true, preferCSSPageSize: true, margin: { top: '0', right: '0', bottom: '0', left: '0' } });
await browser.close();
console.log(output);
