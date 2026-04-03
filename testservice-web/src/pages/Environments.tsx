import React, { useState, useEffect } from 'react';
import { 
  Plus, 
  Search, 
  Server, 
  Edit, 
  Trash2,
  Globe,
  AlertCircle
} from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage } from '../types';

interface Environment {
  id: string;
  name: string;
  displayName?: string;
  description?: string;
  url?: string;
  color?: string;
  isActive?: boolean;
  order?: number;
  createdAt?: string;
  updatedAt?: string;
}

const Environments: React.FC = () => {
  const [environments, setEnvironments] = useState<Environment[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [editingEnvironment, setEditingEnvironment] = useState<Environment | null>(null);
  const [editDisplayName, setEditDisplayName] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [newEnvName, setNewEnvName] = useState('');
  const [newEnvDescription, setNewEnvDescription] = useState('');
  const [error, setError] = useState('');
  const [isCreating, setIsCreating] = useState(false);
  const [isUpdating, setIsUpdating] = useState(false);

  useEffect(() => {
    loadEnvironments();
  }, []);

  const loadEnvironments = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await apiService.getEnvironments();
      setEnvironments(data);
    } catch (err: any) {
      setError(getErrorMessage(err));
      console.error('Failed to load environments:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateEnvironment = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsCreating(true);
    setError('');

    try {
      await apiService.createEnvironment({
        name: newEnvName,
        description: newEnvDescription
      });

      setShowCreateModal(false);
      setNewEnvName('');
      setNewEnvDescription('');
      await loadEnvironments();
    } catch (err: any) {
      setError(getErrorMessage(err));
    } finally {
      setIsCreating(false);
    }
  };

  const handleDelete = async (envId: string, envName: string) => {
    if (!confirm(`Are you sure you want to delete the environment "${envName}"?`)) {
      return;
    }

    try {
      await apiService.request({
        method: 'DELETE',
        url: `/api/environments/${envId}`
      });
      
      await loadEnvironments();
    } catch (err: any) {
      alert(getErrorMessage(err));
    }
  };

  const handleEditClick = (env: Environment) => {
    setEditingEnvironment(env);
    setEditDisplayName(env.displayName || env.name);
    setEditDescription(env.description ?? '');
    setError('');
    setShowEditModal(true);
  };

  const handleCloseEditModal = () => {
    setShowEditModal(false);
    setEditingEnvironment(null);
    setEditDisplayName('');
    setEditDescription('');
    setError('');
  };

  const handleUpdateEnvironment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingEnvironment) return;
    setIsUpdating(true);
    setError('');

    try {
      await apiService.updateEnvironment(editingEnvironment.id, {
        displayName: editDisplayName.trim() || undefined,
        description: editDescription.trim() || undefined
      });
      handleCloseEditModal();
      await loadEnvironments();
    } catch (err: any) {
      setError(getErrorMessage(err));
    } finally {
      setIsUpdating(false);
    }
  };

  const filteredEnvironments = environments.filter(env =>
    env.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    env.description?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.9fr)]">
          <div className="space-y-4">
            <p className="eyebrow">Environment Registry</p>
            <div className="flex items-start gap-4">
              <div className="page-hero-icon">
                <Server className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <h1 className="text-3xl font-semibold tracking-tight text-white">Environments</h1>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-300">
                  Manage environment definitions, update display metadata, and keep runtime targets organized.
                </p>
              </div>
            </div>
          </div>

          <div className="panel p-5">
            <p className="eyebrow">Environment Snapshot</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <div className="stat-card">
                <p className="text-sm text-slate-400">Total</p>
                <p className="mt-2 text-3xl font-semibold text-white">{environments.length}</p>
                <p className="mt-2 text-xs text-slate-500">registered environments</p>
              </div>
              <div className="stat-card">
                <p className="text-sm text-slate-400">Visible</p>
                <p className="mt-2 text-3xl font-semibold text-white">{filteredEnvironments.length}</p>
                <p className="mt-2 text-xs text-slate-500">matching the current search</p>
              </div>
            </div>
            <button onClick={() => setShowCreateModal(true)} className="button-primary mt-5 w-full">
              <Plus className="h-4 w-4" />
              Create Environment
            </button>
          </div>
        </div>
      </section>

      {/* Error Message */}
      {error && (
        <div className="flex items-center gap-2 rounded-2xl border border-red-500/40 bg-red-500/10 p-4 text-red-300">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Search Bar */}
      <div className="panel p-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search environments..."
            className="field-shell w-full pl-10 pr-4"
          />
        </div>
      </div>

      {/* Environment List */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {filteredEnvironments.length > 0 ? (
          filteredEnvironments.map((env) => (
            <div
              key={env.id}
              className="panel p-6 transition-colors hover:border-slate-600"
            >
              <div className="flex items-start justify-between mb-4">
                <div className="p-3 bg-green-500/10 rounded-lg border border-green-500/20">
                  <Globe className="w-6 h-6 text-green-500" />
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => handleEditClick(env)}
                    className="rounded-xl p-2 hover:bg-slate-800 transition-colors"
                    title="Edit"
                  >
                    <Edit className="w-4 h-4 text-slate-400 hover:text-white" />
                  </button>
                  <button
                    onClick={() => handleDelete(env.id, env.displayName || env.name)}
                    className="rounded-xl p-2 hover:bg-red-500/10 transition-colors"
                    title="Delete"
                  >
                    <Trash2 className="w-4 h-4 text-slate-400 hover:text-red-400" />
                  </button>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">
                {env.displayName || env.name}
              </h3>
              {env.description && (
                <p className="text-sm text-slate-400">{env.description}</p>
              )}
              {env.url && (
                <p className="mt-2 flex items-center gap-1 text-xs text-slate-500">
                  <Globe className="w-3 h-3" />
                  {env.url}
                </p>
              )}
            </div>
          ))
        ) : (
          <div className="col-span-full panel py-12 text-center text-slate-500">
            <Server className="w-16 h-16 mx-auto mb-4 opacity-50" />
            <h3 className="text-lg font-medium text-gray-400 mb-2">
              {searchTerm ? 'No environments found' : 'No environments yet'}
            </h3>
            <p className="text-sm mb-4">
              {searchTerm
                ? 'Try adjusting your search terms'
                : 'Create your first environment to get started'}
            </p>
            {!searchTerm && (
              <button
                onClick={() => setShowCreateModal(true)}
                className="button-primary"
              >
                <Plus className="w-5 h-5" />
                Create Environment
              </button>
            )}
          </div>
        )}
      </div>

      {/* Edit Modal */}
      {showEditModal && editingEnvironment && (
        <div className="modal-backdrop">
          <div className="modal-shell max-w-md">
            <div className="border-b border-slate-800 p-6">
              <h2 className="text-xl font-semibold text-white">Edit Environment</h2>
            </div>
            <form onSubmit={handleUpdateEnvironment} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Name
                </label>
                <input
                  type="text"
                  value={editingEnvironment.name}
                  readOnly
                  className="field-shell cursor-not-allowed text-slate-400"
                />
                <p className="text-xs text-slate-500 mt-1">Environment name cannot be changed</p>
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Display Name
                </label>
                <input
                  type="text"
                  value={editDisplayName}
                  onChange={(e) => setEditDisplayName(e.target.value)}
                  className="field-shell"
                  placeholder="e.g., Development, Staging"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Description (Optional)
                </label>
                <textarea
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  className="field-shell resize-none"
                  placeholder="Brief description of this environment"
                  rows={3}
                />
              </div>
              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={handleCloseEditModal}
                  className="button-secondary flex-1 rounded-xl"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isUpdating}
                  className="button-primary flex-1 rounded-xl disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isUpdating ? 'Saving...' : 'Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Create Modal */}
      {showCreateModal && (
        <div className="modal-backdrop">
          <div className="modal-shell max-w-md">
            <div className="border-b border-slate-800 p-6">
              <h2 className="text-xl font-semibold text-white">Create New Environment</h2>
            </div>
            <form onSubmit={handleCreateEnvironment} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Environment Name *
                </label>
                <input
                  type="text"
                  value={newEnvName}
                  onChange={(e) => setNewEnvName(e.target.value)}
                  className="field-shell"
                  placeholder="e.g., dev, staging, production"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-slate-300 mb-2">
                  Description (Optional)
                </label>
                <textarea
                  value={newEnvDescription}
                  onChange={(e) => setNewEnvDescription(e.target.value)}
                  className="field-shell resize-none"
                  placeholder="Brief description of this environment"
                  rows={3}
                />
              </div>
              <div className="flex gap-3 pt-4">
                <button
                  type="button"
                  onClick={() => {
                    setShowCreateModal(false);
                    setNewEnvName('');
                    setNewEnvDescription('');
                    setError('');
                  }}
                  className="button-secondary flex-1 rounded-xl"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isCreating}
                  className="button-primary flex-1 rounded-xl disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isCreating ? 'Creating...' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default Environments;
