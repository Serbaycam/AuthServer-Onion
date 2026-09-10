import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import ts from 'typescript';

// Execute the actual TypeScript modules with no browser or new test dependencies.
const directory = await mkdtemp(join(tmpdir(), 'auth-tests-'));
for (const name of ['authStore', 'api']) {
  const source = await readFile(new URL(`../src/${name}.ts`, import.meta.url), 'utf8');
  const output = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2023, module: ts.ModuleKind.ESNext } }).outputText
    .replace("'./authStore'", "'./authStore.mjs'").replace('import.meta.env.VITE_API_BASE_URL', 'undefined');
  await writeFile(join(directory, `${name}.mjs`), output);
}
const store = await import(pathToFileURL(join(directory, 'authStore.mjs')).href);
const api = await import(pathToFileURL(join(directory, 'api.mjs')).href);
const originalFetch = globalThis.fetch;
after(async () => { globalThis.fetch = originalFetch; await rm(directory, { recursive: true }); });
const json = (body, status = 200) => new Response(JSON.stringify(body), { status });
const initial = { accessToken: 'old-access', refreshToken: 'old-refresh' };
const next = { accessToken: 'new-access', refreshToken: 'new-refresh' };

test('parallel 401 responses share one refresh and retry with new credentials', async () => {
  store.setTokens(initial);
  let rotations = 0;
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('refresh-token')) {
      rotations++;
      await new Promise(resolve => setTimeout(resolve, 10));
      return json({ succeeded: true, data: next });
    }
    return options.headers.get('Authorization') === 'Bearer new-access' ? json({ ok: true }) : json({}, 401);
  };
  const results = await Promise.all([api.fetchWithAuth('/one'), api.fetchWithAuth('/two')]);
  assert.equal(rotations, 1);
  assert.ok(results.every(result => result.ok));
  assert.equal(store.getTokens().refreshToken, next.refreshToken);
});

test('logout during rotation cannot resurrect credentials and revokes the orphan successor', async () => {
  store.setTokens(initial);
  let finishRotation;
  let rotationStarted;
  const started = new Promise(resolve => { rotationStarted = resolve; });
  const revoked = [];
  globalThis.fetch = async (url, options) => {
    if (url.endsWith('refresh-token')) {
      rotationStarted();
      return new Promise(resolve => { finishRotation = resolve; });
    }
    if (url.endsWith('revoke-token')) { revoked.push(JSON.parse(options.body).token); return json({ succeeded: true }); }
    return json({}, 401);
  };
  const pending = api.fetchWithAuth('/one');
  await started;
  await api.logoutSession();
  finishRotation(json({ succeeded: true, data: next }));
  await pending;
  assert.equal(store.getTokens(), null);
  assert.deepEqual(revoked, ['old-refresh', 'new-refresh']);
});

test('failed refresh clears the session and does not loop', async () => {
  store.setTokens(initial);
  let requests = 0;
  globalThis.fetch = async () => { requests++; return json({}, 401); };
  const response = await api.fetchWithAuth('/one');
  assert.equal(response.status, 401);
  assert.equal(requests, 2);
  assert.equal(store.getTokens(), null);
});
