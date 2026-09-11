import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ShieldCheck, Mail, Lock, LogIn } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { errorMessage } from '../api';
import { ErrorNotice, Loading } from '../components/Feedback';
import './Login.css';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const auth = useAuth();
  const location = useLocation();
  const requested = (location.state as { from?: string } | null)?.from;
  const destination = requested && /^\/(dashboard|users|roles|sessions)([/?#]|$)/.test(requested) ? requested : '/dashboard';
  if (auth.status === 'loading') return <div className="centered"><Loading message="Oturum kontrol ediliyor…" /></div>;
  if (auth.isAuthenticated) return <Navigate to={destination} replace />;
  async function submit(event: FormEvent) {
    event.preventDefault(); if (busy) return;
    setBusy(true); setError('');
    try { await auth.login(email.trim(), password); setPassword(''); }
    catch (e) { setError(errorMessage(e)); } finally { setBusy(false); }
  }
  return <div className="login-container"><div className="login-card glass-panel fade-in"><div className="login-header">
    <ShieldCheck size={46} className="login-logo" /><h1>AuthServer</h1><p>Kimlik yönetim merkezi</p></div>
    <ErrorNotice message={auth.error || error} retry={auth.status === 'error' ? auth.retry : undefined} />
    <form onSubmit={submit}><fieldset disabled={busy || auth.status === 'error'}>
      <div className="form-group"><label className="form-label" htmlFor="login-email">E-posta</label><div className="input-with-icon"><Mail size={18} className="input-icon" />
        <input id="login-email" name="email" className="form-control" type="email" value={email} onChange={e => setEmail(e.target.value)} autoComplete="username" required maxLength={256} /></div></div>
      <div className="form-group"><label className="form-label" htmlFor="login-password">Şifre</label><div className="input-with-icon"><Lock size={18} className="input-icon" />
        <input id="login-password" name="password" className="form-control" type="password" value={password} onChange={e => setPassword(e.target.value)} autoComplete="current-password" required maxLength={256} /></div></div>
      <button className="btn btn-primary login-btn" type="submit">{busy ? 'Giriş yapılıyor…' : <><LogIn size={19} />Giriş yap</>}</button>
    </fieldset></form>
    <p className="login-help">Yalnızca yetkili yöneticiler giriş yapabilir.</p>
  </div></div>;
}
