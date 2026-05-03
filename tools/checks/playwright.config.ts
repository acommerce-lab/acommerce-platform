import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  retries: 0,
  use: {
    headless: true,
    viewport: { width: 1280, height: 800 },
    ignoreHTTPSErrors: true,
  },
  projects: [
    { name: 'desktop', use: { browserName: 'chromium' } },
    {
      name: 'mobile-pwa',
      use: {
        browserName: 'chromium',
        viewport: { width: 390, height: 844 },        // iPhone 14
        deviceScaleFactor: 3,
        userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15',
      },
    },
  ],
});
