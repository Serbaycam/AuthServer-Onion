export interface SessionUser { id: string; email: string; fullName: string; roles: string[] }
export interface SessionData { user: SessionUser | null; csrfToken: string; expiresAt: string | null }
export interface AuthState extends SessionData { status: 'loading' | 'ready' | 'error'; error: string | null }
let state: AuthState = { user: null, csrfToken: '', expiresAt: null, status: 'loading', error: null };
const listeners = new Set<() => void>();
export const getSession = () => state;
export function setSession(value: Partial<AuthState>) {
  state = { ...state, ...value };
  listeners.forEach(listener => listener());
}
export function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
