(() => {
  if (globalThis.__nesthubFloating) return;
  globalThis.__nesthubFloating = true;
  let host;
  let position = { side: 'right', ratio: 0.55 };
  let dragged = false;
  const send = message => chrome.runtime.sendMessage(message);
  const remove = () => {
    host?.remove();
    window.removeEventListener('resize', place);
    chrome.runtime.onMessage.removeListener(onMessage);
    globalThis.__nesthubFloating = false;
  };
  function place() {
    if (!host) return;
    host.style.setProperty('top', `${Math.max(0, window.innerHeight - 64) * position.ratio}px`, 'important');
    host.style.setProperty('left', position.side === 'left' ? '0' : 'auto', 'important');
    host.style.setProperty('right', position.side === 'right' ? '0' : 'auto', 'important');
  }
  function onMessage(message) { if (message?.type === 'floating-remove') remove(); }
  (async () => {
    const state = await send({ type: 'floating-state' });
    if (!state?.enabled) { globalThis.__nesthubFloating = false; return; }
    if (state.position) position = state.position;
    host = document.createElement('div');
    host.style.cssText = 'all:initial!important;position:fixed!important;z-index:2147483647!important;width:48px!important;height:64px!important;display:block!important;';
    const shadow = host.attachShadow({ mode: 'closed' });
    const style = document.createElement('style');
    style.textContent = ':host{color-scheme:light}*{box-sizing:border-box}section{width:48px;padding:4px;background:#2563eb;border-radius:10px;box-shadow:0 3px 15px #0003;font:13px system-ui;color:white}button{display:block;width:40px;border:0;color:white;background:transparent;cursor:pointer;font:inherit;padding:4px}button:focus-visible{outline:2px solid white;border-radius:4px}.collect{touch-action:none;user-select:none}.close{font-size:12px;padding:0}.message{position:absolute;right:52px;top:0;width:190px;border-radius:6px;padding:8px;background:#17243a;color:white;font:13px system-ui}';
    const section = document.createElement('section');
    const collect = document.createElement('button');
    collect.className = 'collect'; collect.textContent = '☆ 收藏';
    collect.title = '收藏到 NestHub（可拖动至左右边缘）';
    const close = document.createElement('button');
    close.className = 'close'; close.textContent = '×';
    close.title = '在当前页隐藏'; close.setAttribute('aria-label', '在当前页隐藏收藏浮窗');
    close.addEventListener('click', remove);
    collect.addEventListener('pointerdown', event => {
      if (event.button !== 0) return;
      const startX = event.clientX, startY = event.clientY;
      dragged = false;
      collect.setPointerCapture(event.pointerId);
      const move = next => {
        if (Math.hypot(next.clientX - startX, next.clientY - startY) > 5) dragged = true;
        if (!dragged) return;
        position = { side: next.clientX < window.innerWidth / 2 ? 'left' : 'right', ratio: Math.min(1, Math.max(0, (next.clientY - 24) / Math.max(1, window.innerHeight - 64))) };
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
    collect.addEventListener('click', async () => {
      if (dragged) { dragged = false; return; }
      collect.disabled = true;
      try {
        const result = await send({ type: 'floating-open' });
        if (!result?.ok) throw new Error(result?.error || '打开失败');
      } catch {
        let notice = shadow.querySelector('.message');
        if (!notice) { notice = document.createElement('div'); notice.className = 'message'; notice.setAttribute('role', 'status'); shadow.append(notice); }
        notice.textContent = '请重新加载插件或点击工具栏收藏图标。';
      } finally { collect.disabled = false; }
    });
    section.append(collect, close); shadow.append(style, section);
    document.documentElement.append(host); place();
    window.addEventListener('resize', place);
    chrome.runtime.onMessage.addListener(onMessage);
  })().catch(() => { globalThis.__nesthubFloating = false; });
})();
