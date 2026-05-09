// ─────────────────────────────────────────────────────────────────────────────
//  Playwright screenshots — الفحوصات الستّة على المستوى البصريّ.
//
//  المتطلّبات: docs/DOTNET-SETUP.md → "عشير القديم" section يَشرح تنزيل
//  Chrome-for-Testing داخل sandbox محظور. على جهازك المحلّيّ:
//
//    npm i -D @playwright/test
//    npx playwright install chromium
//    npx playwright test tools/checks/screenshots.spec.ts
//
//  الناتج: tools/checks/screenshots/*.png — صفحات الكيتس الـ 8 + شاشات
//  الـ legacy المتبقّية للمقارنة.
// ─────────────────────────────────────────────────────────────────────────────
import { test, expect } from '@playwright/test';

const BASE = process.env.EJAR_WEB_URL ?? 'http://localhost:5113';

const KIT_PAGES = [
  // route                      filename                 requires-auth
  { route: '/properties',       file: 'kit-listings-explore.png',     auth: false },
  { route: '/chat',             file: 'kit-chat-inbox.png',           auth: true  },
  { route: '/notifications',    file: 'kit-notifications-inbox.png',  auth: true  },
  { route: '/me',               file: 'kit-profile.png',              auth: true  },
  { route: '/plans',            file: 'kit-subscriptions-plans.png',  auth: false },
  { route: '/support',          file: 'kit-support-tickets.png',      auth: true  },
  { route: '/favorites',        file: 'kit-favorites.png',            auth: true  },
  { route: '/login',            file: 'kit-auth-login.png',           auth: false },
];

for (const p of KIT_PAGES) {
  test(`kit page renders: ${p.route}`, async ({ page }) => {
    if (p.auth) {
      // simple OTP: phone +966500000001 → mock SMS code 123456
      await page.goto(`${BASE}/login`);
      await page.fill('input[placeholder*="966"]', '+966500000001');
      await page.click('button:has-text("Request")');
      await page.fill('input[placeholder="123456"]', '123456');
      await page.click('button:has-text("Verify")');
      await page.waitForURL(/\/$|\/me/, { timeout: 5000 }).catch(() => {});
    }
    await page.goto(`${BASE}${p.route}`);
    // wait for the route to be rendered by KitPageRegistry (no @page)
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: `tools/checks/screenshots/${p.file}`, fullPage: true });
    // sanity: page is not 404 / not a blank exception
    await expect(page.locator('body')).not.toContainText('Page not found');
    await expect(page.locator('body')).not.toContainText('Unhandled exception');
  });
}

test('PWA shell loads', async ({ page }) => {
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: 'tools/checks/screenshots/pwa-home.png', fullPage: true });
});

test('legacy page parity (Favorites)', async ({ page }) => {
  // legacy /favorites still active alongside kit page; capture both
  await page.goto(`${BASE}/favorites`);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: 'tools/checks/screenshots/legacy-favorites.png', fullPage: true });
});
