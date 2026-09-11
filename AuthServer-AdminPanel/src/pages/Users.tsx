import { useState, type FormEvent } from 'react';
import { UserPlus, Pencil, KeyRound, Shield, LogOut, Power } from 'lucide-react';
import { request, errorMessage } from '../api';
import { useResource } from '../hooks/useResource';
import { ErrorNotice, Loading, SuccessNotice } from '../components/Feedback';
import { Modal } from '../components/Modal';
import { Pagination } from '../components/Pagination';
import type { Role, User } from '../types';

type Action = { kind: 'create' } | { kind: 'edit' | 'password' | 'roles' | 'status' | 'logout'; user: User };

export default function Users() {
  const users = useResource<User[]>('/UserManagement/all-users');
  const roles = useResource<Role[]>('/RoleManagement/roles');
  const [action, setAction] = useState<Action | null>(null);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('all');
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState('');
  const filtered = (users.data || []).filter(user =>
    `${user.firstName} ${user.lastName} ${user.email} ${user.roles.join(' ')}`.toLocaleLowerCase('tr').includes(search.toLocaleLowerCase('tr')) &&
    (status === 'all' || user.isActive === (status === 'active')));
  const currentPage = Math.min(page, Math.max(1, Math.ceil(filtered.length / 20)));
  const complete = (text: string) => { setAction(null); setMessage(text); users.reload(); };
  return <div className="fade-in">
    <div className="page-header split"><div><h1>Kullanıcılar</h1><p>Hesapları, rolleri ve erişimleri yönetin.</p></div>
      <button className="btn btn-primary" onClick={() => { setMessage(''); setAction({ kind: 'create' }); }} disabled={!roles.data}><UserPlus size={18} />Kullanıcı ekle</button></div>
    <SuccessNotice message={message} /><ErrorNotice message={users.error} retry={users.reload} /><ErrorNotice message={roles.error} retry={roles.reload} />
    <div className="toolbar"><label className="search-field">Kullanıcı ara<input className="form-control" type="search" value={search} placeholder="Ad, e-posta veya rol" onChange={e => { setSearch(e.target.value); setPage(1); }} /></label>
      <label>Hesap durumu<select className="form-control" value={status} onChange={e => { setStatus(e.target.value); setPage(1); }}><option value="all">Tümü</option><option value="active">Aktif</option><option value="passive">Pasif</option></select></label>
      <button className="btn btn-outline" onClick={users.reload}>Yenile</button></div>
    {users.loading ? <Loading message="Kullanıcılar yükleniyor…" /> : users.data && <>
      <div className="table-container"><table><caption className="sr-only">Kullanıcı listesi</caption><thead><tr><th>Ad soyad</th><th>E-posta</th><th>Roller</th><th>Durum</th><th>İşlemler</th></tr></thead>
        <tbody>{filtered.slice((currentPage - 1) * 20, currentPage * 20).map(user => <tr key={user.id}>
          <td>{user.firstName} {user.lastName}</td><td>{user.email}</td><td><div className="badges">{user.roles.map(role => <span className="badge badge-primary" key={role}>{role}</span>)}</div></td>
          <td><span className={`badge badge-${user.isActive ? 'success' : 'danger'}`}>{user.isActive ? 'Aktif' : 'Pasif'}</span></td>
          <td><div className="actions">
            <button className="icon-button" title="Bilgileri düzenle" aria-label={`${user.email}: bilgileri düzenle`} onClick={() => setAction({ kind: 'edit', user })}><Pencil size={18} /></button>
            <button className="icon-button" title="Şifre değiştir" aria-label={`${user.email}: şifre değiştir`} onClick={() => setAction({ kind: 'password', user })}><KeyRound size={18} /></button>
            <button className="icon-button" title="Rolleri düzenle" aria-label={`${user.email}: rolleri düzenle`} disabled={!roles.data} onClick={() => setAction({ kind: 'roles', user })}><Shield size={18} /></button>
            <button className="icon-button" title={user.isActive ? 'Pasifleştir' : 'Etkinleştir'} aria-label={`${user.email}: ${user.isActive ? 'pasifleştir' : 'etkinleştir'}`} onClick={() => setAction({ kind: 'status', user })}><Power size={18} /></button>
            <button className="icon-button danger" title="Tüm oturumları kapat" aria-label={`${user.email}: tüm oturumları kapat`} onClick={() => setAction({ kind: 'logout', user })}><LogOut size={18} /></button>
          </div></td></tr>)}
          {!filtered.length && <tr><td colSpan={5} className="empty-state">Aramanıza uygun kullanıcı bulunamadı.</td></tr>}
        </tbody></table></div><Pagination page={currentPage} total={filtered.length} onChange={setPage} />
    </>}
    {action && <UserAction action={action} roles={roles.data || []} onClose={() => setAction(null)} onComplete={complete} />}
  </div>;
}

function UserAction({ action, roles, onClose, onComplete }: { action: Action; roles: Role[]; onClose: () => void; onComplete: (message: string) => void }) {
  const user = 'user' in action ? action.user : null;
  const [firstName, setFirstName] = useState(user?.firstName || '');
  const [lastName, setLastName] = useState(user?.lastName || '');
  const [email, setEmail] = useState(user?.email || '');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [selectedRoles, setSelectedRoles] = useState(user?.roles || ['Basic']);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const title = { create: 'Kullanıcı ekle', edit: 'Kullanıcı bilgileri', password: 'Şifre değiştir', roles: 'Kullanıcı rolleri', status: user?.isActive ? 'Hesabı pasifleştir' : 'Hesabı etkinleştir', logout: 'Tüm oturumları kapat' }[action.kind];
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (busy) return;
    if ((action.kind === 'create' || action.kind === 'password') && password !== confirmPassword) { setError('Şifreler eşleşmiyor.'); return; }
    setBusy(true); setError('');
    const id = user?.id;
    const commands = {
      create: { endpoint: '/UserManagement/create-user', body: { firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim(), password, roles: selectedRoles } },
      edit: { endpoint: '/UserManagement/update-user', body: { userId: id, firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim() } },
      password: { endpoint: '/UserManagement/change-password', body: { userId: id, newPassword: password } },
      roles: { endpoint: '/UserManagement/assign-roles', body: { userId: id, roles: selectedRoles } },
      status: { endpoint: '/UserManagement/update-status', body: { userId: id, isActive: !user?.isActive } },
      logout: { endpoint: '/UserManagement/revoke-all', body: { userId: id } }
    };
    const command = commands[action.kind];
    try { await request(command.endpoint, { method: action.kind === 'edit' ? 'PUT' : 'POST', body: JSON.stringify(command.body) }); onComplete(`${title}: işlem tamamlandı.`); }
    catch (e) { setError(errorMessage(e)); }
    finally { setBusy(false); }
  }
  return <Modal title={title} busy={busy} onClose={onClose}><form onSubmit={submit}><ErrorNotice message={error} />
    {user && <p className="modal-description">{user.email}</p>}
    <fieldset disabled={busy}>
      {(action.kind === 'create' || action.kind === 'edit') && <div className="form-grid">
        <label>Ad<input className="form-control" required maxLength={256} value={firstName} onChange={e => setFirstName(e.target.value)} autoComplete="given-name" /></label>
        <label>Soyad<input className="form-control" required maxLength={256} value={lastName} onChange={e => setLastName(e.target.value)} autoComplete="family-name" /></label>
        <label className="full-width">E-posta<input className="form-control" required type="email" maxLength={256} value={email} onChange={e => setEmail(e.target.value)} autoComplete="off" /></label>
      </div>}
      {(action.kind === 'create' || action.kind === 'password') && <div className="form-grid">
        <label>Yeni şifre<input className="form-control" required type="password" minLength={12} maxLength={256} value={password} onChange={e => setPassword(e.target.value)} autoComplete="new-password" /><small>En az 12 karakter.</small></label>
        <label>Şifre tekrarı<input className="form-control" required type="password" minLength={12} maxLength={256} value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} autoComplete="new-password" /></label>
      </div>}
      {(action.kind === 'roles' || action.kind === 'create') && <div className="form-group"><p>Roller</p><div className="check-grid">{roles.map(role => <label className="check-option" key={role.id}>
        <input type="checkbox" checked={selectedRoles.includes(role.name)} onChange={e => setSelectedRoles(previous => e.target.checked ? [...previous, role.name] : previous.filter(name => name !== role.name))} />{role.name}
      </label>)}</div><small>Seçim boşsa Basic rolü atanır.</small></div>}
      {action.kind === 'status' && <p>{user?.isActive ? 'Bu hesabın erişimi durdurulacak ve açık oturumları kapatılacak.' : 'Bu hesap yeniden giriş yapabilecek.'}</p>}
      {action.kind === 'password' && <p className="muted">Şifre değiştiğinde kullanıcının açık oturumları kapatılır.</p>}
      {action.kind === 'logout' && <p>Kullanıcının bütün cihazlardaki oturumları kapatılacak. Devam etmek istiyor musunuz?</p>}
      <div className="modal-actions"><button type="button" className="btn btn-outline" onClick={onClose}>Vazgeç</button>
        <button className={`btn ${action.kind === 'logout' ? 'btn-danger' : 'btn-primary'}`} type="submit">{busy ? 'Kaydediliyor…' : action.kind === 'logout' ? 'Oturumları kapat' : 'Kaydet'}</button></div>
    </fieldset></form></Modal>;
}
