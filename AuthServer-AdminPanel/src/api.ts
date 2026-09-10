import { getTokens, setTokens } from './authStore';

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';
let refreshPromise: Promise<string | null> | null = null;

async function refreshTokens(): Promise<string | null> {
  if (refreshPromise) return refreshPromise;
  const previous = getTokens();
  if (!previous) return null;
  refreshPromise = (async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/Auth/refresh-token`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: previous.refreshToken })
      });
      const data = await response.json();
      if (getTokens() !== previous) {
        // A completed rotation must not leave an orphan session after logout.
        if (response.ok && data.succeeded && data.data?.refreshToken) {
          await fetch(`${API_BASE_URL}/Auth/revoke-token`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token: data.data.refreshToken }), keepalive: true
          });
        }
        return null;
      }
      if (!response.ok || !data.succeeded || !data.data?.accessToken || !data.data?.refreshToken) {
        setTokens(null);
        return null;
      }
      setTokens(data.data);
      return data.data.accessToken as string;
    } catch {
      if (getTokens() === previous) setTokens(null);
      return null;
    } finally { refreshPromise = null; }
  })();
  return refreshPromise;
}

export async function fetchWithAuth(endpoint: string, options: RequestInit = {}) {
  const initial = getTokens();
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');
  if (initial) headers.set('Authorization', `Bearer ${initial.accessToken}`);
  let response = await fetch(`${API_BASE_URL}${endpoint}`, { ...options, headers });
  if (response.status === 401 && initial && getTokens()) {
    // A different request may already have rotated the credentials.
    const current = getTokens();
    const accessToken = current !== initial ? current?.accessToken : await refreshTokens();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
      response = await fetch(`${API_BASE_URL}${endpoint}`, { ...options, headers });
      if (response.status === 401 && getTokens()?.accessToken === accessToken) setTokens(null);
    }
  }
  return response;
}

export async function logoutSession() {
  const previous = getTokens();
  setTokens(null);
  if (!previous) return;
  await fetch(`${API_BASE_URL}/Auth/revoke-token`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token: previous.refreshToken }), keepalive: true
  });
}
