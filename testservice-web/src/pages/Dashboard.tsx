import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { 
  Database, 
  Server, 
  Layers, 
  Activity,
  TrendingUp,
  AlertCircle,
  CheckCircle,
  Package,
  ChevronRight
} from 'lucide-react';
import { apiService } from '../services/api';
import type { Schema } from '../types';

interface Stats {
  totalSchemas: number;
  totalEntities: number;
  totalEnvironments: number;
  availableEntities: number;
  consumedEntities: number;
}

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [stats, setStats] = useState<Stats>({
    totalSchemas: 0,
    totalEntities: 0,
    totalEnvironments: 0,
    availableEntities: 0,
    consumedEntities: 0,
  });
  const [isLoading, setIsLoading] = useState(true);
  const [recentSchemas, setRecentSchemas] = useState<Schema[]>([]);

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    setIsLoading(true);
    try {
      // Load schemas
      const schemas = await apiService.getSchemas();
      setRecentSchemas(schemas.slice(0, 5));

      // Load environments
      const environments = await apiService.getEnvironments();

      // Calculate entity statistics across all schemas
      let totalEntities = 0;
      let availableEntities = 0;
      let consumedEntities = 0;

      for (const schema of schemas) {
        try {
          const entities = await apiService.getEntities(schema.entityName);
          totalEntities += entities.length;
          
          // Count available vs consumed
          entities.forEach((entity: { isConsumed?: boolean }) => {
            if (entity.isConsumed) {
              consumedEntities++;
            } else {
              availableEntities++;
            }
          });
        } catch (err) {
          console.error(`Failed to load entities for ${schema.entityName}:`, err);
        }
      }

      setStats({
        totalSchemas: schemas.length,
        totalEntities,
        totalEnvironments: environments.length,
        availableEntities,
        consumedEntities,
      });
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // Calculate functional percentages
  const availablePercentage = stats.totalEntities > 0 
    ? Math.round((stats.availableEntities / stats.totalEntities) * 100)
    : 0;
  
  const consumedPercentage = stats.totalEntities > 0
    ? Math.round((stats.consumedEntities / stats.totalEntities) * 100)
    : 0;

  // Calculate schema utilization (schemas with entities vs total schemas)
  const schemaUtilization = stats.totalSchemas > 0
    ? Math.round((recentSchemas.filter(s => s.fields && s.fields.length > 0).length / stats.totalSchemas) * 100)
    : 0;

  const handleCreateSchema = () => {
    navigate('/schemas/new');
  };

  const handleManageEnvironments = () => {
    navigate('/environments');
  };

  const handleViewActivity = () => {
    navigate('/activity');
  };

  const handleViewAllSchemas = () => {
    navigate('/schemas');
  };

  const handleSchemaClick = (schemaName: string) => {
    navigate(`/schemas/${schemaName}`);
  };

  const statCards = [
    {
      title: 'Total Schemas',
      value: stats.totalSchemas,
      icon: Layers,
      color: 'blue',
      trend: stats.totalSchemas > 0 ? `${schemaUtilization}% active` : 'No data',
      onClick: () => navigate('/schemas'),
    },
    {
      title: 'Environments',
      value: stats.totalEnvironments,
      icon: Server,
      color: 'green',
      trend: stats.totalEnvironments > 0 ? `${stats.totalEnvironments} active` : 'None',
      onClick: () => navigate('/environments'),
    },
    {
      title: 'Available Entities',
      value: stats.availableEntities,
      icon: CheckCircle,
      color: 'purple',
      trend: `${availablePercentage}%`,
      onClick: () => navigate('/entities'),
    },
    {
      title: 'Consumed Entities',
      value: stats.consumedEntities,
      icon: Activity,
      color: 'orange',
      trend: `${consumedPercentage}%`,
      onClick: () => navigate('/entities'),
    },
  ];

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
            <p className="eyebrow">Operations Overview</p>
            <div className="flex items-start gap-4">
              <div className="page-hero-icon">
                <TrendingUp className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <h1 className="text-3xl font-semibold tracking-tight text-white">Welcome back, {user?.username}.</h1>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-300">
                  Track schema health, entity availability, and environment readiness from one enterprise control surface.
                </p>
              </div>
            </div>
          </div>

          <div className="panel p-5">
            <p className="eyebrow">Current Balance</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <div className="stat-card">
                <p className="text-sm text-slate-400">Available pool</p>
                <p className="mt-2 text-3xl font-semibold text-white">{availablePercentage}%</p>
                <p className="mt-2 text-xs text-slate-500">{stats.availableEntities} ready for fetch</p>
              </div>
              <div className="stat-card">
                <p className="text-sm text-slate-400">Schema activity</p>
                <p className="mt-2 text-3xl font-semibold text-white">{schemaUtilization}%</p>
                <p className="mt-2 text-xs text-slate-500">{stats.totalSchemas} total schema definitions</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="section-grid">
        {statCards.map((stat, index) => {
          const Icon = stat.icon;
          const colorClasses = {
            blue: 'bg-blue-500/15 text-blue-300 border-blue-500/20',
            green: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/20',
            purple: 'bg-violet-500/15 text-violet-300 border-violet-500/20',
            orange: 'bg-amber-500/15 text-amber-300 border-amber-500/20',
          };

          return (
            <button
              key={index}
              onClick={stat.onClick}
              className="stat-card text-left transition-all hover:-translate-y-0.5 hover:border-slate-600"
            >
              <div className="flex items-start justify-between gap-4">
                <div className={`rounded-2xl border p-3 ${colorClasses[stat.color as keyof typeof colorClasses]}`}>
                  <Icon className="h-5 w-5" />
                </div>
                <span className="rounded-full border border-slate-700 bg-slate-900/80 px-2.5 py-1 text-xs text-slate-300">{stat.trend}</span>
              </div>
              <p className="mt-5 text-sm text-slate-400">{stat.title}</p>
              <p className="mt-2 text-3xl font-semibold text-white">{stat.value}</p>
            </button>
          );
        })}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <section className="panel-strong p-6">
          <div className="mb-6 flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-lg font-semibold text-white">
              <Layers className="h-5 w-5" />
              Recent Schemas
            </h2>
            <button
              onClick={handleViewAllSchemas}
              className="inline-flex items-center gap-1 text-sm font-medium text-blue-300 transition-colors hover:text-blue-200"
            >
              View All
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
          <div className="space-y-2">
            {recentSchemas.length > 0 ? (
              recentSchemas.map((schema, index) => (
                <button
                  key={index}
                  type="button"
                  onClick={() => handleSchemaClick(schema.entityName)}
                  aria-label={`Open schema ${schema.entityName}`}
                  className="group w-full rounded-2xl border border-slate-800 bg-slate-900/80 p-4 text-left transition-all hover:border-slate-600 hover:bg-slate-800/80"
                >
                  <div className="flex items-center justify-between gap-4">
                    <div className="min-w-0 flex-1">
                      <h3 className="mb-1 font-medium text-white transition-colors group-hover:text-blue-300">
                        {schema.entityName}
                      </h3>
                      <p className="text-sm text-slate-400">{schema.fields?.length || 0} fields</p>
                    </div>
                    <div className="ml-4 flex items-center gap-3">
                      {schema.excludeOnFetch && (
                        <span className="whitespace-nowrap rounded-md border border-amber-500/30 bg-amber-500/15 px-2.5 py-1 text-xs font-medium text-amber-300">
                          Auto-consume
                        </span>
                      )}
                      <div className="flex items-center gap-2 text-slate-500 transition-colors group-hover:text-slate-300">
                        <Package className="h-5 w-5" />
                        <ChevronRight className="h-4 w-4" />
                      </div>
                    </div>
                  </div>
                </button>
              ))
            ) : (
              <div className="py-12 text-center text-slate-500">
                <Layers className="mx-auto mb-3 h-12 w-12 opacity-50" />
                <p className="mb-4 text-slate-400">No schemas found</p>
                <button onClick={handleCreateSchema} className="button-primary">
                  <Layers className="h-4 w-4" />
                  Create your first schema
                </button>
              </div>
            )}
          </div>
        </section>

        <section className="panel-strong p-6">
          <h2 className="mb-4 flex items-center gap-2 text-lg font-semibold text-white">
            <TrendingUp className="h-5 w-5" />
            Quick Actions
          </h2>
          <div className="space-y-3">
            <button
              onClick={handleCreateSchema}
              className="group w-full rounded-2xl border border-blue-500/20 bg-blue-500/12 p-4 text-left transition-colors hover:bg-blue-500/18"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="rounded-xl bg-white/10 p-2">
                    <Database className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="font-semibold text-white">Create New Schema</p>
                    <p className="text-sm text-blue-100">Define a new entity type</p>
                  </div>
                </div>
                <ChevronRight className="h-5 w-5 opacity-0 transition-opacity group-hover:opacity-100" />
              </div>
            </button>

            <button
              onClick={handleManageEnvironments}
              className="group w-full rounded-2xl border border-slate-800 bg-slate-900/80 p-4 text-left transition-colors hover:border-slate-600 hover:bg-slate-800/80"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="rounded-xl bg-white/5 p-2">
                    <Server className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="font-semibold text-white">Manage Environments</p>
                    <p className="text-sm text-slate-400">Configure test environments</p>
                  </div>
                </div>
                <ChevronRight className="h-5 w-5 opacity-0 transition-opacity group-hover:opacity-100" />
              </div>
            </button>

            <button
              onClick={handleViewActivity}
              className="group w-full rounded-2xl border border-slate-800 bg-slate-900/80 p-4 text-left transition-colors hover:border-slate-600 hover:bg-slate-800/80"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="rounded-xl bg-white/5 p-2">
                    <Activity className="h-5 w-5" />
                  </div>
                  <div>
                    <p className="font-semibold text-white">View Activity</p>
                    <p className="text-sm text-slate-400">Check recent operations</p>
                  </div>
                </div>
                <ChevronRight className="h-5 w-5 opacity-0 transition-opacity group-hover:opacity-100" />
              </div>
            </button>
          </div>
        </section>
      </div>

      <section className="panel-strong p-6">
        <h2 className="mb-4 flex items-center gap-2 text-lg font-semibold text-white">
          <AlertCircle className="h-5 w-5" />
          System Status
        </h2>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <div className="flex items-center gap-3 rounded-2xl border border-emerald-500/20 bg-emerald-500/10 p-4">
            <div className="h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse" />
            <div>
              <p className="font-medium text-white">API Service</p>
              <p className="text-sm text-emerald-300">Operational</p>
            </div>
          </div>
          <div className="flex items-center gap-3 rounded-2xl border border-emerald-500/20 bg-emerald-500/10 p-4">
            <div className="h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse" />
            <div>
              <p className="font-medium text-white">Database</p>
              <p className="text-sm text-emerald-300">Connected</p>
            </div>
          </div>
          <div className="flex items-center gap-3 rounded-2xl border border-emerald-500/20 bg-emerald-500/10 p-4">
            <div className="h-2.5 w-2.5 rounded-full bg-emerald-400 animate-pulse" />
            <div>
              <p className="font-medium text-white">Message Bus</p>
              <p className="text-sm text-emerald-300">Active</p>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};

export default Dashboard;
