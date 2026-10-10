const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

async function setup(unauthorized = false, sourceTab = false) {
  const elements = new Map();
  const requests = [];
  const storage = { connection: { site: 'https://nest.example', token: 'test-token' } };
  function element(id) {
    if (!elements.has(id)) elements.set(id, {
      value: '', options: [], handlers: {}, checked: false,
      classList: { toggle() {} },
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
    document: { getElementById: element, createElement: () => ({}) },
    chrome: {
      storage: { local: {
        setAccessLevel: async () => {}, get: async () => ({ ...storage }),
        set: async data => Object.assign(storage, data),
        remove: async keys => keys.forEach(key => delete storage[key])
      } },
      tabs: {
        query: async () => [{ title: 'Example', url: 'https://example.com/' }],
        get: async id => { assert.equal(id, 7); return { title: 'Source page', url: 'https://source.example/' }; }
      },
      permissions: { request: async () => true }
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
  return { element, requests, storage };
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
