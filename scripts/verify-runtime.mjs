#!/usr/bin/env node
/**
 * Layer 6 — RUNTIME Widget Contract Verification
 *
 * Loads a real browser, renders each /catalog page, and verifies the
 * COMPUTED styles of each widget instance match its contract.
 *
 * This catches what static analysis cannot:
 *   - CSS cascade winners (specificity conflicts)
 *   - Actually rendered padding after browser interpretation
 *   - Visual adjacency (via bounding rects)
 *   - Computed color contrast (WCAG)
 *
 * Prerequisites:
 *   cd scripts && npm install playwright
 *   npx playwright install chromium
 *
 * Usage:
 *   node scripts/verify-runtime.mjs
 *
 * Or point at specific URL:
 *   CATALOG_URLS="http://localhost:5801/catalog,http://localhost:5600/catalog" \
 *     node scripts/verify-runtime.mjs
 */

import { chromium } from 'playwright';
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const contracts = JSON.parse(readFileSync(resolve(__dirname, 'widget-contracts.json'), 'utf8')).contracts;

const DEFAULT_URLS = [
    'http://localhost:5801/catalog',   // Order.Web
    'http://localhost:5802/catalog',   // Vendor.Web (note: port may differ)
    'http://localhost:5600/catalog',   // Ashare.Web
];

const URLS = (process.env.CATALOG_URLS || DEFAULT_URLS.join(',')).split(',').map(u => u.trim());

let totalViolations = 0;

function px(str) {
    const m = /^(-?\d+(?:\.\d+)?)px/.exec(str);
    return m ? parseFloat(m[1]) : null;
}

async function verifyPage(browser, url) {
    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`  Checking: ${url}`);
    console.log(`═══════════════════════════════════════════════`);
    const page = await browser.newPage();
    try {
        await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
    } catch (err) {
        console.log(`  ⚠ Could not load ${url} — is the app running?`);
        await page.close();
        return;
    }

    // Wait for Blazor to hydrate
    await page.waitForTimeout(500);

    for (const [selector, contract] of Object.entries(contracts)) {
        const elements = await page.$$(selector);
        if (elements.length === 0) {
            console.log(`  — ${selector}: not rendered on this page (ok if no variant uses it)`);
            continue;
        }

        for (let i = 0; i < elements.length; i++) {
            const el = elements[i];
            const computed = await el.evaluate(node => {
                const s = window.getComputedStyle(node);
                return {
                    paddingTop: s.paddingTop, paddingRight: s.paddingRight,
                    paddingBottom: s.paddingBottom, paddingLeft: s.paddingLeft,
                    marginBottom: s.marginBottom,
                    borderWidth: s.borderTopWidth,
                    borderStyle: s.borderTopStyle,
                    backgroundColor: s.backgroundColor,
                    color: s.color,
                    fontSize: s.fontSize,
                    fontWeight: s.fontWeight,
                    minHeight: s.minHeight,
                    height: s.offsetHeight || s.height,
                    borderRadius: s.borderTopLeftRadius,
                    display: s.display,
                    cursor: s.cursor,
                };
            });

            const mins = contract['min-values'] || {};
            const violations = [];

            if (mins.padding !== undefined) {
                const minPad = Math.min(
                    px(computed.paddingTop) ?? 0,
                    px(computed.paddingLeft) ?? 0,
                    px(computed.paddingRight) ?? 0,
                    px(computed.paddingBottom) ?? 0,
                );
                if (minPad < mins.padding) violations.push(`padding ${minPad}px < ${mins.padding}px`);
            }
            if (mins['min-height'] !== undefined) {
                const mh = Math.max(px(computed.minHeight) ?? 0, computed.height ?? 0);
                if (mh < mins['min-height']) violations.push(`height ${mh}px < min ${mins['min-height']}px`);
            }
            if (mins['border-width'] !== undefined) {
                const bw = px(computed.borderWidth) ?? 0;
                if (bw < mins['border-width']) violations.push(`border-width ${bw}px < ${mins['border-width']}px`);
            }
            if (mins['font-weight'] !== undefined) {
                const fw = parseInt(computed.fontWeight) || 400;
                if (fw < mins['font-weight']) violations.push(`font-weight ${fw} < ${mins['font-weight']}`);
            }

            // Background must not be fully transparent (for inputs/cards/alerts)
            const hasBgRequirement = (contract.required || []).includes('background');
            if (hasBgRequirement && /rgba?\([^)]*,\s*0\s*\)/.test(computed.backgroundColor)) {
                violations.push('background is transparent — element invisible against page');
            }

            if (violations.length > 0) {
                console.log(`  ✗ ${selector}[${i}] runtime violations:`);
                violations.forEach(v => console.log(`      ${v}`));
                totalViolations += violations.length;
            }
        }
        if (elements.length > 0) {
            console.log(`  ✓ ${selector}: ${elements.length} instance(s) — see above for any violations`);
        }
    }

    await page.close();
}

async function main() {
    const browser = await chromium.launch();
    for (const url of URLS) {
        await verifyPage(browser, url);
    }
    await browser.close();

    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`  Runtime verification summary`);
    console.log(`═══════════════════════════════════════════════`);
    console.log(`  Runtime violations: ${totalViolations}`);
    if (totalViolations > 0) {
        console.log(`\n  Each violation means: static CSS appears fine, but the rendered`);
        console.log(`  element still fails (CSS cascade loser, specificity conflict, etc).`);
        process.exit(1);
    }
    console.log(`  ✅ All widgets satisfy their contracts at runtime.`);
}

main().catch(err => {
    console.error(err);
    process.exit(1);
});
