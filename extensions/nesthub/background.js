const scriptId = 'nesthub-floating';
const matches = ['http://*/*', 'https://*/*'];
async function syncFloating() {
  await chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' });
  const { floatingEnabled } = await chrome.storage.local.get('floatingEnabled');
  const allowed = await chrome.permissions.contains({ origins: matches });
  const scripts = await chrome.scripting.getRegisteredContentScripts({ ids: [scriptId] });
  if (floatingEnabled && allowed) {
    if (!scripts.length) await chrome.scripting.registerContentScripts([{
      id: scriptId, matches, js: ['floating.js'], runAt: 'document_idle', allFrames: false,
      persistAcrossSessions: true
    }]);
  } else if (scripts.length) {
    await chrome.scripting.unregisterContentScripts({ ids: [scriptId] });
  }
  return floatingEnabled === true && allowed;
}
chrome.runtime.onInstalled.addListener(() => { syncFloating().catch(console.error); });
chrome.runtime.onStartup.addListener(() => { syncFloating().catch(console.error); });
chrome.permissions.onRemoved.addListener(() => { syncFloating().catch(console.error); });
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  const fromWebpage = Boolean(sender.tab);
  const fromPopup = !fromWebpage && sender.url?.startsWith(chrome.runtime.getURL('popup.html'));
  const accepted = fromWebpage
    ? ['floating-state', 'floating-position', 'floating-open'].includes(message?.type)
    : fromPopup && message?.type === 'floating-toggle';
  if (!accepted) return false;
  (async () => {
    if (message.type === 'floating-state') {
      const { floatingEnabled, floatingPosition } = await chrome.storage.local.get(['floatingEnabled', 'floatingPosition']);
      return { enabled: floatingEnabled === true, position: floatingPosition };
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
      await chrome.windows.create({
        url: `${chrome.runtime.getURL('popup.html')}?sourceTab=${sender.tab.id}`,
        type: 'popup', width: 430, height: 740
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
