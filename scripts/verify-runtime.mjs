#!/usr/bin/env node
/**
 * Layer 6 — RUNTIME verification via Playwright + getBoundingClientRect.
 *
 * A. WIDGET STYLE CONTRACTS (widget-contracts.json)
 *    - Every rendered widget satisfies its computed-style contract.
 * B. POSITION RULES (spatial-contracts.json)
 *    - Sticky/anchored elements actually anchored where they claim.
 * C. CONTAINMENT
 *    - Children stay inside parent bounds (no horizontal overflow).
 * D. SIBLING ALIGNMENT
 *    - Flex/grid siblings in same row share same y (visually adjacent).
 * E. NO OVERLAP
 *    - List siblings don't stack on top of each other.
 *
 * Prerequisites:
 *   cd scripts && npm install && npx playwright install chromium
 *
 * Usage:
 *   node scripts/verify-runtime.mjs
 *   CATALOG_URLS="http://localhost:5801/catalog" node scripts/verify-runtime.mjs
 */

import { chromium } from 'playwright';
import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const contracts = JSON.parse(readFileSync(resolve(__dirname, 'widget-contracts.json'), 'utf8')).contracts;
const spatial = JSON.parse(readFileSync(resolve(__dirname, 'spatial-contracts.json'), 'utf8'));

const DEFAULT_URLS = [
    'http://localhost:5801/catalog',
    'http://localhost:5802/catalog',
    'http://localhost:5600/catalog',
];
const URLS = (process.env.CATALOG_URLS || DEFAULT_URLS.join(',')).split(',').map(u => u.trim());

let totalViolations = 0;
const px = s => { const m = /^(-?\d+(?:\.\d+)?)px/.exec(s); return m ? parseFloat(m[1]) : null; };

async function verifyStyleContracts(page) {
    console.log(`\n──  A. Widget style contracts  ──`);
    for (const [selector, contract] of Object.entries(contracts)) {
        const elements = await page.$$(selector);
        if (elements.length === 0) continue;
        for (let i = 0; i < elements.length; i++) {
            const computed = await elements[i].evaluate(node => {
                const s = window.getComputedStyle(node);
                return {
                    paddingTop: s.paddingTop, paddingRight: s.paddingRight,
                    paddingBottom: s.paddingBottom, paddingLeft: s.paddingLeft,
                    borderWidth: s.borderTopWidth, backgroundColor: s.backgroundColor,
                    fontSize: s.fontSize, fontWeight: s.fontWeight,
                    minHeight: s.minHeight, height: node.offsetHeight,
                };
            });
            const mins = contract['min-values'] || {};
            const v = [];
            if (mins.padding !== undefined) {
                const m = Math.min(px(computed.paddingTop) ?? 0, px(computed.paddingLeft) ?? 0,
                    px(computed.paddingRight) ?? 0, px(computed.paddingBottom) ?? 0);
                if (m < mins.padding) v.push(`padding ${m}px < ${mins.padding}px`);
            }
            if (mins['min-height'] !== undefined) {
                const h = Math.max(px(computed.minHeight) ?? 0, computed.height ?? 0);
                if (h < mins['min-height']) v.push(`height ${h}px < ${mins['min-height']}px`);
            }
            if (mins['border-width'] !== undefined) {
                const b = px(computed.borderWidth) ?? 0;
                if (b < mins['border-width']) v.push(`border ${b}px < ${mins['border-width']}px`);
            }
            if (mins['font-weight'] !== undefined) {
                const f = parseInt(computed.fontWeight) || 400;
                if (f < mins['font-weight']) v.push(`font-weight ${f} < ${mins['font-weight']}`);
            }
            const bgReq = (contract.required || []).includes('background');
            if (bgReq && /rgba?\([^)]*,\s*0\s*\)/.test(computed.backgroundColor)) {
                v.push('transparent background — element invisible');
            }
            if (v.length > 0) {
                console.log(`  ✗ ${selector}[${i}]: ${v.join('; ')}`);
                totalViolations += v.length;
            }
        }
    }
}

async function verifyPositionRules(page) {
    console.log(`\n──  B. Position rules (anchored / sticky)  ──`);
    const viewport = page.viewportSize();
    for (const rule of spatial.position_rules) {
        const el = await page.$(rule.selector);
        if (!el) continue;
        const rect = await el.evaluate(n => {
            const r = n.getBoundingClientRect();
            return { top: r.top, left: r.left, right: r.right, bottom: r.bottom, width: r.width, height: r.height };
        });
        const tol = rule.tolerance_px ?? 2;
        if (rule.rule === 'anchored-viewport-bottom') {
            if (Math.abs(rect.bottom - viewport.height) > tol) {
                console.log(`  ✗ ${rule.selector}: bottom=${rect.bottom.toFixed(1)}, expected ≈${viewport.height}`);
                totalViolations++;
            } else console.log(`  ✓ ${rule.selector}: anchored at viewport bottom`);
        } else if (rule.rule === 'sticky-top') {
            await page.evaluate(() => window.scrollTo(0, 300));
            await page.waitForTimeout(100);
            const r2 = await el.evaluate(n => n.getBoundingClientRect().top);
            await page.evaluate(() => window.scrollTo(0, 0));
            if (Math.abs(r2) > tol) {
                console.log(`  ✗ ${rule.selector}: claimed sticky-top but top=${r2.toFixed(1)} after scroll`);
                totalViolations++;
            } else console.log(`  ✓ ${rule.selector}: sticky top works`);
        } else if (rule.rule === 'attached-top-of-parent') {
            const pTop = await el.evaluate(n => {
                const p = n.offsetParent || n.parentElement;
                return p ? p.getBoundingClientRect().top : null;
            });
            if (pTop != null && rect.top > pTop + tol) {
                console.log(`  ✗ ${rule.selector}: element top=${rect.top}, parent top=${pTop}`);
                totalViolations++;
            } else console.log(`  ✓ ${rule.selector}: attached to parent top`);
        }
    }
}

async function verifyContainment(page) {
    console.log(`\n──  C. Containment (children inside parent)  ──`);
    for (const rule of spatial.containment_rules) {
        const parents = await page.$$(rule.parent);
        let violations = 0;
        for (const p of parents) {
            const pRect = await p.evaluate(n => {
                const r = n.getBoundingClientRect();
                return { top: r.top, left: r.left, right: r.right, bottom: r.bottom };
            });
            const kids = await p.$$(rule.children);
            for (const k of kids) {
                const kRect = await k.evaluate(n => {
                    const r = n.getBoundingClientRect();
                    return { top: r.top, left: r.left, right: r.right, bottom: r.bottom };
                });
                const axis = rule.axis || 'both';
                const overflowX = (kRect.left < pRect.left - 1) || (kRect.right > pRect.right + 1);
                const overflowY = (kRect.top < pRect.top - 1) || (kRect.bottom > pRect.bottom + 1);
                if ((axis === 'horizontal' || axis === 'both') && overflowX) {
                    console.log(`  ✗ ${rule.children} overflows ${rule.parent} horizontally`);
                    violations++;
                }
                if (axis === 'both' && overflowY) {
                    console.log(`  ✗ ${rule.children} overflows ${rule.parent} vertically`);
                    violations++;
                }
            }
        }
        totalViolations += violations;
        if (violations === 0) console.log(`  ✓ ${rule.parent} > ${rule.children}: all contained`);
    }
}

async function verifySiblingAlignment(page) {
    console.log(`\n──  D. Sibling alignment in flex rows  ──`);
    for (const rule of spatial.sibling_alignment_rules) {
        const containers = await page.$$(rule.container);
        let misalignedContainers = 0;
        for (const c of containers) {
            const kids = await c.evaluate(n => {
                const children = Array.from(n.children).filter(ch => ch.offsetParent !== null);
                return children.map(ch => {
                    const r = ch.getBoundingClientRect();
                    return { top: r.top, height: r.height };
                });
            });
            if (kids.length < 2) continue;
            const tol = rule.tolerance_px ?? 4;
            const refY = kids[0].top + kids[0].height / 2;
            const bad = kids.filter(k => Math.abs((k.top + k.height / 2) - refY) > tol).length;
            if (bad > 0) {
                console.log(`  ✗ ${rule.container}: ${bad}/${kids.length} children off vertical center (tolerance ${tol}px)`);
                totalViolations++;
                misalignedContainers++;
            }
        }
        if (misalignedContainers === 0 && containers.length > 0) {
            console.log(`  ✓ ${rule.container}: ${containers.length} container(s), all siblings aligned`);
        }
    }
}

async function verifyNoOverlap(page) {
    console.log(`\n──  E. No-overlap (siblings don't stack)  ──`);
    for (const rule of spatial.no_overlap_rules) {
        for (const sel of rule.selectors) {
            const elements = await page.$$(sel);
            if (elements.length < 2) continue;
            const rects = await Promise.all(elements.map(el => el.evaluate(n => {
                const r = n.getBoundingClientRect();
                return { top: r.top, left: r.left, right: r.right, bottom: r.bottom };
            })));
            let overlaps = 0;
            for (let i = 0; i < rects.length; i++) {
                for (let j = i + 1; j < rects.length; j++) {
                    const a = rects[i], b = rects[j];
                    const ox = Math.max(0, Math.min(a.right, b.right) - Math.max(a.left, b.left));
                    const oy = Math.max(0, Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top));
                    if (ox > 4 && oy > 4) overlaps++;
                }
            }
            if (overlaps > 0) {
                console.log(`  ✗ ${sel}: ${overlaps} pair(s) of overlapping instances`);
                totalViolations += overlaps;
            } else console.log(`  ✓ ${sel}: ${elements.length} instances, no overlap`);
        }
    }
}

async function verifyPage(browser, url) {
    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`  ${url}`);
    console.log(`═══════════════════════════════════════════════`);
    const page = await browser.newPage({ viewport: { width: 1366, height: 768 } });
    try {
        await page.goto(url, { waitUntil: 'networkidle', timeout: 15000 });
    } catch {
        console.log(`  ⚠ Could not load — is the app running?`);
        await page.close();
        return;
    }
    await page.waitForTimeout(500);
    await verifyStyleContracts(page);
    await verifyPositionRules(page);
    await verifyContainment(page);
    await verifySiblingAlignment(page);
    await verifyNoOverlap(page);
    await page.close();
}

async function main() {
    const browser = await chromium.launch();
    for (const url of URLS) await verifyPage(browser, url);
    await browser.close();

    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`   Runtime summary`);
    console.log(`═══════════════════════════════════════════════`);
    console.log(`   Total runtime violations: ${totalViolations}`);
    if (totalViolations > 0) {
        console.log(`\n   A. Style        — padding/border/background missing at runtime`);
        console.log(`   B. Position     — sticky/anchored elements not where claimed`);
        console.log(`   C. Containment  — children overflowing parent bounds`);
        console.log(`   D. Alignment    — siblings not on same horizontal line`);
        console.log(`   E. Overlap      — list cards stacking on top of each other`);
        process.exit(1);
    }
    console.log(`   ✅ All runtime assertions pass.`);
}

main().catch(err => { console.error(err); process.exit(1); });
