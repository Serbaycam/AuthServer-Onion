import { Users, ShieldAlert, KeyRound, Activity } from 'lucide-react';
import { useResource } from '../hooks/useResource';
import { ErrorNotice, Loading } from '../components/Feedback';
import { formatDate, type DashboardStats } from '../types';
import './Dashboard.css';

export default function Dashboard() {
  const stats = useResource<DashboardStats>('/Dashboard/stats', 30000);
  return <div className="fade-in"><div className="page-header split"><div><h1>Genel bakış</h1><p>Kimlik yönetim merkezinizin güncel durumu.</p></div>
    <button className="btn btn-outline" onClick={stats.reload}>Yenile</button></div>
    <ErrorNotice message={stats.error} retry={stats.reload} />
    {stats.loading ? <Loading message="Özet yükleniyor…" /> : stats.data && <>
      <div className="stats-grid">{[
        { name: 'Toplam kullanıcı', value: stats.data.totalUsers, icon: Users, color: 'blue' },
        { name: 'Aktif kullanıcı', value: stats.data.activeUsers, icon: Activity, color: 'green' },
        { name: 'Tanımlı rol', value: stats.data.totalRoles, icon: ShieldAlert, color: 'purple' },
        { name: 'Aktif oturum', value: stats.data.totalActiveSessions, icon: KeyRound, color: 'orange' }
      ].map(item => <div className="stat-card glass-panel" key={item.name}><div className={`stat-icon-wrapper ${item.color}`}><item.icon size={24} /></div>
        <div className="stat-content"><h3>{item.name}</h3><p className="stat-value">{item.value.toLocaleString('tr-TR')}</p></div></div>)}</div>
      <h2 className="section-title">Son işlemler</h2><div className="table-container"><table><caption className="sr-only">Son denetim kayıtları</caption>
        <thead><tr><th>Kullanıcı</th><th>İşlem</th><th>Tarih</th><th>IP adresi</th></tr></thead><tbody>
          {(stats.data.latestActivities || []).map((entry, index) => <tr key={`${entry.date}-${index}`}><td>{entry.userEmail}</td><td>{entry.action}</td><td>{formatDate(entry.date)}</td><td>{entry.ipAddress}</td></tr>)}
          {!stats.data.latestActivities?.length && <tr><td colSpan={4} className="empty-state">Henüz işlem kaydı yok.</td></tr>}
        </tbody></table></div>
    </>}
  </div>;
}
