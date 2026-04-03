import React, { useEffect, useMemo, useState } from 'react';
import { Plus, Users as UsersIcon, Shield, AlertCircle, CheckCircle, Pencil, Trash2, X } from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage, PermissionDescriptor, User } from '../types';
import { normalizeRole } from '../utils/permissions';

type UserFormState = {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: 'Contributor' | 'Admin';
  customPermissions: string[];
  isActive: boolean;
};

const emptyForm: UserFormState = {
  username: '',
  email: '',
  password: '',
  firstName: '',
  lastName: '',
  role: 'Contributor',
  customPermissions: [],
  isActive: true
};

const Users: React.FC = () => {
  const [users, setUsers] = useState<User[]>([]);
  const [permissions, setPermissions] = useState<PermissionDescriptor[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [form, setForm] = useState<UserFormState>(emptyForm);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const load = async () => {
    setIsLoading(true);
    setError('');
    try {
      const [usersData, catalogData] = await Promise.all([
        apiService.getUsers(),
        apiService.getPermissionsCatalog()
      ]);
      setUsers(usersData as User[]);
      setPermissions(catalogData.permissions || []);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const groupedPermissions = useMemo(() => {
    const grouped = new Map<string, PermissionDescriptor[]>();
    for (const permission of permissions) {
      const existing = grouped.get(permission.group) || [];
      existing.push(permission);
      grouped.set(permission.group, existing);
    }
    return Array.from(grouped.entries());
  }, [permissions]);

  const openCreate = () => {
    setForm(emptyForm);
    setEditingUser(null);
    setShowCreate(true);
  };

  const openEdit = (user: User) => {
    setForm({
      username: user.username || '',
      email: user.email || '',
      password: '',
      firstName: user.firstName || '',
      lastName: user.lastName || '',
      role: normalizeRole(user.role),
      customPermissions: user.customPermissions || [],
      isActive: user.isActive ?? true
    });
    setEditingUser(user);
    setShowCreate(true);
  };

  const closeDialog = () => {
    setShowCreate(false);
    setEditingUser(null);
    setForm(emptyForm);
  };

  const setField = <K extends keyof UserFormState>(key: K, value: UserFormState[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const togglePermission = (permissionKey: string) => {
    setForm((prev) => {
      const exists = prev.customPermissions.includes(permissionKey);
      return {
        ...prev,
        customPermissions: exists
          ? prev.customPermissions.filter((p) => p !== permissionKey)
          : [...prev.customPermissions, permissionKey]
      };
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError('');
    try {
      if (editingUser) {
        await apiService.updateUser(editingUser.id, {
          email: form.email,
          firstName: form.firstName || undefined,
          lastName: form.lastName || undefined,
          role: form.role === 'Admin' ? 1 : 0,
          isActive: form.isActive,
          customPermissions: form.customPermissions
        });
        setSuccess(`User "${editingUser.username}" updated.`);
      } else {
        await apiService.createUser({
          username: form.username.trim(),
          email: form.email.trim(),
          password: form.password,
          firstName: form.firstName || undefined,
          lastName: form.lastName || undefined,
          role: form.role === 'Admin' ? 1 : 0,
          customPermissions: form.customPermissions
        });
        setSuccess(`User "${form.username}" created.`);
      }
      closeDialog();
      await load();
      setTimeout(() => setSuccess(''), 3500);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (user: User) => {
    if (!confirm(`Delete user "${user.username}"?`)) return;
    try {
      await apiService.deleteUser(user.id);
      setSuccess(`User "${user.username}" deleted.`);
      setTimeout(() => setSuccess(''), 3500);
      await load();
    } catch (err) {
      setError(getErrorMessage(err));
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500" />
      </div>
    );
  }

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.9fr)]">
          <div className="space-y-4">
            <p className="eyebrow">Access Control</p>
            <div className="flex items-start gap-4">
              <div className="page-hero-icon">
                <UsersIcon className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <h1 className="text-3xl font-semibold tracking-tight text-white">Users & Permissions</h1>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-300">
                  Manage users, roles, and explicit permission overrides with a clearer administrative workflow.
                </p>
              </div>
            </div>
          </div>

          <div className="panel p-5">
            <p className="eyebrow">Access Snapshot</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <div className="stat-card">
                <p className="text-sm text-slate-400">Users</p>
                <p className="mt-2 text-3xl font-semibold text-white">{users.length}</p>
                <p className="mt-2 text-xs text-slate-500">active directory entries</p>
              </div>
              <div className="stat-card">
                <p className="text-sm text-slate-400">Permission catalog</p>
                <p className="mt-2 text-3xl font-semibold text-white">{permissions.length}</p>
                <p className="mt-2 text-xs text-slate-500">assignable permission keys</p>
              </div>
            </div>
            <button onClick={openCreate} className="button-primary mt-5 w-full">
              <Plus className="h-4 w-4" />
              Create User
            </button>
          </div>
        </div>
      </section>

      {error && (
        <div className="flex items-center gap-2 rounded-2xl border border-red-500/40 bg-red-500/10 p-4 text-red-300">
          <AlertCircle className="w-5 h-5" />
          <span>{error}</span>
        </div>
      )}

      {success && (
        <div className="flex items-center gap-2 rounded-2xl border border-green-500/40 bg-green-500/10 p-4 text-green-300">
          <CheckCircle className="w-5 h-5" />
          <span>{success}</span>
        </div>
      )}

      <div className="table-shell">
        <table className="w-full min-w-[760px]">
          <thead>
            <tr>
              <th className="px-4 py-3 text-left text-xs uppercase tracking-wider text-slate-400">User</th>
              <th className="px-4 py-3 text-center text-xs uppercase tracking-wider text-slate-400">Role</th>
              <th className="px-4 py-3 text-center text-xs uppercase tracking-wider text-slate-400">Status</th>
              <th className="px-4 py-3 text-center text-xs uppercase tracking-wider text-slate-400">Permissions</th>
              <th className="px-4 py-3 text-center text-xs uppercase tracking-wider text-slate-400">Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id} className="bg-slate-950/35 transition-colors hover:bg-slate-900/75">
                <td className="px-4 py-3 text-sm text-white">
                  <div>{user.username}</div>
                  <div className="text-xs text-slate-400">{user.email}</div>
                </td>
                <td className="px-4 py-3 text-center text-sm text-slate-200">{normalizeRole(user.role)}</td>
                <td className="px-4 py-3 text-sm text-center">
                  <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs ${user.isActive !== false ? 'bg-green-500/15 text-green-300 border-green-500/30' : 'bg-red-500/15 text-red-300 border-red-500/30'}`}>
                    {user.isActive !== false ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td className="px-4 py-3 text-center text-xs text-slate-300">
                  {(user.permissions || []).length} effective
                  {(user.customPermissions || []).length > 0 && (
                    <span className="text-blue-300"> • {(user.customPermissions || []).length} custom</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center justify-center gap-2">
                    <button
                      onClick={() => openEdit(user)}
                      className="rounded-xl p-2 hover:bg-slate-800"
                      title="Edit user"
                    >
                      <Pencil className="w-4 h-4 text-gray-300" />
                    </button>
                    <button
                      onClick={() => handleDelete(user)}
                      className="rounded-xl p-2 hover:bg-red-600/20"
                      title="Delete user"
                    >
                      <Trash2 className="w-4 h-4 text-red-300" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {users.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-sm text-slate-400">
                  No users found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showCreate && (
        <div className="modal-backdrop">
          <div className="modal-shell max-h-[90vh] max-w-4xl overflow-hidden">
            <div className="flex items-center justify-between border-b border-slate-800 px-6 py-4">
              <h2 className="text-xl text-white font-semibold">
                {editingUser ? `Edit User: ${editingUser.username}` : 'Create User'}
              </h2>
              <button onClick={closeDialog} className="rounded-xl p-2 hover:bg-slate-800">
                <X className="w-5 h-5 text-slate-300" />
              </button>
            </div>
            <form onSubmit={handleSubmit} className="p-6 space-y-6 overflow-y-auto max-h-[calc(90vh-80px)]">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <label className="text-sm text-slate-300">
                  Username
                  <input
                    type="text"
                    value={form.username}
                    onChange={(e) => setField('username', e.target.value)}
                    disabled={!!editingUser}
                    required
                    className="field-shell mt-1 disabled:opacity-60"
                  />
                </label>
                <label className="text-sm text-slate-300">
                  Email
                  <input
                    type="email"
                    value={form.email}
                    onChange={(e) => setField('email', e.target.value)}
                    required
                    className="field-shell mt-1"
                  />
                </label>
                {!editingUser && (
                  <label className="text-sm text-slate-300">
                    Password
                    <input
                      type="password"
                      value={form.password}
                      onChange={(e) => setField('password', e.target.value)}
                      required
                      className="field-shell mt-1"
                    />
                  </label>
                )}
                <label className="text-sm text-slate-300">
                  Role
                  <select
                    value={form.role}
                    onChange={(e) => setField('role', e.target.value as 'Contributor' | 'Admin')}
                    className="field-shell mt-1"
                  >
                    <option value="Contributor">Contributor</option>
                    <option value="Admin">Admin</option>
                  </select>
                </label>
                <label className="text-sm text-slate-300">
                  First Name
                  <input
                    type="text"
                    value={form.firstName}
                    onChange={(e) => setField('firstName', e.target.value)}
                    className="field-shell mt-1"
                  />
                </label>
                <label className="text-sm text-slate-300">
                  Last Name
                  <input
                    type="text"
                    value={form.lastName}
                    onChange={(e) => setField('lastName', e.target.value)}
                    className="field-shell mt-1"
                  />
                </label>
                {editingUser && (
                  <label className="mt-6 flex items-center gap-2 text-sm text-slate-300">
                    <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={(e) => setField('isActive', e.target.checked)}
                      className="w-4 h-4"
                    />
                    Active
                  </label>
                )}
              </div>

              <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
                <div className="flex items-start justify-between gap-4 mb-4">
                  <div>
                    <h3 className="text-white font-medium flex items-center gap-2">
                      <Shield className="w-4 h-4" />
                      Custom Permission Overrides
                    </h3>
                    <p className="mt-1 text-xs text-slate-400">
                      Role defaults are always applied. Choose additional explicit permissions for this user.
                    </p>
                  </div>
                  <span className="text-xs px-2.5 py-1 rounded-full border border-blue-500/40 bg-blue-500/10 text-blue-300 whitespace-nowrap">
                    {form.customPermissions.length} selected
                  </span>
                </div>
                <div className="space-y-4">
                  {groupedPermissions.map(([group, groupPermissions]) => (
                    <div key={group} className="rounded-2xl border border-slate-800 bg-slate-900/60 p-3">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs uppercase tracking-wider text-slate-500">{group}</p>
                        <span className="text-[11px] text-slate-500">{groupPermissions.length} permissions</span>
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5">
                        {groupPermissions.map((permission) => (
                          <label
                            key={permission.key}
                            className={`flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${
                              form.customPermissions.includes(permission.key)
                                ? 'bg-blue-500/10 border-blue-500/40'
                                : 'bg-slate-950/40 border-slate-800 hover:bg-slate-800/40 hover:border-slate-700'
                            }`}
                          >
                            <input
                              type="checkbox"
                              checked={form.customPermissions.includes(permission.key)}
                              onChange={() => togglePermission(permission.key)}
                              className="mt-0.5"
                            />
                            <span className="min-w-0">
                              <span className="text-xs text-blue-300 font-mono break-all">{permission.key}</span>
                              <span className="block text-xs text-slate-400 mt-1">{permission.description}</span>
                            </span>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex justify-end gap-3">
                <button type="button" onClick={closeDialog} className="button-secondary">
                  Cancel
                </button>
                <button type="submit" disabled={isSubmitting} className="button-primary disabled:opacity-50">
                  {isSubmitting ? 'Saving...' : editingUser ? 'Save Changes' : 'Create User'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default Users;
