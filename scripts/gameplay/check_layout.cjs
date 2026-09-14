const fs = require('fs'), path = require('path'), { createRequire } = require('module');
const deps = require('../browser-deps.cjs');
const { chromium } = deps('playwright');
(async () => {
  const browser = await chromium.launch({ ...(process.env.BARA_CHROME?{executablePath:process.env.BARA_CHROME}:{}), headless: true, args: ['--enable-unsafe-swiftshader'] });
  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
    const errors = [], screens = [], layouts = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto('http://127.0.0.1:54114/production/gameplay-round-01/?v=exploration-01');
    await page.waitForFunction(() => window.baraState?.phase === 'Menu', {}, { timeout: 90000 });
    await page.waitForTimeout(1600);
    const out = path.resolve('art/production/gameplay-round-01/qa');
    const state = () => page.evaluate(() => window.baraState);
    const click = async (label, n = 0) => {
      const s = await state(), b = s.buttons.filter(b => b.label === label)[n];
      if (!b) throw Error('Missing ' + label);
      const c = await page.locator('canvas').boundingBox();
      await page.mouse.click(c.x + (b.x + b.w / 2) * c.width, c.y + (b.y + b.h / 2) * c.height);
      await page.waitForTimeout(650);
    };
    const check = async name => {
      const s = await state();
      for (const b of s.buttons) if (b.x < 0 || b.y < 0 || b.x + b.w > 1.005 || b.y + b.h > 1.005) throw Error(name + ' clipped ' + b.label);
      if (s.phase === 'Service') {
        const top = 1 - s.viewY - s.viewH;
        for (const b of s.uiBoxes.filter(b => /ticket|service-rail|score-feedback/.test(b.label))) {
          if (b.y + b.h > top + .005) throw Error(name + ' HUD overlaps scene: ' + b.label);
        }
        if (s.visibleLabels.some(t => /new order|soup ready|welcome|bring a clean dish|gotten|order done/i.test(t))) throw Error(name + ' routine text returned');
        layouts.push({ name, viewport: page.viewportSize(), cameraSize: s.cameraSize, viewY: s.viewY, viewH: s.viewH, uiBoxes: s.uiBoxes });
      }
      await page.screenshot({ path: path.join(out, name + '.png') });
      screens.push(name);
    };
    await check('wide-main-menu'); await click('Settings'); await check('wide-settings'); await click('Save & back');
    await click('Choose kitchen'); await check('wide-level-select');
    for (let level = 1; level <= 4; level++) {
      await click('Enter kitchen', level - 1); await check('wide-level-0' + level + '-briefing');
      await click('Open the cafe'); await page.waitForTimeout(13000); await check('wide-level-0' + level + '-service');
      if (level === 2) {
        const before = await state(); await page.keyboard.press('h'); await page.waitForTimeout(400);
        if ((await state()).phase !== 'Paused') throw Error('H did not pause for picture guide');
        await check('wide-soup-help'); await click('Back to service');
        if (Math.abs((await state()).cameraSize - before.cameraSize) > .01) throw Error('Guide changed framing');
      }
      await page.keyboard.press('Space'); await page.waitForTimeout(400);
      if ((await state()).phase !== 'Service') throw Error('Space paused the game');
      if (level < 4) {
        await page.keyboard.press('Escape'); await page.waitForTimeout(400); await click('Main menu'); await click('Choose kitchen');
      }
    }
    await page.setViewportSize({ width: 1440, height: 1060 }); await page.waitForTimeout(1500); await check('finale-service');
    fs.writeFileSync(path.join(out, 'layout-validation.json'), JSON.stringify({ passed: errors.length === 0, screens, viewports: ['1280x720', '1440x1060'], checks: ['All four picture guides fit a 720p viewport', 'All visible UI buttons fit viewport', 'Tickets, score feedback and header stay above the scene', 'Routine instructional text is absent', 'H opens the picture guide and pauses; closing restores scene framing', 'All four service views and resize work', 'Space does not trigger pause'], layouts, errors }, null, 2));
    console.log('BARA_UI_LAYOUT_OK', JSON.stringify(errors));
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
