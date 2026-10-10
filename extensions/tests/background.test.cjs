const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
function setup() {
  const storage = { connection: { token: 'secret' } };
  const registered = [], windows = [], injected = [], removed = [];
  let listener;
  const event = { addListener() {} };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../nesthub/background.js'), 'utf8'), {
    console,
    chrome: {
      storage: { local: {
        setAccessLevel: async () => {},
        get: async () => storage,
        set: async data => Object.assign(storage, data)
      } },
      runtime: {
        getURL: file => `chrome-extension://test/${file}`,
        onInstalled: event, onStartup: event,
        onMessage: { addListener: handler => { listener = handler; } }
      },
      permissions: { contains: async () => true, onRemoved: event },
      scripting: {
        getRegisteredContentScripts: async () => registered,
        registerContentScripts: async scripts => registered.push(...scripts),
        unregisterContentScripts: async () => { registered.length = 0; },
        executeScript: async target => injected.push(target)
      },
      tabs: {
        query: async () => [{ id: 7, url: 'https://example.com/' }],
        sendMessage: async (...args) => removed.push(args)
      },
      windows: { create: async options => windows.push(options) }
    }
  });
  const send = (message, sender) => new Promise(resolve => {
    if (listener(message, sender, resolve) === false) resolve(null);
  });
  return { storage, registered, windows, injected, removed, send };
}
const popup = { url: 'chrome-extension://test/popup.html' };
const webpage = { tab: { id: 7 }, frameId: 0, url: 'https://example.com/' };

test('opt-in registers floating button, opens original tab collection window and disables injection', async () => {
  const app = setup();
  assert.equal((await app.send({ type: 'floating-toggle', enabled: true }, popup)).ok, true);
  assert.equal(app.registered.length, 1);
  assert.equal(app.injected[0].target.tabId, 7);
  assert.equal((await app.send({ type: 'floating-open' }, webpage)).ok, true);
  assert.equal(app.windows[0].url, 'chrome-extension://test/popup.html?sourceTab=7');
  await app.send({ type: 'floating-toggle', enabled: false }, popup);
  assert.equal(app.registered.length, 0);
  assert.equal(app.removed.length, 1);
  assert.equal((await app.send({ type: 'floating-open' }, webpage)).ok, false);
});

test('webpage receives only floating settings and cannot change global toggle', async () => {
  const app = setup();
  const result = await app.send({ type: 'floating-state' }, webpage);
  assert.equal(result.connection, undefined);
  assert.equal(JSON.stringify(result).includes('secret'), false);
  assert.equal(await app.send({ type: 'floating-toggle', enabled: true }, webpage), null);
  assert.equal((await app.send({ type: 'floating-position', side: 'right', ratio: 2 }, webpage)).ok, false);
  assert.equal((await app.send({ type: 'floating-position', side: 'left', ratio: 0.3 }, webpage)).ok, true);
  assert.equal(app.storage.floatingPosition.ratio, 0.3);
});
