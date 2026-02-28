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
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <UsersIcon className="w-8 h-8" />
            Users & Permissions
          </h1>
          <p className="text-gray-400 mt-1">Manage users, roles, and custom permission overrides.</p>
        </div>
        <button
          onClick={openCreate}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded-lg text-white font-medium"
        >
          <Plus className="w-4 h-4" />
          Create User
        </button>
      </div>

      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/50 rounded-lg text-red-400 flex items-center gap-2">
          <AlertCircle className="w-5 h-5" />
          <span>{error}</span>
        </div>
      )}

      {success && (
        <div className="p-4 bg-green-500/10 border border-green-500/50 rounded-lg text-green-400 flex items-center gap-2">
          <CheckCircle className="w-5 h-5" />
          <span>{success}</span>
        </div>
      )}

      <div className="bg-gray-800 border border-gray-700 rounded-lg overflow-x-auto">
        <table className="w-full min-w-[760px]">
          <thead className="bg-gray-700/50">
            <tr>
              <th className="text-left px-4 py-3 text-xs uppercase tracking-wider text-gray-400">User</th>
              <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Role</th>
              <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Status</th>
              <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Permissions</th>
              <th className="text-center px-4 py-3 text-xs uppercase tracking-wider text-gray-400">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-700">
            {users.map((user) => (
              <tr key={user.id} className="hover:bg-gray-700/30">
                <td className="px-4 py-3 text-sm text-white">
                  <div>{user.username}</div>
                  <div className="text-xs text-gray-400">{user.email}</div>
                </td>
                <td className="px-4 py-3 text-sm text-gray-200 text-center">{normalizeRole(user.role)}</td>
                <td className="px-4 py-3 text-sm text-center">
                  <span className={`inline-flex px-2 py-1 rounded text-xs border ${user.isActive !== false ? 'bg-green-500/15 text-green-300 border-green-500/30' : 'bg-red-500/15 text-red-300 border-red-500/30'}`}>
                    {user.isActive !== false ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td className="px-4 py-3 text-xs text-gray-300 text-center">
                  {(user.permissions || []).length} effective
                  {(user.customPermissions || []).length > 0 && (
                    <span className="text-blue-300"> • {(user.customPermissions || []).length} custom</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center justify-center gap-2">
                    <button
                      onClick={() => openEdit(user)}
                      className="p-1.5 rounded hover:bg-gray-600"
                      title="Edit user"
                    >
                      <Pencil className="w-4 h-4 text-gray-300" />
                    </button>
                    <button
                      onClick={() => handleDelete(user)}
                      className="p-1.5 rounded hover:bg-red-600/20"
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
                <td colSpan={5} className="px-4 py-8 text-center text-sm text-gray-400">
                  No users found.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {showCreate && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-gray-800 border border-gray-700 rounded-lg w-full max-w-4xl max-h-[90vh] overflow-hidden">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-700">
              <h2 className="text-xl text-white font-semibold">
                {editingUser ? `Edit User: ${editingUser.username}` : 'Create User'}
              </h2>
              <button onClick={closeDialog} className="p-2 rounded hover:bg-gray-700">
                <X className="w-5 h-5 text-gray-300" />
              </button>
            </div>
            <form onSubmit={handleSubmit} className="p-6 space-y-6 overflow-y-auto max-h-[calc(90vh-80px)]">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <label className="text-sm text-gray-300">
                  Username
                  <input
                    type="text"
                    value={form.username}
                    onChange={(e) => setField('username', e.target.value)}
                    disabled={!!editingUser}
                    required
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white disabled:opacity-60"
                  />
                </label>
                <label className="text-sm text-gray-300">
                  Email
                  <input
                    type="email"
                    value={form.email}
                    onChange={(e) => setField('email', e.target.value)}
                    required
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  />
                </label>
                {!editingUser && (
                  <label className="text-sm text-gray-300">
                    Password
                    <input
                      type="password"
                      value={form.password}
                      onChange={(e) => setField('password', e.target.value)}
                      required
                      className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                    />
                  </label>
                )}
                <label className="text-sm text-gray-300">
                  Role
                  <select
                    value={form.role}
                    onChange={(e) => setField('role', e.target.value as 'Contributor' | 'Admin')}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  >
                    <option value="Contributor">Contributor</option>
                    <option value="Admin">Admin</option>
                  </select>
                </label>
                <label className="text-sm text-gray-300">
                  First Name
                  <input
                    type="text"
                    value={form.firstName}
                    onChange={(e) => setField('firstName', e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  />
                </label>
                <label className="text-sm text-gray-300">
                  Last Name
                  <input
                    type="text"
                    value={form.lastName}
                    onChange={(e) => setField('lastName', e.target.value)}
                    className="mt-1 w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white"
                  />
                </label>
                {editingUser && (
                  <label className="text-sm text-gray-300 flex items-center gap-2 mt-6">
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

              <div className="bg-gray-900/40 border border-gray-700 rounded-lg p-4">
                <div className="flex items-start justify-between gap-4 mb-4">
                  <div>
                    <h3 className="text-white font-medium flex items-center gap-2">
                      <Shield className="w-4 h-4" />
                      Custom Permission Overrides
                    </h3>
                    <p className="text-xs text-gray-400 mt-1">
                      Role defaults are always applied. Choose additional explicit permissions for this user.
                    </p>
                  </div>
                  <span className="text-xs px-2.5 py-1 rounded-full border border-blue-500/40 bg-blue-500/10 text-blue-300 whitespace-nowrap">
                    {form.customPermissions.length} selected
                  </span>
                </div>
                <div className="space-y-4">
                  {groupedPermissions.map(([group, groupPermissions]) => (
                    <div key={group} className="bg-gray-800/50 border border-gray-700 rounded-lg p-3">
                      <div className="flex items-center justify-between mb-3">
                        <p className="text-xs uppercase tracking-wider text-gray-500">{group}</p>
                        <span className="text-[11px] text-gray-500">{groupPermissions.length} permissions</span>
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5">
                        {groupPermissions.map((permission) => (
                          <label
                            key={permission.key}
                            className={`flex items-start gap-3 p-3 rounded-lg border cursor-pointer transition-colors ${
                              form.customPermissions.includes(permission.key)
                                ? 'bg-blue-500/10 border-blue-500/40'
                                : 'bg-gray-900/40 border-gray-700 hover:bg-gray-700/40 hover:border-gray-600'
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
                              <span className="block text-xs text-gray-400 mt-1">{permission.description}</span>
                            </span>
                          </label>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="flex justify-end gap-3">
                <button type="button" onClick={closeDialog} className="px-4 py-2 bg-gray-700 hover:bg-gray-600 rounded text-white">
                  Cancel
                </button>
                <button type="submit" disabled={isSubmitting} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 rounded text-white disabled:opacity-50">
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
