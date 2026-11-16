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

// Swipe a bottom sheet down to close it, as on a phone. The drag only starts when the sheet (and whatever
// inside it is being touched) is scrolled to the top, so scrolling long content still works. Horizontal
// swipes (day strips, chip rows) and form fields are left alone.
(function sheetSwipeToClose() {
  let drag = null;
  const scrolledToTop = (el, sheet) => {
    for (let n = el; n && n !== sheet.parentElement; n = n.parentElement)
      if (n.scrollTop > 0) return false;
    return true;
  };
  document.addEventListener('touchstart', e => {
    const sheet = e.target.closest?.('.nxt-sheet');
    if (!sheet || e.touches.length !== 1 || e.target.closest('input, textarea, select, [role="slider"]')) return;
    if (!scrolledToTop(e.target, sheet)) return;
    drag = { sheet, x: e.touches[0].clientX, y: e.touches[0].clientY, t: Date.now(), dy: 0, active: false };
  }, { passive: true });
  document.addEventListener('touchmove', e => {
    if (!drag) return;
    const dx = e.touches[0].clientX - drag.x, dy = e.touches[0].clientY - drag.y;
    if (!drag.active) {
      if (Math.abs(dx) > Math.abs(dy) || dy < 6) { if (Math.abs(dx) > 8 || dy < -6) drag = null; return; }
      drag.active = true;
      drag.sheet.style.transition = 'none';
    }
    drag.dy = Math.max(0, dy);
    drag.sheet.style.transform = `translateY(${drag.dy}px)`;
    if (e.cancelable) e.preventDefault();
  }, { passive: false });
  const end = () => {
    if (!drag) return;
    const { sheet, dy, t, active } = drag;
    drag = null;
    if (!active) return;
    const fast = dy / Math.max(1, Date.now() - t) > 0.6;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (dy > Math.min(140, sheet.offsetHeight * 0.3) || (fast && dy > 40)) {
      sheet.style.transition = reduce ? 'none' : 'transform .18s ease-in';
      sheet.style.transform = `translateY(${sheet.offsetHeight}px)`;
      setTimeout(() => sheet.closest('.nxt-sheet-backdrop')?.click(), reduce ? 0 : 170);
    } else {
      sheet.style.transition = reduce ? 'none' : 'transform .2s ease-out';
      sheet.style.transform = '';
    }
  };
  document.addEventListener('touchend', end);
  document.addEventListener('touchcancel', end);
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

// A square JPEG of at most `size` px from any image data URL: the centre is kept, the rest cropped.
window.nxtweek.squareJpeg = (dataUrl, size) => new Promise(resolve => {
  const img = new Image();
  img.onload = () => {
    const side = Math.min(img.width, img.height);
    const out = Math.max(1, Math.min(size, side));
    const canvas = document.createElement('canvas');
    canvas.width = out; canvas.height = out;
    canvas.getContext('2d').drawImage(img, (img.width - side) / 2, (img.height - side) / 2, side, side, 0, 0, out, out);
    resolve(canvas.toDataURL('image/jpeg', 0.8));
  };
  img.onerror = () => resolve(null);
  img.src = dataUrl;
});

// The Week day strip: one page per week in a horizontal scroller that snaps. Reports which week is showing
// once a swipe settles; scrolls to a week when the page changes it. RTL scrollers count leftwards (negative).
window.nxtweek.weekPager = (() => {
  const watched = new Map();
  let next = 1;
  const index = el => Math.round(Math.abs(el.scrollLeft) / Math.max(1, el.clientWidth));
  return {
    watch(el, dotnetRef) {
      const id = next++;
      let timer = 0, last = -1;
      const settle = () => {
        clearTimeout(timer);
        timer = setTimeout(() => {
          const i = index(el);
          if (i !== last && el.dataset.programmatic !== '1') dotnetRef.invokeMethodAsync('WeekSettled', i).catch(() => {});
          last = i;
          el.dataset.programmatic = '';
        }, 120);
      };
      el.addEventListener('scroll', settle, { passive: true });
      watched.set(id, () => { el.removeEventListener('scroll', settle); clearTimeout(timer); });
      return id;
    },
    show(el, i, smooth) {
      const rtl = getComputedStyle(el).direction === 'rtl';
      const left = (rtl ? -1 : 1) * i * el.clientWidth;
      if (Math.abs(el.scrollLeft - left) < 2) return;
      el.dataset.programmatic = '1';
      const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      el.scrollTo({ left, behavior: smooth && !reduce ? 'smooth' : 'auto' });
    },
    unwatch(id) { watched.get(id)?.(); watched.delete(id); }
  };
})();
