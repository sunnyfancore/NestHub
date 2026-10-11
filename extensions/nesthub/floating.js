(() => {
  if (globalThis.__nesthubFloating) return;
  globalThis.__nesthubFloating = true;
  const size = 36, margin = 14;
  let host, panel;
  let position = { side: 'right', ratio: 0.45 };
  let dragged = false;
  const viewport = window.visualViewport;
  const send = message => chrome.runtime.sendMessage(message);
  const remove = () => {
    panel?.remove();
    host?.remove();
    window.removeEventListener('resize', place);
    viewport?.removeEventListener('resize', place);
    viewport?.removeEventListener('scroll', place);
    chrome.runtime.onMessage.removeListener(onMessage);
    globalThis.__nesthubFloating = false;
  };
  function bounds() {
    return { width: viewport?.width || window.innerWidth, height: viewport?.height || window.innerHeight,
      x: viewport?.offsetLeft || 0, y: viewport?.offsetTop || 0 };
  }
  function place() {
    if (!host) return;
    const view = bounds();
    const gap = Math.min(margin, Math.max(0, (view.height - size) / 2));
    const travel = Math.max(0, view.height - size - gap * 2);
    host.style.setProperty('top', `${view.y + gap + travel * position.ratio}px`, 'important');
    const left = position.side === 'left' ? 0 : Math.max(0, view.width - size);
    host.style.setProperty('left', `${view.x + left}px`, 'important');
    host.dataset.side = position.side;
  }
  function onMessage(message) {
    if (message?.type === 'floating-remove') remove();
    if (message?.type === 'floating-close') { panel?.remove(); panel = null; }
    if (message?.type === 'floating-theme' && host) host.dataset.theme = message.theme;
  }
  async function mountRoot() {
    if (document.documentElement) return document.documentElement;
    return new Promise(resolve => {
      const observer = new MutationObserver(() => {
        if (document.documentElement) { observer.disconnect(); resolve(document.documentElement); }
      });
      observer.observe(document, { childList: true });
    });
  }
  (async () => {
    const state = await send({ type: 'floating-state' });
    if (!state?.enabled) { globalThis.__nesthubFloating = false; return; }
    if (state.position && ['left', 'right'].includes(state.position.side) && Number.isFinite(state.position.ratio)) {
      position = { side: state.position.side, ratio: Math.min(1, Math.max(0, state.position.ratio)) };
    }
    const root = await mountRoot();
    host = document.createElement('div');
    host.setAttribute('data-nesthub-floating', '');
    host.dataset.theme = state.theme || 'coral';
    // A top-layer popover escapes page transforms and clipping without blocking the page.
    const topLayer = typeof host.showPopover === 'function';
    if (topLayer) host.setAttribute('popover', 'manual');
    host.style.cssText = `all:initial!important;position:fixed!important;inset:auto!important;margin:0!important;padding:0!important;border:0!important;background:transparent!important;overflow:visible!important;z-index:2147483647!important;width:${size}px!important;height:${size}px!important;display:block!important;transform:none!important;box-sizing:border-box!important;opacity:1!important;visibility:visible!important;pointer-events:auto!important;`;
    const shadow = host.attachShadow({ mode: 'closed' });
    const style = document.createElement('style');
    style.textContent = `
      :host{color-scheme:light}:host::backdrop{background:transparent;pointer-events:none}
      *{box-sizing:border-box}section{position:relative;width:36px;height:36px;font:13px "Microsoft YaHei",system-ui,sans-serif}
      button{font:inherit;cursor:pointer;border:0}button:focus-visible{outline:3px solid #f8a6af;outline-offset:3px}
      .collect{display:flex;align-items:center;justify-content:center;width:36px;height:36px;padding:0;border:1px solid #fff9;border-radius:11px;background:linear-gradient(145deg,#ff717d,#ed4d5d);color:#fff;box-shadow:0 2px 8px #d7425526;touch-action:none;user-select:none;transition:box-shadow .18s,background .18s}
      :host([data-side=right]) .collect{border-top-right-radius:0;border-bottom-right-radius:0}:host([data-side=left]) .collect{border-top-left-radius:0;border-bottom-left-radius:0}
      .collect:hover{background:#e94859;box-shadow:0 3px 10px #d7425533}.collect:disabled{cursor:wait;opacity:.7}.collect svg{width:18px;height:18px;pointer-events:none}
      :host([data-theme=lime]) .collect{background:#c9f36b;color:#253216;box-shadow:0 2px 8px #56743826}
      :host([data-theme=midnight]) .collect{background:#303b58;color:#bdcaff;box-shadow:0 2px 8px #17243a26}
      :host([data-theme=default2]) .collect{background:#ed5363;color:#fff}
      :host([data-theme=ocean]) .collect{background:#2579ae;color:#fff;box-shadow:0 2px 8px #2579ae26}
      :host([data-theme=nord]) .collect{background:#5a788d;color:#fff;box-shadow:0 2px 8px #5a788d26}
      :host([data-theme=glass]) .collect{background:linear-gradient(145deg,#a08ce4,#7361be);color:#fff}
      :host([data-theme=neon]) .collect{background:#15102a;color:#70e8c4;border-color:#70e8c466}
      :host([data-theme=paper]) .collect{background:#96713c;color:#fff8ee;box-shadow:0 2px 8px #96713c26}
      :host([data-theme=directory]) .collect{background:#2563a6;color:#fff}
        :host([data-theme=workspace]) .collect{background:#5265d8;color:#fff}
        :host([data-theme=sakura]) .collect{background:#b93877;color:#fff}
        :host([data-theme=launchpad]) .collect{background:#537862;color:#fff}
        :host([data-theme=blackgold]) .collect{background:linear-gradient(145deg,#29251d,#141413);color:#e4c78c;border-color:#bfa46b99}
      :host([data-theme=liquid]) .collect{background:linear-gradient(145deg,#e7f7ffdd,#b6d2f5dd);color:#315576;border-color:#ffffffe0;backdrop-filter:blur(8px)}
      .message{position:absolute;right:46px;top:0;width:210px;border-radius:10px;padding:12px;background:#fff;border:1px solid #eceef2;box-shadow:0 4px 18px #17243a18;color:#4b5362;font:12px/1.6 system-ui}
      :host([data-side=left]) .message{right:auto;left:46px}
      @media(prefers-reduced-motion:reduce){*{transition:none!important}}
    `;
    const section = document.createElement('section');
    const collect = document.createElement('button');
    collect.className = 'collect';
    collect.setAttribute('aria-label', '收藏到 NestHub');
    const icon = document.createElement('img'); icon.src = chrome.runtime.getURL('icons/icon-32.png'); icon.alt = ''; icon.style.cssText = 'width:28px;height:28px;border-radius:8px;pointer-events:none'; collect.append(icon);
    collect.addEventListener('pointerdown', event => {
      if (event.button !== 0) return;
      const startX = event.clientX, startY = event.clientY;
      dragged = false;
      collect.setPointerCapture(event.pointerId);
      const move = next => {
        if (Math.hypot(next.clientX - startX, next.clientY - startY) > 5) dragged = true;
        if (!dragged) return;
        const view = bounds();
        position = { side: next.clientX < view.width / 2 ? 'left' : 'right',
          ratio: Math.min(1, Math.max(0, (next.clientY - size / 2 - margin) / Math.max(1, view.height - size - margin * 2))) };
        place();
      };
      const finish = () => {
        collect.removeEventListener('pointermove', move);
        collect.removeEventListener('pointerup', finish);
        collect.removeEventListener('pointercancel', finish);
        if (dragged) send({ type: 'floating-position', ...position }).catch(() => {});
      };
      collect.addEventListener('pointermove', move);
      collect.addEventListener('pointerup', finish);
      collect.addEventListener('pointercancel', finish);
    });
    collect.addEventListener('click', async event => {
      if (dragged) { dragged = false; return; }
      collect.disabled = true;
      try {
        if (panel) { panel.remove(); panel = null; return; }
        const result = await send({ type: 'floating-open' });
        if (!result?.ok) throw new Error(result?.error || '打开失败');
        panel = document.createElement('div');
        panel.setAttribute('popover', 'auto');
        const view = bounds(), rect = host.getBoundingClientRect();
        const width = Math.min(380, view.width - 16), height = Math.min(600, view.height - 16);
        const left = position.side === 'right' ? rect.left - width - 8 : rect.right + 8;
        panel.style.cssText = `position:fixed!important;inset:auto!important;left:${view.x + Math.max(8, Math.min(left - view.x, view.width - width - 8))}px!important;top:${view.y + Math.max(8, Math.min(rect.top - view.y, view.height - height - 8))}px!important;width:${width}px!important;height:${height}px!important;margin:0!important;padding:0!important;border:0!important;border-radius:16px!important;overflow:hidden!important;box-shadow:0 12px 40px #17243a30!important;background:white!important;`;
        const frame = document.createElement('iframe');
        frame.src = result.url; frame.title = 'NestHub 快捷收藏';
        frame.style.cssText = 'display:block;width:100%;height:100%;border:0;';
        panel.append(frame); root.append(panel);
        panel.addEventListener('toggle', event => { if (event.newState === 'closed') { panel?.remove(); panel = null; } });
        panel.showPopover();

      } catch {
        let notice = shadow.querySelector('.message');
        if (!notice) { notice = document.createElement('div'); notice.className = 'message'; notice.setAttribute('role', 'status'); shadow.append(notice); }
        notice.textContent = '打开失败，请重新加载插件或点击工具栏收藏图标。';
      } finally { collect.disabled = false; }
    });
    section.append(collect); shadow.append(style, section);
    root.append(host); place();
    if (topLayer) host.showPopover();
    window.addEventListener('resize', place);
    viewport?.addEventListener('resize', place);
    viewport?.addEventListener('scroll', place);
    chrome.runtime.onMessage.addListener(onMessage);
  })().catch(() => { host?.remove(); globalThis.__nesthubFloating = false; });
})();
