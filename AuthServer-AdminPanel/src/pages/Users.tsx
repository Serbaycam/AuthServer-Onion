import { useEffect, useState } from 'react';
import { UserPlus, ToggleLeft, ToggleRight, Trash2, Key, Shield } from 'lucide-react';
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

  // Modal states
  const [showAddUser, setShowAddUser] = useState(false);
  const [newUser, setNewUser] = useState({ firstName: '', lastName: '', email: '', password: '', roles: '' });

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
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, isActive: !currentStatus })
      });
      if (res.ok) {
        loadUsers();
      }
    } catch (err) {
      console.error(err);
    }
  };

  const createUser = async () => {
    if (!newUser.email || !newUser.password) return;
    try {
      const payload = {
        firstName: newUser.firstName,
        lastName: newUser.lastName,
        email: newUser.email,
        password: newUser.password,
        roles: newUser.roles.split(',').map(r => r.trim()).filter(r => r)
      };
      const res = await fetchWithAuth('/UserManagement/user', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (res.ok) {
        setShowAddUser(false);
        setNewUser({ firstName: '', lastName: '', email: '', password: '', roles: '' });
        loadUsers();
      } else {
        alert("Failed to create user.");
      }
    } catch (err) {
      console.error(err);
    }
  };

  const changePassword = async (userId: string) => {
    const newPassword = window.prompt("Enter new password for this user:");
    if (!newPassword) return;
    try {
      const res = await fetchWithAuth('/UserManagement/change-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, newPassword })
      });
      if (res.ok) {
        alert("Password changed successfully!");
      } else {
        alert("Error changing password.");
      }
    } catch (err) {
      console.error(err);
    }
  };

  const manageRoles = async (userId: string, currentRoles: string[]) => {
    const rolesStr = window.prompt("Enter roles separated by comma (e.g. Admin, Basic):", currentRoles.join(', '));
    if (rolesStr === null) return;
    try {
      const rolesArray = rolesStr.split(',').map(r => r.trim()).filter(r => r);
      const res = await fetchWithAuth('/UserManagement/assign-roles', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ userId, roles: rolesArray })
      });
      if (res.ok) {
        loadUsers();
      } else {
        alert("Failed to assign roles");
      }
    } catch (err) {
      console.error(err);
    }
  };

  const forceDisconnect = async () => {
    alert("Use the Active Sessions page to disconnect a specific session or use /UserManagement/revoke-all in API.");
  };

  if (loading) return <div className="loading-state">Loading users...</div>;

  return (
    <div className="fade-in">
      <div className="page-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1>User Management</h1>
          <p>Manage application users and their access</p>
        </div>
        <button className="btn btn-primary" onClick={() => setShowAddUser(!showAddUser)}>
          <UserPlus size={18} /> Add User
        </button>
      </div>

      {showAddUser && (
        <div className="glass-panel" style={{ padding: '1.5rem', marginBottom: '2rem', display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'flex-end' }}>
          <div><small>First Name</small><br /><input className="form-control" value={newUser.firstName} onChange={e => setNewUser({...newUser, firstName: e.target.value})} /></div>
          <div><small>Last Name</small><br /><input className="form-control" value={newUser.lastName} onChange={e => setNewUser({...newUser, lastName: e.target.value})} /></div>
          <div><small>Email</small><br /><input className="form-control" type="email" value={newUser.email} onChange={e => setNewUser({...newUser, email: e.target.value})} /></div>
          <div><small>Password</small><br /><input className="form-control" type="password" value={newUser.password} onChange={e => setNewUser({...newUser, password: e.target.value})} /></div>
          <div><small>Roles (comma list)</small><br /><input className="form-control" value={newUser.roles} onChange={e => setNewUser({...newUser, roles: e.target.value})} /></div>
          <button className="btn btn-primary" onClick={createUser} style={{ height: '42px' }}>Save User</button>
        </div>
      )}

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
                    <button onClick={() => toggleStatus(user.id, user.isActive)} className="btn btn-outline" style={{ padding: '0.4rem' }} title="Toggle Active Status">
                      {user.isActive ? <ToggleRight size={18} color="var(--success)" /> : <ToggleLeft size={18} color="var(--danger)" />}
                    </button>
                    <button onClick={() => changePassword(user.id)} className="btn btn-outline" style={{ padding: '0.4rem' }} title="Change Password">
                      <Key size={18} color="#3b82f6" />
                    </button>
                    <button onClick={() => manageRoles(user.id, user.roles)} className="btn btn-outline" style={{ padding: '0.4rem' }} title="Manage Roles">
                      <Shield size={18} color="#8b5cf6" />
                    </button>
                    <button onClick={forceDisconnect} className="btn btn-danger" style={{ padding: '0.4rem' }} title="Force Logout">
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
