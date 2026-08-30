(() => {
  window.nxtweek ??= {};
  const config = window.nxtweekFirebaseConfig ?? {};
  let auth, db, sdk, error = '';
  const ready = (async () => {
    if (!config.apiKey || !config.appId) return null;
    try {
      const [appSdk, authSdk, databaseSdk] = await Promise.all([
        import('https://www.gstatic.com/firebasejs/12.18.0/firebase-app.js'),
        import('https://www.gstatic.com/firebasejs/12.18.0/firebase-auth.js'),
        import('https://www.gstatic.com/firebasejs/12.18.0/firebase-database.js')
      ]);
      sdk = { ...authSdk, ...databaseSdk };
      const app = appSdk.getApps().length ? appSdk.getApp() : appSdk.initializeApp(config);
      auth = authSdk.getAuth(app); db = databaseSdk.getDatabase(app);
      return auth;
    } catch (e) { error = e?.message || 'تعذر تهيئة Firebase.'; return null; }
  })();
  const user = async current => current ? ({ uid: current.uid, authUid: current.uid, email: current.email || '', displayName: current.email || 'مستخدم', idToken: await current.getIdToken(), isGuest: false }) : null;
  const requireAuth = async () => { await ready; if (auth) return auth; throw new Error(error || 'أضف Firebase Web App configuration أولاً.'); };
  const pending = () => auth && sdk.isSignInWithEmailLink(auth, window.location.href);
  const pendingUsername = () => { const url = new URL(window.location.href); const direct = url.searchParams.get('linkUsername'); if (direct) return direct; const continueUrl = url.searchParams.get('continueUrl'); return continueUrl ? new URL(continueUrl).searchParams.get('linkUsername') || '' : ''; };
  window.nxtweek.emailAuth = {
    async getCurrentUser() { await ready; return user(auth?.currentUser); },
    async sendLink(email, username) { const active = await requireAuth(); const url = `${window.location.origin}/?linkUsername=${encodeURIComponent(username)}`; await sdk.sendSignInLinkToEmail(active, email, { url, handleCodeInApp: true }); localStorage.setItem('nxtweek.emailForSignIn', email); },
    async hasPendingLink() { await ready; return Boolean(pending()); },
    async getPendingUsername() { await ready; return pendingUsername(); },
    async completeStoredLink() { await ready; const email = localStorage.getItem('nxtweek.emailForSignIn'); return email && pending() ? this.completeLink(email) : null; },
    async completeLink(email) { const active = await requireAuth(); if (!pending()) return null; const result = await sdk.signInWithEmailLink(active, email, window.location.href); localStorage.removeItem('nxtweek.emailForSignIn'); return user(result.user); },
    async signOut() { await ready; if (auth) await sdk.signOut(auth); },
    async subscribeToUser(userKey, dotnetRef) { await ready; if (!db) return false; this.unsubscribe(); this._unsubscribe = sdk.onValue(sdk.ref(db, `users/${userKey}`), () => dotnetRef.invokeMethodAsync('OnRemoteUserChanged')); return true; },
    unsubscribe() { if (this._unsubscribe) { this._unsubscribe(); this._unsubscribe = null; } }
  };
})();
