const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

async function setup(unauthorized = false, sourceTab = false, initialStorage = {}) {
  const elements = new Map();
  const requests = [];
  const storage = { connection: { site: 'https://nest.example', token: 'test-token' }, ...initialStorage };
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      value: '', options: [], handlers: {}, checked: false,
      classList: { toggle() {} },
      setAttribute(name, value) { this[name] = value; },
      addEventListener(name, handler) { this.handlers[name] = handler; },
      replaceChildren() { this.options = []; this.value = ''; },
      append(option) { this.options.push(option); if (!this.value) this.value = option.value; }
    });
    return elements.get(id);
  }
  let ready;
  const completed = new Promise(resolve => { ready = resolve; });
  const context = vm.createContext({
    URL, AbortSignal, location: { href: `chrome-extension://test/popup.html${sourceTab ? '?sourceTab=7' : ''}` },
    document: { documentElement: { dataset: {} }, getElementById: element, createElement: () => ({}) },
    chrome: {
      storage: { local: {
        setAccessLevel: async () => {}, get: async () => ({ ...storage }),
        set: async data => Object.assign(storage, data),
        remove: async keys => keys.forEach(key => delete storage[key])
      } },
      tabs: {
        query: async query => query.url ? [{ id: 9, url: 'https://nest.example/' }] : [{ title: 'Example', url: 'https://example.com/', favIconUrl: 'https://example.com/branding/icon.png' }],
        get: async id => { assert.equal(id, 7); return { title: 'Source page', url: 'https://source.example/' }; }
      },
      permissions: { request: async () => true },
      scripting: { executeScript: async options => {
        assert.equal(options.target.tabId, 9);
        assert.equal(options.args[0], 'https://nest.example');
        return [{ result: 'website-token' }];
      } },
      runtime: { sendMessage: async () => ({ ok: true }) }
    },
    fetch: async (url, options) => {
      requests.push({ url, options });
      return { ok: !unauthorized, status: unauthorized ? 401 : 200, json: async () => ({
        isAuthenticated: true, canEdit: true,
        tenant: { id: 'tenant-id', name: '我的空间' },
        editor: { categoryOptions: [{ id: 'category-id', name: '工具', level: 0 }] }
      }) };
    },
    ready
  });
  const source = fs.readFileSync(path.join(__dirname, '../nesthub/popup.js'), 'utf8');
  vm.runInContext(source.replace('init().catch(fail);', 'init().catch(fail).finally(ready);'), context);
  await completed;
  return { element, requests, storage, context };
}

test('restores token, reads current page and saves into portal tenant/category', async () => {
  const { element, requests, storage } = await setup();
  assert.equal(element('title').value, 'Example');
  assert.equal(element('url').value, 'https://example.com/');
  assert.equal(element('saveForm').hidden, false);
  await element('saveForm').handlers.submit({ preventDefault() {} });
  const request = requests.at(-1);
  assert.equal(request.url, 'https://nest.example/api/links?targetTenantId=tenant-id');
  assert.equal(request.options.headers.Authorization, 'Bearer test-token');
  assert.equal(JSON.parse(request.options.body).categoryId, 'category-id');
  assert.equal(storage.lastCategory, 'category-id');
  assert.equal(JSON.parse(request.options.body).iconUrl, 'https://example.com/branding/icon.png');
  assert.match(element('status').textContent, /收藏成功/);
  await element('saveForm').handlers.submit({ preventDefault() {} });
  assert.equal(requests.length, 2, 'same popup must not submit saved URL twice');
});

test('401 clears token and asks for a replacement', async () => {
  const { element, storage } = await setup(true);
  assert.equal(storage.connection, undefined);
  assert.equal(element('connectionForm').hidden, false);
  assert.equal(element('saveForm').hidden, true);
  assert.match(element('status').textContent, /API 令牌无效或已过期/);
});

test('connects with pasted Bearer token without requesting account credentials', async () => {
  const { element, requests, storage } = await setup();
  element('site').value = 'https://other.example';
  element('token').value = 'Bearer pasted-token';
  await element('connectionForm').handlers.submit({ preventDefault() {}, submitter: {} });
  assert.equal(storage.connection.token, 'pasted-token');
  assert.equal(storage.connection.site, 'https://other.example');
  assert.equal(requests.at(-1).options.headers.Authorization, 'Bearer pasted-token');
  assert.equal(requests.some(request => request.url.includes('auth/login')), false);
  assert.equal(element('token').value, '');
});

test('floating collection window reads source webpage instead of itself', async () => {
  const { element } = await setup(false, true);
  assert.equal(element('title').value, 'Source page');
  assert.equal(element('url').value, 'https://source.example/');
});

const categoryCache = {
  site: 'https://nest.example', token: 'test-token',
  tenant: { id: 'cached-tenant', name: '缓存空间' },
  editor: { categoryOptions: [{ id: 'cached-category', name: '缓存分类', level: 0 }] }
};
test('cached categories open without a request and manual refresh replaces them', async () => {
  const app = await setup(false, false, { categoryCache, lastCategory: 'cached-category' });
  assert.equal(app.requests.length, 0);
  assert.equal(app.element('saveForm').hidden, false);
  assert.equal(app.element('category').value, 'cached-category');
  await app.element('refresh').handlers.click();
  assert.equal(app.requests.length, 1);
  assert.equal(app.element('category').value, 'category-id');
  assert.equal(app.storage.categoryCache.tenant.id, 'tenant-id');
  assert.equal(app.element('refresh').disabled, false);
});
test('cache from another token cannot be reused', async () => {
  const app = await setup(false, false, { categoryCache: { ...categoryCache, token: 'other-token' } });
  assert.equal(app.requests.length, 1);
  assert.equal(app.element('category').value, 'category-id');
});
test('expired token when saving with cache clears cached categories too', async () => {
  const app = await setup(true, false, { categoryCache });
  assert.equal(app.requests.length, 0);
  await app.element('saveForm').handlers.submit({ preventDefault() {} });
  assert.equal(app.storage.categoryCache, undefined);
  assert.equal(app.storage.connection, undefined);
  assert.equal(app.element('connectionForm').hidden, false);
});
test('theme updates the website using PATCH and startup reuses the connected address', async () => {
  const app = await setup();
  app.element('theme').value = 'blackgold';
  await app.element('theme').handlers.change();
  assert.equal(app.storage.theme, 'blackgold');
  assert.equal(app.requests.at(-1).url, 'https://nest.example/api/site/theme');
  assert.equal(app.requests.at(-1).options.method, 'PATCH');
  assert.equal(JSON.parse(app.requests.at(-1).options.body).themeName, 'blackgold');
  app.element('openOnStartup').checked = true;
  await app.element('saveStartup').handlers.click();
  assert.equal(app.storage.startupUrl, 'https://nest.example/');
  assert.equal(app.storage.openOnStartup, true);
  await app.element('disconnect').handlers.click();
  app.element('site').value = 'javascript://alert(1)';
  await app.element('saveStartup').handlers.click();
  assert.equal(app.storage.startupUrl, 'https://nest.example/');
});

test('failed candidate validation keeps an existing usable connection and cache', async () => {
  const app = await setup(true, false, { categoryCache });
  app.element('site').value = 'https://other.example/admin';
  app.element('token').value = 'wrong-token';
  await app.element('connectionForm').handlers.submit({ preventDefault() {}, submitter: {} });
  assert.equal(app.storage.connection.site, 'https://nest.example');
  assert.equal(app.storage.categoryCache.tenant.id, 'cached-tenant');
  assert.equal(app.storage.draftToken, 'wrong-token');
  assert.equal(app.requests.at(-1).url, 'https://other.example/api/portal?publicView=false');
});

test('a new connection replaces stale startup URLs and accepts a hostname', async () => {
  const app = await setup(false, false, { startupUrl: 'https://old.example/' });
  app.element('site').value = 'new.example/admin';
  app.element('token').value = 'new-token';
  await app.element('connectionForm').handlers.submit({ preventDefault() {}, submitter: {} });
  assert.equal(app.storage.connection.site, 'https://new.example');
  assert.equal(app.storage.startupUrl, 'https://new.example/');
  assert.equal(app.storage.draftToken, undefined);
});
test('explicit website connection validates the imported login token with the selected site', async () => {
  const app = await setup();
  app.element('site').value = 'https://nest.example';
  await app.element('connectFromSite').handlers.click();
  assert.equal(app.storage.connection.token, 'website-token');
  assert.equal(app.requests.at(-1).options.headers.Authorization, 'Bearer website-token');
  assert.equal(app.element('token').value, '');
});

test('editing the collected URL does not attach the original page icon', async () => {
  const { element, requests } = await setup();
  element('url').value = 'https://different.example/';
  await element('saveForm').handlers.submit({ preventDefault() {} });
  assert.equal(JSON.parse(requests.at(-1).options.body).iconUrl, null);
});


test('concurrent submit sends only one bookmark request', async () => {
  const app = await setup();
  let release;
  const original = app.context.fetch;
  app.context.fetch = async (...args) => { await new Promise(resolve => { release = resolve; }); return original(...args); };
  const first = app.element('saveForm').handlers.submit({ preventDefault() {} });
  await app.element('saveForm').handlers.submit({ preventDefault() {} });
  release(); await first;
  assert.equal(app.requests.filter(r => r.url.includes('/links?')).length, 1);
});

test('disconnect while categories refresh prevents stale response restoring the session', async () => {
  const app = await setup();
  let release;
  const original = app.context.fetch;
  app.context.fetch = async (...args) => { await new Promise(resolve => { release = resolve; }); return original(...args); };
  const refresh = app.element('refresh').handlers.click();
  await app.element('disconnect').handlers.click();
  release(); await refresh;
  assert.equal(app.storage.connection, undefined);
  assert.equal(app.storage.categoryCache, undefined);
  assert.equal(app.element('saveForm').hidden, true);
  assert.equal(app.element('save').disabled, true);
});

test('successful bookmark stays successful when remembering category fails', async () => {
  const app = await setup();
  app.context.chrome.storage.local.set = async () => { throw new Error('storage unavailable'); };
  await app.element('saveForm').handlers.submit({ preventDefault() {} });
  assert.match(app.element('status').textContent, /收藏成功/);
  await app.element('saveForm').handlers.submit({ preventDefault() {} });
  assert.equal(app.requests.filter(r => r.url.includes('/links?')).length, 1);
});

test('401 while saving keeps the save button disabled after disconnect', async () => {
  const app = await setup();
  app.context.fetch = async () => ({ok: false, status: 401, json: async () => ({})});
  await app.element('saveForm').handlers.submit({preventDefault() {}});
  assert.equal(app.element('save').disabled, true);
  assert.equal(app.storage.connection, undefined);
});


test('malformed cached category triggers fresh read instead of crashing popup', async () => {
  const app = await setup(false, false, {categoryCache: {
    site: 'https://nest.example', token: 'test-token', tenant: {id: 'tenant-id', name: '我的空间'},
    editor: {categoryOptions: [null]}
  }});
  assert.equal(app.requests.length, 1);
  assert.equal(app.element('saveForm').hidden, false);
  assert.equal(app.element('category').value, 'category-id');
});

test('top theme choices select directly and reflect the active theme', async () => {
  const { element, storage } = await setup();
  assert.equal(element('theme-default2')['aria-pressed'], 'true');
  await element('theme-lime').handlers.click();
  assert.equal(storage.theme, 'lime');
  assert.equal(element('theme-lime')['aria-pressed'], 'true');
  assert.equal(element('theme-default2')['aria-pressed'], 'false');
});
