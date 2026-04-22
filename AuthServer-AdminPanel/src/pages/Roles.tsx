import { useEffect, useState } from 'react';
import { ShieldAlert, ShieldCheck } from 'lucide-react';
import { fetchWithAuth } from '../api';

interface Role {
  id: string;
  name: string;
}

export default function Roles() {
  const [roles, setRoles] = useState<Role[]>([]);
  const [allPermissions, setAllPermissions] = useState<string[]>([]);
  const [selectedRole, setSelectedRole] = useState<string | null>(null);
  const [rolePermissions, setRolePermissions] = useState<string[]>([]);
  
  const [newRoleName, setNewRoleName] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchRolesAndPerms = async () => {
    try {
      const [rolesRes, permsRes] = await Promise.all([
        fetchWithAuth('/RoleManagement/roles'),
        fetchWithAuth('/RoleManagement/permissions')
      ]);
      
      if (rolesRes.ok && permsRes.ok) {
        const rolesData = await rolesRes.json();
        const permsData = await permsRes.json();
        setRoles(rolesData.data || []);
        setAllPermissions(permsData.data || []);
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRolesAndPerms();
  }, []);

  const loadRolePermissions = async (roleId: string) => {
    setSelectedRole(roleId);
    try {
      const res = await fetchWithAuth(`/RoleManagement/role-permissions/${roleId}`);
      if (res.ok) {
        const data = await res.json();
        // The API returns [{ permissionName: "Permissions.Users.View" }], map to string array:
        const dtoList = data.data || [];
        setRolePermissions(dtoList.map((p: any) => p.permissionName));
      }
    } catch (err) {
      console.error(err);
    }
  };

  const savePermissions = async () => {
    if (!selectedRole) return;
    try {
      const res = await fetchWithAuth('/RoleManagement/permissions', {
        method: 'POST',
        body: JSON.stringify({ roleId: selectedRole, permissions: rolePermissions })
      });
      if (res.ok) {
        alert("Permissions saved successfully!");
      }
    } catch (err) {
      console.error(err);
    }
  };

  const togglePermission = (perm: string) => {
    if (rolePermissions.includes(perm)) {
      setRolePermissions(rolePermissions.filter(p => p !== perm));
    } else {
      setRolePermissions([...rolePermissions, perm]);
    }
  };

  const createRole = async () => {
    if (!newRoleName.trim()) return;
    try {
      const res = await fetchWithAuth('/RoleManagement/role', {
        method: 'POST',
        body: JSON.stringify({ roleName: newRoleName })
      });
      if (res.ok) {
        setNewRoleName('');
        fetchRolesAndPerms();
      }
    } catch (err) {
      console.error(err);
    }
  };

  const deleteRole = async (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    if (!window.confirm("Are you sure you want to delete this role?")) return;
    try {
      const res = await fetchWithAuth(`/RoleManagement/role/${id}`, { method: 'DELETE' });
      if (res.ok) {
        if (selectedRole === id) setSelectedRole(null);
        fetchRolesAndPerms();
      }
    } catch (err) {
      console.error(err);
    }
  };

  if (loading) return <div className="loading-state">Loading roles...</div>;

  return (
    <div className="fade-in">
      <div className="page-header">
        <h1>Roles & Permissions</h1>
        <p>Manage system roles and assign fine-grained permissions.</p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(250px, 1fr) 2fr', gap: '2rem' }}>
        <div className="glass-panel" style={{ padding: '1rem' }}>
          <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
            <ShieldAlert size={20} /> Roles
          </h3>
          <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem' }}>
            <input 
              type="text" 
              className="form-control" 
              placeholder="New role name..." 
              value={newRoleName}
              onChange={e => setNewRoleName(e.target.value)}
              style={{ padding: '0.5rem', flex: 1 }}
            />
            <button className="btn btn-primary" onClick={createRole} style={{ padding: '0.5rem 1rem' }}>Add</button>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {roles.map(role => (
              <div
                key={role.id}
                onClick={() => loadRolePermissions(role.id)}
                className={`btn btn-outline ${selectedRole === role.id ? 'btn-primary' : ''}`}
                style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', cursor: 'pointer' }}
              >
                <span>{role.name}</span>
                <button 
                  onClick={(e) => deleteRole(e, role.id)}
                  style={{ background: 'transparent', border: 'none', color: '#ef4444', cursor: 'pointer', padding: '0.2rem' }}
                  title="Delete Role"
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>

        <div className="glass-panel" style={{ padding: '2rem' }}>
          {selectedRole ? (
            <>
              <h3 style={{ marginBottom: '2rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <ShieldCheck size={20} /> Assign Permissions
              </h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '1rem', marginBottom: '2rem' }}>
                {allPermissions.map(perm => (
                  <label key={perm} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                    <input 
                      type="checkbox" 
                      checked={rolePermissions.includes(perm)}
                      onChange={() => togglePermission(perm)}
                      style={{ width: '18px', height: '18px', accentColor: 'var(--primary)' }}
                    />
                    <span style={{ fontSize: '0.9rem' }}>{perm}</span>
                  </label>
                ))}
              </div>
              <button className="btn btn-primary" onClick={savePermissions}>
                Save Changes
              </button>
            </>
          ) : (
            <div style={{ textAlign: 'center', color: 'var(--text-muted)', marginTop: '4rem' }}>
              Select a role from the left to manage permissions.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
