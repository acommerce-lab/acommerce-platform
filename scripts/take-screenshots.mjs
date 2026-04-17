import { chromium } from 'playwright';
import { mkdirSync } from 'fs';
const [,,outDir,label,...urls] = process.argv;
mkdirSync(outDir, { recursive: true });
const b = await chromium.launch({ executablePath: '/opt/chrome/chrome-linux64/chrome', args:['--no-sandbox','--disable-setuid-sandbox','--disable-dev-shm-usage']});
for (const url of urls) {
    const p = await b.newPage({viewport:{width:1366,height:900}});
    try {
        await p.goto(url, {waitUntil:'domcontentloaded', timeout:15000});
        await p.waitForTimeout(1200);
        const slug = url.replace(/https?:\/\/localhost:/,'').replace(/[^a-z0-9]+/gi,'_');
        const path = `${outDir}/${label}-${slug}.png`;
        await p.screenshot({ path, fullPage: false });
        console.log(`saved ${path}`);
    } catch(e) { console.error('fail', url, String(e).slice(0,80)); }
    await p.close();
}
await b.close();
