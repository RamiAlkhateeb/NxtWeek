window.nxtweek = {
  downloadJson(content, filename) {
    const blob = new Blob([content], { type: 'application/json;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    URL.revokeObjectURL(link.href);
  },
  shouldShowIosInstallGuide() {
    const storageKey = 'nxtweek.iosInstallGuideSeen';
    const isIos = this.isIosDevice();
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches
      || window.navigator.standalone === true;

    return isIos && !isStandalone && localStorage.getItem(storageKey) !== 'true';
  },
  isIosDevice() {
    const userAgent = navigator.userAgent || '';
    return /iPad|iPhone|iPod/.test(userAgent)
      || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  },
  markIosInstallGuideSeen() {
    localStorage.setItem('nxtweek.iosInstallGuideSeen', 'true');
  },
  shouldShowFirstLaunchWizard() {
    const storageKey = 'nxtweek.firstLaunchWizardSeen';
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches
      || window.navigator.standalone === true;
    // Already installed / running as an app: no point showing install steps.
    if (isStandalone) return false;
    return localStorage.getItem(storageKey) !== 'true';
  },
  markFirstLaunchWizardSeen() {
    localStorage.setItem('nxtweek.firstLaunchWizardSeen', 'true');
  },
  goBack(fallback) {
    if (window.history.length > 1) window.history.back();
    else window.location.href = fallback || '/';
  },
  // Opens the camera/gallery picker and resolves with a downscaled JPEG data URL (or null if cancelled).
  pickPhoto(maxSize) {
    return new Promise(resolve => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';
      input.onchange = () => {
        const file = input.files && input.files[0];
        if (!file) { resolve(null); return; }
        const url = URL.createObjectURL(file);
        const img = new Image();
        img.onload = () => {
          const scale = Math.min(1, maxSize / Math.max(img.width, img.height));
          const canvas = document.createElement('canvas');
          canvas.width = Math.max(1, Math.round(img.width * scale));
          canvas.height = Math.max(1, Math.round(img.height * scale));
          canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
          URL.revokeObjectURL(url);
          resolve(canvas.toDataURL('image/jpeg', 0.78));
        };
        img.onerror = () => { URL.revokeObjectURL(url); resolve(null); };
        img.src = url;
      };
      input.addEventListener('cancel', () => resolve(null));
      input.click();
    });
  },
  scrollToId(elementId, block = 'center') {
    const element = document.getElementById(elementId);
    if (element) element.scrollIntoView({ behavior: 'smooth', block, inline: 'nearest' });
  },
  // Calls dotnetRef.SetCompact(true) once the element has mostly scrolled away, false when it is back.
  // Returns an id for unobserveCompact, so a page being disposed never disconnects its successor.
  observeCompact(elementId, dotnetRef) {
    const element = document.getElementById(elementId);
    if (!element || !('IntersectionObserver' in window)) return 0;
    // Compact once less than half of the panel is left on screen above the fold.
    const observer = new IntersectionObserver(([entry]) => {
      dotnetRef.invokeMethodAsync('SetCompact', entry.intersectionRatio < 0.5 && entry.boundingClientRect.top < 0);
    }, { threshold: [0, 0.25, 0.5, 0.75, 1] });
    observer.observe(element);
    this._compactObservers ??= new Map();
    const id = (this._compactId = (this._compactId || 0) + 1);
    this._compactObservers.set(id, observer);
    return id;
  },
  unobserveCompact(id) {
    this._compactObservers?.get(id)?.disconnect();
    this._compactObservers?.delete(id);
  }
};

// Reusable, storage-safe deployed-version check. It only reads/writes one
// localStorage key and never clears browser site data.
window.nxtweek.checkVersion = async function checkVersion(options = {}) {
  const versionUrl = options.versionUrl || '/version.json';
  const storageKey = options.storageKey || 'nxtweek.app_version';
  const timeoutMs = options.timeoutMs || 2500;

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    const separator = versionUrl.includes('?') ? '&' : '?';
    const response = await fetch(`${versionUrl}${separator}t=${Date.now()}`, {
      cache: 'no-store',
      headers: { 'Cache-Control': 'no-cache' },
      signal: controller.signal
    });
    clearTimeout(timeout);

    if (!response.ok) return;
    const payload = await response.json();
    const version = payload && payload.version;
    if (typeof version !== 'string' || !version) return;

    const storedVersion = localStorage.getItem(storageKey);
    if (!storedVersion) {
      localStorage.setItem(storageKey, version);
      return;
    }
    if (storedVersion === version) return;

    localStorage.setItem(storageKey, version);
    await updateServiceWorker();

    // `true` is retained for older browsers. Modern browsers reload normally,
    // while the cache-busted version file plus updated service worker refresh
    // the document/static-asset path without clearing site data.
    window.location.reload(true);
  } catch {
    // Offline, timeout, malformed response, or 404: do not block startup.
  }
};

async function updateServiceWorker() {
  if (!('serviceWorker' in navigator)) return;
  try {
    const registration = await navigator.serviceWorker.getRegistration();
    if (!registration) return;
    const change = new Promise(resolve => {
      const timer = setTimeout(resolve, 1500);
      navigator.serviceWorker.addEventListener('controllerchange', () => {
        clearTimeout(timer);
        resolve();
      }, { once: true });
    });
    await registration.update();
    await change;
  } catch {
    // A regular reload is still safe if the service worker cannot update.
  }
}

// Bottom sheets (Nxt.UI NxtBottomSheet): Esc closes the top one and focus moves into a sheet when it
// opens, so keyboard and screen-reader users land on its content. Belongs in Nxt.UI; kept here until then.
(function sheetKeyboardSupport() {
  document.addEventListener('keydown', e => {
    if (e.key !== 'Escape') return;
    const backdrops = document.querySelectorAll('.nxt-sheet-backdrop');
    const top = backdrops[backdrops.length - 1];
    if (top) { e.preventDefault(); top.click(); }
  });
  const focusNewSheets = () => {
    document.querySelectorAll('.nxt-sheet:not([data-focused])').forEach(sheet => {
      sheet.setAttribute('data-focused', '');
      if (!sheet.hasAttribute('tabindex')) sheet.setAttribute('tabindex', '-1');
      sheet.focus({ preventScroll: true });
    });
  };
  new MutationObserver(focusNewSheets).observe(document.documentElement, { childList: true, subtree: true });
})();

// Live updates without polling: Firebase's REST streaming (Server-Sent Events) on one narrow path.
// The callback runs when the data there changes (not for the first snapshot, nor for a reconnect that
// brings the same data), and when the tab becomes visible again after being hidden.
(function liveWatch() {
  const watches = new Map();
  let next = 1;
  window.nxtweek.watch = (url, dotnetRef, method) => {
    const id = next++;
    const w = { last: undefined, timer: 0, source: null };
    const fire = () => { clearTimeout(w.timer); w.timer = setTimeout(() => dotnetRef.invokeMethodAsync(method).catch(() => {}), 250); };
    const onPut = e => {
      let msg; try { msg = JSON.parse(e.data); } catch { return; }
      if (msg.path === '/') {
        const text = JSON.stringify(msg.data);
        const first = w.last === undefined;
        const same = text === w.last;
        w.last = text;
        if (first || same) return;
      } else {
        w.last = null;
      }
      fire();
    };
    try {
      w.source = new EventSource(url);
      w.source.addEventListener('put', onPut);
      w.source.addEventListener('patch', () => { w.last = null; fire(); });
    } catch { /* no streaming: the visibility reload below still works */ }
    w.visible = () => { if (document.visibilityState === 'visible') fire(); };
    document.addEventListener('visibilitychange', w.visible);
    watches.set(id, w);
    return id;
  };
  window.nxtweek.unwatch = id => {
    const w = watches.get(id);
    if (!w) return;
    clearTimeout(w.timer);
    w.source?.close();
    document.removeEventListener('visibilitychange', w.visible);
    watches.delete(id);
  };
})();
