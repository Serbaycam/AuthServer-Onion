import { useEffect, useState } from 'react';
import { Users, ShieldAlert, KeyRound, Activity } from 'lucide-react';
import { fetchWithAuth } from '../api';
import './Dashboard.css';

interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  totalRoles: number;
  totalActiveSessions: number;
}

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadStats = async () => {
      try {
        const res = await fetchWithAuth('/Dashboard/stats');
        if (res.ok) {
          const data = await res.json();
          if (data.succeeded) {
            setStats(data.data);
          }
        }
      } catch (err) {
        console.error("Failed to load stats", err);
      } finally {
        setLoading(false);
      }
    };
    loadStats();
  }, []);

  if (loading) {
    return <div className="loading-state">Loading dashboard...</div>;
  }

  return (
    <div className="fade-in">
      <div className="page-header">
        <h1>Dashboard Overview</h1>
        <p>Welcome to the AuthServer control panel.</p>
      </div>

      <div className="stats-grid">
        <div className="stat-card glass-panel">
          <div className="stat-icon-wrapper blue">
            <Users size={24} />
          </div>
          <div className="stat-content">
            <h3>Total Users</h3>
            <p className="stat-value">{stats?.totalUsers || 0}</p>
          </div>
        </div>

        <div className="stat-card glass-panel">
          <div className="stat-icon-wrapper green">
            <Activity size={24} />
          </div>
          <div className="stat-content">
            <h3>Active Users</h3>
            <p className="stat-value">{stats?.activeUsers || 0}</p>
          </div>
        </div>

        <div className="stat-card glass-panel">
          <div className="stat-icon-wrapper purple">
            <ShieldAlert size={24} />
          </div>
          <div className="stat-content">
            <h3>Total Roles</h3>
            <p className="stat-value">{stats?.totalRoles || 0}</p>
          </div>
        </div>

        <div className="stat-card glass-panel">
          <div className="stat-icon-wrapper orange">
            <KeyRound size={24} />
          </div>
          <div className="stat-content">
            <h3>Active Sessions</h3>
            <p className="stat-value">{stats?.totalActiveSessions || 0}</p>
          </div>
        </div>
      </div>
    </div>
  );
}
