import { useState, type FormEvent } from 'react';
import { Pencil, Trash2, ShieldCheck } from 'lucide-react';
import { request, errorMessage } from '../api';
import { useResource } from '../hooks/useResource';
import { ErrorNotice, Loading, SuccessNotice } from '../components/Feedback';
import { Modal } from '../components/Modal';
import type { Role, Permission } from '../types';

type RoleAction = { kind: 'create' } | { kind: 'rename' | 'delete'; role: Role };
export default function Roles() {
  const roles = useResource<Role[]>('/RoleManagement/roles');
  const catalog = useResource<string[]>('/RoleManagement/permissions');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [action, setAction] = useState<RoleAction | null>(null);
  const [message, setMessage] = useState('');
  const selected = roles.data?.find(role => role.id === selectedId);
  return <div className="fade-in"><div className="page-header split"><div><h1>Roller ve yetkiler</h1><p>Kullanıcı gruplarını ve izinlerini yönetin.</p></div>
    <button className="btn btn-primary" onClick={() => setAction({ kind: 'create' })}>Rol ekle</button></div>
    <SuccessNotice message={message} /><ErrorNotice message={roles.error} retry={roles.reload} /><ErrorNotice message={catalog.error} retry={catalog.reload} />
    {roles.loading ? <Loading /> : <div className="roles-grid"><section className="role-list" aria-label="Roller">
      {(roles.data || []).map(role => <div className={`role-row ${selectedId === role.id ? 'selected' : ''}`} key={role.id}>
        <button className="role-select" aria-pressed={selectedId === role.id} onClick={() => setSelectedId(role.id)}>{role.name}</button>
        {!['SuperAdmin', 'Basic'].includes(role.name) && <div className="actions">
          <button className="icon-button" aria-label={`${role.name}: adı değiştir`} onClick={() => setAction({ kind: 'rename', role })}><Pencil size={16} /></button>
          <button className="icon-button danger" aria-label={`${role.name}: sil`} onClick={() => setAction({ kind: 'delete', role })}><Trash2 size={16} /></button>
        </div>}
      </div>)}
      {roles.data?.length === 0 && <p className="empty-state">Henüz rol yok.</p>}
    </section><section className="permissions-panel" aria-label="Rol yetkileri">
      {selected && catalog.data ? <RolePermissions key={selected.id} role={selected} catalog={catalog.data} /> : <p className="empty-state">Yetkilerini düzenlemek için bir rol seçin.</p>}
    </section></div>}
    {action && <RoleForm action={action} onClose={() => setAction(null)} onComplete={() => {
      if (action.kind === 'delete' && action.role.id === selectedId) setSelectedId(null);
      setAction(null); setMessage('Rol işlemi tamamlandı.'); roles.reload();
    }} />}
  </div>;
}
function RolePermissions({ role, catalog }: { role: Role; catalog: string[] }) {
  const permissions = useResource<Permission[]>(`/RoleManagement/role-permissions/${encodeURIComponent(role.id)}`);
  return <><h2><ShieldCheck size={21} /> {role.name}</h2><ErrorNotice message={permissions.error} retry={permissions.reload} />
    {role.name === 'SuperAdmin' && <p className="notice">SuperAdmin bütün yönetim yetkilerine sahiptir; bu listedeki seçimler yönetim erişimini sınırlamaz.</p>}
    {permissions.loading ? <Loading message="Yetkiler yükleniyor…" /> : permissions.data &&
      <PermissionForm key={`${role.id}:${permissions.data.map(p => p.permissionName).join('|')}`} role={role} catalog={catalog} initial={permissions.data.map(p => p.permissionName)} />}
  </>;
}
function PermissionForm({ role, catalog, initial }: { role: Role; catalog: string[]; initial: string[] }) {
  const [checked, setChecked] = useState(initial);
  const [baseline, setBaseline] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState(false);
  const dirty = [...checked].sort().join('|') !== [...baseline].sort().join('|');
  const unknown = checked.filter(permission => !catalog.includes(permission));
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy || !dirty) return;
    setBusy(true); setError(''); setSaved(false);
    try { await request('/RoleManagement/permissions', { method: 'POST', body: JSON.stringify({ roleId: role.id, permissions: checked }) }); setBaseline([...checked]); setSaved(true); }
    catch (e) { setError(errorMessage(e)); }
    finally { setBusy(false); }
  }
  return <form onSubmit={submit}><ErrorNotice message={error} /><SuccessNotice message={saved ? 'Yetkiler kaydedildi.' : ''} /><fieldset disabled={busy || role.name === 'SuperAdmin'}>
    <div className="check-grid">{[...new Set([...catalog, ...unknown])].map(permission => <label className="check-option" key={permission}>
      <input type="checkbox" checked={checked.includes(permission)} onChange={e => { setSaved(false); setChecked(previous => e.target.checked ? [...previous, permission] : previous.filter(p => p !== permission)); }} />{permission}
    </label>)}</div>
    {!!unknown.length && <p className="notice">Katalogdan kaldırılan yetkilerin işaretini kaldırarak kaydedebilirsiniz.</p>}
    {!catalog.length && <p className="empty-state">Tanımlı yetki bulunamadı.</p>}
    <button type="submit" className="btn btn-primary" disabled={!dirty}>{busy ? 'Kaydediliyor…' : 'Yetkileri kaydet'}</button>
  </fieldset></form>;
}
function RoleForm({ action, onClose, onComplete }: { action: RoleAction; onClose: () => void; onComplete: () => void }) {
  const [name, setName] = useState('role' in action ? action.role.name : '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const title = { create: 'Rol ekle', rename: 'Rol adını değiştir', delete: 'Rolü sil' }[action.kind];
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError('');
    try {
      if (action.kind === 'delete') await request(`/RoleManagement/role/${encodeURIComponent(action.role.id)}`, { method: 'DELETE' });
      else await request('/RoleManagement/role', { method: action.kind === 'create' ? 'POST' : 'PUT', body: JSON.stringify(action.kind === 'create' ? { roleName: name.trim() } : { roleId: action.role.id, newRoleName: name.trim() }) });
      onComplete();
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <Modal title={title} busy={busy} onClose={onClose}><form onSubmit={submit}><ErrorNotice message={error} /><fieldset disabled={busy}>
    {action.kind === 'delete' ? <p><strong>{action.role.name}</strong> silinecek. Bu role bağlı kullanıcıların yetkileri etkilenecek.</p> :
      <label>Rol adı<input className="form-control" required maxLength={256} value={name} onChange={e => setName(e.target.value)} /></label>}
    <div className="modal-actions"><button className="btn btn-outline" type="button" onClick={onClose}>Vazgeç</button>
      <button className={`btn ${action.kind === 'delete' ? 'btn-danger' : 'btn-primary'}`} type="submit" disabled={action.kind !== 'delete' && !name.trim()}>{busy ? 'İşleniyor…' : action.kind === 'delete' ? 'Sil' : 'Kaydet'}</button></div>
  </fieldset></form></Modal>;
}
