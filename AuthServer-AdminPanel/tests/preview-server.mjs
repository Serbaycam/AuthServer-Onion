// Disposable UI fixture. Not imported into the app or included in the production build.
// Run after npm run build; open http://127.0.0.1:4173. No real accounts or credentials.
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../dist/', import.meta.url));
let signedIn = true;
const roles = [{ id: 'admin', name: 'SuperAdmin' }, { id: 'basic', name: 'Basic' }, { id: 'lab', name: 'LabManager' }];
const catalog = ['Permissions.Laboratories.View', 'Permissions.Laboratories.Create', 'Permissions.Laboratories.Edit', 'Permissions.Laboratories.Delete'];
const permissions = { admin: [], basic: [], lab: [catalog[0]] };
const users = Array.from({ length: 24 }, (_, i) => ({ id: `user-${i}`, firstName: i ? 'Deneme' : 'Test', lastName: i ? String(i) : 'Yönetici', email: i ? `user${i}@example.test` : 'admin@example.test', isActive: true, roles: i ? ['Basic'] : ['SuperAdmin'] }));
const sessions = [{ tokenId: 'current', userEmail: users[0].email, fullName: 'Test Yönetici', ipAddress: '127.0.0.1', createdDate: new Date().toISOString(), expirationDate: new Date(Date.now()+43200000).toISOString(), isCurrentSession: true },
 { tokenId: 'other', userEmail: users[1].email, fullName: 'Deneme 1', ipAddress: '192.0.2.10', createdDate: new Date().toISOString(), expirationDate: new Date(Date.now()+43200000).toISOString(), isCurrentSession: false }];
createServer(async (req, res) => {
  const path = new URL(req.url, 'http://localhost').pathname;
  const reply = (data, status = 200, message) => { res.writeHead(status, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' }); res.end(JSON.stringify({ succeeded: status < 400, data, message })); };
  if (path.startsWith('/api/')) {
    let raw = ''; for await (const chunk of req) raw += chunk;
    const body = raw ? JSON.parse(raw) : {};
    if (path === '/api/admin-session') return reply({ user: signedIn ? { ...users[0], fullName: 'Test Yönetici' } : null, csrfToken: 'fixture-csrf', expiresAt: new Date(Date.now()+43200000).toISOString() });
    if (path === '/api/admin-session/logout') { signedIn = false; return reply({ user: null, csrfToken: 'fixture-csrf', expiresAt: null }); }
    if (!signedIn) return reply(null, 401, 'Oturum sona erdi.');
    if (path === '/api/UserManagement/all-users') return reply(users);
    if (path === '/api/RoleManagement/roles') return reply(roles);
    if (path === '/api/RoleManagement/permissions') {
      if (req.method === 'POST') { permissions[body.roleId] = body.permissions; return reply(true); }
      return reply(catalog);
    }
    if (path.startsWith('/api/RoleManagement/role-permissions/')) return reply((permissions[path.split('/').pop()] || []).map(permissionName => ({ permissionName })));
    if (path === '/api/UserManagement/update-user') { Object.assign(users.find(u => u.id === body.userId), body); return reply(true); }
    if (path === '/api/UserManagement/update-status') {
      if (body.userId === 'user-0') return reply(null, 400, 'Son aktif yönetici pasifleştirilemez.');
      Object.assign(users.find(u => u.id === body.userId), { isActive: body.isActive }); return reply(true);
    }
    if (path === '/api/SessionManagement/active-sessions') return reply(sessions);
    if (path === '/api/SessionManagement/kill-session') { const index = sessions.findIndex(s => s.tokenId === body.tokenId); if (index >= 0) sessions.splice(index, 1); return reply(true); }
    if (path === '/api/Dashboard/stats') return reply({ totalUsers: users.length, activeUsers: users.filter(u => u.isActive).length, totalRoles: roles.length, totalActiveSessions: sessions.length,
      latestActivities: [{ userEmail: users[0].email, action: 'AdminPanelLogin', date: new Date().toISOString(), ipAddress: '127.0.0.1' }] });
    return reply(null, 400, 'Test ortamında bu işlem için örnek hata.');
  }
  const file = resolve(root, '.' + path);
  if (!file.startsWith(root)) { res.writeHead(403); res.end(); return; }
  let data;
  try { data = await readFile(file); } catch { data = await readFile(resolve(root, 'index.html')); }
  const type = { '.css': 'text/css', '.js': 'text/javascript', '.svg': 'image/svg+xml' }[extname(file)] || 'text/html';
  res.writeHead(200, { 'Content-Type': type }); res.end(data);
}).listen(4173, '127.0.0.1', () => console.log('Disposable panel fixture: http://127.0.0.1:4173'));
