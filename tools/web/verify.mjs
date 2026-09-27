// Browser verification for the Trust No Wall WebGL build.
//
// Usage (serve docs/ first):
//   python3 -m http.server 8766 --directory docs
//   node tools/web/verify.mjs [--base=http://localhost:8766] [--demo-seconds=120] [--skip-tour] [--skip-demo]
//
// Tour: title, level 1 with a few seconds of keyboard play, pause overlay, then F9 through
// levels 2..15 screenshotting each intro card and the level in play.
// Demo: loads /?demo=1 and lets the autopilot play, screenshotting every 6 s and right after
// each death / level complete event the game logs.
// Screenshots go to test-results/web/. Exits non-zero on console errors, page errors, or if the
// demo does not reach level 5.

import { chromium } from 'playwright';
import { mkdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(HERE, '..', '..', 'test-results', 'web');

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? true];
  }),
);
const BASE = args.base || 'http://localhost:8766';
const DEMO_SECONDS = Number(args['demo-seconds'] || 120);
const TOUR_LEVELS = Number(args['tour-levels'] || 15);

const errors = [];
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function openPage(browser, url, onLog) {
  const page = await browser.newPage({ viewport: { width: 1100, height: 700 } });
  let booted = false;
  page.on('console', (msg) => {
    const text = msg.text();
    if (text.includes('Trust No Wall booted')) booted = true;
    if (msg.type() === 'error') errors.push(`[console] ${url}: ${text}`);
    else if (/exception/i.test(text)) errors.push(`[exception] ${url}: ${text}`);
    if (onLog) onLog(text);
  });
  page.on('pageerror', (err) => errors.push(`[pageerror] ${url}: ${err.message}`));
  await page.goto(url);
  for (let i = 0; i < 240 && !booted; i++) await sleep(250);
  if (!booted) throw new Error(`Game did not boot within 60 s at ${url}`);
  await sleep(3500); // let the Unity splash finish
  return page;
}

async function shot(page, name) {
  const canvas = page.locator('#unity-canvas');
  await canvas.screenshot({ path: join(OUT, `${name}.png`) });
  console.log(`  shot ${name}.png`);
}

async function focus(page) {
  // Clicking the canvas gives Unity keyboard focus.
  await page.locator('#unity-canvas').click({ position: { x: 5, y: 5 } });
  await sleep(200);
}

async function tap(page, key, holdMs = 60) {
  await page.keyboard.down(key);
  await sleep(holdMs);
  await page.keyboard.up(key);
  await sleep(80);
}

async function tour(browser) {
  console.log('Tour');
  let deathSeen = null;
  const page = await openPage(browser, `${BASE}/`, (text) => {
    if (/^Death:/.test(text)) deathSeen = text;
  });
  await focus(page);
  await shot(page, 'tour-00-title');

  await tap(page, 'Space');
  await sleep(600);
  await shot(page, 'tour-01-level1');

  // A few seconds of real keyboard play.
  const moves = ['ArrowRight', 'ArrowUp', 'ArrowRight', 'ArrowUp', 'KeyD', 'KeyW'];
  for (const k of moves) await tap(page, k, 450);
  await shot(page, 'tour-02-level1-played');

  await tap(page, 'KeyP');
  await sleep(300);
  await shot(page, 'tour-03-paused');
  await tap(page, 'KeyP');
  await sleep(300);

  for (let level = 2; level <= TOUR_LEVELS; level++) {
    await tap(page, 'F9');
    await sleep(500);
    const tag = String(level).padStart(2, '0');
    await shot(page, `tour-L${tag}-a-intro`);
    await tap(page, 'Space');
    await sleep(2500);
    await shot(page, `tour-L${tag}-b-play`);
    if (level <= 9) {
      await sleep(900);
      await shot(page, `tour-L${tag}-c-play`);
    }
    if (level === 8) {
      // Stand still at Start: the shadow spawns there 5 s into the attempt and catches us.
      deathSeen = null;
      for (let i = 0; i < 100 && !deathSeen; i++) await sleep(100);
      if (!deathSeen) errors.push('[tour] the idle player was never caught by the shadow on level 8');
      else console.log(`  ${deathSeen}`);
      await sleep(300);
      await shot(page, 'tour-L08-d-death');
      await sleep(1500);
      await shot(page, 'tour-L08-e-restarted');
    }
  }
  await page.close();
}

async function demo(browser) {
  console.log(`Demo (${DEMO_SECONDS} s)`);
  let maxLevel = 1;
  const pending = [];
  let deaths = 0;
  let completes = 0;
  let page;
  const onLog = (text) => {
    let m = /Level (\d+) start/.exec(text);
    if (m) maxLevel = Math.max(maxLevel, Number(m[1]));
    if (/^Death:/.test(text) && deaths < 6) pending.push(`demo-death-${++deaths}`);
    m = /Level (\d+) complete/.exec(text);
    if (m && completes < 6) pending.push(`demo-complete-L${m[1]}`);
    if (m) completes++;
  };
  page = await openPage(browser, `${BASE}/?demo=1`, onLog);
  const start = Date.now();
  let next = 0;
  let idx = 0;
  while (Date.now() - start < DEMO_SECONDS * 1000) {
    while (pending.length) {
      const name = pending.shift();
      await sleep(250);
      await shot(page, name);
    }
    if (Date.now() - start >= next) {
      await shot(page, `demo-${String(idx++).padStart(2, '0')}`);
      next += 6000;
    }
    await sleep(100);
  }
  console.log(`  demo max level ${maxLevel}, deaths ${deaths}, completes ${completes}`);
  await page.close();
  return maxLevel;
}

rmSync(OUT, { recursive: true, force: true });
mkdirSync(OUT, { recursive: true });
const browser = await chromium.launch({ args: ['--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
let demoLevel = null;
try {
  if (!args['skip-tour']) await tour(browser);
  if (!args['skip-demo']) demoLevel = await demo(browser);
} finally {
  await browser.close();
}

let failed = false;
if (errors.length) {
  failed = true;
  console.log(`FAIL: ${errors.length} console errors / exceptions`);
  for (const e of errors.slice(0, 30)) console.log(`  ${e}`);
}
if (demoLevel !== null && demoLevel < 5) {
  failed = true;
  console.log(`FAIL: demo only reached level ${demoLevel} (need 5+)`);
}
console.log(failed ? 'verify: FAILED' : `verify: OK (screenshots in ${OUT})`);
process.exit(failed ? 1 : 0);
