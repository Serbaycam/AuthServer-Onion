import { Outlet, Navigate, useLocation } from 'react-router-dom';
import Sidebar from './Sidebar';
import { useAuth } from '../context/AuthContext';
import { ErrorNotice, Loading } from './Feedback';
import './Layout.css';

export default function Layout() {
  const auth = useAuth();
  const location = useLocation();
  if (auth.status === 'loading') return <div className="centered"><Loading message="Oturumunuz yükleniyor…" /></div>;
  if (auth.status === 'error') return <div className="centered"><ErrorNotice message={auth.error || 'Oturum kontrol edilemedi.'} retry={auth.retry} /></div>;
  if (!auth.isAuthenticated) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  const canManage = auth.user?.roles.includes('SuperAdmin');
  return <div className="app-layout"><Sidebar canManage={!!canManage} /><main className="main-content" id="main-content"><div className="glass-panel content-glass">
    {canManage ? <Outlet key={auth.user?.id} /> : <div className="empty-state"><h1>Erişim yetkiniz bulunmuyor</h1><p>Hesabınızın yönetici yetkisi kaldırılmış olabilir. Sistem yöneticinizle iletişime geçin.</p></div>}
  </div></main></div>;
}
