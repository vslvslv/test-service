import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Database,
  Filter,
  Package,
  Search,
  XCircle
} from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage } from '../types';

interface Schema {
  id?: string;
  entityName: string;
  fields: Array<{ name: string; required?: boolean; type?: string }>;
  filterableFields?: string[];
  excludeOnFetch: boolean;
  createdAt?: string;
  updatedAt?: string;
}

interface EntityTypeStats {
  entityName: string;
  schema: Schema;
  totalCount: number;
  availableCount: number;
  consumedCount: number;
}

const Entities: React.FC = () => {
  const navigate = useNavigate();
  const [schemas, setSchemas] = useState<Schema[]>([]);
  const [entityStats, setEntityStats] = useState<Map<string, EntityTypeStats>>(new Map());
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [showAutoConsumeOnly, setShowAutoConsumeOnly] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      const schemasData = await apiService.getSchemas();
      setSchemas(schemasData);

      const statsMap = new Map<string, EntityTypeStats>();
      for (const schema of schemasData) {
        try {
          const entities = await apiService.getEntities(schema.entityName);
          const totalCount = entities.length;
          const consumedCount = entities.filter((entity: { isConsumed?: boolean }) => entity.isConsumed).length;
          const availableCount = totalCount - consumedCount;

          statsMap.set(schema.entityName, {
            entityName: schema.entityName,
            schema,
            totalCount,
            availableCount,
            consumedCount
          });
        } catch (statsError) {
          console.error(`Failed to load entities for ${schema.entityName}:`, statsError);
          statsMap.set(schema.entityName, {
            entityName: schema.entityName,
            schema,
            totalCount: 0,
            availableCount: 0,
            consumedCount: 0
          });
        }
      }

      setEntityStats(statsMap);
    } catch (err: unknown) {
      setError(getErrorMessage(err));
      console.error('Failed to load data:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const filteredSchemas = schemas
    .filter((schema) => schema.entityName?.toLowerCase().includes(searchTerm.toLowerCase()))
    .filter((schema) => !showAutoConsumeOnly || schema.excludeOnFetch);

  const totalEntities = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.totalCount, 0);
  const totalAvailable = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.availableCount, 0);
  const totalConsumed = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.consumedCount, 0);

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
                <Database className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <p className="eyebrow">Entity Workspace</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">Manage entity inventories across all schemas</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Inspect data availability, identify exhausted pools, and jump directly into a schema-specific entity workspace.
            </p>
          </div>

          <div className="grid min-w-full gap-3 sm:grid-cols-3 lg:min-w-[420px]">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.2em] text-slate-500">Entity Types</p>
              <p className="mt-3 text-3xl font-semibold text-white">{schemas.length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.2em] text-slate-500">Available</p>
              <p className="mt-3 text-3xl font-semibold text-emerald-300">{totalAvailable}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.2em] text-slate-500">Consumed</p>
              <p className="mt-3 text-3xl font-semibold text-amber-300">{totalConsumed}</p>
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

      <section className="panel p-5">
        <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
          <div className="relative flex-1">
            <Search className="pointer-events-none absolute left-4 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Search entity types"
              className="field-shell pl-11"
            />
          </div>

          <label className="inline-flex items-center gap-3 rounded-2xl border border-slate-700/70 bg-slate-900/70 px-4 py-3 text-sm text-slate-300">
            <Filter className="h-4 w-4 text-slate-400" />
            <input
              type="checkbox"
              checked={showAutoConsumeOnly}
              onChange={(e) => setShowAutoConsumeOnly(e.target.checked)}
              className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
            />
            <span>Show auto-consume only</span>
          </label>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
            <p className="text-sm text-slate-400">Total entity records</p>
            <p className="mt-2 text-2xl font-semibold text-white">{totalEntities}</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
            <p className="flex items-center gap-2 text-sm text-slate-400">
              <CheckCircle2 className="h-4 w-4 text-emerald-400" />
              Ready for allocation
            </p>
            <p className="mt-2 text-2xl font-semibold text-white">{totalAvailable}</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
            <p className="flex items-center gap-2 text-sm text-slate-400">
              <XCircle className="h-4 w-4 text-amber-400" />
              Exhausted or already used
            </p>
            <p className="mt-2 text-2xl font-semibold text-white">{totalConsumed}</p>
          </div>
        </div>
      </section>

      <section className="table-shell">
        <div className="panel-header flex items-center justify-between">
          <div>
            <p className="eyebrow">Entity Types</p>
            <h2 className="mt-2 text-xl font-semibold text-white">{filteredSchemas.length} workspaces available</h2>
          </div>
        </div>

        {filteredSchemas.length > 0 ? (
          <div className="divide-y divide-slate-800/80">
            {filteredSchemas.map((schema) => {
              const stats = entityStats.get(schema.entityName);
              const availablePercent = stats && stats.totalCount > 0
                ? Math.round((stats.availableCount / stats.totalCount) * 100)
                : 0;

              return (
                <button
                  key={schema.entityName}
                  type="button"
                  onClick={() => navigate(`/entities/${schema.entityName}`)}
                  aria-label={`Open ${schema.entityName} entities`}
                  className="group w-full px-5 py-5 text-left transition-colors hover:bg-slate-900/45"
                >
                  <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
                    <div className="flex min-w-0 items-start gap-4">
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
                          <span className="badge-soft">{stats?.totalCount || 0} records</span>
                          <span className="badge-soft text-emerald-300">{stats?.availableCount || 0} available</span>
                          {(stats?.consumedCount || 0) > 0 && (
                            <span className="badge-soft text-amber-300">{stats?.consumedCount || 0} consumed</span>
                          )}
                        </div>
                        {stats && stats.totalCount > 0 && (
                          <div className="mt-4 max-w-xl">
                            <div className="mb-2 flex items-center justify-between text-xs uppercase tracking-[0.18em] text-slate-500">
                              <span>Availability</span>
                              <span>{availablePercent}% ready</span>
                            </div>
                            <div className="h-2 overflow-hidden rounded-full bg-slate-800">
                              <div
                                className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-blue-400"
                                style={{ width: `${availablePercent}%` }}
                              />
                            </div>
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center justify-between gap-4 xl:min-w-[220px] xl:justify-end">
                      <div className="text-right">
                        <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Primary action</p>
                        <p className="mt-1 text-sm text-slate-300">Open entity workspace</p>
                      </div>
                      <div className="rounded-full border border-slate-700/70 bg-slate-900/70 p-3 text-slate-400 transition-colors group-hover:text-white">
                        <ArrowRight className="h-4 w-4" />
                      </div>
                    </div>
                  </div>
                </button>
              );
            })}
          </div>
        ) : (
          <div className="px-6 py-16 text-center">
            <Database className="mx-auto h-14 w-14 text-slate-600" />
            <h3 className="mt-4 text-lg font-medium text-white">
              {searchTerm || showAutoConsumeOnly ? 'No entity types match the current filters' : 'No entity types available'}
            </h3>
            <p className="mt-2 text-sm text-slate-400">
              {searchTerm || showAutoConsumeOnly
                ? 'Try widening the search or disabling the auto-consume filter.'
                : 'Create schemas first to start loading and managing entity data.'}
            </p>
          </div>
        )}
      </section>
    </div>
  );
};

export default Entities;
