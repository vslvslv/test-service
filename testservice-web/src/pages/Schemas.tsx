import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertCircle,
  Calendar,
  CheckSquare,
  Database,
  Edit,
  Filter,
  Grid3x3,
  Hash,
  Layers,
  List,
  Package,
  Plus,
  Search,
  Trash2,
  Type
} from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage, type Schema } from '../types';

type SortOption = 'name' | 'fields' | 'recent';

const Schemas: React.FC = () => {
  const navigate = useNavigate();
  const [schemas, setSchemas] = useState<Schema[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [viewMode, setViewMode] = useState<'list' | 'grid'>('list');
  const [sortBy, setSortBy] = useState<SortOption>('name');
  const [showAutoConsumeOnly, setShowAutoConsumeOnly] = useState(false);

  useEffect(() => {
    loadSchemas();
  }, []);

  const loadSchemas = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await apiService.getSchemas();
      setSchemas(data);
    } catch (err) {
      setError(getErrorMessage(err));
      console.error('Failed to load schemas:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleDelete = async (schemaEntityName: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm(`Are you sure you want to delete the schema "${schemaEntityName}"?\n\nThis will also affect any entities using this schema.`)) {
      return;
    }

    try {
      await apiService.request({
        method: 'DELETE',
        url: `/api/schemas/${schemaEntityName}`
      });
      await loadSchemas();
    } catch (err) {
      alert(getErrorMessage(err));
    }
  };

  const handleDeleteAllEntities = async (schemaEntityName: string, e: React.MouseEvent) => {
    e.stopPropagation();
    if (!confirm(`Are you sure you want to delete ALL entities of type "${schemaEntityName}"?\n\nThis action cannot be undone.`)) {
      return;
    }

    try {
      const result = await apiService.deleteAllSchemaEntities(schemaEntityName);
      alert(`Successfully deleted ${result.deletedCount} entities from ${schemaEntityName}`);
      await loadSchemas();
    } catch (err) {
      alert(getErrorMessage(err));
    }
  };

  const getFieldTypeIcon = (type: string) => {
    switch (type) {
      case 'string':
        return <Type className="h-3.5 w-3.5" />;
      case 'number':
        return <Hash className="h-3.5 w-3.5" />;
      case 'boolean':
        return <CheckSquare className="h-3.5 w-3.5" />;
      case 'date':
      case 'datetime':
        return <Calendar className="h-3.5 w-3.5" />;
      default:
        return <Package className="h-3.5 w-3.5" />;
    }
  };

  const getFieldTypeColor = (type: string) => {
    switch (type) {
      case 'string':
        return 'border-blue-500/20 bg-blue-500/10 text-blue-200';
      case 'number':
        return 'border-emerald-500/20 bg-emerald-500/10 text-emerald-200';
      case 'boolean':
        return 'border-violet-500/20 bg-violet-500/10 text-violet-200';
      case 'date':
      case 'datetime':
        return 'border-amber-500/20 bg-amber-500/10 text-amber-200';
      default:
        return 'border-slate-700 bg-slate-900/70 text-slate-300';
    }
  };

  const filteredSchemas = schemas
    .filter((schema) => schema.entityName)
    .filter((schema) => schema.entityName.toLowerCase().includes(searchTerm.toLowerCase()))
    .filter((schema) => !showAutoConsumeOnly || schema.excludeOnFetch);

  const sortedSchemas = [...filteredSchemas].sort((a, b) => {
    switch (sortBy) {
      case 'fields':
        return (b.fields?.length || 0) - (a.fields?.length || 0);
      case 'recent':
        return (b.createdAt || '').localeCompare(a.createdAt || '');
      case 'name':
      default:
        return (a.entityName || '').localeCompare(b.entityName || '');
    }
  });

  const totalFields = schemas.reduce((sum, schema) => sum + (schema.fields?.length || 0), 0);
  const autoConsumeCount = schemas.filter((schema) => schema.excludeOnFetch).length;

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="h-12 w-12 animate-spin rounded-full border-b-2 border-blue-500" />
      </div>
    );
  }

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-3">
              <div className="page-hero-icon">
                <Layers className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <p className="eyebrow">Schema Control Plane</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">Define the structure behind every entity workflow</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Maintain field contracts, auto-consume behavior, and entity cleanup actions from one operational schema workspace.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button type="button" onClick={() => navigate('/schemas/new')} className="button-primary">
              <Plus className="h-4 w-4" />
              Create Schema
            </button>
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

      <section className="panel p-5">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Search schemas by entity name"
              className="field-shell pl-11"
            />
          </div>

          <div className="flex flex-col gap-3 sm:flex-row">
            <select
              aria-label="Sort schemas"
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as SortOption)}
              className="field-shell sm:min-w-[180px]"
            >
              <option value="name">Sort by name</option>
              <option value="fields">Sort by field count</option>
              <option value="recent">Sort by most recent</option>
            </select>

            <div className="inline-flex items-center rounded-full border border-slate-700/80 bg-slate-900/80 p-1">
              <button
                type="button"
                onClick={() => setViewMode('list')}
                className={`rounded-full px-3 py-2 text-sm transition-colors ${viewMode === 'list' ? 'bg-blue-500 text-slate-950' : 'text-slate-300 hover:text-white'}`}
              >
                <span className="inline-flex items-center gap-2">
                  <List className="h-4 w-4" />
                  List
                </span>
              </button>
              <button
                type="button"
                onClick={() => setViewMode('grid')}
                className={`rounded-full px-3 py-2 text-sm transition-colors ${viewMode === 'grid' ? 'bg-blue-500 text-slate-950' : 'text-slate-300 hover:text-white'}`}
              >
                <span className="inline-flex items-center gap-2">
                  <Grid3x3 className="h-4 w-4" />
                  Grid
                </span>
              </button>
            </div>
          </div>
        </div>

        <div className="mt-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-700/70 bg-slate-900/70 px-4 py-3 text-sm text-slate-300">
            <Filter className="h-4 w-4 text-slate-400" />
            <input
              type="checkbox"
              checked={showAutoConsumeOnly}
              onChange={(e) => setShowAutoConsumeOnly(e.target.checked)}
              className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
            />
            <span>Show auto-consume schemas only</span>
          </label>

          <div className="grid gap-3 sm:grid-cols-3 lg:min-w-[520px]">
            <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
              <p className="text-sm text-slate-400">Total schemas</p>
              <p className="mt-2 text-2xl font-semibold text-white">{schemas.length}</p>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
              <p className="text-sm text-slate-400">Auto-consume enabled</p>
              <p className="mt-2 text-2xl font-semibold text-amber-300">{autoConsumeCount}</p>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
              <p className="text-sm text-slate-400">Total fields tracked</p>
              <p className="mt-2 text-2xl font-semibold text-white">{totalFields}</p>
            </div>
          </div>
        </div>
      </section>

      {sortedSchemas.length > 0 ? (
        viewMode === 'list' ? (
          <section className="table-shell">
            <div className="panel-header">
              <p className="eyebrow">Definitions</p>
              <h2 className="mt-2 text-xl font-semibold text-white">{sortedSchemas.length} schemas in view</h2>
            </div>
            <div className="divide-y divide-slate-800/80">
              {sortedSchemas.map((schema) => (
                <div
                  key={schema.entityName}
                  className="group flex flex-col gap-4 px-5 py-5 transition-colors hover:bg-slate-900/45 xl:flex-row xl:items-start xl:justify-between"
                >
                  <button
                    type="button"
                    onClick={() => navigate(`/schemas/${schema.entityName}`)}
                    className="flex min-w-0 flex-1 items-start gap-4 text-left"
                  >
                    <div className="rounded-2xl border border-blue-500/20 bg-blue-500/10 p-3">
                      <Package className="h-6 w-6 text-blue-300" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <h3 className="text-lg font-semibold text-white">{schema.entityName}</h3>
                        {schema.excludeOnFetch && (
                          <span className="badge-soft border-amber-500/25 bg-amber-500/10 text-amber-300">Auto-consume</span>
                        )}
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-slate-400">
                        <span className="badge-soft">{schema.fields?.length || 0} fields</span>
                        <span className="badge-soft">{schema.fields?.filter((field) => field.required).length || 0} required</span>
                      </div>
                      {schema.fields && schema.fields.length > 0 && (
                        <div className="mt-4 flex flex-wrap gap-2">
                          {schema.fields.slice(0, 6).map((field) => (
                            <div
                              key={`${schema.entityName}-${field.name}`}
                              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs ${getFieldTypeColor(field.type || 'string')}`}
                            >
                              {getFieldTypeIcon(field.type || 'string')}
                              <span>{field.name}</span>
                              {field.required && <span className="text-red-300">*</span>}
                            </div>
                          ))}
                          {schema.fields.length > 6 && (
                            <span className="badge-soft">+{schema.fields.length - 6} more</span>
                          )}
                        </div>
                      )}
                    </div>
                  </button>

                  <div className="flex flex-wrap items-center gap-2 xl:justify-end">
                    <button
                      type="button"
                      onClick={(e) => handleDeleteAllEntities(schema.entityName, e)}
                      className="button-secondary"
                    >
                      <Database className="h-4 w-4" />
                      Delete Entities
                    </button>
                    <button
                      type="button"
                      onClick={() => navigate(`/schemas/${schema.entityName}`)}
                      className="button-secondary"
                    >
                      <Edit className="h-4 w-4" />
                      Edit
                    </button>
                    <button
                      type="button"
                      onClick={(e) => handleDelete(schema.entityName, e)}
                      className="inline-flex items-center justify-center gap-2 rounded-full border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-200 transition-colors hover:bg-red-500/15"
                    >
                      <Trash2 className="h-4 w-4" />
                      Delete
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </section>
        ) : (
          <section className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
            {sortedSchemas.map((schema) => (
              <button
                key={schema.entityName}
                type="button"
                onClick={() => navigate(`/schemas/${schema.entityName}`)}
                aria-label={`Open schema ${schema.entityName}`}
                className="panel group p-5 text-left transition-colors hover:bg-slate-900/70"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="rounded-2xl border border-blue-500/20 bg-blue-500/10 p-3">
                    <Package className="h-6 w-6 text-blue-300" />
                  </div>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={(e) => handleDeleteAllEntities(schema.entityName, e)}
                      className="rounded-full border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400 transition-colors hover:text-amber-300"
                    >
                      <Database className="h-4 w-4" />
                    </button>
                    <button
                      type="button"
                      onClick={(e) => handleDelete(schema.entityName, e)}
                      className="rounded-full border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400 transition-colors hover:text-red-300"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>

                <div className="mt-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="text-xl font-semibold text-white">{schema.entityName}</h3>
                    {schema.excludeOnFetch && (
                      <span className="badge-soft border-amber-500/25 bg-amber-500/10 text-amber-300">Auto-consume</span>
                    )}
                  </div>
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-3">
                      <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Fields</p>
                      <p className="mt-2 text-xl font-semibold text-white">{schema.fields?.length || 0}</p>
                    </div>
                    <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-3">
                      <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Required</p>
                      <p className="mt-2 text-xl font-semibold text-white">{schema.fields?.filter((field) => field.required).length || 0}</p>
                    </div>
                  </div>

                  <div className="mt-4 flex flex-wrap gap-2">
                    {Array.from(new Set(schema.fields.map((field) => field?.type || 'string'))).slice(0, 4).map((type) => (
                      <span key={`${schema.entityName}-${type}`} className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs ${getFieldTypeColor(type)}`}>
                        {getFieldTypeIcon(type)}
                        <span>{type}</span>
                      </span>
                    ))}
                  </div>
                </div>
              </button>
            ))}
          </section>
        )
      ) : (
        <section className="panel p-12 text-center">
          <Layers className="mx-auto h-14 w-14 text-slate-600" />
          <h3 className="mt-4 text-lg font-medium text-white">
            {searchTerm || showAutoConsumeOnly ? 'No schemas match the current filters' : 'No schemas yet'}
          </h3>
          <p className="mt-2 text-sm text-slate-400">
            {searchTerm || showAutoConsumeOnly
              ? 'Try changing the search or filter criteria.'
              : 'Create your first schema to define a reusable entity contract.'}
          </p>
          {!searchTerm && !showAutoConsumeOnly && (
            <div className="mt-6">
              <button type="button" onClick={() => navigate('/schemas/new')} className="button-primary">
                <Plus className="h-4 w-4" />
                Create Schema
              </button>
            </div>
          )}
        </section>
      )}
    </div>
  );
};

export default Schemas;
