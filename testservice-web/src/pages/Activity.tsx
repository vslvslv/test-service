import React, { useCallback, useEffect, useState } from 'react';
import { Activity as ActivityIcon, Filter, RefreshCw, Wifi } from 'lucide-react';
import { apiService } from '../services/api';
import { useSignalR } from '../hooks/useSignalR';
import type { Activity, ActivityFilters } from '../types';
import ActivityTimeline from '../components/ActivityTimeline';
import ActivityFiltersPanel from '../components/ActivityFiltersPanel';

const ActivityPage: React.FC = () => {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showFilters, setShowFilters] = useState(false);
  const [filters, setFilters] = useState<ActivityFilters>({});
  const [hasMore, setHasMore] = useState(false);

  const { isConnected } = useSignalR((activityData) => {
    const newActivity = activityData as Activity;
    setActivities((prev) => [newActivity, ...prev]);
  }, 'ActivityCreated');

  const loadActivities = useCallback(async (skipCount = 0, appendResults = false) => {
    try {
      setIsLoading(!appendResults);
      setError(null);

      const response = await apiService.getActivities({
        ...filters,
        skip: skipCount,
        limit: 50,
      });

      if (appendResults) {
        setActivities((prev) => [...prev, ...response.activities]);
      } else {
        setActivities(response.activities);
      }

      setHasMore(response.hasMore);
    } catch (err) {
      console.error('Failed to load activities:', err);
      setError('Failed to load activities. Please try again.');
    } finally {
      setIsLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    loadActivities();
  }, [loadActivities]);

  const activeFiltersCount = Object.values(filters).filter(Boolean).length;

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="flex flex-col gap-6 xl:flex-row xl:items-end xl:justify-between">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-3">
              <div className="page-hero-icon">
                <ActivityIcon className="h-7 w-7 text-blue-300" />
              </div>
              <div>
                <p className="eyebrow">Operational Audit Trail</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">Monitor system activity in real time</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Track entity lifecycle changes, schema edits, user operations, and system events from one chronological workspace.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-3 xl:min-w-[520px]">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Connection</p>
              <p className={`mt-3 text-xl font-semibold ${isConnected ? 'text-emerald-300' : 'text-slate-300'}`}>
                {isConnected ? 'Live Feed' : 'Offline'}
              </p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Loaded Events</p>
              <p className="mt-3 text-3xl font-semibold text-white">{activities.length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Active Filters</p>
              <p className="mt-3 text-3xl font-semibold text-white">{activeFiltersCount}</p>
            </div>
          </div>
        </div>
      </section>

      <section className="panel p-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex flex-wrap items-center gap-3">
            <div className="inline-flex items-center gap-2 rounded-full border border-slate-700/80 bg-slate-900/80 px-4 py-2 text-sm text-slate-300">
              <Wifi className={`h-4 w-4 ${isConnected ? 'text-emerald-300' : 'text-slate-500'}`} />
              <span>{isConnected ? 'Receiving live activity updates' : 'SignalR disconnected'}</span>
            </div>
            {activeFiltersCount > 0 && (
              <button type="button" onClick={() => setFilters({})} className="badge-soft cursor-pointer">
                {activeFiltersCount} filter(s) active
              </button>
            )}
          </div>

          <div className="flex flex-wrap gap-3">
            <button type="button" onClick={() => loadActivities(0, false)} disabled={isLoading} className="button-secondary disabled:cursor-not-allowed disabled:opacity-60">
              <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </button>
            <button
              type="button"
              onClick={() => setShowFilters((current) => !current)}
              className={showFilters ? 'button-primary' : 'button-secondary'}
            >
              <Filter className="h-4 w-4" />
              {showFilters ? 'Hide Filters' : 'Show Filters'}
            </button>
          </div>
        </div>
      </section>

      {showFilters && (
        <ActivityFiltersPanel
          filters={filters}
          onFilterChange={setFilters}
          onClearFilters={() => setFilters({})}
        />
      )}

      {error && (
        <div className="rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          {error}
        </div>
      )}

      {isLoading && activities.length === 0 ? (
        <div className="flex items-center justify-center py-20">
          <div className="h-12 w-12 animate-spin rounded-full border-b-2 border-blue-500" />
        </div>
      ) : activities.length === 0 ? (
        <section className="panel p-12 text-center">
          <ActivityIcon className="mx-auto h-14 w-14 text-slate-600" />
          <h3 className="mt-4 text-lg font-medium text-white">No activity found</h3>
          <p className="mt-2 text-sm text-slate-400">
            {activeFiltersCount > 0
              ? 'Try widening the filters to reveal more operational history.'
              : 'Activity will appear here as users and background processes interact with the system.'}
          </p>
        </section>
      ) : (
        <>
          <ActivityTimeline activities={activities} />
          {hasMore && (
            <div className="flex justify-center pt-2">
              <button type="button" onClick={() => loadActivities(activities.length, true)} disabled={isLoading} className="button-secondary disabled:cursor-not-allowed disabled:opacity-60">
                {isLoading ? 'Loading...' : 'Load More'}
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default ActivityPage;
