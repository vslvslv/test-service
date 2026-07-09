import React, { useEffect, useState } from 'react';
import { Calendar, Database, Tag, X } from 'lucide-react';
import { apiService } from '../services/api';
import type { ActivityFilters, Schema } from '../types';

interface ActivityFiltersPanelProps {
  filters: ActivityFilters;
  onFilterChange: (filters: ActivityFilters) => void;
  onClearFilters: () => void;
}

const ActivityFiltersPanel: React.FC<ActivityFiltersPanelProps> = ({
  filters,
  onFilterChange,
  onClearFilters,
}) => {
  const [schemas, setSchemas] = useState<Schema[]>([]);
  const [localFilters, setLocalFilters] = useState<ActivityFilters>(filters);

  useEffect(() => {
    loadSchemas();
  }, []);

  useEffect(() => {
    setLocalFilters(filters);
  }, [filters]);

  const loadSchemas = async () => {
    try {
      const data = await apiService.getSchemas();
      setSchemas(data);
    } catch (error) {
      console.error('Failed to load schemas:', error);
    }
  };

  const handleFilterChange = (key: keyof ActivityFilters, value: string) => {
    const nextFilters = { ...localFilters, [key]: value || undefined };
    setLocalFilters(nextFilters);
    onFilterChange(nextFilters);
  };

  const handleDateRangeChange = (range: 'today' | 'yesterday' | 'week') => {
    const now = new Date();
    let startDate: Date;

    switch (range) {
      case 'today':
        startDate = new Date(now);
        startDate.setHours(0, 0, 0, 0);
        break;
      case 'yesterday':
        startDate = new Date(now);
        startDate.setDate(startDate.getDate() - 1);
        startDate.setHours(0, 0, 0, 0);
        break;
      case 'week':
      default:
        startDate = new Date(now);
        startDate.setDate(startDate.getDate() - 7);
        break;
    }

    const nextFilters = {
      ...localFilters,
      startDate: startDate.toISOString(),
      endDate: new Date().toISOString(),
    };

    setLocalFilters(nextFilters);
    onFilterChange(nextFilters);
  };

  return (
    <section className="panel p-5">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p className="eyebrow">Activity Filters</p>
          <h3 className="mt-2 text-xl font-semibold text-white">Refine the operational timeline</h3>
        </div>
        <button type="button" onClick={onClearFilters} className="button-secondary">
          <X className="h-4 w-4" />
          Clear All
        </button>
      </div>

      <div className="mt-5 grid gap-4 xl:grid-cols-4">
        <fieldset className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <legend className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-300">
            <Calendar className="h-4 w-4 text-slate-400" />
            Time Period
          </legend>
          <div className="grid grid-cols-2 gap-2">
            <button type="button" onClick={() => handleDateRangeChange('today')} className="button-secondary !rounded-xl !px-3 !py-2 text-xs">
              Today
            </button>
            <button type="button" onClick={() => handleDateRangeChange('yesterday')} className="button-secondary !rounded-xl !px-3 !py-2 text-xs">
              Yesterday
            </button>
            <button type="button" onClick={() => handleDateRangeChange('week')} className="button-secondary !rounded-xl !px-3 !py-2 text-xs col-span-2">
              Last 7 Days
            </button>
          </div>
        </fieldset>

        <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <label htmlFor="activity-filter-schema" className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-300">
            <Database className="h-4 w-4 text-slate-400" />
            Schema
          </label>
          <select
            id="activity-filter-schema"
            value={localFilters.entityType || ''}
            onChange={(e) => handleFilterChange('entityType', e.target.value)}
            className="field-shell"
          >
            <option value="">All Schemas</option>
            {schemas.map((schema) => (
              <option key={schema.entityName} value={schema.entityName}>
                {schema.entityName}
              </option>
            ))}
          </select>
        </div>

        <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <label htmlFor="activity-filter-type" className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-300">
            <Tag className="h-4 w-4 text-slate-400" />
            Type
          </label>
          <select
            id="activity-filter-type"
            value={localFilters.type || ''}
            onChange={(e) => handleFilterChange('type', e.target.value)}
            className="field-shell"
          >
            <option value="">All Types</option>
            <option value="entity">Entity</option>
            <option value="schema">Schema</option>
            <option value="user">User</option>
            <option value="environment">Environment</option>
            <option value="system">System</option>
          </select>
        </div>

        <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <label htmlFor="activity-filter-action" className="mb-3 flex items-center gap-2 text-sm font-medium text-slate-300">
            <Tag className="h-4 w-4 text-slate-400" />
            Action
          </label>
          <select
            id="activity-filter-action"
            value={localFilters.action || ''}
            onChange={(e) => handleFilterChange('action', e.target.value)}
            className="field-shell"
          >
            <option value="">All Actions</option>
            <option value="created">Created</option>
            <option value="updated">Updated</option>
            <option value="deleted">Deleted</option>
            <option value="consumed">Consumed</option>
            <option value="reset">Reset</option>
            <option value="bulk-reset">Bulk Reset</option>
            <option value="logged-in">Logged In</option>
            <option value="logged-out">Logged Out</option>
          </select>
        </div>
      </div>

      {(localFilters.startDate || localFilters.endDate) && (
        <div className="mt-5 grid gap-4 lg:grid-cols-2">
          <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
            <label htmlFor="activity-filter-start-date" className="mb-3 block text-sm font-medium text-slate-300">Start Date</label>
            <input
              id="activity-filter-start-date"
              type="datetime-local"
              value={localFilters.startDate ? new Date(localFilters.startDate).toISOString().slice(0, 16) : ''}
              onChange={(e) => handleFilterChange('startDate', e.target.value ? new Date(e.target.value).toISOString() : '')}
              className="field-shell"
            />
          </div>
          <div className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
            <label htmlFor="activity-filter-end-date" className="mb-3 block text-sm font-medium text-slate-300">End Date</label>
            <input
              id="activity-filter-end-date"
              type="datetime-local"
              value={localFilters.endDate ? new Date(localFilters.endDate).toISOString().slice(0, 16) : ''}
              onChange={(e) => handleFilterChange('endDate', e.target.value ? new Date(e.target.value).toISOString() : '')}
              className="field-shell"
            />
          </div>
        </div>
      )}
    </section>
  );
};

export default ActivityFiltersPanel;
