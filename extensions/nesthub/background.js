const scriptId = 'nesthub-floating';
const matches = ['http://*/*', 'https://*/*'];
async function syncFloating() {
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
async function openStartupPage() {
  const { openOnStartup, startupUrl } = await chrome.storage.local.get(['openOnStartup', 'startupUrl']);
  if (!openOnStartup || !startupUrl) return;
  const url = new URL(startupUrl);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) return;
  const tabs = await chrome.tabs.query({});
  if (tabs.some(tab => tab.url === url.href)) return;
  await chrome.tabs.create({ url: url.href, active: true });
}
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
    : fromPopup && ['floating-toggle', 'theme-sync'].includes(message?.type);
  if (!accepted) return false;
  (async () => {
    if (message.type === 'floating-state') {
      const { floatingEnabled, floatingPosition, theme } = await chrome.storage.local.get(['floatingEnabled', 'floatingPosition', 'theme']);
      return { enabled: floatingEnabled === true, position: floatingPosition, theme };
    }
    if (message.type === 'theme-sync') {
      const { theme } = await chrome.storage.local.get('theme');
      for (const tab of await chrome.tabs.query({})) {
        try { await chrome.tabs.sendMessage(tab.id, { type: 'floating-theme', theme }); } catch {}
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
    if (typeof message.enabled !== 'boolean') throw new Error('无效设置');
    await chrome.storage.local.set({ floatingEnabled: message.enabled });
    const enabled = await syncFloating();
    for (const tab of await chrome.tabs.query({})) {
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
