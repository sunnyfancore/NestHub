const $ = id => document.getElementById(id);
let connection = null;
let targetTenantId = null;
let savedUrl = null;
function status(message, error = false) {
  $('status').textContent = message;
  $('status').classList.toggle('error', error);
}
function showConnection() {
  $('connectionForm').hidden = false;
  $('saveForm').hidden = true;
  $('disconnect').hidden = true;
}
async function disconnect() {
  await chrome.storage.local.remove(['connection', 'lastCategory', 'email']);
  connection = null;
  savedUrl = null;
  $('token').value = '';
  showConnection();
}
async function api(path, body) {
  const response = await fetch(`${connection.site}/api/${path}`, {
    method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(connection.token ? { Authorization: `Bearer ${connection.token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    signal: AbortSignal.timeout(15000),
    credentials: 'omit',
    redirect: 'error'
  });
  const data = await response.json().catch(() => null);
  if (response.status === 401) {
    await disconnect();
    throw new Error('API 令牌无效或已过期，请更换令牌。');
  }
  if (!response.ok) throw new Error(data?.message || Object.values(data?.errors || {}).flat().join('；') || `请求失败（${response.status}）`);
  return data;
}
async function loadCategories() {
  $('save').disabled = true;
  const portal = await api('portal?publicView=false');
  if (!portal.isAuthenticated || !portal.canEdit || !portal.editor) throw new Error('当前账号无法编辑此空间。');
  targetTenantId = portal.tenant.id;
  $('space').textContent = `保存到：${portal.tenant.name}`;
  const previous = $('category').value || (await chrome.storage.local.get('lastCategory')).lastCategory;
  $('category').replaceChildren();
  for (const category of portal.editor.categoryOptions) {
    const option = document.createElement('option');
    option.value = category.id;
    option.textContent = `${'　'.repeat(category.level)}${category.name}`;
    $('category').append(option);
  }
  if ([...$('category').options].some(option => option.value === previous)) $('category').value = previous;
  $('connectionForm').hidden = true;
  $('saveForm').hidden = false;
  $('disconnect').hidden = false;
  $('save').disabled = !$('category').options.length;
  status($('category').options.length ? '确认标题和分类后即可收藏。' : '此空间暂无分类，请先在 NestHub 中创建分类，再刷新。');
}
function fail(error) {
  status(error.message === 'Failed to fetch' ? '无法连接站点，请检查地址、网络及服务是否已启动。' : error.message, true);
}
$('connectionForm').addEventListener('submit', async event => {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  try {
    const site = new URL($('site').value.trim());
    if (!['http:', 'https:'].includes(site.protocol) || site.username || site.password || site.search || site.hash || site.pathname !== '/') throw new Error('请填写站点根地址，例如 https://nav.example.com（不含路径）。');
    if (site.protocol === 'http:' && !['localhost', '127.0.0.1', '[::1]'].includes(site.hostname)) throw new Error('远程站点请使用 HTTPS，避免令牌明文传输。');
    const granted = await chrome.permissions.request({ origins: [`${site.origin}/*`] });
    if (!granted) throw new Error('需要允许访问此 NestHub 站点才能连接。');
    const token = $('token').value.trim().replace(/^Bearer\s+/i, '');
    if (!token || /\s/.test(token)) throw new Error('请填写有效的 API 令牌。');
    connection = { site: site.origin, token };
    await loadCategories();
    await chrome.storage.local.remove(['lastCategory', 'email']);
    await chrome.storage.local.set({ connection, site: site.origin });
    $('token').value = '';
  } catch (error) { fail(error); } finally { button.disabled = false; }
});
$('saveForm').addEventListener('submit', async event => {
  event.preventDefault();
  $('save').disabled = true;
  try {
    const url = new URL($('url').value.trim());
    const title = $('title').value.trim();
    if (!['http:', 'https:'].includes(url.protocol)) throw new Error('只能收藏 HTTP 或 HTTPS 网页。');
    if (!title) throw new Error('请填写标题。');
    if (savedUrl === url.href) throw new Error('本次已保存此地址。');
    await api(`links?targetTenantId=${encodeURIComponent(targetTenantId)}`, {
      categoryId: $('category').value, title, url: url.href,
      description: $('description').value.trim(), tags: $('tags').value.trim(), isPinned: $('pinned').checked
    });
    savedUrl = url.href;
    await chrome.storage.local.set({ lastCategory: $('category').value });
    status('收藏成功，已添加到 NestHub。');
  } catch (error) { fail(error); } finally { $('save').disabled = !$('category').options.length; }
});
$('refresh').addEventListener('click', async () => {
  $('refresh').disabled = true;
  try { await loadCategories(); } catch (error) { fail(error); } finally { $('refresh').disabled = false; }
});
$('disconnect').addEventListener('click', async () => { await disconnect(); status('已断开连接并清除令牌。'); });
$('floating').addEventListener('change', async () => {
  const enabled = $('floating').checked;
  $('floating').disabled = true;
  try {
    if (enabled && !await chrome.permissions.request({ origins: ['http://*/*', 'https://*/*'] })) throw new Error('未授权网页访问，贴边按钮未开启。');
    const result = await chrome.runtime.sendMessage({ type: 'floating-toggle', enabled });
    if (!result?.ok) throw new Error(result?.error || '设置失败，请重试。');
    status(enabled ? '贴边收藏按钮已开启。' : '贴边收藏按钮已关闭。');
  } catch (error) {
    $('floating').checked = !enabled;
    fail(error);
  } finally { $('floating').disabled = false; }
});
async function init() {
  // Keep the access token unavailable to content scripts.
  await chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' });
  const settings = await chrome.storage.local.get(['connection', 'site', 'floatingEnabled']);
  $('floating').checked = settings.floatingEnabled === true;
  $('site').value = settings.site || '';
  const sourceTab = new URL(location.href).searchParams.get('sourceTab');
  const tab = sourceTab && /^\d+$/.test(sourceTab)
    ? await chrome.tabs.get(Number(sourceTab))
    : (await chrome.tabs.query({ active: true, currentWindow: true }))[0];
  if (tab?.url && /^https?:\/\//i.test(tab.url)) {
    $('url').value = tab.url;
    $('title').value = (tab.title || tab.url).slice(0, 120);
  }
  connection = settings.connection;
  if (connection?.token) {
    try { await loadCategories(); } catch (error) { showConnection(); fail(error); }
  } else { showConnection(); status('连接你的 NestHub，即可收藏当前网页。'); }
}
init().catch(fail);
