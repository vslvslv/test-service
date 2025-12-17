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
  const [newEnvName, setNewEnvName] = useState('');
  const [newEnvDescription, setNewEnvDescription] = useState('');
  const [error, setError] = useState('');
  const [isCreating, setIsCreating] = useState(false);

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
      setError(err.response?.data?.message || 'Failed to load environments');
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
      setError(err.response?.data?.message || 'Failed to create environment');
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
      alert(err.response?.data?.message || 'Failed to delete environment');
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
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Server className="w-8 h-8" />
            Environments
          </h1>
          <p className="text-gray-400 mt-1">Manage test environment configurations</p>
        </div>
        <button
          onClick={() => setShowCreateModal(true)}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
        >
          <Plus className="w-5 h-5" />
          Create Environment
        </button>
      </div>

      {/* Error Message */}
      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/50 rounded-lg flex items-center gap-2 text-red-400">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Search Bar */}
      <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search environments..."
            className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Total Environments</p>
          <p className="text-2xl font-bold text-white mt-1">{environments.length}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Search Results</p>
          <p className="text-2xl font-bold text-white mt-1">{filteredEnvironments.length}</p>
        </div>
      </div>

      {/* Environment List */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {filteredEnvironments.length > 0 ? (
          filteredEnvironments.map((env) => (
            <div
              key={env.id}
              className="bg-gray-800 rounded-lg border border-gray-700 p-6 hover:border-gray-600 transition-colors"
            >
              <div className="flex items-start justify-between mb-4">
                <div className="p-3 bg-green-500/10 rounded-lg border border-green-500/20">
                  <Globe className="w-6 h-6 text-green-500" />
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => {/* TODO: Edit functionality */}}
                    className="p-2 hover:bg-gray-700 rounded-lg transition-colors"
                    title="Edit"
                  >
                    <Edit className="w-4 h-4 text-gray-400 hover:text-white" />
                  </button>
                  <button
                    onClick={() => handleDelete(env.id, env.displayName || env.name)}
                    className="p-2 hover:bg-red-500/10 rounded-lg transition-colors"
                    title="Delete"
                  >
                    <Trash2 className="w-4 h-4 text-gray-400 hover:text-red-400" />
                  </button>
                </div>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">
                {env.displayName || env.name}
              </h3>
              {env.description && (
                <p className="text-sm text-gray-400">{env.description}</p>
              )}
              {env.url && (
                <p className="text-xs text-gray-500 mt-2 flex items-center gap-1">
                  <Globe className="w-3 h-3" />
                  {env.url}
                </p>
              )}
            </div>
          ))
        ) : (
          <div className="col-span-full text-center py-12 text-gray-500">
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
                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
              >
                <Plus className="w-5 h-5" />
                Create Environment
              </button>
            )}
          </div>
        )}
      </div>

      {/* Create Modal */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-gray-800 rounded-lg border border-gray-700 w-full max-w-md">
            <div className="p-6 border-b border-gray-700">
              <h2 className="text-xl font-semibold text-white">Create New Environment</h2>
            </div>
            <form onSubmit={handleCreateEnvironment} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-2">
                  Environment Name *
                </label>
                <input
                  type="text"
                  value={newEnvName}
                  onChange={(e) => setNewEnvName(e.target.value)}
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder="e.g., dev, staging, production"
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-2">
                  Description (Optional)
                </label>
                <textarea
                  value={newEnvDescription}
                  onChange={(e) => setNewEnvDescription(e.target.value)}
                  className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
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
                  className="flex-1 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isCreating}
                  className="flex-1 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
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
