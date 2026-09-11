import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { LayoutDashboard, Users, ShieldAlert, KeyRound, LogOut } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { errorMessage } from '../api';
import { ErrorNotice } from './Feedback';
import './Sidebar.css';

export default function Sidebar({ canManage }: { canManage: boolean }) {
  const { logout, user } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function signOut() {
    if (busy) return;
    setBusy(true); setError('');
    try { await logout(); } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <aside className="sidebar glass-panel"><a href="#main-content" className="skip-link">İçeriğe geç</a>
    <div className="sidebar-header"><ShieldAlert size={27} className="sidebar-icon" /><h2>AuthAdmin</h2></div>
    {canManage && <nav className="sidebar-nav" aria-label="Ana menü">{[
      { to: '/dashboard', label: 'Genel bakış', icon: LayoutDashboard }, { to: '/users', label: 'Kullanıcılar', icon: Users },
      { to: '/roles', label: 'Roller ve yetkiler', icon: ShieldAlert }, { to: '/sessions', label: 'Aktif oturumlar', icon: KeyRound }
    ].map(item => <NavLink key={item.to} to={item.to} className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}><item.icon size={20} /><span>{item.label}</span></NavLink>)}</nav>}
    <div className="sidebar-footer"><p className="signed-in-name">{user?.fullName}</p><p className="signed-in-email">{user?.email}</p><ErrorNotice message={error} />
      <button onClick={() => { void signOut(); }} disabled={busy} className="logout-btn"><LogOut size={19} /><span>{busy ? 'Çıkış yapılıyor…' : 'Çıkış yap'}</span></button></div>
  </aside>;
}
