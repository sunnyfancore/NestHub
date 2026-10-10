const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
let playwright;
try { playwright = require(process.env.NESTHUB_BROWSER_TEST_MODULE || 'playwright'); } catch {}
const root = path.join(__dirname, '../nesthub');
const output = path.join(__dirname, '../../publish/plugin-preview');
async function browser() {
  return playwright.chromium.launch({ headless: true, ...(process.env.NESTHUB_BROWSER_EXECUTABLE ? { executablePath: process.env.NESTHUB_BROWSER_EXECUTABLE } : {}) });
}

test('floating button is visible before scroll on transformed pages, stays fixed and can be dragged', { skip: !playwright }, async () => {
  const app = await browser();
  try {
    const page = await app.newPage({ viewport: { width: 1280, height: 800 } });
    await page.setContent('<style>html{transform:translateY(900px)}body{height:6000px;background:#f6f7fa}</style><h1>测试网页</h1>');
    await page.evaluate(() => {
      globalThis.messages = [];
      globalThis.chrome = { runtime: {
        sendMessage: async message => { messages.push(message); return message.type === 'floating-state' ? { enabled: true } : { ok: true }; },
        onMessage: { addListener() {}, removeListener() {} }
      } };
    });
    await page.addScriptTag({ path: path.join(root, 'floating.js') });
    const button = page.locator('[data-nesthub-floating]');
    await button.waitFor({ state: 'visible' });
    await page.waitForFunction(() => document.querySelector('[data-nesthub-floating]').matches(':popover-open'));
    const initial = await button.boundingBox();
    assert.ok(initial.y >= 0 && initial.y + initial.height < 800, 'button must be in the first viewport');
    assert.equal(initial.width, 36);
    assert.equal(initial.x + initial.width, 1280);
    await page.evaluate(() => scrollTo(0, 1400));
    const scrolled = await button.boundingBox();
    assert.ok(Math.abs(scrolled.y - initial.y) < 1, 'page scroll must not move the button');
    await page.mouse.click(scrolled.x + 18, scrolled.y + 18);
    assert.ok(await page.evaluate(() => messages.some(message => message.type === 'floating-open')));
    await page.mouse.move(scrolled.x + 18, scrolled.y + 18);
    await page.mouse.down();
    await page.mouse.move(30, 180, { steps: 8 });
    await page.mouse.up();
    await page.waitForFunction(() => messages.some(message => message.type === 'floating-position'));
    assert.equal(await button.getAttribute('data-side'), 'left');
    assert.equal(Math.round((await button.boundingBox()).x), 0);
    await page.setViewportSize({ width: 640, height: 400 });
    await page.waitForFunction(() => document.querySelector('[data-nesthub-floating]').getBoundingClientRect().bottom < 400);
    // Reinjecting into an already enhanced tab must not duplicate the control.
    await page.addScriptTag({ path: path.join(root, 'floating.js') });
    assert.equal(await button.count(), 1);
    fs.mkdirSync(output, { recursive: true });
    await page.screenshot({ path: path.join(output, 'floating-test.png') });
  } finally { await app.close(); }
});

test('collection window save action is visible without scrolling, including expanded optional fields', { skip: !playwright }, async () => {
  const app = await browser();
  try {
    // Reproduce Chrome's initial action-popup measurement, rather than assuming the desired width.
    const page = await app.newPage({ viewport: { width: 150, height: 600 } });
    await page.addInitScript(() => {
      globalThis.chrome = {
        storage: { local: {
          setAccessLevel: async () => {}, get: async () => ({ connection: { site: 'https://nest.example', token: 'test-token' } }),
          set: async () => {}, remove: async () => {}
        } },
        tabs: { query: async () => [{ title: '设计资源 · 我的灵感收藏', url: 'https://example.com/design' }] },
        runtime: { sendMessage: async () => ({ ok: true }) }, permissions: { request: async () => true }
      };
    });
    await page.route('https://nesthub.test/**', async route => {
      const file = path.join(root, new URL(route.request().url()).pathname);
      const mime = { '.html': 'text/html', '.css': 'text/css', '.js': 'application/javascript', '.png': 'image/png' }[path.extname(file)];
      await route.fulfill({ body: fs.readFileSync(file), contentType: mime });
    });
    await page.route('https://nest.example/api/**', route => route.fulfill({ json: {
      isAuthenticated: true, canEdit: true, tenant: { id: 'tenant', name: '我的导航空间' },
      editor: { categoryOptions: [{ id: 'category', name: '设计与灵感', level: 0 }] }
    } }));
    await page.goto('https://nesthub.test/popup.html');
    const save = page.locator('#save');
    await save.waitFor({ state: 'visible' });
    assert.equal(await page.evaluate(() => document.documentElement.getBoundingClientRect().width), 380,
      'the popup must declare its width even when the initial measurement viewport is narrow');
    assert.equal(await page.evaluate(() => document.body.getBoundingClientRect().width), 380);
    await page.setViewportSize({ width: 380, height: 600 });
    const box = await save.boundingBox();
    assert.ok(box.y >= 0 && box.y + box.height <= 600, 'save button must be entirely visible initially');
    assert.equal(await page.evaluate(() => scrollY), 0);
    assert.ok(await page.locator('.extra-options').evaluate(element => !element.open));
    fs.mkdirSync(output, { recursive: true });
    await page.screenshot({ path: path.join(output, 'popup-test.png') });
    await page.locator('.extra-options summary').click();
    const expanded = await save.boundingBox();
    assert.ok(expanded.y >= 0 && expanded.y + expanded.height <= 600, 'sticky save action must remain visible');
    const point = { x: expanded.x + expanded.width / 2, y: expanded.y + expanded.height / 2 };
    assert.equal(await page.evaluate(({ x, y }) => document.elementFromPoint(x, y)?.closest('button')?.id, point), 'save');
    await save.click();
    await page.locator('#status.success').waitFor();
  } finally { await app.close(); }
});
