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
  async captureAndShare(elementId) {
    const element = document.getElementById(elementId);
    if (!element || typeof window.html2canvas !== 'function') {
      throw new Error('The week preview could not be created.');
    }

    const canvas = await window.html2canvas(element, { backgroundColor: '#FFF9F2', scale: 2 });
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    if (!blob) throw new Error('The week image could not be created.');

    const file = new File([blob], 'my-week.png', { type: 'image/png' });
    if (navigator.canShare?.({ files: [file] })) {
      try {
        await navigator.share({ files: [file], title: 'طبخات الأسبوع', text: 'طبخات الأسبوع' });
        return 'shared';
      } catch (error) {
        if (error?.name === 'AbortError') return 'cancelled';
        throw error;
      }
    }

    const url = URL.createObjectURL(blob);
    try {
      const link = document.createElement('a');
      link.href = url;
      link.download = 'طبخات-الأسبوع.png';
      link.click();
      return 'downloaded';
    } finally {
      setTimeout(() => URL.revokeObjectURL(url), 0);
    }
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
