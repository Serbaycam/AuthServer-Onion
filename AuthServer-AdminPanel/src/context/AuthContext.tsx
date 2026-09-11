import { createContext, useContext, useEffect, useSyncExternalStore, type ReactNode } from 'react';
import { getSession, subscribe, type AuthState } from '../authStore';
import { loginSession, logoutSession, restoreSession } from '../api';

interface AuthContextType extends AuthState {
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  retry: () => void;
}
const AuthContext = createContext<AuthContextType | undefined>(undefined);
export function AuthProvider({ children }: { children: ReactNode }) {
  const state = useSyncExternalStore(subscribe, getSession, getSession);
  useEffect(() => {
    const refresh = () => { void restoreSession().catch(() => {}); };
    const onVisible = () => { if (document.visibilityState === 'visible') refresh(); };
    const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('authserver-session') : null;
    const notify = () => channel?.postMessage('changed');
    if (channel) channel.onmessage = refresh;
    window.addEventListener('focus', refresh);
    window.addEventListener('admin-session-changed', notify);
    document.addEventListener('visibilitychange', onVisible);
    // Remove credentials left by versions older than the memory-only store.
    try { localStorage.removeItem('auth_tokens'); } catch { /* Storage may be disabled. */ }
    refresh();
    const timer = window.setInterval(onVisible, 60000);
    return () => {
      window.clearInterval(timer); channel?.close();
      window.removeEventListener('focus', refresh);
      window.removeEventListener('admin-session-changed', notify);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);
  return <AuthContext.Provider value={{ ...state, isAuthenticated: !!state.user, login: loginSession, logout: logoutSession,
    retry: () => { void restoreSession().catch(() => {}); } }}>{children}</AuthContext.Provider>;
}
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
