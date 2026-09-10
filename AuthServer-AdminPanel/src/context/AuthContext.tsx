import { createContext, useContext, useSyncExternalStore, type ReactNode } from 'react';
import { getTokens, setTokens, subscribe, type AuthTokens } from '../authStore';
import { logoutSession } from '../api';

interface User { email: string; roles: string[] }
interface AuthContextType {
  isAuthenticated: boolean;
  tokens: AuthTokens | null;
  user: User | null;
  login: (tokens: AuthTokens) => void;
  logout: () => void;
}
const AuthContext = createContext<AuthContextType | undefined>(undefined);

function decodeUser(token: string): User | null {
  try {
    const encoded = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(encoded), c => c.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes));
    const roles = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    return { email: payload.email || '', roles: Array.isArray(roles) ? roles : roles ? [roles] : [] };
  } catch { return null; }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const tokens = useSyncExternalStore(subscribe, getTokens, () => null);
  const user = tokens ? decodeUser(tokens.accessToken) : null;
  const logout = () => { void logoutSession().catch(() => { /* Local credentials already cleared. */ }); };
  return <AuthContext.Provider value={{ tokens, user, isAuthenticated: !!tokens && !!user, login: setTokens, logout }}>
    {children}
  </AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within an AuthProvider');
  return context;
}
