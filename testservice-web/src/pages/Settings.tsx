import React, { useEffect, useRef, useState } from 'react';
import {
  AlertCircle,
  Calendar,
  CheckCircle,
  Copy,
  Eye,
  EyeOff,
  Info,
  Key,
  Plus,
  Save,
  Settings as SettingsIcon,
  Trash2,
} from 'lucide-react';
import { apiService } from '../services/api';

interface DataRetentionSettings {
  schemaRetentionDays: number | null;
  entityRetentionDays: number | null;
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
  const [dataRetention, setDataRetention] = useState<DataRetentionSettings>({
    schemaRetentionDays: null,
    entityRetentionDays: 30,
    autoCleanupEnabled: true,
  });
  const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);
  const [showCreateKeyDialog, setShowCreateKeyDialog] = useState(false);
  const [newKeyName, setNewKeyName] = useState('');
  const [newKeyExpiration, setNewKeyExpiration] = useState<number | null>(90);
  const [visibleKeys, setVisibleKeys] = useState<Set<string>>(new Set());
  const [isSaving, setIsSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [error, setError] = useState('');
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const createKeyNameInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (showCreateKeyDialog) {
      createKeyNameInputRef.current?.focus();
    }
  }, [showCreateKeyDialog]);

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

      setApiKeys((current) => [...current, newKey]);
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
      setApiKeys((current) => current.filter((key) => key.id !== id));
    } catch (err) {
      setError('Failed to delete API key');
      console.error(err);
    }
  };

  const toggleKeyVisibility = (id: string) => {
    setVisibleKeys((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
  };

  const handleRetentionChange = (field: keyof DataRetentionSettings, value: boolean | number | null) => {
    setDataRetention((prev) => ({ ...prev, [field]: value }));
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
    if (days === null) return 'Never';
    if (days === 1) return '1 day';
    return `${days} days`;
  };

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="flex flex-col gap-6 xl:flex-row xl:items-end xl:justify-between">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-3">
              <div className="page-hero-icon">
                <SettingsIcon className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <p className="eyebrow">Platform Configuration</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">Control retention rules and integration access</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Tune cleanup behavior, review data lifecycle risk, and manage API credentials used by external consumers.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-3 xl:min-w-[520px]">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Cleanup</p>
              <p className={`mt-3 text-xl font-semibold ${dataRetention.autoCleanupEnabled ? 'text-emerald-300' : 'text-slate-300'}`}>
                {dataRetention.autoCleanupEnabled ? 'Enabled' : 'Disabled'}
              </p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">API Keys</p>
              <p className="mt-3 text-3xl font-semibold text-white">{apiKeys.length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Pending Changes</p>
              <p className="mt-3 text-3xl font-semibold text-white">{hasUnsavedChanges ? 1 : 0}</p>
            </div>
          </div>
        </div>
      </section>

      {saveSuccess && (
        <div className="rounded-2xl border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-300">
          <div className="flex items-center gap-2">
            <CheckCircle className="h-4 w-4" />
            <span>Settings saved successfully.</span>
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4" />
            <span>{error}</span>
          </div>
        </div>
      )}

      <section className="panel p-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="eyebrow">Retention Policy</p>
            <h2 className="mt-2 text-xl font-semibold text-white">Data lifecycle controls</h2>
          </div>
          {hasUnsavedChanges && (
            <button type="button" onClick={handleSaveSettings} disabled={isSaving} className="button-primary disabled:cursor-not-allowed disabled:opacity-60">
              <Save className="h-4 w-4" />
              {isSaving ? 'Saving...' : 'Save Changes'}
            </button>
          )}
        </div>

        <div className="mt-5 space-y-5">
          <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-5">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
              <div className="max-w-2xl">
                <div className="flex items-start gap-3">
                  <input
                    type="checkbox"
                    aria-label="Enable automatic cleanup"
                    checked={dataRetention.autoCleanupEnabled}
                    onChange={(e) => handleRetentionChange('autoCleanupEnabled', e.target.checked)}
                    className="mt-1 h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                  />
                  <div>
                    <p className="text-base font-medium text-white">Enable automatic cleanup</p>
                    <p className="mt-2 text-sm leading-6 text-slate-400">
                      Automatically remove data after its configured retention period. Disable this if your environment requires manual cleanup only.
                    </p>
                  </div>
                </div>
              </div>
              <span className={`badge-soft ${dataRetention.autoCleanupEnabled ? 'border-emerald-500/25 bg-emerald-500/10 text-emerald-300' : ''}`}>
                {dataRetention.autoCleanupEnabled ? 'Active' : 'Inactive'}
              </span>
            </div>
          </div>

          <div className="grid gap-5 xl:grid-cols-2">
            <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-5">
              <div className="mb-4 flex items-center gap-2">
                <span className="text-sm font-medium text-white">Schema retention period</span>
                <div className="group relative">
                  <Info className="h-4 w-4 text-slate-500" />
                  <div className="absolute left-0 top-full z-10 mt-2 hidden w-64 rounded-xl border border-slate-700 bg-slate-950 p-3 text-xs leading-5 text-slate-300 group-hover:block">
                    Controls how long schema definitions remain available before cleanup.
                  </div>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_180px]">
                <select
                  aria-label="Schema retention period"
                  value={dataRetention.schemaRetentionDays === null ? 'never' : dataRetention.schemaRetentionDays}
                  onChange={(e) => handleRetentionChange('schemaRetentionDays', e.target.value === 'never' ? null : parseInt(e.target.value, 10))}
                  className="field-shell"
                >
                  <option value="never">Never</option>
                  <option value="7">7 days</option>
                  <option value="30">30 days</option>
                  <option value="60">60 days</option>
                  <option value="90">90 days</option>
                  <option value="180">180 days</option>
                  <option value="365">1 year</option>
                </select>
                <div className="inline-flex items-center gap-2 rounded-2xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-300">
                  <Calendar className="h-4 w-4 text-slate-500" />
                  {getRetentionDisplay(dataRetention.schemaRetentionDays)}
                </div>
              </div>
            </div>

            <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-5">
              <div className="mb-4 flex items-center gap-2">
                <span className="text-sm font-medium text-white">Entity retention period</span>
                <div className="group relative">
                  <Info className="h-4 w-4 text-slate-500" />
                  <div className="absolute left-0 top-full z-10 mt-2 hidden w-64 rounded-xl border border-slate-700 bg-slate-950 p-3 text-xs leading-5 text-slate-300 group-hover:block">
                    Applies to both available and consumed entities stored in the system.
                  </div>
                </div>
              </div>
              <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_180px]">
                <select
                  aria-label="Entity retention period"
                  value={dataRetention.entityRetentionDays === null ? 'never' : dataRetention.entityRetentionDays}
                  onChange={(e) => handleRetentionChange('entityRetentionDays', e.target.value === 'never' ? null : parseInt(e.target.value, 10))}
                  className="field-shell"
                >
                  <option value="never">Never</option>
                  <option value="1">1 day</option>
                  <option value="7">7 days</option>
                  <option value="14">14 days</option>
                  <option value="30">30 days</option>
                  <option value="60">60 days</option>
                  <option value="90">90 days</option>
                  <option value="180">180 days</option>
                </select>
                <div className="inline-flex items-center gap-2 rounded-2xl border border-slate-800 bg-slate-900/70 px-4 py-3 text-sm text-slate-300">
                  <Calendar className="h-4 w-4 text-slate-500" />
                  {getRetentionDisplay(dataRetention.entityRetentionDays)}
                </div>
              </div>
            </div>
          </div>

          {dataRetention.autoCleanupEnabled && (dataRetention.schemaRetentionDays !== null || dataRetention.entityRetentionDays !== null) && (
            <div className="rounded-[24px] border border-amber-500/30 bg-amber-500/10 p-5">
              <div className="flex gap-3">
                <AlertCircle className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-300" />
                <div>
                  <p className="text-sm font-medium text-amber-200">Automatic deletion is active</p>
                  <p className="mt-2 text-sm leading-6 text-amber-100/80">
                    Data older than the configured retention thresholds will be permanently removed. Verify backup and recovery expectations before shortening these windows.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>
      </section>

      <section className="table-shell">
        <div className="panel-header flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="eyebrow">Integration Access</p>
            <h2 className="mt-2 text-xl font-semibold text-white">API key management</h2>
          </div>
          <button type="button" onClick={() => setShowCreateKeyDialog(true)} className="button-primary">
            <Plus className="h-4 w-4" />
            Generate Key
          </button>
        </div>

        <div className="p-5">
          {apiKeys.length === 0 ? (
            <div className="py-12 text-center">
              <Key className="mx-auto h-14 w-14 text-slate-600" />
              <h3 className="mt-4 text-lg font-medium text-white">No API keys</h3>
              <p className="mt-2 text-sm text-slate-400">Generate your first key to enable external integrations or automation flows.</p>
              <div className="mt-6">
                <button type="button" onClick={() => setShowCreateKeyDialog(true)} className="button-primary">
                  <Plus className="h-4 w-4" />
                  Generate API Key
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              {apiKeys.map((apiKey) => (
                <div key={apiKey.id} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-5">
                  <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <h3 className="text-lg font-semibold text-white">{apiKey.name}</h3>
                        {apiKey.expiresAt ? (
                          <span className="badge-soft border-amber-500/25 bg-amber-500/10 text-amber-300">Expires {formatDate(apiKey.expiresAt)}</span>
                        ) : (
                          <span className="badge-soft border-emerald-500/25 bg-emerald-500/10 text-emerald-300">No Expiration</span>
                        )}
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-slate-500">
                        <span>Created {formatDate(apiKey.createdAt)}</span>
                        {apiKey.lastUsed && <span>Last used {formatDate(apiKey.lastUsed)}</span>}
                      </div>
                    </div>
                  </div>

                  <div className="mt-4 flex flex-col gap-3 lg:flex-row lg:items-center">
                    <div className="flex-1 rounded-2xl border border-slate-800 bg-slate-900/80 px-4 py-3 font-mono text-sm text-slate-300">
                      {visibleKeys.has(apiKey.id) ? apiKey.key : '••••••••••••••••••••••••'}
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => toggleKeyVisibility(apiKey.id)}
                        className="button-secondary !rounded-xl !px-3 !py-2"
                        title={visibleKeys.has(apiKey.id) ? 'Hide key' : 'Show key'}
                      >
                        {visibleKeys.has(apiKey.id) ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                      </button>
                      <button type="button" onClick={() => copyToClipboard(apiKey.key)} className="button-secondary !rounded-xl !px-3 !py-2" title="Copy to clipboard">
                        <Copy className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeleteApiKey(apiKey.id)}
                        className="inline-flex items-center justify-center rounded-xl border border-red-500/30 bg-red-500/10 px-3 py-2 text-red-200 transition-colors hover:bg-red-500/15"
                        title="Delete key"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>

      {showCreateKeyDialog && (
        <div
          className="modal-backdrop"
          role="presentation"
          onClick={(e) => { if (e.target === e.currentTarget) setShowCreateKeyDialog(false); }}
        >
          <div
            role="dialog"
            aria-modal="true"
            className="modal-shell max-w-xl"
          >
            <div className="border-b border-slate-800 px-6 py-5">
              <h3 className="text-xl font-semibold text-white">Generate API key</h3>
              <p className="mt-2 text-sm text-slate-400">Create a credential for automation, integrations, or CI consumers.</p>
            </div>

            <div className="space-y-4 px-6 py-5">
              <div>
                <label htmlFor="settings-create-key-name" className="mb-2 block text-sm font-medium text-slate-300">Key name</label>
                <input
                  id="settings-create-key-name"
                  ref={createKeyNameInputRef}
                  type="text"
                  value={newKeyName}
                  onChange={(e) => setNewKeyName(e.target.value)}
                  placeholder="e.g. CI Pipeline, Prod Sync"
                  className="field-shell"
                />
              </div>

              <div>
                <label htmlFor="settings-create-key-expiration" className="mb-2 block text-sm font-medium text-slate-300">Expiration</label>
                <select
                  id="settings-create-key-expiration"
                  value={newKeyExpiration === null ? 'never' : newKeyExpiration}
                  onChange={(e) => setNewKeyExpiration(e.target.value === 'never' ? null : parseInt(e.target.value, 10))}
                  className="field-shell"
                >
                  <option value="30">30 days</option>
                  <option value="60">60 days</option>
                  <option value="90">90 days</option>
                  <option value="180">180 days</option>
                  <option value="365">1 year</option>
                  <option value="never">Never</option>
                </select>
              </div>

              <div className="rounded-[24px] border border-blue-500/25 bg-blue-500/10 p-4 text-sm leading-6 text-blue-100/80">
                Copy the generated secret after creation. For security reasons, this is the only place where the raw key may be visible.
              </div>
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-800 px-6 py-5">
              <button
                type="button"
                onClick={() => {
                  setShowCreateKeyDialog(false);
                  setNewKeyName('');
                  setNewKeyExpiration(90);
                }}
                className="button-secondary"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleCreateApiKey}
                disabled={!newKeyName.trim()}
                className="button-primary disabled:cursor-not-allowed disabled:opacity-60"
              >
                Generate Key
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Settings;
