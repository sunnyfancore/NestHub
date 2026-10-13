const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
function setup(initialTabs = [{ id: 7, url: 'https://example.com/' }], failRegistration = false) {
  const storage = { connection: { token: 'secret' } };
  const registered = [], windows = [], injected = [], removed = [];
  let listener;
  let startup;
  const openedTabs = [];
  const updatedTabs = [];
  let createdTab;
  let updatedTab;
  const event = { addListener() {} };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../nesthub/background.js'), 'utf8'), {
    console, URL,
    chrome: {
      storage: { local: {
        setAccessLevel: async () => {},
        get: async () => storage,
        set: async data => Object.assign(storage, data),
        remove: async keys => keys.forEach(key => delete storage[key])
      } },
      runtime: {
        getURL: file => `chrome-extension://test/${file}`,
        onInstalled: event, onStartup: { addListener: handler => { startup = handler; } },
        onMessage: { addListener: handler => { listener = handler; } }
      },
      permissions: { contains: async () => true, onRemoved: event },
      scripting: {
        getRegisteredContentScripts: async () => registered,
        registerContentScripts: async scripts => { if (failRegistration) { failRegistration = false; throw new Error('Registration failed'); } registered.push(...scripts); },
        updateContentScripts: async scripts => { registered.splice(0, registered.length, ...scripts); },
        unregisterContentScripts: async () => { registered.length = 0; },
        executeScript: async target => injected.push(target)
      },
      tabs: {
        create: async options => openedTabs.push(options),
        query: async () => initialTabs,
        update: async (id, options) => {
          updatedTabs.push({ id, ...options });
          const tab = initialTabs.find(tab => tab.id === id);
          if (tab) { tab.url = options.url; delete tab.pendingUrl; }
        },
        onCreated: { addListener: handler => { createdTab = handler; } },
        onUpdated: { addListener: handler => { updatedTab = handler; } },
        sendMessage: async (...args) => removed.push(args)
      },
      windows: { create: async options => windows.push(options) }
    }
  });
  const send = (message, sender) => new Promise(resolve => {
    if (listener(message, sender, resolve) === false) resolve(null);
  });
  return { storage, registered, windows, injected, removed, send, openedTabs, updatedTabs, startup, createdTab, updatedTab };
}
const popup = { url: 'chrome-extension://test/popup.html' };
const webpage = { tab: { id: 7 }, frameId: 0, url: 'https://example.com/' };

test('opt-in registers floating button, opens original tab collection window and disables injection', async () => {
  const app = setup();
  assert.equal((await app.send({ type: 'floating-toggle', enabled: true }, popup)).ok, true);
  assert.equal(app.registered.length, 1);
  assert.equal(app.registered[0].runAt, 'document_start');
  // Existing installs must also receive the earlier injection timing.
  app.registered[0].runAt = 'document_idle';
  await app.send({ type: 'floating-toggle', enabled: true }, popup);
  assert.equal(app.registered[0].runAt, 'document_start');
  assert.equal(app.injected[0].target.tabId, 7);
  assert.equal((await app.send({ type: 'floating-open' }, webpage)).ok, true);
  assert.equal((await app.send({ type: 'floating-open' }, webpage)).url, 'chrome-extension://test/popup.html?embedded=1&sourceTab=7');
  assert.equal(app.windows.length, 0);
  await app.send({ type: 'floating-toggle', enabled: false }, popup);
  assert.equal(app.registered.length, 0);
  assert.equal(app.removed.length, 1);
  assert.equal((await app.send({ type: 'floating-open' }, webpage)).ok, false);
});

test('startup reuses the blank tab and simultaneous events do not open duplicate pages', async () => {
  const app = setup([{ id: 1, windowId: 1, url: 'chrome://newtab/' }]);
  app.storage.connection.site = 'https://nav.example';
  app.storage.startupUrl = 'https://obsolete.example/';
  app.storage.openOnStartup = true;
  app.startup(); app.startup();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.openedTabs.length, 0);
  assert.equal(app.updatedTabs.length, 1);
  assert.equal(app.updatedTabs[0].url, 'https://nav.example/');
});

test('new-tab navigation updates only blank tabs and is opt-in', async () => {
  const app = setup();
  app.storage.connection.site = 'https://nav.example';
  app.createdTab({ id: 2, url: 'chrome://newtab/' });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.updatedTabs.length, 0);
  app.storage.openOnNewTab = true;
  app.createdTab({ id: 3, url: 'chrome://newtab/' });
  app.createdTab({ id: 4, url: 'https://work.example/' });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.updatedTabs.length, 1);
  assert.equal(app.updatedTabs[0].id, 3);
  assert.equal(app.openedTabs.length, 0);
});

test('startup navigation is opt-in, uses the saved address, and avoids an existing matching tab', async () => {
  const app = setup();
  app.startup();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.openedTabs.length, 0);
  app.storage.openOnStartup = true;
  app.storage.startupUrl = 'https://nav.example/';
  app.startup();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.openedTabs[0].url, 'https://nav.example/');
  app.storage.startupUrl = 'https://example.com/';
  app.startup();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.openedTabs.length, 1);
  app.storage.startupUrl = 'javascript:alert(1)';
  app.startup();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.openedTabs.length, 1);
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

test('bookmark notification reaches only configured site and rejects webpage senders', async () => {
  const app = setup([{id:1,url:'https://nav.example/app/'},{id:2,url:'https://nav.example/other'},{id:3,url:'https://evil.example/app/'}]);
  app.storage.connection.site = 'https://nav.example/app';
  assert.equal(await app.send({type:'bookmark-saved'},webpage),null);
  assert.equal((await app.send({type:'bookmark-saved'},popup)).ok,true);
  assert.deepEqual(app.injected.map(x=>x.target.tabId),[1]);
  assert.equal(app.injected[0].world,'MAIN');
  const events=[], removed=[];
  vm.runInNewContext('('+app.injected[0].func.toString()+')()', {localStorage:{removeItem:k=>removed.push(k)},window:{dispatchEvent:e=>{events.push(e.type);return false}},Event:class{constructor(type){this.type=type}}});
  assert.deepEqual(events,['nesthub-bookmark-saved']);
  assert.deepEqual(removed,['nesthub-portal-cache']);
  let reloads=0;
  vm.runInNewContext('('+app.injected[0].func.toString()+')()', {localStorage:{removeItem(){}},window:{dispatchEvent:()=>true},location:{reload:()=>reloads++},Event:class{}});
  assert.equal(reloads,1,'legacy page reloads when no receiver acknowledges');
});
test('overlapping floating toggles do not register the same content script twice', async () => {
  const app = setup();
  const results = await Promise.all([
    app.send({type: 'floating-toggle', enabled: true}, popup),
    app.send({type: 'floating-toggle', enabled: true}, popup)
  ]);
  assert.ok(results.every(r => r.ok));
  assert.equal(app.registered.length, 1);
});

test('malformed saved navigation address does not crash new-tab handling', async () => {
  const app = setup();
  app.storage.openOnNewTab = true;
  app.storage.connection.site = 'not a url';
  await app.createdTab({id: 30, url: 'chrome://newtab/'});
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(app.updatedTabs.length, 0);
});


test('combined settings save persists all options and registers the floating script', async () => {
  const app = setup();
  const result = await app.send({ type: 'settings-save', settings: { openOnStartup: true, openOnNewTab: true, startupUrl: 'https://nest.example/', floatingEnabled: true } }, popup);
  assert.equal(result.ok, true);
  assert.equal(app.storage.openOnStartup, true);
  assert.equal(app.storage.openOnNewTab, true);
  assert.equal(app.storage.floatingEnabled, true);
  assert.equal(app.registered.length, 1);
});


test('registration failure rolls back all combined settings', async () => {
  const app = setup([], true);
  Object.assign(app.storage, { openOnStartup: false, openOnNewTab: false, floatingEnabled: false, startupUrl: 'https://old.example/' });
  const result = await app.send({ type: 'settings-save', settings: { openOnStartup: true, openOnNewTab: true, startupUrl: 'https://new.example/', floatingEnabled: true } }, popup);
  assert.equal(result.ok, false);
  assert.equal(app.storage.openOnStartup, false);
  assert.equal(app.storage.openOnNewTab, false);
  assert.equal(app.storage.floatingEnabled, false);
  assert.equal(app.storage.startupUrl, 'https://old.example/');
});
