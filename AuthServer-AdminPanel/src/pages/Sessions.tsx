import { useEffect, useState } from 'react';
import { KeyRound, ShieldBan, Clock } from 'lucide-react';
import { fetchWithAuth } from '../api';

interface ActiveSession {
  tokenId: string;
  userEmail: string;
  fullName: string;
  ipAddress: string;
  createdDate: string;
  expirationDate: string;
  isCurrentSession: boolean;
}

export default function Sessions() {
  const [sessions, setSessions] = useState<ActiveSession[]>([]);
  const [loading, setLoading] = useState(true);

  const loadSessions = async () => {
    try {
      // NOTE: Normally we send currentToken in body/query to identify current
      const res = await fetchWithAuth('/SessionManagement/active-sessions');
      if (res.ok) {
        const data = await res.json();
        if (data.succeeded) {
          setSessions(data.data || []);
        }
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadSessions();
  }, []);

  const killSession = async (tokenId: string) => {
    if (!window.confirm("Are you sure you want to terminate this session?")) return;
    
    try {
      const res = await fetchWithAuth('/SessionManagement/kill-session', {
        method: 'POST',
        body: JSON.stringify({ tokenId })
      });
      if (res.ok) {
        loadSessions();
      }
    } catch (err) {
      console.error(err);
    }
  };

  if (loading) return <div className="loading-state">Loading sessions...</div>;

  return (
    <div className="fade-in">
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>Active Sessions</h1>
          <p>Monitor and manage currently logged-in users across the system.</p>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', color: 'var(--text-muted)' }}>
          <Clock size={18} /> Auto-refreshing
        </div>
      </div>

      <div className="table-container glass-panel">
        <table>
          <thead>
            <tr>
              <th>User</th>
              <th>IP Address</th>
              <th>Created</th>
              <th>Expires</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {sessions.map(session => (
              <tr key={session.tokenId} style={{ backgroundColor: session.isCurrentSession ? 'rgba(59, 130, 246, 0.05)' : '' }}>
                <td>
                  <div style={{ fontWeight: 500 }}>{session.fullName}</div>
                  <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>{session.userEmail}</div>
                </td>
                <td><span style={{ fontFamily: 'monospace' }}>{session.ipAddress}</span></td>
                <td>{new Date(session.createdDate).toLocaleString()}</td>
                <td>{new Date(session.expirationDate).toLocaleString()}</td>
                <td>
                  {session.isCurrentSession ? (
                     <span className="badge badge-primary">Current Session</span>
                  ) : (
                     <span className="badge badge-success">Active</span>
                  )}
                </td>
                <td>
                  {!session.isCurrentSession && (
                    <button 
                      onClick={() => killSession(session.tokenId)}
                      className="btn btn-danger"
                      style={{ padding: '0.4rem 0.6rem', fontSize: '0.8rem' }}
                    >
                      <ShieldBan size={16} /> Kill
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {sessions.length === 0 && (
              <tr>
                <td colSpan={6} style={{ textAlign: 'center', padding: '2rem' }}>No active sessions found.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
