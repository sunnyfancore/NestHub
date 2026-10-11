const $ = id => document.getElementById(id);
const themeNames = ['default2', 'ocean', 'neon', 'paper', 'lime', 'blackgold', 'liquid', 'directory', 'workspace', 'launchpad', 'sakura'];
const themeDescriptions = {
  sakura: '樱花粉与奶油白，糖果渐变和星光点缀，轻盈可爱的二次元风格。',
  directory: '白底蓝绿链接、紧凑网址网格与分类分隔，适合大量网址快速查找。',
  workspace: '深色分类侧栏与整齐工具卡片，适合工作资源和团队常用工具。',
  launchpad: '居中大搜索、宽松留白与图标卡片，适合个人浏览器起始页。',

  default2: '雾白底色与珊瑚点缀，清晰轻快，适合日常收藏。',
  ocean: '深蓝标题与海洋蓝按钮，层次清爽，适合专注工作。',
  nord: '冰蓝灰底色与克制的线条，安静舒适，适合长时间使用。',
  glass: '柔和紫色渐变与半透明面板，轻盈通透，增添灵感。',
  neon: '深紫底色与薄荷霓虹高光，鲜明有趣，带一点未来感。',
  paper: '暖色纸张与棕金细节，温润自然，适合阅读与笔记。',
  coral: '白色留白与珊瑚红按钮，明亮亲切，收藏操作一目了然。',
  lime: '雾白底色与青柠色按钮，干净利落，带来清新的视觉节奏。',
  midnight: '石墨蓝底色与浅蓝按钮，柔和对比，适合夜间使用。',
  blackgold: '曜石黑底色与香槟金细节，细腻光泽、克制层次，沉稳而精致。',
  liquid: '冰透渐变与液态玻璃面板，折射高光、柔和边缘，清透灵动。'
};
function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  for (const name of themeNames) $('theme-' + name).setAttribute('aria-pressed', String(name === theme));
  $('themeDescription').textContent = themeDescriptions[theme];
}
let connection = null;
let sourcePageUrl = '';
let sourcePageIcon = null;
let targetTenantId = null;
let savedUrl = null;
let connecting = false;
let saving = false;
let categoryRequest = 0;
function status(message, error = false) {
  $('status').textContent = message;
  $('status').classList.toggle('error', error);
  $('status').classList.toggle('success', !error && message.startsWith('收藏成功'));
}
function showConnection() {
  $('connectionForm').hidden = false;
  $('saveForm').hidden = true;
  $('disconnect').hidden = true;
  $('space').hidden = true;
}
async function disconnect() {
  ++categoryRequest;
  connection = null;
  targetTenantId = null;
  $('save').disabled = true;
  await chrome.storage.local.remove(['connection', 'categoryCache', 'lastCategory', 'email', 'draftToken']);
  connection = null;
  targetTenantId = null;
  savedUrl = null;
  $('token').value = '';
  showConnection();
}
async function api(path, body, method = body === undefined ? 'GET' : 'POST', selectedConnection = connection) {
  if (!selectedConnection?.site) throw new Error('请先连接导航站点。');
  const response = await fetch(`${selectedConnection.site}/api/${path}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(selectedConnection.token ? { Authorization: `Bearer ${selectedConnection.token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    signal: AbortSignal.timeout(15000),
    credentials: 'omit',
    redirect: 'error'
  });
  const data = await response.json().catch(() => null);
  if (response.status === 401) {
    if (selectedConnection === connection) await disconnect();
    throw new Error('API 令牌无效或已过期，请更换令牌。');
  }
  if (response.status === 404 && path === 'site/theme') throw new Error('当前网站版本尚不支持主题同步，请更新网站后重试。');
  if (!response.ok) throw new Error(data?.message || Object.values(data?.errors || {}).flat().join('；') || `请求失败（${response.status}）`);
  return data;
}
function validCategories(options) {
  return Array.isArray(options) && options.every(item => item && typeof item.id === 'string' && typeof item.name === 'string' && Number.isInteger(item.level) && item.level >= 0 && item.level <= 20);
}
function renderCategories(portal, previous) {
  targetTenantId = portal.tenant.id;
  $('space').textContent = `保存到：${portal.tenant.name}`;
  $('space').hidden = false;
  $('space').title = `保存到：${portal.tenant.name}`;
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
async function loadCategories() {
  const selected = connection;
  const request = ++categoryRequest;
  $('save').disabled = true;
  try {
    const portal = await api('portal?publicView=false', undefined, 'GET', selected);
    if (request !== categoryRequest || selected !== connection) return;
    if (!portal?.tenant?.id || !validCategories(portal.editor?.categoryOptions)) throw new Error('站点返回的分类数据不完整，请更新网站后重试。');
    if (!portal.isAuthenticated || !portal.canEdit || !portal.editor) {
      await chrome.storage.local.remove(['categoryCache']);
      targetTenantId = null;
      showConnection();
      throw new Error('当前账号无法编辑此空间。');
    }
    const { lastCategory } = await chrome.storage.local.get('lastCategory');
    if (request !== categoryRequest || selected !== connection) return;
    renderCategories(portal, $('category').value || lastCategory);
    await chrome.storage.local.set({ categoryCache: {
      site: selected.site, token: selected.token,
      tenant: portal.tenant, editor: { categoryOptions: portal.editor.categoryOptions }
    } });
  } finally {
    if (request === categoryRequest) $('save').disabled = saving || !connection || !targetTenantId || !$('category').options.length;
  }
}
function fail(error) {
  const message = error.name === 'TimeoutError' ? '连接超时，请检查站点是否可访问，然后重试。' : error.message === 'Failed to fetch' ? '无法连接站点，请检查地址、网络及服务是否已启动。' : error.message;
  status(message, true);
}
function normalizeSite(raw) {
  const value = raw.trim();
  const site = new URL(/^[a-z][a-z\d+.-]*:\/\//i.test(value) ? value : `https://${value}`);
  if (!['http:', 'https:'].includes(site.protocol) || site.username || site.password) throw new Error('请填写导航地址，例如 https://nav.example.com。');
  if (site.protocol === 'http:' && !['localhost', '127.0.0.1', '[::1]'].includes(site.hostname)) throw new Error('远程站点请使用 HTTPS，避免令牌明文传输。');
  return site;
}
async function connectWithToken(site, token) {
  const candidate = { site: site.origin, token };
  const portal = await api('portal?publicView=false', undefined, 'GET', candidate);
  if (!portal?.isAuthenticated || !portal.canEdit || !validCategories(portal.editor?.categoryOptions) || !portal.tenant?.id) throw new Error('当前账号无法编辑此空间，请确认站点与令牌是否匹配。');
  const cache = { site: candidate.site, token, tenant: portal.tenant, editor: { categoryOptions: portal.editor.categoryOptions } };
  // Persist the validated connection before displaying success. Popup closure cannot lose it.
  await chrome.storage.local.set({ connection: candidate, site: site.origin, startupUrl: `${site.origin}/`, categoryCache: cache });
  try { await chrome.storage.local.remove(['draftToken', 'lastCategory', 'email']); } catch { /* Connection was already persisted; optional cleanup must not undo success. */ }
  ++categoryRequest;
  connection = candidate;
  renderCategories(portal);
  $('site').value = site.origin;
  $('startupAddress').textContent = `导航地址：${site.origin}`;
  $('token').value = '';
  status('连接成功，可以直接收藏当前网页。');
}
$('connectFromSite').addEventListener('click', async () => {
  if (connecting) return;
  connecting = true;
  $('connectFromSite').disabled = true;
  try {
    const site = normalizeSite($('site').value);
    const permission = chrome.permissions.request({ origins: [`${site.origin}/*`] });
    await chrome.storage.local.set({ site: site.origin });
    if (!await permission) throw new Error('需要允许访问此 NestHub 站点才能连接。');
    const tabs = await chrome.tabs.query({ url: `${site.origin}/*` });
    let token = null;
    for (const tab of tabs.filter(tab => tab.id != null).sort((a, b) => Number(b.active) - Number(a.active))) {
      try {
        const results = await chrome.scripting.executeScript({
          target: { tabId: tab.id },
          func: origin => location.origin === origin ? localStorage.getItem('nesthub.token') : null,
          args: [site.origin]
        });
        const value = results[0]?.result;
        if (typeof value === 'string' && value && !/\s/.test(value)) { token = value; break; }
      } catch { /* A tab may close or navigate while checking its login state. */ }
    }
    if (!token) {
      if (!tabs.length) await chrome.tabs.create({ url: site.href });
      throw new Error('请先在打开的 NestHub 网站登录，再回来点击此按钮。');
    }
    await connectWithToken(site, token);
  } catch (error) { fail(error); } finally { connecting = false; $('connectFromSite').disabled = false; }
});
$('connectionForm').addEventListener('submit', async event => {
  event.preventDefault();
  if (connecting) return;
  connecting = true;
  const button = event.submitter || $('connectionForm').querySelector('button[type=submit]');
  button.disabled = true;
  try {
    const site = normalizeSite($('site').value);
    const token = $('token').value.trim().replace(/^Bearer\s+/i, '');
    if (!token || /\s/.test(token)) throw new Error('请填写有效的 API 令牌。');
    const permission = chrome.permissions.request({ origins: [`${site.origin}/*`] });
    await chrome.storage.local.set({ site: site.origin, draftToken: token });
    const granted = await permission;
    if (!granted) throw new Error('需要允许访问此 NestHub 站点才能连接。');
    await connectWithToken(site, token);
  } catch (error) { fail(error); } finally { connecting = false; button.disabled = false; }
});
$('saveForm').addEventListener('submit', async event => {
  event.preventDefault();
  if (saving) return;
  saving = true;
  $('save').disabled = true;
  try {
    if (!connection?.token || !targetTenantId || !$('category').value) throw new Error('请先连接站点并选择分类。');
    const url = new URL($('url').value.trim());
    const title = $('title').value.trim();
    if (!['http:', 'https:'].includes(url.protocol)) throw new Error('只能收藏 HTTP 或 HTTPS 网页。');
    if (!title) throw new Error('请填写标题。');
    if (savedUrl === url.href) throw new Error('本次已保存此地址。');
    await api(`links?targetTenantId=${encodeURIComponent(targetTenantId)}`, {
      categoryId: $('category').value, title, url: url.href,
      iconUrl: url.href === sourcePageUrl ? sourcePageIcon : null,
      description: $('description').value.trim(), tags: $('tags').value.trim(), isPinned: $('pinned').checked
    });
    savedUrl = url.href;
    try { await chrome.storage.local.set({ lastCategory: $('category').value }); } catch { /* A successful server save must never be reported as failed due to a local preference. */ }
    status('收藏成功，已添加到 NestHub。');
  } catch (error) { fail(error); } finally { saving = false; $('save').disabled = !connection || !targetTenantId || !$('category').options.length; }
});
$('refresh').addEventListener('click', async () => {
  $('refresh').disabled = true;
  try { await loadCategories(); } catch (error) { fail(error); } finally { $('refresh').disabled = false; }
});
$('disconnect').addEventListener('click', async () => { try { await disconnect(); status('已断开连接并清除令牌。'); } catch (error) { fail(error); } });
async function changeTheme() {
  const theme = $('theme').value;
  if (!themeNames.includes(theme)) return;
  applyTheme(theme);
  $('theme').disabled = true;
  try {
    if (connection?.token) await api('site/theme', { themeName: theme }, 'PATCH');
    await chrome.storage.local.set({ theme });
    const result = await chrome.runtime.sendMessage({ type: 'theme-sync', tenantId: targetTenantId });
    if (!result?.ok) throw new Error(result?.error || '主题已保存，刷新导航页面后生效。');
    status(connection?.token ? '主题已同步到导航页面与插件。' : '插件主题已保存，连接站点后可同步导航主题。');
  } catch (error) {
    const { theme: previous } = await chrome.storage.local.get('theme');
    const restored = themeNames.includes(previous) ? previous : 'default2';
    applyTheme(restored); $('theme').value = restored;
    fail(error);
  } finally { $('theme').disabled = false; }
}
$('theme').addEventListener('change', changeTheme);
for (const name of themeNames) {
  $('theme-' + name).addEventListener('click', () => {
    if ($('theme').disabled || document.documentElement.dataset.theme === name) return;
    $('theme').value = name;
    return changeTheme();
  });
}
let settingsSaving = false;
$('saveStartup').addEventListener('click', async () => {
  if (settingsSaving) return;
  const enabled = $('openOnStartup').checked;
  const newTabEnabled = $('openOnNewTab').checked;
  const floatingEnabled = $('floating').checked;
  settingsSaving = true;
  for (const id of ['saveStartup', 'openOnStartup', 'openOnNewTab', 'floating']) $(id).disabled = true;
  try {
    const raw = connection?.site || $('site').value.trim();
    let url = '';
    if (raw) url = normalizeSite(raw).href;
    if ((enabled || newTabEnabled) && !url) throw new Error('请先连接导航站点。');
    const permissions = {};
    if (enabled || newTabEnabled) permissions.permissions = ['tabs'];
    if (floatingEnabled) permissions.origins = ['http://*/*', 'https://*/*'];
    // Request the complete set directly in the click gesture, before any other async work.
    if (Object.keys(permissions).length && !await chrome.permissions.request(permissions))
      throw new Error('未获得所需权限，设置未保存。可取消需要权限的选项后再保存。');
    const result = await chrome.runtime.sendMessage({ type: 'settings-save', settings: {
      openOnStartup: enabled, openOnNewTab: newTabEnabled, startupUrl: url, floatingEnabled
    } });
    if (!result?.ok) throw new Error(result?.error || '插件设置保存失败，请重试。');
    $('startupAddress').textContent = url ? `导航地址：${url}` : '连接导航站点后自动使用该地址。';
    status('插件设置已保存。');
  } catch (error) { fail(error); }
  finally {
    settingsSaving = false;
    for (const id of ['saveStartup', 'openOnStartup', 'openOnNewTab', 'floating']) $(id).disabled = false;
  }
});
$('openNavigation').addEventListener('click', async () => {
  try {
    const site = normalizeSite(connection?.site || $('site').value);
    await chrome.tabs.create({ url: site.href, active: true });
  } catch (error) { fail(error); }
});
for (const id of ['openOnStartup', 'openOnNewTab', 'floating']) {
  $(id).addEventListener('change', () => status('设置尚未保存，请点击「保存插件设置」。'));
}
for (const panel of ['collect', 'theme', 'settings']) {
  $('panel-' + panel).addEventListener('click', () => {
    document.body.dataset.panel = panel;
    for (const name of ['collect', 'theme', 'settings']) $('panel-' + name).setAttribute('aria-pressed', String(name === panel));
  });
}
async function init() {
  // Keep the access token unavailable to content scripts.
  await chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' });
  const settings = await chrome.storage.local.get(['connection', 'site', 'floatingEnabled', 'categoryCache', 'lastCategory', 'theme', 'openOnStartup', 'openOnNewTab', 'draftToken']);
  const savedTheme = ({ coral: 'default2', nord: 'ocean', glass: 'liquid', midnight: 'blackgold' })[settings.theme] || settings.theme;
  const theme = themeNames.includes(savedTheme) ? savedTheme : 'default2';
  applyTheme(theme);
  $('theme').value = theme;
  $('openOnStartup').checked = settings.openOnStartup === true;
  $('openOnNewTab').checked = settings.openOnNewTab === true;
  $('startupAddress').textContent = settings.connection?.site ? `导航地址：${settings.connection.site}` : '连接导航站点后自动使用该地址。';
  $('floating').checked = settings.floatingEnabled === true;
  $('site').value = settings.connection?.site || settings.site || '';
  $('token').value = settings.draftToken || '';
  const sourceTab = new URL(location.href).searchParams.get('sourceTab');
  const tab = sourceTab && /^\d+$/.test(sourceTab)
    ? await chrome.tabs.get(Number(sourceTab)).catch(() => null)
    : (await chrome.tabs.query({ active: true, currentWindow: true }).catch(() => []))[0];
  if (tab?.url && /^https?:\/\//i.test(tab.url)) {
    sourcePageUrl = new URL(tab.url).href;
    sourcePageIcon = tab.favIconUrl && /^https?:\/\//i.test(tab.favIconUrl) && tab.favIconUrl.length <= 1024
      ? tab.favIconUrl : null;
    $('url').value = tab.url;
    $('title').value = (tab.title || tab.url).slice(0, 120);
  }
  connection = settings.connection;
  if (connection?.token) {
    const cache = settings.categoryCache;
    if (cache?.site === connection.site && cache.token === connection.token && cache.tenant?.id
      && validCategories(cache.editor?.categoryOptions)) {
      renderCategories(cache, settings.lastCategory);
    } else {
      try { await loadCategories(); } catch (error) { showConnection(); fail(error); }
    }
  } else { showConnection(); status('连接你的 NestHub，即可收藏当前网页。'); }
}
init().catch(fail);

if (new URL(location.href).searchParams.get('embedded') === '1') {
  document.documentElement.classList.add('embedded');
  const close = document.createElement('button'); close.type = 'button'; close.className = 'embedded-close'; close.textContent = '×'; close.setAttribute('aria-label', '关闭收藏面板');
  close.addEventListener('click', () => chrome.runtime.sendMessage({ type: 'floating-close' }).catch(() => {}));
  document.body.prepend(close);
}
