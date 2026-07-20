// Firebase Auth owns persistence (IndexedDB/localStorage as selected by the SDK),
// rather than application-managed identity flags.
import { initializeApp, getApps } from "https://www.gstatic.com/firebasejs/10.14.1/firebase-app.js";
import { getAuth, browserLocalPersistence, setPersistence, sendSignInLinkToEmail,
    isSignInWithEmailLink, signInWithEmailLink, signOut, onAuthStateChanged } from "https://www.gstatic.com/firebasejs/10.14.1/firebase-auth.js";

let auth;
let ready;
function ensure(config) {
  console.log("[JS-Auth] ensure() called. auth initialized:", !!auth);
  if (auth) return;
  if (!config.apiKey) throw new Error("Firebase ApiKey is not configured.");
  console.log("[JS-Auth] Initializing Firebase App and Auth...");
  const app = getApps()[0] || initializeApp(config);
  auth = getAuth(app);
  console.log("[JS-Auth] Subscribing to auth.authStateReady()...");
  ready = auth.authStateReady().then(() => {
    console.log("[JS-Auth] authStateReady resolved. Current user:", auth.currentUser ? auth.currentUser.uid : "null");
  });
}
async function serialize(user) {
  if (!user) return null;
  const token = await user.getIdToken();
  console.log("[JS-Auth] serialize() - User UID:", user.uid, "token length:", token ? token.length : 0);
  return { uid: user.uid, email: user.email || "", idToken: token };
}
window.nxtWeekAuth = {
  init: ensure,
  async currentUser() {
    console.log("[JS-Auth] currentUser() called. Awaiting ready...");
    await ready;
    console.log("[JS-Auth] ready resolved. auth.currentUser:", auth.currentUser ? auth.currentUser.uid : "null");
    return serialize(auth.currentUser);
  },
  async sendSignInLink(email, continueUrl) {
    await sendSignInLinkToEmail(auth, email, { url: continueUrl, handleCodeInApp: true });
    // Firebase documents this as the cross-device fallback. It is an email hint, not identity.
    sessionStorage.setItem("nxtweek.emailLinkEmail", email);
  },
  async completeSignIn(email) {
    if (!isSignInWithEmailLink(auth, window.location.href)) return null;
    const supplied = email || sessionStorage.getItem("nxtweek.emailLinkEmail");
    if (!supplied) return { uid: "", email: "", idToken: "" };
    console.log("[JS-Auth] completeSignIn: Setting persistence to browserLocalPersistence...");
    await setPersistence(auth, browserLocalPersistence);
    console.log("[JS-Auth] completeSignIn: Signing in with email link...");
    const result = await signInWithEmailLink(auth, supplied, window.location.href);
    sessionStorage.removeItem("nxtweek.emailLinkEmail");
    window.history.replaceState({}, document.title, window.location.pathname);
    return serialize(result.user);
  },
  signOut: () => signOut(auth),
  isEmailLink: () => auth && isSignInWithEmailLink(auth, window.location.href)
};
