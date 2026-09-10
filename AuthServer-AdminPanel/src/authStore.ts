export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

// Credentials live only in memory. Reloading the panel requires a new login.
let tokens: AuthTokens | null = null;
const listeners = new Set<() => void>();
export const getTokens = () => tokens;
export function setTokens(value: AuthTokens | null) {
  tokens = value;
  listeners.forEach(listener => listener());
}
export function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => { listeners.delete(listener); };
}
