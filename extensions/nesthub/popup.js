const $ = id => document.getElementById(id);
const themeNames = ['default2', 'ocean', 'neon', 'paper', 'lime', 'blackgold', 'liquid', 'directory', 'workspace', 'launchpad'];
const themeDescriptions = {
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
  $('themeDescription').textContent = themeDescriptions[theme];
}
let connection = null;
let targetTenantId = null;
let savedUrl = null;
function status(message, error = false) {
  $('status').textContent = message;
  $('status').classList.toggle('error', error);
  $('status').classList.toggle('success', !error && message.startsWith('收藏成功'));
}
function showConnection() {
  $('connectionForm').hidden = false;
  $('saveForm').hidden = true;
  $('disconnect').hidden = true;
}
async function disconnect() {
  await chrome.storage.local.remove(['connection', 'categoryCache', 'lastCategory', 'email']);
  connection = null;
  targetTenantId = null;
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
function renderCategories(portal, previous) {
  targetTenantId = portal.tenant.id;
  $('space').textContent = `保存到：${portal.tenant.name}`;
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
  $('save').disabled = true;
  try {
    const portal = await api('portal?publicView=false');
    if (!portal.isAuthenticated || !portal.canEdit || !portal.editor) {
      await chrome.storage.local.remove(['categoryCache']);
      targetTenantId = null;
      showConnection();
      throw new Error('当前账号无法编辑此空间。');
    }
    const { lastCategory } = await chrome.storage.local.get('lastCategory');
    renderCategories(portal, $('category').value || lastCategory);
    await chrome.storage.local.set({ categoryCache: {
      site: connection.site, token: connection.token,
      tenant: portal.tenant, editor: { categoryOptions: portal.editor.categoryOptions }
    } });
  } finally {
    $('save').disabled = !connection || !targetTenantId || !$('category').options.length;
  }
}
function fail(error) {
  status(error.message === 'Failed to fetch' ? '无法连接站点，请检查地址、网络及服务是否已启动。' : error.message, true);
}
function normalizeSite(raw) {
  const site = new URL(raw.trim());
  if (!['http:', 'https:'].includes(site.protocol) || site.username || site.password || site.search || site.hash || site.pathname !== '/') throw new Error('请填写站点根地址，例如 https://nav.example.com（不含路径）。');
  if (site.protocol === 'http:' && !['localhost', '127.0.0.1', '[::1]'].includes(site.hostname)) throw new Error('远程站点请使用 HTTPS，避免令牌明文传输。');
  return site;
}
async function connectWithToken(site, token) {
  connection = { site: site.origin, token };
  targetTenantId = null;
  $('category').replaceChildren();
  await chrome.storage.local.remove(['categoryCache', 'lastCategory', 'email']);
  await loadCategories();
  await chrome.storage.local.set({ connection, site: site.origin });
  if (!$('startupUrl').value) $('startupUrl').value = site.origin;
  $('token').value = '';
}
$('connectFromSite').addEventListener('click', async () => {
  $('connectFromSite').disabled = true;
  try {
    const site = normalizeSite($('site').value);
    if (!await chrome.permissions.request({ origins: [`${site.origin}/*`] })) throw new Error('需要允许访问此 NestHub 站点才能连接。');
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
  } catch (error) { fail(error); } finally { $('connectFromSite').disabled = false; }
});
$('connectionForm').addEventListener('submit', async event => {
  event.preventDefault();
  const button = event.submitter;
  button.disabled = true;
  try {
    const site = normalizeSite($('site').value);
    const granted = await chrome.permissions.request({ origins: [`${site.origin}/*`] });
    if (!granted) throw new Error('需要允许访问此 NestHub 站点才能连接。');
    const token = $('token').value.trim().replace(/^Bearer\s+/i, '');
    if (!token || /\s/.test(token)) throw new Error('请填写有效的 API 令牌。');
    await connectWithToken(site, token);
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
$('theme').addEventListener('change', async () => {
  const theme = $('theme').value;
  if (!themeNames.includes(theme)) return;
  applyTheme(theme);
  try {
    await chrome.storage.local.set({ theme });
    await chrome.runtime.sendMessage({ type: 'theme-sync' });
  } catch (error) { fail(error); }
});
$('saveStartup').addEventListener('click', async () => {
  const enabled = $('openOnStartup').checked;
  $('saveStartup').disabled = true;
  try {
    const raw = $('startupUrl').value.trim() || connection?.site || $('site').value.trim();
    let url = '';
    if (raw) {
      const parsed = new URL(raw);
      if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password) throw new Error('请填写不含账号密码的 HTTP 或 HTTPS 导航地址。');
      url = parsed.href;
    }
    if (enabled && !url) throw new Error('请先填写导航地址。');
    await chrome.storage.local.set({ openOnStartup: enabled, startupUrl: url });
    $('startupUrl').value = url;
    status(enabled ? '已保存，下次启动浏览器时打开导航。' : '已关闭启动时自动打开导航。');
  } catch (error) { fail(error); } finally { $('saveStartup').disabled = false; }
});
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
  const settings = await chrome.storage.local.get(['connection', 'site', 'floatingEnabled', 'categoryCache', 'lastCategory', 'theme', 'openOnStartup', 'startupUrl']);
  const savedTheme = ({ coral: 'default2', nord: 'ocean', glass: 'liquid', midnight: 'blackgold' })[settings.theme] || settings.theme;
  const theme = themeNames.includes(savedTheme) ? savedTheme : 'default2';
  applyTheme(theme);
  $('theme').value = theme;
  $('openOnStartup').checked = settings.openOnStartup === true;
  $('startupUrl').value = settings.startupUrl || settings.connection?.site || settings.site || '';
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
    const cache = settings.categoryCache;
    if (cache?.site === connection.site && cache.token === connection.token && cache.tenant?.id
      && Array.isArray(cache.editor?.categoryOptions)) {
      renderCategories(cache, settings.lastCategory);
    } else {
      try { await loadCategories(); } catch (error) { showConnection(); fail(error); }
    }
  } else { showConnection(); status('连接你的 NestHub，即可收藏当前网页。'); }
}
init().catch(fail);
