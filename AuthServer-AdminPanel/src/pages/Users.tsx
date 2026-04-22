import { useEffect, useState } from 'react';
import { UserPlus, ToggleLeft, ToggleRight, Trash2 } from 'lucide-react';
import { fetchWithAuth } from '../api';

interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  roles: string[];
}

export default function Users() {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);

  const loadUsers = async () => {
    try {
      const res = await fetchWithAuth('/UserManagement/all-users');
      if (res.ok) {
        const data = await res.json();
        if (data.succeeded) {
          setUsers(data.data);
        }
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUsers();
  }, []);

  const toggleStatus = async (userId: string, currentStatus: boolean) => {
    try {
      const res = await fetchWithAuth('/UserManagement/update-status', {
        method: 'POST',
        body: JSON.stringify({ userId, isActive: !currentStatus })
      });
      if (res.ok) {
        loadUsers();
      }
    } catch (err) {
      console.error(err);
    }
  };

  if (loading) return <div className="loading-state">Loading users...</div>;

  return (
    <div className="fade-in">
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>User Management</h1>
          <p>Manage application users and their access</p>
        </div>
        <button className="btn btn-primary">
          <UserPlus size={18} /> Add User
        </button>
      </div>

      <div className="table-container glass-panel">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Roles</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.map(user => (
              <tr key={user.id}>
                <td>{user.firstName} {user.lastName}</td>
                <td>{user.email}</td>
                <td>
                  <div style={{ display: 'flex', gap: '4px', flexWrap: 'wrap' }}>
                    {user.roles.map(r => (
                      <span key={r} className="badge badge-primary">{r}</span>
                    ))}
                  </div>
                </td>
                <td>
                  <span className={`badge ${user.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {user.isActive ? 'Active' : 'Passive'}
                  </span>
                </td>
                <td>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button 
                      onClick={() => toggleStatus(user.id, user.isActive)}
                      className="btn btn-outline"
                      style={{ padding: '0.4rem', borderRadius: '4px' }}
                      title={user.isActive ? "Deactivate User" : "Activate User"}
                    >
                      {user.isActive ? <ToggleRight size={18} color="var(--success)" /> : <ToggleLeft size={18} color="var(--danger)" />}
                    </button>
                    <button className="btn btn-danger" style={{ padding: '0.4rem', borderRadius: '4px' }} title="Force Logout">
                      <Trash2 size={18} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {users.length === 0 && (
              <tr>
                <td colSpan={5} style={{ textAlign: 'center', padding: '2rem' }}>No users found.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
