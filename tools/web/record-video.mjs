// Records raw gameplay footage of the Trust No Wall WebGL build for Task 11's gameplay video.
//
// Usage (serve docs/ first):
//   python3 -m http.server 8768 --directory docs
//   node tools/web/record-video.mjs [--base=http://localhost:8768] [--seconds=120] [--out=video/raw] [--takes=3]
//
// Loads /?demo=1 (auto-starts after 1.5 s, dismisses intro cards after 1.5 s, autopilot plays),
// records the page with Playwright's built-in video capture at 1280x720, and logs every
// "Level N start" / "Level N complete" / "Death:" console message with its timestamp (ms since
// navigation) to a sidecar JSON file next to the video so the video can be edited into a short
// highlight reel with ffmpeg afterward. Runs up to --takes attempts and keeps the take that
// reaches the highest level (ties broken by more distinct levels started).
//
// Bounded: each take runs for exactly --seconds and the whole script exits after --takes takes.

import { chromium } from 'playwright';
import { mkdirSync, rmSync, renameSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const ROOT = resolve(HERE, '..', '..');

const args = Object.fromEntries(
  process.argv.slice(2).map((a) => {
    const [k, v] = a.replace(/^--/, '').split('=');
    return [k, v ?? true];
  }),
);
const BASE = args.base || 'http://localhost:8768';
const SECONDS = Number(args.seconds || 120);
const TAKES = Number(args.takes || 3);
const OUT_DIR = resolve(ROOT, args.out || 'video/raw');

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function recordTake(browser, takeIndex) {
  const takeDir = join(OUT_DIR, `take-${takeIndex}`);
  rmSync(takeDir, { recursive: true, force: true });
  mkdirSync(takeDir, { recursive: true });

  const events = [];
  let maxLevel = 0;
  const levelsSeen = new Set();
  let booted = false;
  const t0 = Date.now();

  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    recordVideo: { dir: takeDir, size: { width: 1280, height: 720 } },
  });
  const page = await context.newPage();
  page.on('console', (msg) => {
    const text = msg.text();
    if (text.includes('Trust No Wall booted')) booted = true;
    const t = Date.now() - t0;
    let m = /Level (\d+) start/.exec(text);
    if (m) {
      const lvl = Number(m[1]);
      maxLevel = Math.max(maxLevel, lvl);
      levelsSeen.add(lvl);
      events.push({ t, type: 'start', level: lvl, text });
    }
    m = /Level (\d+) complete/.exec(text);
    if (m) events.push({ t, type: 'complete', level: Number(m[1]), text });
    if (/^Death:/.test(text)) events.push({ t, type: 'death', text });
  });
  page.on('pageerror', (err) => events.push({ t: Date.now() - t0, type: 'pageerror', text: err.message }));

  await page.goto(`${BASE}/?demo=1`);

  // Bounded wait for boot (60 s max), then record for the requested duration from t=0
  // (recording started at context creation, so this just bounds our own wait loop).
  for (let i = 0; i < 240 && !booted; i++) await sleep(250);

  const recordStart = Date.now();
  while (Date.now() - recordStart < SECONDS * 1000) {
    await sleep(500);
  }

  await context.close();

  // Playwright names the video file with a generated hash; find it and give it a stable name.
  const files = readdirSync(takeDir).filter((f) => f.endsWith('.webm'));
  let videoPath = null;
  if (files.length) {
    videoPath = join(takeDir, 'take.webm');
    renameSync(join(takeDir, files[0]), videoPath);
  }

  const meta = { take: takeIndex, maxLevel, levelsStarted: levelsSeen.size, events, videoPath };
  writeFileSync(join(takeDir, 'events.json'), JSON.stringify(meta, null, 2));
  console.log(
    `take ${takeIndex}: maxLevel=${maxLevel} levelsStarted=${levelsSeen.size} events=${events.length} video=${videoPath}`,
  );
  return meta;
}

mkdirSync(OUT_DIR, { recursive: true });
const browser = await chromium.launch({ args: ['--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const takes = [];
try {
  for (let i = 1; i <= TAKES; i++) {
    console.log(`Recording take ${i}/${TAKES} (${SECONDS} s)...`);
    takes.push(await recordTake(browser, i));
  }
} finally {
  await browser.close();
}

takes.sort((a, b) => b.maxLevel - a.maxLevel || b.levelsStarted - a.levelsStarted);
const best = takes[0];
console.log(`Best take: take-${best.take} (maxLevel=${best.maxLevel})`);

const bestFile = join(OUT_DIR, 'best.json');
writeFileSync(bestFile, JSON.stringify(best, null, 2));
console.log(`Wrote ${bestFile}`);
if (!best.videoPath || !existsSync(best.videoPath)) {
  console.error('FAIL: best take has no video file');
  process.exit(1);
}
console.log(`Best video: ${best.videoPath}`);
