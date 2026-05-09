import { chromium } from '/tmp/shot-deps/node_modules/playwright/index.mjs';

const URL = process.env.URL || 'http://localhost:5114';
const OUT = process.env.OUT || '/home/user/acommerce-platform/tools/checks/screenshots';

const PAGES = [
  { path: '/',              name: '01-home' },
  { path: '/dashboard',     name: '02-dashboard' },
  { path: '/properties',    name: '03-properties' },
  { path: '/notifications', name: '04-notifications' },
  { path: '/chat',          name: '05-chat' },
  { path: '/login',         name: '06-login' },
];

const browser = await chromium.launch({
  executablePath: '/opt/browsers/chrome-linux64/chrome',
  args: ['--no-sandbox', '--disable-setuid-sandbox'],
});
const ctx = await browser.newContext({
  viewport: { width: 414, height: 896 },     // mobile (iPhone 11)
  deviceScaleFactor: 2,
});
const page = await ctx.newPage();

for (const p of PAGES) {
  try {
    await page.goto(URL + p.path, { waitUntil: 'networkidle', timeout: 15000 });
    await page.waitForTimeout(800);          // settle Blazor interactive
    await page.screenshot({ path: `${OUT}/v2-${p.name}.png`, fullPage: true });
    const title = await page.title();
    console.log(`✓ ${p.path.padEnd(20)} → v2-${p.name}.png  (${title})`);
  } catch (e) {
    console.log(`✗ ${p.path.padEnd(20)} ${e.message.slice(0,80)}`);
  }
}

await browser.close();
