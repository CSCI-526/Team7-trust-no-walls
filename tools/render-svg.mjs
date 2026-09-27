// Renders one or more hand-written SVG files to PNG at 2x device scale using
// a headless Chromium (Playwright), so dark-theme diagrams in design/diagrams/
// come out crisp. Usage:
//   node tools/render-svg.mjs design/diagrams/game-loop.svg design/diagrams/level-generation.svg
//
// Requires: cd tools/web && npm i && npx playwright install chromium

import { chromium } from "./web/node_modules/playwright/index.mjs";
import fs from "node:fs";
import path from "node:path";

const files = process.argv.slice(2);
if (files.length === 0) {
  console.error("Usage: node tools/render-svg.mjs <file.svg> [more.svg ...]");
  process.exit(1);
}

function readDimension(svg, attr) {
  const m = svg.match(new RegExp(`${attr}="(\\d+(?:\\.\\d+)?)"`));
  if (!m) {
    throw new Error(`Could not find ${attr} attribute on the root <svg> element`);
  }
  return Math.ceil(parseFloat(m[1]));
}

const browser = await chromium.launch();
try {
  for (const file of files) {
    const abs = path.resolve(file);
    const svg = fs.readFileSync(abs, "utf8");
    const width = readDimension(svg, "width");
    const height = readDimension(svg, "height");

    const page = await browser.newPage({
      viewport: { width, height },
      deviceScaleFactor: 2,
    });

    const html = `<!doctype html>
<html>
  <head>
    <meta charset="utf-8" />
    <style>
      html, body { margin: 0; padding: 0; background: #0E1016; }
      svg { display: block; }
    </style>
  </head>
  <body>${svg}</body>
</html>`;

    await page.setContent(html, { waitUntil: "networkidle" });

    const out = abs.replace(/\.svg$/i, ".png");
    await page.screenshot({ path: out });
    await page.close();

    console.log(`Rendered ${path.relative(process.cwd(), abs)} -> ${path.relative(process.cwd(), out)} (${width * 2}x${height * 2})`);
  }
} finally {
  await browser.close();
}
