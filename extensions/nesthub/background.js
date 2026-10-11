const scriptId = 'nesthub-floating';
const matches = ['http://*/*', 'https://*/*'];
let floatingSync = Promise.resolve();
function syncFloating() {
  const task = floatingSync.catch(() => {}).then(syncFloatingNow);
  floatingSync = task;
  return task;
}
async function syncFloatingNow() {
  await chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' });
  const { floatingEnabled } = await chrome.storage.local.get('floatingEnabled');
  const allowed = await chrome.permissions.contains({ origins: matches });
  const scripts = await chrome.scripting.getRegisteredContentScripts({ ids: [scriptId] });
  if (floatingEnabled && allowed) {
    const registration = {
      id: scriptId, matches, js: ['floating.js'], runAt: 'document_start', allFrames: false,
      persistAcrossSessions: true
    };
    if (!scripts.length) await chrome.scripting.registerContentScripts([registration]);
    else await chrome.scripting.updateContentScripts([registration]);
  } else if (scripts.length) {
    await chrome.scripting.unregisterContentScripts({ ids: [scriptId] });
  }
  return floatingEnabled === true && allowed;
}
chrome.runtime.onInstalled.addListener(() => { syncFloating().catch(console.error); });
const blankPages = new Set(['chrome://newtab/', 'chrome://new-tab-page/', 'edge://newtab/', 'about:newtab', 'about:blank']);
const pendingWindows = new Set();
const redirectedTabs = new Set();
const startupFlights = new Map();
function navigationUrl(settings) {
  const address = settings.connection?.site || settings.site || settings.startupUrl;
  if (!address) return null;
  let url;
  try { url = new URL(address); } catch { return null; }
  return ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password ? url.href : null;
}
async function openStartupPage(windowId) {
  const key = windowId ?? 'startup';
  if (startupFlights.has(key)) return startupFlights.get(key);
  const task = runStartupPage(windowId).finally(() => { startupFlights.delete(key); });
  startupFlights.set(key, task);
  return task;
}
async function runStartupPage(windowId) {
  if (windowId == null && chrome.windows.getAll) {
    const windows = await chrome.windows.getAll({windowTypes: ['normal']});
    await Promise.all(windows.filter(window => !window.incognito).map(window => openStartupPage(window.id)));
    return;
  }
  const { openOnStartup, connection, site, startupUrl } = await chrome.storage.local.get(['openOnStartup', 'connection', 'site', 'startupUrl']);
  const url = navigationUrl({ connection, site, startupUrl });
  if (!openOnStartup || !url) return;
  if (!await chrome.permissions.contains({ permissions: ['tabs'] })) return;
  const tabs = await chrome.tabs.query(windowId == null ? {} : { windowId });
  if (tabs.some(tab => (tab.pendingUrl || tab.url) === url)) { pendingWindows.delete(windowId); return; }
  if (!tabs.length && windowId != null) { pendingWindows.add(windowId); return; }
  const blank = tabs.find(tab => blankPages.has(tab.pendingUrl || tab.url));
  if (blank?.id != null) {
    redirectedTabs.add(blank.id);
    try { await chrome.tabs.update(blank.id, { url, active: true }); } finally { redirectedTabs.delete(blank.id); }
  } else {
    await chrome.tabs.create({ url, active: true, ...(windowId == null ? {} : { windowId }) });
  }
  pendingWindows.delete(windowId);
}
chrome.windows.onCreated?.addListener(window => {
  if (window.type === 'normal' && !window.incognito) openStartupPage(window.id).catch(console.error);
});
chrome.windows.onRemoved?.addListener(id => { pendingWindows.delete(id); });
async function redirectNewTab(tab) {
  if (tab.id == null || tab.incognito || redirectedTabs.has(tab.id) || !blankPages.has(tab.pendingUrl || tab.url)) return;
  redirectedTabs.add(tab.id);
  try {
    const settings = await chrome.storage.local.get(['openOnNewTab', 'openOnStartup', 'connection', 'site', 'startupUrl']);
    const url = navigationUrl(settings);
    if (!url || (!settings.openOnNewTab && !(settings.openOnStartup && pendingWindows.has(tab.windowId)))) return;
    if (!await chrome.permissions.contains({ permissions: ['tabs'] })) return;
    await chrome.tabs.update(tab.id, { url, active: true });
    pendingWindows.delete(tab.windowId);
  } finally { redirectedTabs.delete(tab.id); }
}
chrome.tabs.onCreated?.addListener(tab => { redirectNewTab(tab).catch(console.error); });
chrome.tabs.onUpdated?.addListener((id, change, tab) => {
  if (change.url) redirectNewTab({ ...tab, id, url: change.url }).catch(console.error);
});
chrome.tabs.onRemoved?.addListener(id => { redirectedTabs.delete(id); });
chrome.runtime.onStartup.addListener(() => {
  syncFloating().catch(console.error);
  openStartupPage().catch(console.error);
});
chrome.permissions.onRemoved.addListener(() => { syncFloating().catch(console.error); });
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  const fromWebpage = Boolean(sender.tab);
  const fromPopup = !fromWebpage && sender.url?.startsWith(chrome.runtime.getURL('popup.html'));
  const accepted = fromWebpage
    ? ['floating-state', 'floating-position', 'floating-open'].includes(message?.type)
    : fromPopup && ['floating-toggle', 'theme-sync', 'settings-save'].includes(message?.type);
  if (!accepted) return false;
  (async () => {
    if (message.type === 'floating-state') {
      const { floatingEnabled, floatingPosition, theme } = await chrome.storage.local.get(['floatingEnabled', 'floatingPosition', 'theme']);
      return { enabled: floatingEnabled === true, position: floatingPosition, theme };
    }
    if (message.type === 'theme-sync') {
      const { theme, connection } = await chrome.storage.local.get(['theme', 'connection']);
      for (const tab of await chrome.tabs.query({})) {
        try { await chrome.tabs.sendMessage(tab.id, { type: 'floating-theme', theme }); } catch {}
        if (connection?.site && tab.url?.startsWith(`${connection.site}/`)) {
          try {
            await chrome.scripting.executeScript({
              target: { tabId: tab.id },
              func: (themeName, tenantId) => {
                localStorage.removeItem('nesthub-portal-cache');
                window.dispatchEvent(new CustomEvent('nesthub-theme-updated', { detail: { themeName, tenantId } }));
              }, args: [theme, message.tenantId], world: 'MAIN'
            });
          } catch { /* Closed or navigating tabs load the persisted theme on their next visit. */ }
        }
      }
      return { ok: true };
    }
    if (message.type === 'floating-position') {
      const { side, ratio } = message;
      if (!['left', 'right'].includes(side) || !Number.isFinite(ratio) || ratio < 0 || ratio > 1) throw new Error('无效位置');
      await chrome.storage.local.set({ floatingPosition: { side, ratio } });
      return { ok: true };
    }
    if (message.type === 'floating-open') {
      const { floatingEnabled } = await chrome.storage.local.get('floatingEnabled');
      if (!floatingEnabled || sender.frameId !== 0) throw new Error('浮窗未开启');
      const placement = { width: 430, height: 640 };
      const anchor = message.anchor;
      const area = anchor?.screen;
      if (anchor && area && [anchor.x, anchor.y, area.left, area.top, area.width, area.height].every(Number.isFinite)
        && area.width >= 380 && area.height >= 400 && ['left', 'right'].includes(anchor.side)) {
        placement.width = Math.min(placement.width, area.width);
        placement.height = Math.min(placement.height, area.height);
        const x = anchor.side === 'right' ? anchor.x - placement.width - 12 : anchor.x + 12;
        placement.left = Math.round(Math.max(area.left, Math.min(x, area.left + area.width - placement.width)));
        placement.top = Math.round(Math.max(area.top, Math.min(anchor.y - 24, area.top + area.height - placement.height)));
      }
      await chrome.windows.create({
        url: `${chrome.runtime.getURL('popup.html')}?sourceTab=${sender.tab.id}`,
        type: 'popup', ...placement
      });
      return { ok: true };
    }
    let nextSettings;
    if (message.type === 'settings-save') {
      const value = message.settings;
      if (!value || ['openOnStartup', 'openOnNewTab', 'floatingEnabled'].some(key => typeof value[key] !== 'boolean') || typeof value.startupUrl !== 'string') throw new Error('无效设置');
      if ((value.openOnStartup || value.openOnNewTab) && !navigationUrl({ startupUrl: value.startupUrl })) throw new Error('请先连接有效的导航站点。');
      if ((value.openOnStartup || value.openOnNewTab) && !await chrome.permissions.contains({ permissions: ['tabs'] })) throw new Error('缺少标签页权限，设置未保存。');
      if (value.floatingEnabled && !await chrome.permissions.contains({ origins: matches })) throw new Error('缺少网页访问权限，设置未保存。');
      nextSettings = { openOnStartup: value.openOnStartup, openOnNewTab: value.openOnNewTab, startupUrl: value.startupUrl, floatingEnabled: value.floatingEnabled };
    } else {
    if (typeof message.enabled !== 'boolean') throw new Error('无效设置');
      nextSettings = { floatingEnabled: message.enabled };
    }
    const previous = { ...await chrome.storage.local.get(Object.keys(nextSettings)) };
    await chrome.storage.local.set(nextSettings);
    let enabled;
    try { enabled = await syncFloating(); }
    catch (error) {
      const absent = Object.keys(nextSettings).filter(key => !(key in previous));
      await chrome.storage.local.set(previous);
      if (absent.length) await chrome.storage.local.remove(absent);
      await syncFloating().catch(() => {});
      throw error;
    }
    for (const tab of await chrome.tabs.query({}).catch(() => [])) {
      if (!/^https?:\/\//i.test(tab.url || '')) continue;
      try {
        if (enabled) await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ['floating.js'] });
        else await chrome.tabs.sendMessage(tab.id, { type: 'floating-remove' });
      } catch { /* Restricted browser pages and closed tabs cannot be injected. */ }
    }
    return { ok: true };
  })().then(sendResponse, error => sendResponse({ ok: false, error: error.message }));
  return true;
});
