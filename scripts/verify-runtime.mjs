#!/usr/bin/env node
/**
 * Layer 6 — RUNTIME verification via Playwright.
 *
 * Five check families:
 *   A. STYLE contracts        — widgets satisfy computed-style baselines
 *   B. POSITION rules         — anchored/sticky elements are where they claim
 *   C. CONTAINMENT            — children stay inside parent bounds
 *   D. SIBLING ALIGNMENT      — flex/grid siblings on the same y
 *   E. NO-OVERLAP             — list siblings don't stack
 *   F. COMPUTED VALUES        — font-size on scale; border; touch-target
 *   G. CONTRAST               — WCAG AA 4.5:1 foreground vs background
 *
 * Usage:
 *   node scripts/verify-runtime.mjs                                 # default URL list
 *   TARGET_URLS="http://h/a,http://h/b" node scripts/verify-runtime.mjs
 *   REPORT_JSON=report.json node scripts/verify-runtime.mjs
 */

import { chromium } from 'playwright';
import { readFileSync, writeFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, resolve } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const contracts = JSON.parse(readFileSync(resolve(__dirname, 'widget-contracts.json'), 'utf8')).contracts;
const spatial = JSON.parse(readFileSync(resolve(__dirname, 'spatial-contracts.json'), 'utf8'));

// ─── URL list: either from env, or derived from per-app port map + common routes
const APP_BASES = {
    'Order.Web':           'http://localhost:5701',
    'Vendor.Web':          'http://localhost:5801',
    'Order.Admin.Web':     'http://localhost:5702',
    'Ashare.Web':          'http://localhost:5600',
    'Ashare.Provider.Web': 'http://localhost:5601',
    'Ashare.Admin.Web':    'http://localhost:5602',
};

const APP_ROUTES = {
    'Order.Web':           ['/', '/catalog', '/login', '/search', '/settings', '/cart', '/favorites', '/legal', '/orders', '/notifications', '/profile', '/messages'],
    'Vendor.Web':          ['/', '/catalog', '/login', '/offers', '/orders', '/earnings', '/schedule', '/store-settings', '/settings', '/profile', '/messages', '/notifications'],
    'Order.Admin.Web':     ['/', '/login', '/categories', '/offers', '/orders', '/settings', '/users', '/vendors'],
    'Ashare.Web':          ['/', '/catalog', '/login', '/favorites', '/my-listings', '/my-bookings', '/my-subscription', '/notifications', '/plans', '/profile', '/settings', '/messages', '/create-listing'],
    'Ashare.Provider.Web': ['/', '/login', '/my-listings', '/owner-bookings', '/plans', '/settings'],
    'Ashare.Admin.Web':    ['/', '/login', '/bookings', '/categories', '/listings', '/notifications', '/plans', '/settings', '/subscriptions', '/users'],
};

function buildDefaultUrls() {
    const out = [];
    for (const [app, base] of Object.entries(APP_BASES)) {
        for (const route of APP_ROUTES[app] || []) out.push(base + route);
    }
    return out;
}

const URLS = (process.env.TARGET_URLS || process.env.CATALOG_URLS || '')
    ? (process.env.TARGET_URLS || process.env.CATALOG_URLS).split(',').map(s => s.trim()).filter(Boolean)
    : buildDefaultUrls();

const REPORT_JSON = process.env.REPORT_JSON || resolve(__dirname, '../runtime-report.json');
const REPORT = { startedAt: new Date().toISOString(), urls: [], totalViolations: 0 };
let currentUrlReport = null;

const px = s => { const m = /^(-?\d+(?:\.\d+)?)px/.exec(s); return m ? parseFloat(m[1]) : null; };

function recordViolation(category, message, selector = null) {
    if (!currentUrlReport) return;
    currentUrlReport.violations.push({ category, message, selector });
    REPORT.totalViolations++;
}

// ═══════════════════════════════════════════════════════════════════════
// A. Widget style contracts (computed values of widgets)
// ═══════════════════════════════════════════════════════════════════════
async function verifyStyleContracts(page) {
    for (const [selector, contract] of Object.entries(contracts)) {
        const elements = await page.$$(selector);
        if (elements.length === 0) continue;
        for (let i = 0; i < Math.min(elements.length, 20); i++) {
            const computed = await elements[i].evaluate(node => {
                const s = window.getComputedStyle(node);
                // Take max of all four sides so border-bottom-only widgets
                // (like .ac-card-header) still satisfy the border-width check.
                const bt = parseFloat(s.borderTopWidth) || 0;
                const br = parseFloat(s.borderRightWidth) || 0;
                const bb = parseFloat(s.borderBottomWidth) || 0;
                const bl = parseFloat(s.borderLeftWidth) || 0;
                return {
                    paddingTop: s.paddingTop, paddingRight: s.paddingRight,
                    paddingBottom: s.paddingBottom, paddingLeft: s.paddingLeft,
                    borderWidth: Math.max(bt, br, bb, bl) + 'px', backgroundColor: s.backgroundColor,
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
            for (const msg of v) recordViolation('A-style', `${selector}[${i}]: ${msg}`, selector);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// B. Position rules
// ═══════════════════════════════════════════════════════════════════════
async function verifyPositionRules(page) {
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
                recordViolation('B-position', `${rule.selector}: bottom=${rect.bottom.toFixed(1)}, expected ≈${viewport.height}`, rule.selector);
            }
        } else if (rule.rule === 'sticky-top') {
            try {
                await page.evaluate(() => window.scrollTo(0, 300));
                await page.waitForTimeout(100);
                const r2 = await el.evaluate(n => n.getBoundingClientRect().top);
                await page.evaluate(() => window.scrollTo(0, 0));
                if (Math.abs(r2) > tol) {
                    recordViolation('B-position', `${rule.selector}: claimed sticky-top but top=${r2.toFixed(1)} after scroll`, rule.selector);
                }
            } catch {}
        } else if (rule.rule === 'attached-top-of-parent') {
            const pTop = await el.evaluate(n => {
                const p = n.offsetParent || n.parentElement;
                return p ? p.getBoundingClientRect().top : null;
            });
            if (pTop != null && rect.top > pTop + tol) {
                recordViolation('B-position', `${rule.selector}: element top=${rect.top}, parent top=${pTop}`, rule.selector);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// C. Containment
// ═══════════════════════════════════════════════════════════════════════
async function verifyContainment(page) {
    for (const rule of spatial.containment_rules) {
        const parents = await page.$$(rule.parent);
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
                    recordViolation('C-containment', `${rule.children} overflows ${rule.parent} horizontally`, rule.children);
                }
                if (axis === 'both' && overflowY) {
                    recordViolation('C-containment', `${rule.children} overflows ${rule.parent} vertically`, rule.children);
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// D. Sibling alignment
// ═══════════════════════════════════════════════════════════════════════
async function verifySiblingAlignment(page) {
    for (const rule of spatial.sibling_alignment_rules) {
        const containers = await page.$$(rule.container);
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
                recordViolation('D-alignment', `${rule.container}: ${bad}/${kids.length} children off vertical center (tolerance ${tol}px)`, rule.container);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// E. No-overlap
// ═══════════════════════════════════════════════════════════════════════
async function verifyNoOverlap(page) {
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
                recordViolation('E-overlap', `${sel}: ${overlaps} pair(s) of overlapping instances`, sel);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// F. Computed-value rules (font-size on scale, border presence, touch target)
// ═══════════════════════════════════════════════════════════════════════
async function verifyComputedValues(page) {
    for (const rule of spatial.computed_value_rules) {
        const maxV = rule.max_violations ?? 50;
        let count = 0;
        if (rule.rule === 'font-size-on-scale') {
            const allowed = rule.allowed_px;
            const tol = rule.tolerance_px ?? 0.5;
            const elements = await page.$$(rule.selector);
            for (const el of elements) {
                if (count >= maxV) break;
                const data = await el.evaluate(n => {
                    if (!n.offsetParent && n !== document.body) return null;
                    const text = (n.textContent || '').trim();
                    if (text.length === 0) return null;
                    const fs = window.getComputedStyle(n).fontSize;
                    const tag = n.tagName.toLowerCase();
                    return { fontSize: fs, tag };
                });
                if (!data) continue;
                const fs = px(data.fontSize);
                if (fs == null) continue;
                const onScale = allowed.some(a => Math.abs(a - fs) <= tol);
                if (!onScale) {
                    recordViolation('F-computed', `off-scale computed font-size ${fs}px on <${data.tag}> (allowed: ${allowed.slice(0, 8).join(',')}...)`, rule.selector);
                    count++;
                }
            }
        } else if (rule.rule === 'has-visible-border') {
            const elements = await page.$$(rule.selector);
            for (let i = 0; i < elements.length && count < maxV; i++) {
                const el = elements[i];
                const visible = await el.evaluate(n => n.offsetParent !== null);
                if (!visible) continue;
                const bw = await el.evaluate(n => window.getComputedStyle(n).borderTopWidth);
                if ((px(bw) ?? 0) < 0.5) {
                    recordViolation('F-computed', `${rule.selector}[${i}]: border-width=${bw} at runtime (input invisible)`, rule.selector);
                    count++;
                }
            }
        } else if (rule.rule === 'min-touch-target') {
            const elements = await page.$$(rule.selector);
            const minH = rule.min_height_px ?? 32;
            for (let i = 0; i < elements.length && count < maxV; i++) {
                const el = elements[i];
                const h = await el.evaluate(n => {
                    if (!n.offsetParent) return -1;
                    return n.getBoundingClientRect().height;
                });
                if (h >= 0 && h < minH) {
                    recordViolation('F-computed', `${rule.selector}[${i}]: height ${h.toFixed(1)}px < ${minH}px (too small to tap)`, rule.selector);
                    count++;
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// G. WCAG AA contrast
// ═══════════════════════════════════════════════════════════════════════
function parseRgb(str) {
    const m = /rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?/.exec(str);
    if (!m) return null;
    return { r: +m[1], g: +m[2], b: +m[3], a: m[4] != null ? +m[4] : 1 };
}
function luminance({ r, g, b }) {
    const c = [r, g, b].map(v => {
        v = v / 255;
        return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
}
function contrastRatio(fg, bg) {
    const L1 = luminance(fg), L2 = luminance(bg);
    const lo = Math.min(L1, L2), hi = Math.max(L1, L2);
    return (hi + 0.05) / (lo + 0.05);
}

async function verifyContrast(page) {
    for (const rule of spatial.contrast_rules) {
        const maxV = rule.max_violations ?? 10;
        let count = 0;
        const elements = await page.$$(rule.selector);
        for (const el of elements) {
            if (count >= maxV) break;
            const data = await el.evaluate(n => {
                if (!n.offsetParent && n !== document.body) return null;
                const text = (n.textContent || '').trim();
                if (text.length === 0) return null;
                const s = window.getComputedStyle(n);
                // Walk up the tree to find an actual (non-transparent) background
                let bg = s.backgroundColor, p = n.parentElement, hops = 0;
                while ((bg === 'rgba(0, 0, 0, 0)' || bg === 'transparent') && p && hops < 8) {
                    bg = window.getComputedStyle(p).backgroundColor;
                    p = p.parentElement; hops++;
                }
                return { color: s.color, bg, tag: n.tagName.toLowerCase(), text: text.slice(0, 30) };
            });
            if (!data) continue;
            const fg = parseRgb(data.color), bg = parseRgb(data.bg);
            if (!fg || !bg) continue;
            // If bg alpha is 0 or we can't resolve, assume white
            const bgFinal = bg.a < 0.5 ? { r: 255, g: 255, b: 255 } : bg;
            const ratio = contrastRatio(fg, bgFinal);
            if (ratio < rule.min_ratio) {
                recordViolation('G-contrast', `<${data.tag}> "${data.text}" ratio ${ratio.toFixed(2)} < ${rule.min_ratio} (fg=rgb(${fg.r},${fg.g},${fg.b}) bg=rgb(${bgFinal.r},${bgFinal.g},${bgFinal.b}))`, rule.selector);
                count++;
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// ═══════════════════════════════════════════════════════════════════════
// L. Box-model hygiene — border-box + symmetric padding guarantees a
// widget stays inside its frame regardless of parent width/flex behavior.
// ═══════════════════════════════════════════════════════════════════════
async function verifyBoxModel(page) {
    for (const rule of (spatial.box_model_rules || [])) {
        const max = rule.max_violations ?? 10;
        const els = await page.$$(rule.selector);
        let count = 0;
        if (rule.rule === 'box-sizing-border-box') {
            for (const el of els) {
                if (count >= max) break;
                const data = await el.evaluate(n => {
                    if (!n.offsetParent) return null;
                    return { boxSizing: getComputedStyle(n).boxSizing, cls: n.className.slice(0, 60) };
                });
                if (!data) continue;
                if (data.boxSizing !== 'border-box') {
                    recordViolation('L-box-model', `${rule.selector}: "${data.cls}" computed box-sizing=${data.boxSizing} (expected border-box) — padding may push element outside its frame`, rule.selector);
                    count++;
                }
            }
        } else if (rule.rule === 'symmetric-padding') {
            const minPad = rule.min_padding_px ?? 4;
            for (const el of els) {
                if (count >= max) break;
                const data = await el.evaluate(n => {
                    if (!n.offsetParent) return null;
                    const s = getComputedStyle(n);
                    return {
                        top: parseFloat(s.paddingTop) || 0,
                        right: parseFloat(s.paddingRight) || 0,
                        bottom: parseFloat(s.paddingBottom) || 0,
                        left: parseFloat(s.paddingLeft) || 0,
                        cls: n.className.slice(0, 60)
                    };
                });
                if (!data) continue;
                const sides = [data.top, data.right, data.bottom, data.left];
                const zeros = sides.filter(v => v < minPad).length;
                if (zeros > 0) {
                    recordViolation('L-box-model', `${rule.selector}: "${data.cls}" padding (${data.top}/${data.right}/${data.bottom}/${data.left}) — ${zeros} side(s) below ${minPad}px minimum`, rule.selector);
                    count++;
                }
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// H. Text overflow — scrollWidth > clientWidth means text bleeds out
// ═══════════════════════════════════════════════════════════════════════
async function verifyTextOverflow(page) {
    for (const rule of (spatial.text_overflow_rules || [])) {
        const max = rule.max_violations ?? 10;
        let count = 0;
        const elements = await page.$$(rule.selector);
        for (const el of elements) {
            if (count >= max) break;
            const info = await el.evaluate(n => {
                if (!n.offsetParent) return null;
                // Intentional scroll containers (overflow-x: auto/scroll) legitimately
                // have scrollWidth > clientWidth; they're carousels, rails, lists.
                const s = getComputedStyle(n);
                if (s.overflowX === 'auto' || s.overflowX === 'scroll') return null;
                const overflowX = n.scrollWidth - n.clientWidth;
                const overflowY = n.scrollHeight - n.clientHeight;
                const text = (n.textContent || '').trim().slice(0, 40);
                return { overflowX, overflowY, text, tag: n.tagName.toLowerCase() };
            });
            if (!info) continue;
            // Allow 2-pixel rounding slack; > 2px means real overflow
            if (info.overflowX > 2) {
                recordViolation('H-overflow', `${rule.selector}: "${info.text}" overflows ${info.overflowX}px horizontally`, rule.selector);
                count++;
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// I. Cluster atomicity — stat cards where icon/value/label lost their layout
// ═══════════════════════════════════════════════════════════════════════
async function verifyClusterAtomicity(page) {
    for (const rule of (spatial.cluster_atomicity_rules || [])) {
        const max = rule.max_violations ?? 5;
        let count = 0;
        const cards = await page.$$(rule.container);
        for (const c of cards) {
            if (count >= max) break;
            const h = await c.evaluate(n => {
                if (!n.offsetParent) return null;
                return n.getBoundingClientRect().height;
            });
            if (h && h > (rule.max_intra_card_height_px ?? 200)) {
                recordViolation('I-cluster', `${rule.container} is ${h.toFixed(0)}px tall (>${rule.max_intra_card_height_px}px) — children likely un-gridded`, rule.container);
                count++;
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// J. Template compliance — the top-level shell itself AND its navbar/footer
// must receive non-default template styling (background or border).  "Has
// children" is NOT enough: a shell with zero background + zero border is
// indistinguishable from raw page flow — a clear sign the template CSS
// failed to load or was not linked for this app.
// ═══════════════════════════════════════════════════════════════════════
async function verifyTemplateCompliance(page) {
    for (const rule of (spatial.template_compliance_rules || [])) {
        const approvedSelectors = rule.approved_shell_selectors || [];
        const result = await page.evaluate(selectors => {
            const check = el => {
                if (!el) return null;
                const s = getComputedStyle(el);
                const bg = s.backgroundColor;
                const hasBg = bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent';
                const borders = ['Top','Right','Bottom','Left'].map(d => parseFloat(s['border'+d+'Width'])||0);
                const hasBorder = borders.some(b => b > 0);
                return { bg, hasBg, hasBorder, borders };
            };
            // Shell itself
            let shell = null;
            for (const sel of selectors) {
                const el = document.querySelector(sel);
                if (el) { shell = { sel, ...check(el) }; break; }
            }
            if (!shell) return { noShell: true, fallback: document.body.children[0]?.tagName };
            // Navbar inside shell
            const navEl = document.querySelector('nav, header, .act-navbar, .adm-navbar, .ord-bottom-nav, .vnd-bottom-nav');
            const nav   = navEl ? { tag: navEl.tagName.toLowerCase(), cls: navEl.className, ...check(navEl) } : null;
            // Footer
            const footEl = document.querySelector('footer, .act-footer');
            const foot   = footEl ? { tag: footEl.tagName.toLowerCase(), ...check(footEl) } : null;
            return { shell, nav, foot };
        }, approvedSelectors);

        if (result.noShell) {
            recordViolation('J-template', `no top-level shell found matching ${approvedSelectors.join(' / ')} (body starts with <${result.fallback}>)`, 'shell');
            continue;
        }
        if (!result.shell.hasBg && !result.shell.hasBorder) {
            recordViolation('J-template', `${result.shell.sel}: shell has no background and no border (template CSS likely not loaded)`, result.shell.sel);
        }
        if (result.nav && !result.nav.hasBg && !result.nav.hasBorder) {
            recordViolation('J-template', `<${result.nav.tag} class="${result.nav.cls}">: navbar has no background and no border — template CSS missing`, 'navbar');
        }
        if (result.foot && !result.foot.hasBg && !result.foot.hasBorder) {
            recordViolation('J-template', `<${result.foot.tag}>: footer has no background and no border — template CSS missing`, 'footer');
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// W. Icon-only buttons must not expose default OS/browser button styling.
//    A <button> that contains only an SVG (icon button) must have:
//      – borderTopWidth == 0   (no visible border)
//      – backgroundColor == transparent  (no baked-in background)
//      – appearance == "none" or "auto"  (no OS 3-D bevel)
//    This catches the "Windows-98 button" problem where the developer
//    set CSS classes but forgot -webkit-appearance: none / border reset.
// ═══════════════════════════════════════════════════════════════════════
async function verifyIconButtons(page) {
    const violations = await page.evaluate(() => {
        const results = [];
        const buttons = document.querySelectorAll('button');
        for (const btn of buttons) {
            // Skip framework-styled buttons — they have intentional borders/backgrounds
            // ac-btn / btn = widget library buttons (ghost, outline, etc.)
            if (btn.classList.contains('ac-btn') || btn.classList.contains('btn')) continue;
            // Skip form controls that deliberately look like inputs
            if (btn.classList.contains('ac-citypicker-trigger') ||
                btn.classList.contains('ac-sort-select') ||
                btn.classList.contains('ac-viewtoggle-btn')) continue;

            // Only check bare icon buttons: contain at least one SVG and no visible text
            if (!btn.querySelector('svg')) continue;
            const visibleText = (btn.textContent || '').replace(/\s/g, '');
            if (visibleText.length > 0) continue;

            const s = getComputedStyle(btn);
            const borderW = parseFloat(s.borderTopWidth) || 0;
            const bg = s.backgroundColor;
            const isTransparentBg = bg === 'rgba(0, 0, 0, 0)' || bg === 'transparent';
            const app = s.appearance || s.webkitAppearance || '';
            const hasNativeBevel = app === 'button' || app === 'auto';

            if (borderW > 0) {
                results.push({ issue: `border-top-width: ${borderW}px (visible border)`, cls: btn.className });
            }
            if (!isTransparentBg) {
                // Allow brand colours — only flag gray/white which signal un-reset defaults
                const isGrayish = /rgb\((\d+), \1, \1\)|rgb\(2[0-4]\d|rgb\(25[0-5]/.test(bg);
                if (isGrayish) results.push({ issue: `background: ${bg} (default browser bg)`, cls: btn.className });
            }
            if (hasNativeBevel && borderW > 0) {
                results.push({ issue: `appearance: ${app} with visible border — OS bevel`, cls: btn.className });
            }
        }
        return results;
    });
    for (const v of violations) {
        recordViolation('W-interactive', `Icon button .${v.cls}: ${v.issue}`, 'button svg');
    }
}

// ═══════════════════════════════════════════════════════════════════════
// X. Brand palette enforcement — interactive elements must not use
//    off-brand background colors. Catches un-reset OS default button
//    backgrounds (gray, ButtonFace) that appear without brand CSS.
//
//    Approved backgrounds: transparent, brand teal (#345454), brand
//    orange (#F4844C), brand peach (#FEE8D6), white, ac-surface tokens.
//    Gray (e.g. rgb(N,N,N) with near-equal channels) = OS default = violation.
// ═══════════════════════════════════════════════════════════════════════
async function verifyBrandBackgrounds(page) {
    const violations = await page.evaluate(() => {
        const results = [];
        const candidates = document.querySelectorAll('button, .ac-btn, .ac-card, nav, header');
        for (const el of candidates) {
            const s = getComputedStyle(el);
            const bg = s.backgroundColor;
            if (!bg || bg === 'rgba(0, 0, 0, 0)' || bg === 'transparent') continue;
            // Parse rgb(r,g,b) or rgba(r,g,b,a)
            const m = bg.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
            if (!m) continue;
            const [r, g, b] = [+m[1], +m[2], +m[3]];
            // Convert to hex for brand check
            const hex = '#' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('').toUpperCase();
            // Allowed brand colors (exact and near-match ±10)
            const brandColors = [
                [0x34, 0x54, 0x54],   // #345454 teal
                [0x26, 0x3F, 0x3F],   // #263F3F dark teal
                [0x3E, 0x5A, 0x56],   // #3E5A56 SVG teal
                [0xF4, 0x84, 0x4C],   // #F4844C orange
                [0xFE, 0xE8, 0xD6],   // #FEE8D6 peach
                [0xFF, 0xFF, 0xFF],   // white
                [0xF8, 0xF7, 0xF5],   // off-white surface
                [0x1C, 0x19, 0x17],   // near-black
                [0xE5, 0xE2, 0xDF],   // border color
            ];
            const near = brandColors.some(([br, bg2, bb]) =>
                Math.abs(r - br) <= 15 && Math.abs(g - bg2) <= 15 && Math.abs(b - bb) <= 15
            );
            if (near) continue;
            // Flag achromatic grays that signal un-reset OS defaults
            const maxDiff = Math.max(Math.abs(r - g), Math.abs(g - b), Math.abs(r - b));
            if (maxDiff <= 20 && r >= 150 && r <= 230) {
                results.push({ el: el.tagName + (el.className ? '.' + el.className.trim().split(/\s+/)[0] : ''), hex });
            }
        }
        return results;
    });
    for (const v of violations) {
        recordViolation('X-brand-bg', `${v.el} has off-brand achromatic background ${v.hex} (likely un-reset OS default)`, v.el);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// Y. Spacing scale enforcement — padding/margin on key structural
//    elements must be on the 4-point grid (multiple of 4px).
//    Catches one-off magic numbers that break visual rhythm.
// ═══════════════════════════════════════════════════════════════════════
async function verifySpacingScale(page) {
    const violations = await page.evaluate(() => {
        const results = [];
        const targets = document.querySelectorAll('.ac-btn, .ac-card-body, .ac-card-header, nav, .acm-mobile-nav-inner, .ac-topnav');
        for (const el of targets) {
            const s = getComputedStyle(el);
            const props = ['paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft'];
            for (const prop of props) {
                const val = parseFloat(s[prop]);
                if (!val || val === 0) continue;
                // Must be a multiple of 4 (tolerance ±0.5 for sub-pixel)
                const mod = val % 4;
                if (mod > 0.5 && mod < 3.5) {
                    results.push({ el: el.tagName + (el.className ? '.' + el.className.trim().split(/\s+/)[0] : ''), prop, val });
                }
            }
        }
        return results;
    });
    const max = 5;
    let count = 0;
    for (const v of violations) {
        if (count++ >= max) break;
        recordViolation('Y-spacing-scale', `${v.el} ${v.prop}: ${v.val}px is not on the 4-point grid`, v.el);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// K. UI-only operations must not fire /api/ requests when toggled
// ═══════════════════════════════════════════════════════════════════════
async function verifyUiOnlyOps(page, url) {
    for (const rule of (spatial.ui_only_ops_rules || [])) {
        // Only run on /settings pages — that's where language/theme toggles live
        if (!/\/settings(\?|$)/.test(url)) continue;
        // Locate a candidate toggle (button with Arabic/English labels inside)
        const toggle = await page.evaluateHandle(() => {
            const all = [...document.querySelectorAll('button')];
            return all.find(b => {
                const t = (b.textContent || '').trim();
                return /English|عربي|عربية|ar|en/i.test(t) && t.length < 20;
            }) || null;
        });
        const el = toggle.asElement();
        if (!el) continue;
        // Install a request sniffer for /api/* and click
        const captured = [];
        const handler = req => { if (req.url().includes('/api/')) captured.push(req.url()); };
        page.on('request', handler);
        try {
            await el.click();
            await page.waitForTimeout(800);
        } catch {}
        page.off('request', handler);
        if (captured.length > 0) {
            recordViolation('K-ui-op-routed', `language/theme toggle on ${url} fired ${captured.length} /api/* request(s); first: ${captured[0]}`, 'settings-lang-btn');
        }
    }
}

async function verifyUrl(browser, url) {
    currentUrlReport = { url, status: 'loaded', violations: [] };
    REPORT.urls.push(currentUrlReport);

    const [vw, vh] = (process.env.VIEWPORT || '1366x900').split('x').map(n => parseInt(n, 10) || 0);
    const page = await browser.newPage({ viewport: { width: vw || 1366, height: vh || 900 } });
    try {
        await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.waitForTimeout(800);
    } catch (e) {
        currentUrlReport.status = 'unreachable';
        await page.close();
        return;
    }
    try {
        await verifyStyleContracts(page);
        await verifyPositionRules(page);
        await verifyContainment(page);
        await verifySiblingAlignment(page);
        await verifyNoOverlap(page);
        await verifyComputedValues(page);
        await verifyContrast(page);
        await verifyTextOverflow(page);
        await verifyClusterAtomicity(page);
        await verifyTemplateCompliance(page);
        await verifyBoxModel(page);
        await verifyIconButtons(page);
        await verifyBrandBackgrounds(page);
        await verifySpacingScale(page);
        await verifyUiOnlyOps(page, url);
    } catch (e) {
        currentUrlReport.error = String(e).slice(0, 200);
    }
    await page.close();
}

async function main() {
    const execPath = process.env.CHROME_EXEC_PATH || '/opt/chrome/chrome-linux64/chrome';
    const browser = await chromium.launch({
        executablePath: execPath,
        args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage']
    });
    process.stdout.write(`Checking ${URLS.length} URLs...\n`);
    let done = 0;
    for (const url of URLS) {
        await verifyUrl(browser, url);
        done++;
        const last = REPORT.urls[REPORT.urls.length - 1];
        process.stdout.write(`[${done}/${URLS.length}] ${last.status.padEnd(12)} ${last.violations?.length ?? 0} viol  ${url}\n`);
    }
    await browser.close();

    REPORT.finishedAt = new Date().toISOString();
    writeFileSync(REPORT_JSON, JSON.stringify(REPORT, null, 2));

    // Print summary per category
    console.log(`\n═══════════════════════════════════════════════`);
    console.log(`   Runtime summary`);
    console.log(`═══════════════════════════════════════════════`);
    const byCat = {};
    for (const u of REPORT.urls) for (const v of (u.violations || [])) {
        byCat[v.category] = (byCat[v.category] || 0) + 1;
    }
    for (const [cat, n] of Object.entries(byCat).sort()) console.log(`   ${cat}: ${n}`);
    console.log(`   URLs reachable: ${REPORT.urls.filter(u => u.status === 'loaded').length}/${REPORT.urls.length}`);
    console.log(`   Total violations: ${REPORT.totalViolations}`);
    console.log(`   Report: ${REPORT_JSON}`);
    // Exit 0 always — runtime is report mode, issues are triaged after
}

main().catch(err => { console.error(err); process.exit(1); });
