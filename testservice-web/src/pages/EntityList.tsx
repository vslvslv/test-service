import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
  Download,
  Eye,
  Filter,
  Package,
  Plus,
  RefreshCw,
  Search,
  Settings,
  Trash2,
  Upload,
  XCircle
} from 'lucide-react';
import { apiService } from '../services/api';
import EntityCreateDialog from '../components/EntityCreateDialog';
import EntityViewDialog from '../components/EntityViewDialog';
import { getErrorMessage, getErrorStatus } from '../types';

interface Entity {
  id: string;
  entityType: string;
  fields: Record<string, unknown>;
  isConsumed: boolean;
  environment?: string;
  createdAt?: string;
  updatedAt?: string;
}

interface SchemaField {
  name: string;
  type?: string;
  required?: boolean;
}

interface Schema {
  entityName: string;
  fields: SchemaField[];
  filterableFields?: string[];
  excludeOnFetch: boolean;
}

interface ImportResult {
  created: number;
  updated: number;
  skipped: number;
  errors: Array<{ row: number; message: string }>;
}

const EntityList: React.FC = () => {
  const navigate = useNavigate();
  const { entityType } = useParams<{ entityType: string }>();
  const [entities, setEntities] = useState<Entity[]>([]);
  const [schema, setSchema] = useState<Schema | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [schemaNotFound, setSchemaNotFound] = useState(false);
  const [showConsumedOnly, setShowConsumedOnly] = useState(false);
  const [selectedEnvironment, setSelectedEnvironment] = useState('all');
  const [selectedEntity, setSelectedEntity] = useState<Entity | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [importFile, setImportFile] = useState<File | null>(null);
  const [importEnvironment, setImportEnvironment] = useState('');
  const [importMode, setImportMode] = useState<'append' | 'upsert'>('append');
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [isImporting, setIsImporting] = useState(false);
  const [showExportMenu, setShowExportMenu] = useState(false);
  const [importEnvironments, setImportEnvironments] = useState<string[]>([]);
  const [showColumnMenu, setShowColumnMenu] = useState(false);
  const [visibleColumns, setVisibleColumns] = useState<Set<string>>(new Set(['status']));

  useEffect(() => {
    if (entityType) {
      loadData();
    }
    // loadData closes over `entityType` only; setters are stable. Re-running on entityType change is the explicit intent.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entityType]);

  useEffect(() => {
    if (!schema) return;

    const defaultColumns = new Set<string>(['status']);
    schema.fields.slice(0, 3).forEach((field) => defaultColumns.add(field.name));
    if (entities.some((entity) => entity.environment)) {
      defaultColumns.add('environment');
    }
    setVisibleColumns(defaultColumns);
  }, [schema, entities]);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    setSchemaNotFound(false);
    setSchema(null);
    setEntities([]);
    try {
      const schemaData = await apiService.getSchema(entityType!);
      setSchema(schemaData);

      const entitiesData = await apiService.request({
        method: 'GET',
        url: `/api/entities/${entityType}`
      });
      setEntities(entitiesData);
    } catch (err: unknown) {
      if (getErrorStatus(err) === 404) {
        setSchemaNotFound(true);
      } else {
        setError(getErrorMessage(err));
      }
      console.error('Failed to load entities:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateSuccess = async () => {
    setSuccessMessage('Entity created successfully.');
    setTimeout(() => setSuccessMessage(''), 4000);
    try {
      await loadData();
    } catch (refreshError: unknown) {
      setError(getErrorMessage(refreshError));
    }
  };

  const handleDeleteEntity = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm('Are you sure you want to delete this entity?')) {
      return;
    }

    try {
      await apiService.deleteEntity(entityType!, id);
      if (selectedEntity?.id === id) {
        setIsViewDialogOpen(false);
        setSelectedEntity(null);
      }
      setSuccessMessage('Entity deleted successfully.');
      setTimeout(() => setSuccessMessage(''), 4000);
      setEntities((current) => current.filter((entity) => entity.id !== id));
      await loadData();
    } catch (err: unknown) {
      alert(getErrorMessage(err));
    }
  };

  const handleResetEntity = async (id: string, e?: React.MouseEvent) => {
    if (e) e.stopPropagation();
    try {
      await apiService.request({
        method: 'POST',
        url: `/api/entities/${entityType}/${id}/reset`
      });
      await loadData();
      if (selectedEntity?.id === id) {
        setSelectedEntity((current) => (current ? { ...current, isConsumed: false } : current));
      }
    } catch (err: unknown) {
      alert(getErrorMessage(err));
    }
  };

  const handleResetAll = async () => {
    if (!confirm('Are you sure you want to reset all consumed entities? This will make them available again.')) {
      return;
    }

    try {
      await apiService.request({
        method: 'POST',
        url: `/api/entities/${entityType}/reset-all`
      });
      await loadData();
    } catch (err: unknown) {
      alert(getErrorMessage(err));
    }
  };

  const handleGetNext = async () => {
    try {
      const entity = await apiService.getNextAvailable(entityType!);
      if (!entity) {
        alert('No available entities found');
        return;
      }
      await loadData();
      setSelectedEntity(entity);
      setIsViewDialogOpen(true);
    } catch (err: unknown) {
      alert(getErrorMessage(err));
    }
  };

  const handleExport = async (format: 'json' | 'csv') => {
    try {
      const environment = selectedEnvironment !== 'all' ? selectedEnvironment : undefined;
      const blob = await apiService.exportEntities(entityType!, format, environment);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${entityType}-export.${format}`;
      a.click();
      URL.revokeObjectURL(url);
      setShowExportMenu(false);
    } catch (err: unknown) {
      alert(getErrorMessage(err));
    }
  };

  const openImportModal = async () => {
    setImportFile(null);
    setImportResult(null);
    setImportEnvironment(selectedEnvironment !== 'all' ? selectedEnvironment : '');
    setImportMode('append');
    setIsImportModalOpen(true);
    try {
      const envs = await apiService.getEnvironments();
      setImportEnvironments(envs.map((env: { name: string }) => env.name));
    } catch {
      setImportEnvironments(environments);
    }
  };

  const handleImportSubmit = async () => {
    if (!importFile || !entityType) return;

    setIsImporting(true);
    setImportResult(null);
    try {
      const result = await apiService.importEntities(entityType, importFile, {
        environment: importEnvironment || undefined,
        mode: importMode
      });
      setImportResult(result);
      if (result.created + result.updated > 0) {
        await loadData();
      }
    } catch (err: unknown) {
      setImportResult({
        created: 0,
        updated: 0,
        skipped: 0,
        errors: [{ row: 0, message: getErrorMessage(err) }]
      });
    } finally {
      setIsImporting(false);
    }
  };

  const handleEditFromDialog = async (updatedEntity: { fields: Record<string, unknown>; environment?: string }) => {
    if (!selectedEntity || !entityType) return;

    await apiService.updateEntity(entityType, selectedEntity.id, updatedEntity);
    setSuccessMessage('Entity updated successfully.');
    setTimeout(() => setSuccessMessage(''), 4000);
    await loadData();

    const refreshedEntity = await apiService.getEntity(entityType, selectedEntity.id);
    setSelectedEntity(refreshedEntity as Entity);
  };

  const toggleColumn = (columnName: string) => {
    setVisibleColumns((current) => {
      const next = new Set(current);
      if (next.has(columnName)) {
        if (columnName !== 'status') {
          next.delete(columnName);
        }
      } else {
        next.add(columnName);
      }
      return next;
    });
  };

  const selectAllColumns = () => {
    if (!schema) return;
    const allColumns = new Set<string>(['status']);
    if (entities.some((entity) => entity.environment)) {
      allColumns.add('environment');
    }
    schema.fields.forEach((field) => allColumns.add(field.name));
    setVisibleColumns(allColumns);
  };

  const deselectAllColumns = () => {
    setVisibleColumns(new Set(['status']));
  };

  const filteredEntities = entities.filter((entity) => {
    if (searchTerm) {
      const searchLower = searchTerm.toLowerCase();
      const matchesSearch = Object.values(entity.fields).some((value) => String(value).toLowerCase().includes(searchLower));
      if (!matchesSearch) return false;
    }

    if (showConsumedOnly && !entity.isConsumed) return false;
    if (selectedEnvironment !== 'all' && entity.environment !== selectedEnvironment) return false;
    return true;
  });

  const availableCount = entities.filter((entity) => !entity.isConsumed).length;
  const consumedCount = entities.filter((entity) => entity.isConsumed).length;
  const environments = Array.from(new Set(entities.map((entity) => entity.environment).filter(Boolean))) as string[];
  const totalFields = schema?.fields.length || 0;

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="h-12 w-12 animate-spin rounded-full border-b-2 border-blue-500" />
      </div>
    );
  }

  if (schemaNotFound || (!schema && !error)) {
    return (
      <div className="panel p-12 text-center">
        <AlertCircle className="mx-auto h-14 w-14 text-red-400" />
        <h2 className="mt-4 text-xl font-semibold text-white">Entity type not found</h2>
        <p className="mt-2 text-sm text-slate-400">The entity type "{entityType}" could not be found.</p>
        <div className="mt-6">
          <button type="button" onClick={() => navigate('/entities')} className="button-primary">
            Back to Entities
          </button>
        </div>
      </div>
    );
  }

  if (error && !schema) {
    return (
      <div className="rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
        <div className="flex items-center gap-2">
          <AlertCircle className="h-4 w-4" />
          <span>{error}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
            <div className="max-w-3xl">
              <div className="inline-flex items-center gap-3">
                <button
                  type="button"
                  onClick={() => navigate('/entities')}
                  className="page-hero-icon text-slate-300 transition-colors hover:text-white"
                  title="Back to entities"
                >
                  <ArrowLeft className="h-5 w-5" />
                </button>
                <div className="page-hero-icon">
                  <Package className="h-6 w-6 text-blue-300" />
                </div>
                <div>
                  <p className="eyebrow">Entity Workspace</p>
                  <div className="mt-2 flex flex-wrap items-center gap-3">
                    <h1 className="text-3xl font-semibold tracking-tight text-white">{entityType}</h1>
                    {schema.excludeOnFetch && (
                      <span className="badge-soft border-amber-500/25 bg-amber-500/10 text-amber-300">Auto-consume</span>
                    )}
                  </div>
                </div>
              </div>
              <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
                Operate this entity pool with import, export, allocation, reset, and direct inspection from one dense workspace.
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              {schema.excludeOnFetch && consumedCount > 0 && (
                <button type="button" onClick={handleResetAll} className="button-secondary">
                  <RefreshCw className="h-4 w-4" />
                  Reset All
                </button>
              )}
              {schema.excludeOnFetch && availableCount > 0 && (
                <button type="button" onClick={handleGetNext} className="button-secondary">
                  <Download className="h-4 w-4" />
                  Get Next
                </button>
              )}
              <button type="button" onClick={openImportModal} className="button-secondary">
                <Upload className="h-4 w-4" />
                Import
              </button>
              <div className="relative">
                <button type="button" onClick={() => setShowExportMenu((current) => !current)} className="button-secondary">
                  <Download className="h-4 w-4" />
                  Export
                  <ChevronDown className="h-4 w-4" />
                </button>
                {showExportMenu && (
                  <>
                    <button type="button" aria-label="Close export menu" className="fixed inset-0 z-10" onClick={() => setShowExportMenu(false)} />
                    <div className="absolute right-0 z-20 mt-2 w-40 rounded-2xl border border-slate-700 bg-slate-950/95 p-1 shadow-2xl">
                      <button type="button" onClick={() => handleExport('json')} className="block w-full rounded-xl px-3 py-2 text-left text-sm text-slate-200 transition-colors hover:bg-slate-800">
                        Export JSON
                      </button>
                      <button type="button" onClick={() => handleExport('csv')} className="block w-full rounded-xl px-3 py-2 text-left text-sm text-slate-200 transition-colors hover:bg-slate-800">
                        Export CSV
                      </button>
                    </div>
                  </>
                )}
              </div>
              <button type="button" onClick={() => setIsCreateDialogOpen(true)} className="button-primary">
                <Plus className="h-4 w-4" />
                Create Entity
              </button>
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-4">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Entities</p>
              <p className="mt-3 text-3xl font-semibold text-white">{entities.length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Available</p>
              <p className="mt-3 text-3xl font-semibold text-emerald-300">{availableCount}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Consumed</p>
              <p className="mt-3 text-3xl font-semibold text-amber-300">{consumedCount}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Fields</p>
              <p className="mt-3 text-3xl font-semibold text-white">{totalFields}</p>
            </div>
          </div>
        </div>
      </section>

      {error && (
        <div className="rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4" />
            <span>{error}</span>
          </div>
        </div>
      )}

      {successMessage && (
        <div className="rounded-2xl border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-300">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="h-4 w-4" />
            <span>{successMessage}</span>
          </div>
        </div>
      )}

      <section className="panel p-5">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Search across entity field values"
              className="field-shell pl-11"
            />
          </div>

          <div className="flex flex-col gap-3 sm:flex-row">
            {environments.length > 0 && (
              <select value={selectedEnvironment} onChange={(e) => setSelectedEnvironment(e.target.value)} className="field-shell sm:min-w-[220px]">
                <option value="all">All environments</option>
                {environments.map((environment) => (
                  <option key={environment} value={environment}>
                    {environment}
                  </option>
                ))}
              </select>
            )}

            <div className="relative">
              <button type="button" onClick={() => setShowColumnMenu((current) => !current)} className="button-secondary">
                <Settings className="h-4 w-4" />
                Columns
                <ChevronDown className="h-4 w-4" />
              </button>
              {showColumnMenu && (
                <>
                  <button type="button" aria-label="Close columns menu" className="fixed inset-0 z-10" onClick={() => setShowColumnMenu(false)} />
                  <div className="absolute right-0 z-20 mt-2 w-72 rounded-[24px] border border-slate-700 bg-slate-950/95 p-4 shadow-2xl">
                    <div className="mb-3 flex items-center justify-between">
                      <div>
                        <p className="text-sm font-medium text-white">Visible columns</p>
                        <p className="text-xs text-slate-500">Status stays pinned.</p>
                      </div>
                      <span className="badge-soft">{visibleColumns.size} active</span>
                    </div>
                    <div className="mb-3 flex gap-2">
                      <button type="button" onClick={selectAllColumns} className="button-secondary !rounded-xl !px-3 !py-2 text-xs">
                        Select all
                      </button>
                      <button type="button" onClick={deselectAllColumns} className="button-secondary !rounded-xl !px-3 !py-2 text-xs">
                        Reset
                      </button>
                    </div>
                    <div className="max-h-72 space-y-2 overflow-y-auto">
                      <label className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-500">
                        <input type="checkbox" checked disabled className="h-4 w-4 rounded border-slate-600 bg-slate-800" />
                        <span>Status</span>
                      </label>
                      {schema.fields.map((field) => (
                        <label key={field.name} className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300">
                          <input
                            type="checkbox"
                            checked={visibleColumns.has(field.name)}
                            onChange={() => toggleColumn(field.name)}
                            className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                          />
                          <span className="flex-1">{field.name}</span>
                          <span className="text-xs text-slate-500">{field.type || 'string'}</span>
                        </label>
                      ))}
                      {entities.some((entity) => entity.environment) && (
                        <label className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm text-slate-300">
                          <input
                            type="checkbox"
                            checked={visibleColumns.has('environment')}
                            onChange={() => toggleColumn('environment')}
                            className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                          />
                          <span className="flex-1">Environment</span>
                        </label>
                      )}
                    </div>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>

        {schema.excludeOnFetch && (
          <div className="mt-4">
            <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-700/70 bg-slate-900/70 px-4 py-3 text-sm text-slate-300">
              <Filter className="h-4 w-4 text-slate-400" />
              <input
                type="checkbox"
                checked={showConsumedOnly}
                onChange={(e) => setShowConsumedOnly(e.target.checked)}
                className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
              />
              <span>Show consumed entities only</span>
            </label>
          </div>
        )}
      </section>

      {filteredEntities.length > 0 ? (
        <section className="table-shell">
          <div className="panel-header flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <p className="eyebrow">Entity Inventory</p>
              <h2 className="mt-2 text-xl font-semibold text-white">{filteredEntities.length} entities in current view</h2>
            </div>
            <div className="flex flex-wrap gap-2 text-sm text-slate-400">
              <span className="badge-soft">{totalFields} schema fields</span>
              {selectedEnvironment !== 'all' && <span className="badge-soft">{selectedEnvironment}</span>}
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="min-w-full">
              <thead>
                <tr>
                  {visibleColumns.has('status') && (
                    <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-[0.18em] text-slate-400">Status</th>
                  )}
                  {schema.fields.map((field) => (
                    visibleColumns.has(field.name) && (
                      <th key={field.name} className="px-5 py-3 text-left text-xs font-medium uppercase tracking-[0.18em] text-slate-400">
                        {field.name}
                      </th>
                    )
                  ))}
                  {entities.some((entity) => entity.environment) && visibleColumns.has('environment') && (
                    <th className="px-5 py-3 text-left text-xs font-medium uppercase tracking-[0.18em] text-slate-400">Environment</th>
                  )}
                  <th className="px-5 py-3 text-right text-xs font-medium uppercase tracking-[0.18em] text-slate-400">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredEntities.map((entity) => (
                  <tr
                    key={entity.id}
                    className="cursor-pointer transition-colors hover:bg-slate-900/45"
                    onClick={() => {
                      setSelectedEntity(entity);
                      setIsViewDialogOpen(true);
                    }}
                  >
                    {visibleColumns.has('status') && (
                      <td className="px-5 py-4 text-sm">
                        {entity.isConsumed ? (
                          <span className="inline-flex items-center gap-2 text-amber-300">
                            <XCircle className="h-4 w-4" />
                            Consumed
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-2 text-emerald-300">
                            <CheckCircle2 className="h-4 w-4" />
                            Available
                          </span>
                        )}
                      </td>
                    )}
                    {schema.fields.map((field) => (
                      visibleColumns.has(field.name) && (
                        <td key={field.name} className="px-5 py-4 text-sm text-slate-300">
                          <div className="max-w-[260px] truncate">{String(entity.fields[field.name] ?? '-')}</div>
                        </td>
                      )
                    ))}
                    {entities.some((item) => item.environment) && visibleColumns.has('environment') && (
                      <td className="px-5 py-4 text-sm text-slate-300">{entity.environment || '-'}</td>
                    )}
                    <td className="px-5 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setSelectedEntity(entity);
                            setIsViewDialogOpen(true);
                          }}
                          className="rounded-full border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400 transition-colors hover:text-white"
                          title="View entity"
                        >
                          <Eye className="h-4 w-4" />
                        </button>
                        {entity.isConsumed && schema.excludeOnFetch && (
                          <button
                            type="button"
                            onClick={(e) => handleResetEntity(entity.id, e)}
                            className="rounded-full border border-emerald-500/20 bg-emerald-500/10 p-2 text-emerald-200 transition-colors hover:bg-emerald-500/15"
                            title="Reset entity"
                          >
                            <RefreshCw className="h-4 w-4" />
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={(e) => handleDeleteEntity(entity.id, e)}
                          className="rounded-full border border-red-500/20 bg-red-500/10 p-2 text-red-200 transition-colors hover:bg-red-500/15"
                          title="Delete entity"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ) : (
        <section className="panel p-12 text-center">
          <Package className="mx-auto h-14 w-14 text-slate-600" />
          <h3 className="mt-4 text-lg font-medium text-white">
            {searchTerm || showConsumedOnly ? 'No entities match the current filters' : 'No entities yet'}
          </h3>
          <p className="mt-2 text-sm text-slate-400">
            {searchTerm || showConsumedOnly
              ? 'Try adjusting the search, environment filter, or consumed-only toggle.'
              : `Create your first ${entityType} entity or import a dataset to populate this workspace.`}
          </p>
          {!searchTerm && !showConsumedOnly && (
            <div className="mt-6 flex flex-wrap justify-center gap-3">
              <button type="button" onClick={() => setIsCreateDialogOpen(true)} className="button-primary">
                <Plus className="h-4 w-4" />
                Create Entity
              </button>
              <button type="button" onClick={openImportModal} className="button-secondary">
                <Upload className="h-4 w-4" />
                Import Dataset
              </button>
            </div>
          )}
        </section>
      )}

      <EntityViewDialog
        isOpen={isViewDialogOpen}
        onClose={() => {
          setIsViewDialogOpen(false);
          setSelectedEntity(null);
        }}
        entity={selectedEntity}
        schema={schema}
        onReset={selectedEntity ? () => handleResetEntity(selectedEntity.id) : undefined}
        onEdit={handleEditFromDialog}
      />

      <EntityCreateDialog
        isOpen={isCreateDialogOpen}
        onClose={() => setIsCreateDialogOpen(false)}
        onSuccess={handleCreateSuccess}
        schema={schema}
        entityType={entityType!}
      />

      {isImportModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-shell max-w-2xl">
            <div className="border-b border-slate-800 px-6 py-5">
              <h2 className="text-xl font-semibold text-white">Import entities</h2>
              <p className="mt-2 text-sm text-slate-400">
                Upload a CSV or JSON file. JSON expects an array of objects with `fields` and optional `environment`.
              </p>
            </div>

            <div className="space-y-4 px-6 py-5">
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-300">File</label>
                <input
                  type="file"
                  accept=".csv,.json"
                  onChange={(e) => setImportFile(e.target.files?.[0] ?? null)}
                  className="field-shell file:mr-3 file:rounded-full file:border-0 file:bg-blue-500 file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-slate-950"
                />
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-300">Environment</label>
                  <select value={importEnvironment} onChange={(e) => setImportEnvironment(e.target.value)} className="field-shell">
                    <option value="">None</option>
                    {importEnvironments.map((environment) => (
                      <option key={environment} value={environment}>
                        {environment}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="mb-2 block text-sm font-medium text-slate-300">Mode</label>
                  <select value={importMode} onChange={(e) => setImportMode(e.target.value as 'append' | 'upsert')} className="field-shell">
                    <option value="append">Append</option>
                    <option value="upsert">Upsert</option>
                  </select>
                </div>
              </div>

              {importResult && (
                <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
                  <div className="flex flex-wrap gap-2 text-sm">
                    <span className="badge-soft text-emerald-300">{importResult.created} created</span>
                    {importResult.updated > 0 && <span className="badge-soft text-blue-300">{importResult.updated} updated</span>}
                    {importResult.skipped > 0 && <span className="badge-soft text-amber-300">{importResult.skipped} skipped</span>}
                  </div>
                  {importResult.errors.length > 0 && (
                    <div className="mt-4">
                      <p className="text-xs font-medium uppercase tracking-[0.18em] text-red-300">Errors</p>
                      <ul className="mt-2 space-y-1 text-sm text-slate-400">
                        {importResult.errors.map((resultError, index) => (
                          <li key={`${resultError.row}-${index}`}>Row {resultError.row}: {resultError.message}</li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              )}
            </div>

            <div className="flex justify-end gap-3 border-t border-slate-800 px-6 py-5">
              <button
                type="button"
                onClick={() => {
                  setIsImportModalOpen(false);
                  setImportResult(null);
                }}
                className="button-secondary"
              >
                Close
              </button>
              <button
                type="button"
                onClick={handleImportSubmit}
                disabled={!importFile || isImporting}
                className="button-primary disabled:cursor-not-allowed disabled:opacity-60"
              >
                {isImporting ? 'Importing...' : 'Import'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default EntityList;
