import React, { useState, useEffect } from 'react';
import { Calendar, Database, Tag, User, X } from 'lucide-react';
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
    const newFilters = { ...localFilters, [key]: value || undefined };
    setLocalFilters(newFilters);
    onFilterChange(newFilters);
  };

  const handleDateRangeChange = (range: 'today' | 'yesterday' | 'week' | 'custom') => {
    const now = new Date();
    let startDate: Date;

    switch (range) {
      case 'today':
        startDate = new Date(now.setHours(0, 0, 0, 0));
        break;
      case 'yesterday':
        startDate = new Date(now.setDate(now.getDate() - 1));
        startDate.setHours(0, 0, 0, 0);
        break;
      case 'week':
        startDate = new Date(now.setDate(now.getDate() - 7));
        break;
      default:
        return;
    }

    const newFilters = {
      ...localFilters,
      startDate: startDate.toISOString(),
      endDate: new Date().toISOString(),
    };
    setLocalFilters(newFilters);
    onFilterChange(newFilters);
  };

  return (
    <div className="bg-gray-800 border border-gray-700 rounded-lg p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-white flex items-center gap-2">
          <Tag className="w-5 h-5 text-blue-400" />
          Filters
        </h3>
        <button
          onClick={onClearFilters}
          className="text-sm text-gray-400 hover:text-white flex items-center gap-1"
        >
          <X className="w-4 h-4" />
          Clear All
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Date Range Quick Select */}
        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2 flex items-center gap-2">
            <Calendar className="w-4 h-4" />
            Time Period
          </label>
          <div className="grid grid-cols-2 gap-2">
            <button
              onClick={() => handleDateRangeChange('today')}
              className="px-3 py-2 text-sm bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
            >
              Today
            </button>
            <button
              onClick={() => handleDateRangeChange('yesterday')}
              className="px-3 py-2 text-sm bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
            >
              Yesterday
            </button>
            <button
              onClick={() => handleDateRangeChange('week')}
              className="px-3 py-2 text-sm bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors col-span-2"
            >
              Last 7 Days
            </button>
          </div>
        </div>

        {/* Entity Type (Schema) Filter */}
        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2 flex items-center gap-2">
            <Database className="w-4 h-4" />
            Schema
          </label>
          <select
            value={localFilters.entityType || ''}
            onChange={(e) => handleFilterChange('entityType', e.target.value)}
            className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          >
            <option value="">All Schemas</option>
            {schemas.map((schema) => (
              <option key={schema.entityName} value={schema.entityName}>
                {schema.entityName}
              </option>
            ))}
          </select>
        </div>

        {/* Activity Type Filter */}
        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2 flex items-center gap-2">
            <Tag className="w-4 h-4" />
            Type
          </label>
          <select
            value={localFilters.type || ''}
            onChange={(e) => handleFilterChange('type', e.target.value)}
            className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          >
            <option value="">All Types</option>
            <option value="entity">Entity</option>
            <option value="schema">Schema</option>
            <option value="user">User</option>
            <option value="environment">Environment</option>
            <option value="system">System</option>
          </select>
        </div>

        {/* Action Filter */}
        <div>
          <label className="block text-sm font-medium text-gray-300 mb-2 flex items-center gap-2">
            <Tag className="w-4 h-4" />
            Action
          </label>
          <select
            value={localFilters.action || ''}
            onChange={(e) => handleFilterChange('action', e.target.value)}
            className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
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

      {/* Custom Date Range Inputs */}
      {(localFilters.startDate || localFilters.endDate) && (
        <div className="mt-4 pt-4 border-t border-gray-700">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                Start Date
              </label>
              <input
                type="datetime-local"
                value={localFilters.startDate ? new Date(localFilters.startDate).toISOString().slice(0, 16) : ''}
                onChange={(e) => handleFilterChange('startDate', e.target.value ? new Date(e.target.value).toISOString() : '')}
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                End Date
              </label>
              <input
                type="datetime-local"
                value={localFilters.endDate ? new Date(localFilters.endDate).toISOString().slice(0, 16) : ''}
                onChange={(e) => handleFilterChange('endDate', e.target.value ? new Date(e.target.value).toISOString() : '')}
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ActivityFiltersPanel;
