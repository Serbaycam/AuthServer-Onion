import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import ts from 'typescript';

const directory = await mkdtemp(join(tmpdir(), 'admin-session-tests-'));
for (const name of ['authStore', 'api']) {
  const source = await readFile(new URL(`../src/${name}.ts`, import.meta.url), 'utf8');
  const output = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext } }).outputText
    .replace("'./authStore'", "'./authStore.mjs'");
  await writeFile(join(directory, `${name}.mjs`), output);
}
const store = await import(pathToFileURL(join(directory, 'authStore.mjs')).href);
const api = await import(pathToFileURL(join(directory, 'api.mjs')).href);
const originalFetch = globalThis.fetch;
after(async () => { globalThis.fetch = originalFetch; await rm(directory, { recursive: true }); });
const user = { id: 'admin', email: 'admin@example.test', fullName: 'Test Admin', roles: ['SuperAdmin'] };
const session = { user, csrfToken: 'csrf-valid', expiresAt: '2030-01-01T00:00:00Z' };
const json = (body, status = 200) => new Response(JSON.stringify(body), { status });
const envelope = data => json({ succeeded: true, data });
const ready = () => store.setSession({ ...session, status: 'ready', error: null });

test('reload restores server session without bearer credentials or JS token storage', async () => {
  store.setSession({ user: null, csrfToken: '', expiresAt: null, status: 'loading', error: null });
  globalThis.fetch = async (url, options) => {
    assert.equal(url, '/api/admin-session');
    assert.equal(options.credentials, 'same-origin');
    assert.equal(options.cache, 'no-store');
    assert.equal(options.headers.get('Authorization'), null);
    return envelope(session);
  };
  await api.restoreSession();
  assert.equal(store.getSession().user.id, user.id);
  assert.equal(store.getSession().status, 'ready');
});

test('simultaneous bootstrap calls share one request and keep loading until resolved', async () => {
  store.setSession({ user: null, status: 'loading' });
  let calls = 0;
  let finish;
  globalThis.fetch = () => { calls++; return new Promise(resolve => { finish = resolve; }); };
  const first = api.restoreSession();
  const second = api.restoreSession();
  assert.equal(store.getSession().status, 'loading');
  finish(envelope(session));
  await Promise.all([first, second]);
  assert.equal(calls, 1);
  assert.equal(store.getSession().user.id, user.id);
});

test('bootstrap network error is recoverable and never masquerades as signed out', async () => {
  store.setSession({ user: null, status: 'loading' });
  globalThis.fetch = async () => { throw new TypeError('Offline'); };
  await assert.rejects(api.restoreSession());
  assert.equal(store.getSession().status, 'error');
  globalThis.fetch = async () => envelope(session);
  await api.restoreSession();
  assert.equal(store.getSession().user.id, user.id);
});

test('temporary server failure does not clear an established session', async () => {
  ready();
  globalThis.fetch = async () => json({ message: 'Unavailable' }, 503);
  await assert.rejects(api.restoreSession());
  assert.equal(store.getSession().user.id, user.id);
  assert.equal(store.getSession().status, 'ready');
});

test('cookie mutation sends CSRF and retries only an explicit pre-action rejection once', async () => {
  ready();
  let mutations = 0;
  globalThis.fetch = async (url, options) => {
    if (url === '/api/admin-session') return envelope({ ...session, csrfToken: 'new-csrf' });
    mutations++;
    if (mutations === 1) return json({ succeeded: false, code: 'csrf_invalid' }, 400);
    assert.equal(options.headers.get('X-CSRF-Token'), 'new-csrf');
    return envelope(true);
  };
  assert.equal(await api.request('/role', { method: 'POST', body: '{}' }), true);
  assert.equal(mutations, 2);
});

test('a server-side mutation failure is shown and is not automatically repeated', async () => {
  ready(); let calls = 0;
  globalThis.fetch = async () => { calls++; return json({ succeeded: false, message: 'İşlem kaydedilemedi.' }, 500); };
  await assert.rejects(api.request('/role', { method: 'POST', body: '{}' }), /İşlem kaydedilemedi/);
  assert.equal(calls, 1);
  assert.equal(store.getSession().user.id, user.id);
});

test('validation problem details and legacy HTTP 200 failures are surfaced', async () => {
  ready();
  globalThis.fetch = async () => json({ errors: { Email: ['Geçerli e-posta girin.'] } }, 400);
  await assert.rejects(api.request('/user', { method: 'POST', body: '{}' }), /Geçerli e-posta/);
  globalThis.fetch = async () => json({ succeeded: false, message: 'Son yönetici kapatılamaz.' });
  await assert.rejects(api.request('/user', { method: 'POST', body: '{}' }), /Son yönetici/);
});

test('logout clears state only after successful server revocation', async () => {
  ready();
  globalThis.fetch = async url => url === '/api/admin-session' ? envelope(session) : json({ message: 'Connection failed' }, 503);
  await assert.rejects(api.logoutSession());
  assert.equal(store.getSession().user.id, user.id);
  globalThis.fetch = async url => url === '/api/admin-session' ? envelope(session) : envelope({ user: null, csrfToken: 'anonymous', expiresAt: null });
  await api.logoutSession();
  assert.equal(store.getSession().user, null);
});

test('401 clears session, without refresh loops or retrying the protected action', async () => {
  ready(); let calls = 0;
  globalThis.fetch = async () => { calls++; return json({}, 401); };
  await assert.rejects(api.request('/users'), /Oturumunuz sona erdi/);
  assert.equal(store.getSession().user, null);
  assert.equal(calls, 1);
});

test('identity change during CSRF renewal never replays a mutation as the other account', async () => {
  ready(); let mutations = 0;
  globalThis.fetch = async url => {
    if (url === '/api/admin-session') return envelope({ ...session, user: { ...user, id: 'another' } });
    mutations++; return json({ code: 'csrf_invalid' }, 400);
  };
  await assert.rejects(api.request('/users', { method: 'POST', body: '{}' }), /Oturum değişti/);
  assert.equal(mutations, 1);
});
