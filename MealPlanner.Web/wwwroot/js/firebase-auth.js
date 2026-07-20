// Firebase Auth owns persistence (IndexedDB/localStorage as selected by the SDK),
// rather than application-managed identity flags.
import { initializeApp, getApps } from "https://www.gstatic.com/firebasejs/10.14.1/firebase-app.js";
import { getAuth, browserLocalPersistence, setPersistence, sendSignInLinkToEmail,
    isSignInWithEmailLink, signInWithEmailLink, signOut, onAuthStateChanged } from "https://www.gstatic.com/firebasejs/10.14.1/firebase-auth.js";

let auth;
let ready;
async function ensure(config) {
  if (auth) return;
  if (!config.apiKey) throw new Error("Firebase ApiKey is not configured.");
  const app = getApps()[0] || initializeApp(config);
  auth = getAuth(app);
  await setPersistence(auth, browserLocalPersistence);
  ready = new Promise(resolve => {
    const unsub = onAuthStateChanged(auth, user => { unsub(); resolve(user); });
  });
}
async function serialize(user) {
  if (!user) return null;
  return { uid: user.uid, email: user.email || "", idToken: await user.getIdToken() };
}
window.nxtWeekAuth = {
  init: ensure,
  async currentUser() { await ready; return serialize(auth.currentUser); },
  async sendSignInLink(email, continueUrl) {
    await sendSignInLinkToEmail(auth, email, { url: continueUrl, handleCodeInApp: true });
    // Firebase documents this as the cross-device fallback. It is an email hint, not identity.
    sessionStorage.setItem("nxtweek.emailLinkEmail", email);
  },
  async completeSignIn(email) {
    if (!isSignInWithEmailLink(auth, window.location.href)) return null;
    const supplied = email || sessionStorage.getItem("nxtweek.emailLinkEmail");
    if (!supplied) return { uid: "", email: "", idToken: "" };
    const result = await signInWithEmailLink(auth, supplied, window.location.href);
    sessionStorage.removeItem("nxtweek.emailLinkEmail");
    window.history.replaceState({}, document.title, window.location.pathname);
    return serialize(result.user);
  },
  signOut: () => signOut(auth),
  isEmailLink: () => auth && isSignInWithEmailLink(auth, window.location.href)
};
