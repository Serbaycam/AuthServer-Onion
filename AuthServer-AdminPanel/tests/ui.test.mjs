import { test, beforeEach, afterEach, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, rm, readdir, mkdir } from 'node:fs/promises';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import ts from 'typescript';
import { JSDOM } from 'jsdom';

// Compile the actual screens into an isolated temporary module graph; no copied component logic.
const src = fileURLToPath(new URL('../src/', import.meta.url));
const directory = await mkdtemp(fileURLToPath(new URL('.runtime-', import.meta.url)));
async function compile(folder) {
  for (const item of await readdir(folder, { withFileTypes: true })) {
    const path = join(folder, item.name);
    if (item.isDirectory()) { await compile(path); continue; }
    if (!/\.tsx?$/.test(item.name) || item.name.endsWith('.d.ts')) continue;
    const output = ts.transpileModule(await readFile(path, 'utf8'), { compilerOptions: {
      target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX
    } }).outputText.replace(/import ['"][^'"]+\.css['"];?/g, '')
      .replace(/from (['"])(\.[^'"]+)\1/g, (_, quote, name) => `from ${quote}${name.replace(/\.tsx?$/, '')}.mjs${quote}`);
    const destination = join(directory, relative(src, path).replace(/\.tsx?$/, '.mjs'));
    await mkdir(dirname(destination), { recursive: true }); await writeFile(destination, output);
  }
}
await compile(src);
const dom = new JSDOM('<!doctype html><div id="root"></div>', { url: 'https://panel.example.test/users' });
for (const key of ['window', 'document', 'HTMLElement', 'HTMLDialogElement', 'localStorage', 'Event', 'MouseEvent']) globalThis[key] = dom.window[key];
globalThis.IS_REACT_ACT_ENVIRONMENT = true;
globalThis.BroadcastChannel = undefined;
// jsdom has no layout/top layer; native dialog behavior is outside this DOM regression suite.
HTMLDialogElement.prototype.showModal = function () { this.open = true; };
HTMLDialogElement.prototype.close = function () { this.open = false; };
const { createElement: h, act } = await import('react');
const { createRoot } = await import('react-dom/client');
const { MemoryRouter, Routes, Route } = await import('react-router-dom');
const moduleAt = name => import(pathToFileURL(join(directory, `${name}.mjs`)).href);
const { AuthProvider } = await moduleAt('context/AuthContext');
const { default: Layout } = await moduleAt('components/Layout');
const { default: Login } = await moduleAt('pages/Login');
const { default: Users } = await moduleAt('pages/Users');
const { default: Roles } = await moduleAt('pages/Roles');
const { default: Sessions } = await moduleAt('pages/Sessions');
const store = await moduleAt('authStore');
const user = { id: 'admin', email: 'admin@example.test', fullName: 'Test Yönetici', roles: ['SuperAdmin'] };
const session = { user, csrfToken: 'csrf-fixture', expiresAt: '2030-01-01T00:00:00Z' };
const roleList = [{ id: 'admin', name: 'SuperAdmin' }, { id: 'basic', name: 'Basic' }, { id: 'lab', name: 'LabManager' }];
const users = Array.from({ length: 24 }, (_, index) => ({ id: `user-${index}`, firstName: `Ad${index}`, lastName: 'Test', email: `user${index}@example.test`, roles: ['Basic'], isActive: true }));
const json = (data, status = 200) => new Response(JSON.stringify({ succeeded: status === 200, data, message: status === 200 ? null : data }), { status });
let root;
beforeEach(() => { store.setSession({ ...session, status: 'ready', error: null }); root = createRoot(document.getElementById('root')); });
afterEach(async () => { await act(async () => root.unmount()); });
after(async () => { dom.window.close(); await rm(directory, { recursive: true }); });
const render = async element => act(async () => { root.render(element); });
const click = async element => { assert.ok(element, 'Expected control exists'); await act(async () => element.click()); };
const button = text => [...document.querySelectorAll('button')].find(element => element.textContent === text);
const change = async (input, value) => act(async () => {
  Object.getOwnPropertyDescriptor(dom.window.HTMLInputElement.prototype, 'value').set.call(input, value);
  input.dispatchEvent(new Event('input', { bubbles: true }));
});
function protectedApp() {
  return h(MemoryRouter, { initialEntries: ['/users'] }, h(AuthProvider, null, h(Routes, null,
    h(Route, { path: '/login', element: h(Login) }),
    h(Route, { element: h(Layout) }, h(Route, { path: '/users', element: h('h1', null, 'Korumalı kullanıcı ekranı') })) )));
}

test('protected deep link waits for cookie bootstrap and never flashes the login form', async () => {
  store.setSession({ user: null, csrfToken: '', expiresAt: null, status: 'loading', error: null });
  let finish;
  globalThis.fetch = () => new Promise(resolve => { finish = resolve; });
  await render(protectedApp());
  assert.match(document.body.textContent, /Oturumunuz yükleniyor/);
  assert.equal(document.querySelector('#login-email'), null);
  await act(async () => finish(json(session)));
  assert.match(document.body.textContent, /Korumalı kullanıcı ekranı/);
  assert.equal(document.querySelector('#login-email'), null);
});

test('failed bootstrap shows retry and recovers the requested protected page', async () => {
  store.setSession({ user: null, status: 'loading' });
  globalThis.fetch = async () => { throw new TypeError('Offline'); };
  await render(protectedApp());
  assert.match(document.querySelector('[role="alert"]').textContent, /Sunucuya ulaşılamadı/);
  assert.equal(document.querySelector('#login-email'), null);
  globalThis.fetch = async () => json(session);
  await click(button('Tekrar dene'));
  assert.match(document.body.textContent, /Korumalı kullanıcı ekranı/);
});

test('user edit sends the documented PUT contract, preserves form on failure and refreshes on success', async () => {
  const entries = structuredClone(users);
  let reject = true;
  const writes = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/all-users')) return json(entries);
    if (url.endsWith('/roles')) return json(roleList);
    assert.equal(url, '/api/UserManagement/update-user');
    assert.equal(options.method, 'PUT');
    assert.equal(options.headers.get('X-CSRF-Token'), 'csrf-fixture');
    const body = JSON.parse(options.body); writes.push(body);
    if (reject) return json('E-posta kullanımda.', 400);
    Object.assign(entries[0], body); return json(true);
  };
  await render(h(Users));
  await click(document.querySelector('[aria-label="user0@example.test: bilgileri düzenle"]'));
  await change(document.querySelector('dialog input'), 'Yeni Ad');
  await act(async () => document.querySelector('dialog form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
  assert.match(document.querySelector('dialog [role="alert"]').textContent, /E-posta kullanımda/);
  assert.equal(document.querySelector('dialog input').value, 'Yeni Ad');
  assert.equal(writes.length, 1);
  reject = false;
  await act(async () => document.querySelector('dialog form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
  assert.equal(document.querySelector('dialog'), null);
  assert.equal(writes[1].userId, 'user-0');
  assert.equal(writes[1].firstName, 'Yeni Ad');
  assert.match(document.querySelector('tbody').textContent, /Yeni Ad/);
});

test('user search and pagination constrain displayed rows and reset page for a new search', async () => {
  globalThis.fetch = async url => json(url.endsWith('/roles') ? roleList : users);
  await render(h(Users));
  assert.equal(document.querySelectorAll('tbody tr').length, 20);
  await click(button('Sonraki'));
  assert.equal(document.querySelectorAll('tbody tr').length, 4);
  await change(document.querySelector('input[type="search"]'), 'user0@');
  assert.equal(document.querySelectorAll('tbody tr').length, 1);
  assert.match(document.querySelector('tbody').textContent, /user0@example.test/);
  assert.match(document.querySelector('.pagination').textContent, /Sayfa 1 \/ 1/);
});

test('late role-permission responses cannot overwrite a newly selected role', async () => {
  let finishOld;
  globalThis.fetch = async url => {
    if (url.endsWith('/roles')) return json(roleList);
    if (url.endsWith('/permissions')) return json(['Permissions.Lab.View']);
    if (url.endsWith('/lab')) return new Promise(resolve => { finishOld = resolve; });
    if (url.endsWith('/basic')) return json([]);
    throw new Error(`Unexpected fixture endpoint ${url}`);
  };
  await render(h(Roles));
  await click(button('LabManager'));
  await click(button('Basic'));
  assert.equal(document.querySelector('.permissions-panel input').checked, false);
  await act(async () => finishOld(json([{ permissionName: 'Permissions.Lab.View' }])));
  assert.match(document.querySelector('.permissions-panel h2').textContent, /Basic/);
  assert.equal(document.querySelector('.permissions-panel input').checked, false);
});

test('session termination targets the selected session, retains errors and protects the current session', async () => {
  const entries = [{ tokenId: 'current', fullName: 'Me', userEmail: 'me@example.test', ipAddress: '127.0.0.1', createdDate: '2026-09-11', expirationDate: '2026-09-12', isCurrentSession: true },
    { tokenId: 'other', fullName: 'Other', userEmail: 'other@example.test', ipAddress: '192.0.2.10', createdDate: '2026-09-11', expirationDate: '2026-09-12', isCurrentSession: false }];
  let reject = true;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('/active-sessions')) return json(entries);
    assert.deepEqual(JSON.parse(options.body), { tokenId: 'other' });
    if (reject) return json('Geçici hata.', 503);
    entries.splice(1, 1); return json(true);
  };
  await render(h(Sessions));
  assert.equal(document.querySelector('[aria-label="me@example.test: oturumu kapat"]'), null);
  await click(document.querySelector('[aria-label="other@example.test: oturumu kapat"]'));
  await click(button('Oturumu kapat'));
  assert.match(document.querySelector('dialog [role="alert"]').textContent, /Geçici hata/);
  reject = false; await click(button('Oturumu kapat'));
  assert.equal(document.querySelector('dialog'), null);
  assert.equal(document.querySelectorAll('tbody tr').length, 1);
});
