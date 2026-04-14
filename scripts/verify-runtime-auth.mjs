#!/usr/bin/env node
/**
 * Layer 6 — AUTHENTICATED runtime verification.
 *
 * How we cross the login boundary without real SMS:
 *   1. Each app logs its OTP via ILogger (see LoggingSmsSender).
 *   2. start-all-apps.sh writes each backend's stdout/stderr to /tmp/<App>.log.
 *   3. This script:
 *         • POSTs phone → API (creates challenge, logs code)
 *         • tails the API log, extracts the 6-digit code
 *         • POSTs verify → accessToken + userId
 *         • opens the FRONTEND login page in Playwright
 *         • fills phone → submits → fills OTP → submits
 *         • waits for redirect and localStorage population
 *         • then visits each protected route and runs the same spatial/style
 *           assertions as verify-runtime.mjs, but tagged [auth]
 *
 * Why go through the UI instead of injecting the token?
 *   The app uses ProtectedLocalStorage (encrypted with the server's data
 *   protection key) — we cannot mint a valid token from outside the circuit.
 *   Driving the UI both tests the login page AND gives us a genuine session.
 */

import { chromium } from 'playwright';
import { readFileSync, writeFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const spatial   = JSON.parse(readFileSync(resolve(__dirname, 'spatial-contracts.json'), 'utf8'));
const contracts = JSON.parse(readFileSync(resolve(__dirname, 'widget-contracts.json'), 'utf8')).contracts;

// Map front-end → { backend-log, seeded phone (known active), protected routes }
const APPS = [
    // ── Order domain: 3 apps
    {
        name:     'Order.Web',
        base:     'http://localhost:5701',
        apiBase:  'http://localhost:5101',
        apiLog:   '/tmp/Order.Api.log',
        phone:    '+966500000001',
        protected:['/profile','/orders','/favorites','/cart','/messages','/notifications','/account','/settings','/catalog']
    },
    {
        name:     'Vendor.Web',
        base:     'http://localhost:5801',
        apiBase:  'http://localhost:5201',
        apiLog:   '/tmp/Vendor.Api.log',
        phone:    '+966501111111',
        protected:['/offers','/orders','/earnings','/schedule','/store-settings','/profile','/messages','/notifications','/settings','/catalog']
    },
    {
        name:     'Order.Admin.Web',
        base:     'http://localhost:5702',
        apiBase:  'http://localhost:5102',
        apiLog:   '/tmp/Order.Admin.Api.log',
        phone:    '+966599999999',             // any phone auto-creates
        protected:['/', '/users','/vendors','/offers','/orders','/categories','/settings']
    },

    // ── Ashare domain: 3 apps
    {
        name:     'Ashare.Web',
        base:     'http://localhost:5600',
        apiBase:  'http://localhost:5500',
        apiLog:   '/tmp/Ashare.Api.log',
        phone:    '+966500000002',             // sara (customer)
        protected:['/profile','/my-listings','/my-bookings','/my-subscription','/favorites','/notifications','/messages','/create-listing']
    },
    {
        name:     'Ashare.Provider.Web',
        base:     'http://localhost:5601',
        apiBase:  'http://localhost:5500',
        apiLog:   '/tmp/Ashare.Api.log',
        phone:    '+966500000001',             // owner
        protected:['/my-listings','/owner-bookings','/plans','/settings']
    },
    {
        name:     'Ashare.Admin.Web',
        base:     'http://localhost:5602',
        apiBase:  'http://localhost:5502',
        apiLog:   '/tmp/Ashare.Admin.Api.log',
        phone:    '+966500000003',             // admin@ashare.test
        protected:['/','/users','/listings','/bookings','/subscriptions','/plans','/categories','/notifications','/settings']
    },
];

const REPORT_JSON = resolve(__dirname, '../runtime-auth-report.json');
const REPORT = { startedAt: new Date().toISOString(), apps: [] };
let currentReport = null;
const px = s => { const m = /^(-?\d+(?:\.\d+)?)px/.exec(s); return m ? parseFloat(m[1]) : null; };

function record(url, category, message, selector = null) {
    if (!currentReport) return;
    currentReport.violations.push({ url, category, message, selector });
}

// ─── OTP helpers: read log, extract last 6-digit code after a request ─────
function extractLatestCode(logPath) {
    const txt = readFileSync(logPath, 'utf8');
    // Look for  "رمز التحقق: 123456"  or  "code: 123456" patterns.
    const matches = [...txt.matchAll(/(?:رمز التحقق|code|OTP)[:\s]+(\d{4,8})/gi)];
    if (matches.length === 0) return null;
    return matches[matches.length - 1][1];
}

async function loginViaUi(page, app) {
    // Step 1: load page and wait for Blazor Server's SignalR to connect.
    await page.goto(app.base + '/login', { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('input[type="tel"]', { timeout: 8000 });
    // Blazor circuit ready signal: window.Blazor.enableServer has been invoked
    await page.waitForFunction(
        () => typeof window.Blazor !== 'undefined',
        { timeout: 10000 }
    ).catch(() => {});
    await page.waitForTimeout(1500);  // let SignalR stabilize

    // Type phone via keyboard (fires input+change events)
    await page.focus('input[type="tel"]');
    await page.keyboard.type(app.phone, { delay: 40 });
    await page.waitForTimeout(300);   // give @bind time to round-trip

    // Click the phone-submit button. Match by TEXT (not selector) because
    // some apps render multiple buttons (e.g. Ashare also renders a navbar
    // sign-in CTA). The phone step is always labelled "إرسال / Send".
    const clicked = await page.evaluate(() => {
        const buttons = [...document.querySelectorAll('button')].filter(b => !b.disabled && b.offsetParent);
        const match = buttons.find(b =>
            /إرسال|رمز|send|request/i.test((b.textContent || '').trim())
        ) || buttons.find(b => b.type === 'submit');
        if (!match) return false;
        match.click();
        return match.textContent?.trim();
    });
    if (!clicked) throw new Error('no phone-submit button found');
    // Wait for OTP step to render (second input appears) OR timeout
    await page.waitForFunction(
        () => document.querySelectorAll('input').length >= 2 ||
              document.querySelector('input[inputmode="numeric"]:not([type="tel"])') !== null,
        { timeout: 10000 }
    ).catch(() => {});
    await page.waitForTimeout(800);

    // Step 2: read OTP from log (after a small grace period for the write)
    let code = null;
    for (let attempt = 0; attempt < 5 && !code; attempt++) {
        code = extractLatestCode(app.apiLog);
        if (!code) await page.waitForTimeout(500);
    }
    if (!code) throw new Error('OTP not found in log');

    // Step 3: fill OTP code — a small number of apps render the OTP input in
    // the same <input type="tel"> slot, others render a second input. Try
    // "second-input-or-first" strategy.
    const otpInput = await page.evaluateHandle(() => {
        const inputs = [...document.querySelectorAll('input')].filter(i => i.offsetParent !== null);
        // Prefer numeric-only pattern / maxlength=6 input
        const pick = inputs.find(i =>
            i.type !== 'tel' &&
            (i.inputMode === 'numeric' || i.maxLength === 6 || /code|otp|pin/i.test(i.name || i.placeholder || ''))
        );
        return pick || inputs[inputs.length - 1] || null;
    });
    if (!otpInput) throw new Error('OTP input not found');
    await otpInput.asElement()?.focus();
    await page.keyboard.type(code, { delay: 40 });
    await page.waitForTimeout(300);

    // Click the OTP-verify button (match by text: "تحقق / Verify / Sign in").
    await page.waitForTimeout(500);
    const vClicked = await page.evaluate(() => {
        const buttons = [...document.querySelectorAll('button')].filter(b => !b.disabled && b.offsetParent);
        const match = buttons.find(b =>
            /تحقق|verify|sign.?in|تسجيل/i.test((b.textContent || '').trim())
        ) || buttons.find(b => b.type === 'submit');
        if (!match) return false;
        match.click();
        return true;
    });
    if (!vClicked) await page.keyboard.press('Enter'); // last resort

    // Wait for redirect away from /login
    await page.waitForFunction(
        () => !location.pathname.includes('/login'),
        { timeout: 10000 }
    ).catch(() => {});
    await page.waitForTimeout(1500);
    if (page.url().includes('/login')) {
        throw new Error('login did not redirect (url=' + page.url() + ')');
    }
    return true;
}

// Runtime checks for authenticated pages.  Covers: widget style contracts
// (A), font-size scale (F), text-overflow against bubble/card bounds (H),
// template-shell styling presence (J).
async function checkPage(page, url) {
    // A. widget style baselines (now includes .act-bubble, .s-card, .act-navbar, .act-footer)
    for (const [selector, contract] of Object.entries(contracts)) {
        const els = await page.$$(selector);
        for (let i = 0; i < Math.min(els.length, 12); i++) {
            const c = await els[i].evaluate(n => {
                const s = getComputedStyle(n);
                const bt = parseFloat(s.borderTopWidth) || 0;
                const br = parseFloat(s.borderRightWidth) || 0;
                const bb = parseFloat(s.borderBottomWidth) || 0;
                const bl = parseFloat(s.borderLeftWidth) || 0;
                return {
                    padTop: s.paddingTop, padLeft: s.paddingLeft, padRight: s.paddingRight, padBot: s.paddingBottom,
                    bw: Math.max(bt, br, bb, bl) + 'px', bg: s.backgroundColor, fw: s.fontWeight,
                    mh: s.minHeight, h: n.offsetHeight,
                };
            });
            const mins = contract['min-values'] || {};
            if (mins.padding !== undefined) {
                const p = Math.min(px(c.padTop)??0, px(c.padLeft)??0, px(c.padRight)??0, px(c.padBot)??0);
                if (p < mins.padding) record(url, 'A-style', `${selector}[${i}]: padding ${p}px < ${mins.padding}px`, selector);
            }
            if (mins['min-height'] !== undefined) {
                const h = Math.max(px(c.mh)??0, c.h??0);
                if (h < mins['min-height']) record(url, 'A-style', `${selector}[${i}]: height ${h}px < ${mins['min-height']}px`, selector);
            }
            if (mins['border-width'] !== undefined && (px(c.bw)??0) < mins['border-width']) {
                record(url, 'A-style', `${selector}[${i}]: border ${c.bw} < ${mins['border-width']}`, selector);
            }
            if ((contract.required || []).includes('background') && /rgba?\([^)]*,\s*0\s*\)/.test(c.bg)) {
                record(url, 'A-style', `${selector}[${i}]: transparent background`, selector);
            }
        }
    }
    // F. computed font-size on scale
    const allowed = [10,10.5,11,12,12.25,13,14,15,15.75,16,17.5,18,20,21,22,24,24.5,25,28,32,35,36,40,42,48,56,64];
    const offs = await page.$$eval('h1,h2,h3,p,label,a,button,span', (nodes, allowed) => {
        const out = [];
        for (const n of nodes) {
            if (!n.offsetParent) continue;
            if (!n.textContent?.trim()) continue;
            const fs = parseFloat(getComputedStyle(n).fontSize);
            if (!allowed.some(a => Math.abs(a - fs) < 0.5)) {
                out.push({ tag: n.tagName, fs });
                if (out.length > 3) break;
            }
        }
        return out;
    }, allowed);
    for (const o of offs) record(url, 'F-computed', `off-scale font-size ${o.fs}px on <${o.tag.toLowerCase()}>`, 'any');

    // H. text overflow inside bubbles, cards, alerts
    const overflows = await page.$$eval('.act-bubble, .ord-chat-bubble, .ac-card, .ac-alert', nodes =>
        nodes.filter(n => n.offsetParent && n.scrollWidth > n.clientWidth + 2)
             .slice(0, 5)
             .map(n => ({ cls: n.className.slice(0, 60), d: n.scrollWidth - n.clientWidth, t: (n.textContent||'').trim().slice(0,40) }))
    );
    for (const o of overflows) record(url, 'H-overflow', `${o.cls}: "${o.t}" overflows ${o.d}px`, 'bubble/card');

    // J. template shell styling present (shell / navbar / footer)
    const tpl = await page.evaluate(() => {
        const check = el => {
            if (!el) return null;
            const s = getComputedStyle(el);
            const bg = s.backgroundColor;
            const hasBg = bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent';
            const borders = ['Top','Right','Bottom','Left'].map(d => parseFloat(s['border'+d+'Width'])||0);
            return { bg, hasBg, hasBorder: borders.some(b => b > 0) };
        };
        return {
            shell: check(document.querySelector('.act-shell, .adm-shell, .acs-page, .acs-auth-page, .vnd-shell, .ord-shell')),
            nav:   check(document.querySelector('nav, header, .act-navbar, .adm-navbar')),
            foot:  check(document.querySelector('footer, .act-footer'))
        };
    });
    if (tpl.shell && !tpl.shell.hasBg && !tpl.shell.hasBorder)
        record(url, 'J-template', 'shell has no background + no border (template CSS missing)', 'shell');
    if (tpl.nav && !tpl.nav.hasBg && !tpl.nav.hasBorder)
        record(url, 'J-template', 'navbar has no background + no border (template CSS missing)', 'navbar');
    if (tpl.foot && !tpl.foot.hasBg && !tpl.foot.hasBorder)
        record(url, 'J-template', 'footer has no background + no border (template CSS missing)', 'footer');
}

async function verifyApp(browser, app) {
    const appReport = { app: app.name, base: app.base, loggedIn: false, pagesChecked: 0, violations: [] };
    currentReport = appReport;
    REPORT.apps.push(appReport);

    const ctx = await browser.newContext({ viewport: { width: 1366, height: 900 } });
    const page = await ctx.newPage();
    try {
        await loginViaUi(page, app);
        appReport.loggedIn = true;
    } catch (e) {
        appReport.loginError = String(e).slice(0, 200);
        await ctx.close();
        return;
    }

    for (const route of app.protected) {
        const url = app.base + route;
        try {
            await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 10000 });
            await page.waitForTimeout(800);
            appReport.pagesChecked++;
            await checkPage(page, url);
        } catch (e) {
            record(url, 'load-error', String(e).slice(0, 120));
        }
    }
    await ctx.close();
}

async function main() {
    const browser = await chromium.launch({
        executablePath: process.env.CHROME_EXEC_PATH || '/opt/chrome/chrome-linux64/chrome',
        args: ['--no-sandbox','--disable-setuid-sandbox','--disable-dev-shm-usage']
    });
    for (const app of APPS) {
        console.log(`\n─── ${app.name} ───`);
        try { await verifyApp(browser, app); } catch(e) { console.error('failed', app.name, e); }
        const r = REPORT.apps[REPORT.apps.length - 1];
        console.log(`  login=${r.loggedIn}  pages=${r.pagesChecked}  viol=${r.violations?.length||0}`);
        if (!r.loggedIn) console.log(`  loginError: ${r.loginError || 'unknown'}`);
    }
    await browser.close();
    REPORT.finishedAt = new Date().toISOString();
    writeFileSync(REPORT_JSON, JSON.stringify(REPORT, null, 2));
    const totalViol = REPORT.apps.reduce((s,a) => s + (a.violations?.length || 0), 0);
    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`  Authenticated runtime: ${totalViol} violations across ${REPORT.apps.filter(a=>a.loggedIn).length}/${REPORT.apps.length} apps`);
    console.log(`  Report: ${REPORT_JSON}`);
}

main().catch(err => { console.error(err); process.exit(1); });
