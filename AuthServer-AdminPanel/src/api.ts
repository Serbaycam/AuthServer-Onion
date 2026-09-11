import { getSession, setSession, type SessionData } from './authStore';

// Browser cookies and CSRF protection are intentionally restricted to this origin.
export const API_BASE_URL = '/api';
export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(message: string, status = 0, code?: string) { super(message); this.name = 'ApiError'; this.status = status; this.code = code; }
}
interface Envelope<T> { succeeded?: boolean; data?: T; message?: string; code?: string; errors?: string[] | Record<string, string[]> }
let restorePromise: Promise<void> | null = null;
let revision = 0;
let authMutation = false;

export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'İşlem tamamlanamadı. Tekrar deneyin.';
}
export function isAbort(error: unknown) { return error instanceof DOMException && error.name === 'AbortError'; }

async function send<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json');
  if (options.body) headers.set('Content-Type', 'application/json');
  const signal = options.signal ? AbortSignal.any([options.signal, AbortSignal.timeout(20000)]) : AbortSignal.timeout(20000);
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${endpoint}`, { ...options, signal, headers, credentials: 'same-origin', cache: 'no-store' });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError('Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.');
  }
  let body: Envelope<T>;
  try { body = await response.json(); }
  catch {
    if (response.ok) throw new ApiError('Sunucudan beklenmeyen yanıt geldi. Lütfen tekrar deneyin.', response.status);
    body = {};
  }
  if (!response.ok || body.succeeded === false) {
    const details = body.errors ? (Array.isArray(body.errors) ? body.errors : Object.values(body.errors).flat()).join(' ') : '';
    const fallback = response.status === 401 ? 'Oturumunuz sona erdi. Tekrar giriş yapın.' :
      response.status === 403 ? 'Bu işlem için yetkiniz bulunmuyor.' :
      response.status === 429 ? 'Çok fazla deneme yapıldı. Bir süre bekleyip tekrar deneyin.' : 'İşlem tamamlanamadı.';
    throw new ApiError(details || body.message || fallback, response.status, body.code);
  }
  return body.data as T;
}

export function restoreSession(): Promise<void> {
  if (authMutation) return Promise.resolve();
  if (restorePromise) return restorePromise;
  const started = revision;
  restorePromise = send<SessionData>('/admin-session').then(data => {
    if (started === revision) setSession({ ...data, status: 'ready', error: null });
  }).catch(error => {
    if (started === revision) setSession({ status: getSession().user ? 'ready' : 'error', error: errorMessage(error) });
    throw error;
  }).finally(() => { restorePromise = null; });
  return restorePromise;
}

export async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const method = (options.method || 'GET').toUpperCase();
  const mutation = !['GET', 'HEAD', 'OPTIONS'].includes(method);
  if (mutation && !getSession().csrfToken) await restoreSession();
  const identity = getSession().user?.id;
  const started = revision;
  for (let attempt = 0; ; attempt++) {
    const headers = new Headers(options.headers);
    if (mutation) headers.set('X-CSRF-Token', getSession().csrfToken);
    try { return await send<T>(endpoint, { ...options, headers }); }
    catch (error) {
      if (!(error instanceof ApiError)) throw error;
      // CSRF rejection occurs before the action; retry only that explicit rejection once.
      if (error.code === 'csrf_invalid' && attempt === 0 && !authMutation) {
        await restoreSession();
        if (getSession().user?.id !== identity) throw new ApiError('Oturum değişti. İşlemi yeniden başlatın.', 401);
        continue;
      }
      if (error.status === 401 && started === revision) {
        revision++;
        setSession({ user: null, expiresAt: null, csrfToken: '', status: 'ready', error: null });
      }
      if (error.status === 403 && !authMutation) void restoreSession().catch(() => {});
      throw error;
    }
  }
}

async function changeSession(endpoint: string, body?: object) {
  if (authMutation) throw new ApiError('Önceki oturum işleminin tamamlanmasını bekleyin.');
  // Get a token for the current cookie identity, including after expiry or another tab's logout.
  await restoreSession();
  if (authMutation) throw new ApiError('Önceki oturum işleminin tamamlanmasını bekleyin.');
  authMutation = true;
  revision++;
  try {
    const data = await request<SessionData>(endpoint, { method: 'POST', body: JSON.stringify(body ?? {}) });
    setSession({ ...data, status: 'ready', error: null });
    if (typeof window !== 'undefined') window.dispatchEvent(new Event('admin-session-changed'));
  } finally { authMutation = false; }
}
export const loginSession = (email: string, password: string) => changeSession('/admin-session/login', { email, password });
export const logoutSession = () => changeSession('/admin-session/logout');
