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
  
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
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
    fetchData();
  }, []);

  const loadRolePermissions = async (roleId: string) => {
    setSelectedRole(roleId);
    try {
      const res = await fetchWithAuth(`/RoleManagement/role-permissions/${roleId}`);
      if (res.ok) {
        const data = await res.json();
        setRolePermissions(data.data || []);
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
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {roles.map(role => (
              <button 
                key={role.id}
                onClick={() => loadRolePermissions(role.id)}
                className={`btn btn-outline ${selectedRole === role.id ? 'btn-primary' : ''}`}
                style={{ justifyContent: 'flex-start' }}
              >
                {role.name}
              </button>
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
