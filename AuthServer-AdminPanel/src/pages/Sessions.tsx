import { useState } from 'react';
import { RefreshCw, ShieldBan } from 'lucide-react';
import { request, errorMessage } from '../api';
import { useResource } from '../hooks/useResource';
import { ErrorNotice, Loading, SuccessNotice } from '../components/Feedback';
import { Modal } from '../components/Modal';
import { Pagination } from '../components/Pagination';
import { formatDate, type ActiveSession } from '../types';

export default function Sessions() {
  const sessions = useResource<ActiveSession[]>('/SessionManagement/active-sessions', 30000);
  const [target, setTarget] = useState<ActiveSession | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const filtered = (sessions.data || []).filter(session => `${session.fullName} ${session.userEmail} ${session.ipAddress}`.toLocaleLowerCase('tr').includes(search.toLocaleLowerCase('tr')));
  const currentPage = Math.min(page, Math.max(1, Math.ceil(filtered.length / 20)));
  async function kill() {
    if (!target || busy) return;
    setBusy(true); setError('');
    try {
      await request('/SessionManagement/kill-session', { method: 'POST', body: JSON.stringify({ tokenId: target.tokenId }) });
      setTarget(null); setMessage('Oturum kapatıldı.'); sessions.reload();
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <div className="fade-in"><div className="page-header split"><div><h1>Aktif oturumlar</h1><p>Liste 30 saniyede bir yenilenir.</p></div>
    <button className="btn btn-outline" onClick={sessions.reload}><RefreshCw size={17} />Yenile</button></div>
    <SuccessNotice message={message} /><ErrorNotice message={sessions.error} retry={sessions.reload} />
    <div className="toolbar"><label className="search-field">Oturum ara<input className="form-control" type="search" placeholder="Kullanıcı veya IP adresi" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} /></label></div>
    {sessions.loading ? <Loading message="Oturumlar yükleniyor…" /> : sessions.data && <><div className="table-container"><table><caption className="sr-only">Aktif oturum listesi</caption>
      <thead><tr><th>Kullanıcı</th><th>IP adresi</th><th>Başlangıç</th><th>Bitiş</th><th>Durum</th><th>İşlem</th></tr></thead><tbody>
        {filtered.slice((currentPage - 1) * 20, currentPage * 20).map(session => <tr key={session.tokenId} className={session.isCurrentSession ? 'current-session' : ''}>
          <td><strong>{session.fullName}</strong><div className="muted">{session.userEmail}</div></td><td>{session.ipAddress}</td>
          <td>{formatDate(session.createdDate)}</td><td>{formatDate(session.expirationDate)}</td>
          <td><span className={`badge badge-${session.isCurrentSession ? 'primary' : 'success'}`}>{session.isCurrentSession ? 'Bu oturum' : 'Aktif'}</span></td>
          <td>{!session.isCurrentSession && <button className="btn btn-danger compact" aria-label={`${session.userEmail}: oturumu kapat`} onClick={() => { setTarget(session); setError(''); }}><ShieldBan size={16} />Kapat</button>}</td>
        </tr>)}
        {!filtered.length && <tr><td colSpan={6} className="empty-state">Gösterilecek oturum bulunamadı.</td></tr>}
      </tbody></table></div><Pagination page={currentPage} total={filtered.length} onChange={setPage} /></>}
    {target && <Modal title="Oturumu kapat" busy={busy} onClose={() => setTarget(null)}><ErrorNotice message={error} />
      <p><strong>{target.userEmail}</strong> kullanıcısının {target.ipAddress} adresindeki oturumu sonlandırılacak.</p>
      <div className="modal-actions"><button className="btn btn-outline" disabled={busy} onClick={() => setTarget(null)}>Vazgeç</button>
        <button className="btn btn-danger" disabled={busy} onClick={() => { void kill(); }}>{busy ? 'Kapatılıyor…' : 'Oturumu kapat'}</button></div></Modal>}
  </div>;
}
