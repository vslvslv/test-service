import React, { useState, useEffect } from 'react';
import {
  Settings as SettingsIcon,
  Database,
  Key,
  Save,
  AlertCircle,
  CheckCircle,
  Trash2,
  Plus,
  Copy,
  Eye,
  EyeOff,
  Calendar,
  Info
} from 'lucide-react';
import { apiService } from '../services/api';

interface DataRetentionSettings {
  schemaRetentionDays: number | null; // null = infinity
  entityRetentionDays: number | null; // null = infinity
  autoCleanupEnabled: boolean;
}

interface ApiKey {
  id: string;
  name: string;
  key: string;
  expiresAt: string | null;
  createdAt: string;
  lastUsed: string | null;
}

const Settings: React.FC = () => {
  // Data Retention State
  const [dataRetention, setDataRetention] = useState<DataRetentionSettings>({
    schemaRetentionDays: null,
    entityRetentionDays: 30,
    autoCleanupEnabled: true,
  });

  // API Keys State
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);
  const [showCreateKeyDialog, setShowCreateKeyDialog] = useState(false);
  const [newKeyName, setNewKeyName] = useState('');
  const [newKeyExpiration, setNewKeyExpiration] = useState<number | null>(90);
  const [visibleKeys, setVisibleKeys] = useState<Set<string>>(new Set());

  // UI State
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [error, setError] = useState('');
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  useEffect(() => {
    loadSettings();
    loadApiKeys();
  }, []);

  const loadSettings = async () => {
    try {
      const data = await apiService.getSettings();
      setDataRetention(data.dataRetention);
    } catch (err) {
      console.error('Failed to load settings:', err);
      setError('Failed to load settings');
    }
  };

  const loadApiKeys = async () => {
    try {
      const keys = await apiService.getApiKeys();
      setApiKeys(keys);
    } catch (err) {
      console.error('Failed to load API keys:', err);
      setError('Failed to load API keys');
    }
  };

  const handleSaveSettings = async () => {
    setIsSaving(true);
    setError('');
    setSaveSuccess(false);

    try {
      await apiService.updateSettings({ dataRetention });
      
      setSaveSuccess(true);
      setHasUnsavedChanges(false);
      
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err) {
      setError('Failed to save settings');
      console.error(err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCreateApiKey = async () => {
    if (!newKeyName.trim()) {
      setError('API key name is required');
      return;
    }

    try {
      const newKey = await apiService.createApiKey({
        name: newKeyName,
        expirationDays: newKeyExpiration
      });

      setApiKeys([...apiKeys, newKey]);
      setShowCreateKeyDialog(false);
      setNewKeyName('');
      setNewKeyExpiration(90);
      setError('');
    } catch (err) {
      setError('Failed to create API key');
      console.error(err);
    }
  };

  const handleDeleteApiKey = async (id: string) => {
    if (!confirm('Are you sure you want to delete this API key? This action cannot be undone.')) {
      return;
    }

    try {
      await apiService.deleteApiKey(id);
      setApiKeys(apiKeys.filter(key => key.id !== id));
    } catch (err) {
      setError('Failed to delete API key');
      console.error(err);
    }
  };

  const toggleKeyVisibility = (id: string) => {
    const newVisible = new Set(visibleKeys);
    if (newVisible.has(id)) {
      newVisible.delete(id);
    } else {
      newVisible.add(id);
    }
    setVisibleKeys(newVisible);
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    // Could add a toast notification here
  };

  const handleRetentionChange = (field: keyof DataRetentionSettings, value: any) => {
    setDataRetention(prev => ({ ...prev, [field]: value }));
    setHasUnsavedChanges(true);
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'Never';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const getRetentionDisplay = (days: number | null) => {
    if (days === null) return 'Never (Keep Forever)';
    if (days === 1) return '1 day';
    return `${days} days`;
  };

  return (
    <div className="space-y-6 max-w-6xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <SettingsIcon className="w-8 h-8" />
            Settings
          </h1>
          <p className="text-gray-400 mt-1">Manage application configuration and preferences</p>
        </div>
        {hasUnsavedChanges && (
          <button
            onClick={handleSaveSettings}
            disabled={isSaving}
            className="flex items-center gap-2 px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isSaving ? (
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                Saving...
              </>
            ) : (
              <>
                <Save className="w-5 h-5" />
                Save Changes
              </>
            )}
          </button>
        )}
      </div>

      {/* Success Message */}
      {saveSuccess && (
        <div className="p-4 bg-green-500/10 border border-green-500/50 rounded-lg flex items-center gap-2 text-green-400">
          <CheckCircle className="w-5 h-5 flex-shrink-0" />
          <span>Settings saved successfully!</span>
        </div>
      )}

      {/* Error Message */}
      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/50 rounded-lg flex items-center gap-2 text-red-400">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Data Retention Section */}
      <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
        <div className="p-6 border-b border-gray-700 bg-gray-800/50">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-purple-500/10 rounded-lg border border-purple-500/20">
              <Database className="w-6 h-6 text-purple-500" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-white">Data Retention</h2>
              <p className="text-sm text-gray-400 mt-1">Configure how long data is stored before automatic cleanup</p>
            </div>
          </div>
        </div>

        <div className="p-6 space-y-6">
          {/* Auto Cleanup Toggle */}
          <div className="flex items-start justify-between p-4 bg-gray-700/30 rounded-lg border border-gray-600">
            <div className="flex-1">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={dataRetention.autoCleanupEnabled}
                  onChange={(e) => handleRetentionChange('autoCleanupEnabled', e.target.checked)}
                  className="w-5 h-5 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-purple-500"
                />
                <div>
                  <span className="text-white font-medium">Enable Automatic Cleanup</span>
                  <p className="text-sm text-gray-400 mt-1">
                    Automatically delete expired data based on retention periods
                  </p>
                </div>
              </label>
            </div>
            <div className="ml-4">
              <div className={`px-3 py-1 rounded-full text-sm font-medium ${
                dataRetention.autoCleanupEnabled 
                  ? 'bg-green-500/20 text-green-400' 
                  : 'bg-gray-600/50 text-gray-400'
              }`}>
                {dataRetention.autoCleanupEnabled ? 'Active' : 'Inactive'}
              </div>
            </div>
          </div>

          {/* Schema Retention */}
          <div className="space-y-3">
            <label className="block">
              <div className="flex items-center gap-2 mb-3">
                <span className="text-white font-medium">Schema Retention Period</span>
                <div className="group relative">
                  <Info className="w-4 h-4 text-gray-400 cursor-help" />
                  <div className="absolute left-0 bottom-full mb-2 hidden group-hover:block w-64 p-2 bg-gray-900 border border-gray-700 rounded text-xs text-gray-300 z-10">
                    How long to keep schema definitions before deletion. Set to "Never" to keep schemas indefinitely.
                  </div>
                </div>
              </div>
              <div className="flex gap-3">
                <div className="flex-1">
                  <select
                    value={dataRetention.schemaRetentionDays === null ? 'never' : dataRetention.schemaRetentionDays}
                    onChange={(e) => handleRetentionChange('schemaRetentionDays', e.target.value === 'never' ? null : parseInt(e.target.value))}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500"
                  >
                    <option value="never">Never (Keep Forever)</option>
                    <option value="7">7 days</option>
                    <option value="30">30 days</option>
                    <option value="60">60 days</option>
                    <option value="90">90 days</option>
                    <option value="180">180 days</option>
                    <option value="365">1 year</option>
                  </select>
                </div>
                <div className="px-4 py-2 bg-gray-700/50 border border-gray-600 rounded-lg text-gray-300 min-w-[180px] flex items-center">
                  <Calendar className="w-4 h-4 mr-2 text-gray-400" />
                  {getRetentionDisplay(dataRetention.schemaRetentionDays)}
                </div>
              </div>
            </label>
          </div>

          {/* Entity Retention */}
          <div className="space-y-3">
            <label className="block">
              <div className="flex items-center gap-2 mb-3">
                <span className="text-white font-medium">Entity Retention Period</span>
                <div className="group relative">
                  <Info className="w-4 h-4 text-gray-400 cursor-help" />
                  <div className="absolute left-0 bottom-full mb-2 hidden group-hover:block w-64 p-2 bg-gray-900 border border-gray-700 rounded text-xs text-gray-300 z-10">
                    How long to keep entity data before deletion. Applies to both consumed and available entities.
                  </div>
                </div>
              </div>
              <div className="flex gap-3">
                <div className="flex-1">
                  <select
                    value={dataRetention.entityRetentionDays === null ? 'never' : dataRetention.entityRetentionDays}
                    onChange={(e) => handleRetentionChange('entityRetentionDays', e.target.value === 'never' ? null : parseInt(e.target.value))}
                    className="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-purple-500"
                  >
                    <option value="never">Never (Keep Forever)</option>
                    <option value="1">1 day</option>
                    <option value="7">7 days</option>
                    <option value="14">14 days</option>
                    <option value="30">30 days</option>
                    <option value="60">60 days</option>
                    <option value="90">90 days</option>
                    <option value="180">180 days</option>
                  </select>
                </div>
                <div className="px-4 py-2 bg-gray-700/50 border border-gray-600 rounded-lg text-gray-300 min-w-[180px] flex items-center">
                  <Calendar className="w-4 h-4 mr-2 text-gray-400" />
                  {getRetentionDisplay(dataRetention.entityRetentionDays)}
                </div>
              </div>
            </label>
          </div>

          {/* Warning Message */}
          {dataRetention.autoCleanupEnabled && (dataRetention.schemaRetentionDays !== null || dataRetention.entityRetentionDays !== null) && (
            <div className="p-4 bg-orange-500/10 border border-orange-500/30 rounded-lg">
              <div className="flex gap-3">
                <AlertCircle className="w-5 h-5 text-orange-400 flex-shrink-0 mt-0.5" />
                <div className="flex-1">
                  <p className="text-orange-400 font-medium mb-1">Automatic Cleanup Enabled</p>
                  <p className="text-sm text-orange-300/80">
                    Data older than the configured retention period will be permanently deleted. 
                    This action cannot be undone. Make sure you have backups if needed.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* API Keys Section */}
      <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
        <div className="p-6 border-b border-gray-700 bg-gray-800/50">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="p-2 bg-blue-500/10 rounded-lg border border-blue-500/20">
                <Key className="w-6 h-6 text-blue-500" />
              </div>
              <div>
                <h2 className="text-xl font-semibold text-white">API Keys</h2>
                <p className="text-sm text-gray-400">Manage API keys for external integrations</p>
              </div>
            </div>
            <button
              onClick={() => setShowCreateKeyDialog(true)}
              className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
            >
              <Plus className="w-4 h-4" />
              Generate Key
            </button>
          </div>
        </div>

        <div className="p-6">
          {apiKeys.length === 0 ? (
            <div className="text-center py-12">
              <Key className="w-16 h-16 mx-auto mb-4 opacity-50 text-gray-500" />
              <h3 className="text-lg font-medium text-gray-400 mb-2">No API Keys</h3>
              <p className="text-sm text-gray-500 mb-4">
                Generate your first API key to start integrating with external services
              </p>
              <button
                onClick={() => setShowCreateKeyDialog(true)}
                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
              >
                <Plus className="w-4 h-4" />
                Generate API Key
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              {apiKeys.map((apiKey) => (
                <div
                  key={apiKey.id}
                  className="p-4 bg-gray-700/30 rounded-lg border border-gray-600 hover:border-gray-500 transition-colors"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex-1">
                      <h3 className="text-white font-medium mb-1">{apiKey.name}</h3>
                      <div className="flex items-center gap-4 text-sm text-gray-400">
                        <span>Created {formatDate(apiKey.createdAt)}</span>
                        {apiKey.lastUsed && (
                          <span>Last used {formatDate(apiKey.lastUsed)}</span>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {apiKey.expiresAt && (
                        <div className="px-3 py-1 bg-orange-500/20 text-orange-400 rounded-md text-sm font-medium">
                          Expires {formatDate(apiKey.expiresAt)}
                        </div>
                      )}
                      {!apiKey.expiresAt && (
                        <div className="px-3 py-1 bg-green-500/20 text-green-400 rounded-md text-sm font-medium">
                          Never Expires
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    <div className="flex-1 flex items-center gap-2 px-3 py-2 bg-gray-800 border border-gray-600 rounded-lg font-mono text-sm">
                      <code className="flex-1 text-gray-300">
                        {visibleKeys.has(apiKey.id) ? apiKey.key : '••••••••••••••••••••'}
                      </code>
                    </div>
                    <button
                      onClick={() => toggleKeyVisibility(apiKey.id)}
                      className="p-2 hover:bg-gray-600 rounded-lg transition-colors"
                      title={visibleKeys.has(apiKey.id) ? 'Hide key' : 'Show key'}
                    >
                      {visibleKeys.has(apiKey.id) ? (
                        <EyeOff className="w-5 h-5 text-gray-400 hover:text-white" />
                      ) : (
                        <Eye className="w-5 h-5 text-gray-400 hover:text-white" />
                      )}
                    </button>
                    <button
                      onClick={() => copyToClipboard(apiKey.key)}
                      className="p-2 hover:bg-gray-600 rounded-lg transition-colors"
                      title="Copy to clipboard"
                    >
                      <Copy className="w-5 h-5 text-gray-400 hover:text-white" />
                    </button>
                    <button
                      onClick={() => handleDeleteApiKey(apiKey.id)}
                      className="p-2 hover:bg-red-500/10 rounded-lg transition-colors"
                      title="Delete key"
                    >
                      <Trash2 className="w-5 h-5 text-gray-400 hover:text-red-400" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Create API Key Dialog */}
      {showCreateKeyDialog && (
        <>
          <div 
            className="fixed inset-0 bg-black/50 z-40"
            onClick={() => setShowCreateKeyDialog(false)}
          />
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div 
              className="bg-gray-800 rounded-lg border border-gray-700 max-w-md w-full"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="p-6 border-b border-gray-700">
                <h3 className="text-xl font-semibold text-white">Generate API Key</h3>
                <p className="text-sm text-gray-400 mt-1">Create a new API key for external integrations</p>
              </div>

              <div className="p-6 space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    Key Name *
                  </label>
                  <input
                    type="text"
                    value={newKeyName}
                    onChange={(e) => setNewKeyName(e.target.value)}
                    placeholder="e.g., Production API, CI/CD Pipeline"
                    className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                    autoFocus
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-300 mb-2">
                    Expiration Period
                  </label>
                  <select
                    value={newKeyExpiration === null ? 'never' : newKeyExpiration}
                    onChange={(e) => setNewKeyExpiration(e.target.value === 'never' ? null : parseInt(e.target.value))}
                    className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                  >
                    <option value="30">30 days</option>
                    <option value="60">60 days</option>
                    <option value="90">90 days</option>
                    <option value="180">180 days</option>
                    <option value="365">1 year</option>
                    <option value="never">Never (No expiration)</option>
                  </select>
                </div>

                <div className="p-3 bg-blue-500/10 border border-blue-500/30 rounded-lg">
                  <p className="text-sm text-blue-300">
                    <strong>Note:</strong> Make sure to copy your API key after creation. 
                    You won't be able to see it again for security reasons.
                  </p>
                </div>
              </div>

              <div className="flex items-center justify-end gap-3 p-6 border-t border-gray-700">
                <button
                  onClick={() => {
                    setShowCreateKeyDialog(false);
                    setNewKeyName('');
                    setNewKeyExpiration(90);
                  }}
                  className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
                >
                  Cancel
                </button>
                <button
                  onClick={handleCreateApiKey}
                  disabled={!newKeyName.trim()}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Generate Key
                </button>
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
};

export default Settings;
