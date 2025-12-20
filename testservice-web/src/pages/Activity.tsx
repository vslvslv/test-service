import React, { useState, useEffect, useCallback } from 'react';
import { Activity as ActivityIcon, Filter, Calendar, User as UserIcon, RefreshCw } from 'lucide-react';
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

  // Connect to SignalR for real-time updates
  const { isConnected } = useSignalR((activityData) => {
    // Add new activity to the top of the list
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

  const handleRefresh = () => {
    loadActivities(0, false);
  };

  const handleLoadMore = () => {
    loadActivities(activities.length, true);
  };

  const handleFilterChange = (newFilters: ActivityFilters) => {
    setFilters(newFilters);
  };

  const handleClearFilters = () => {
    setFilters({});
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-3">
            <ActivityIcon className="w-8 h-8 text-blue-400" />
            Activity Log
          </h1>
          <p className="text-gray-400 mt-1">
            Monitor all system activities and operations in real-time
          </p>
        </div>

        <div className="flex items-center gap-3">
          {/* Connection Status */}
          <div className="flex items-center gap-2 px-3 py-2 bg-gray-800 rounded-lg border border-gray-700">
            <div className={`w-2 h-2 rounded-full ${isConnected ? 'bg-green-500 animate-pulse' : 'bg-gray-500'}`} />
            <span className="text-sm text-gray-300">
              {isConnected ? 'Live' : 'Offline'}
            </span>
          </div>

          {/* Refresh Button */}
          <button
            onClick={handleRefresh}
            disabled={isLoading}
            className="flex items-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </button>

          {/* Filter Toggle Button */}
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={`flex items-center gap-2 px-4 py-2 rounded-lg transition-colors ${
              showFilters
                ? 'bg-blue-600 hover:bg-blue-700 text-white'
                : 'bg-gray-700 hover:bg-gray-600 text-white'
            }`}
          >
            <Filter className="w-4 h-4" />
            Filters
          </button>
        </div>
      </div>

      {/* Filters Panel */}
      {showFilters && (
        <ActivityFiltersPanel
          filters={filters}
          onFilterChange={handleFilterChange}
          onClearFilters={handleClearFilters}
        />
      )}

      {/* Active Filters Summary */}
      {Object.keys(filters).length > 0 && (
        <div className="flex items-center gap-2 p-3 bg-blue-500/10 border border-blue-500/20 rounded-lg">
          <Filter className="w-4 h-4 text-blue-400" />
          <span className="text-sm text-blue-300">
            {Object.keys(filters).length} filter(s) active
          </span>
          <button
            onClick={handleClearFilters}
            className="ml-auto text-sm text-blue-400 hover:text-blue-300"
          >
            Clear all
          </button>
        </div>
      )}

      {/* Error Message */}
      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/20 rounded-lg">
          <p className="text-red-400 text-sm">{error}</p>
        </div>
      )}

      {/* Activity Timeline */}
      {isLoading && activities.length === 0 ? (
        <div className="flex items-center justify-center py-20">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500" />
        </div>
      ) : activities.length === 0 ? (
        <div className="text-center py-20">
          <ActivityIcon className="w-16 h-16 text-gray-600 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-gray-400 mb-2">No activities found</h3>
          <p className="text-gray-500">
            {Object.keys(filters).length > 0
              ? 'Try adjusting your filters to see more activities'
              : 'Activities will appear here as actions are performed'}
          </p>
        </div>
      ) : (
        <>
          <ActivityTimeline activities={activities} />

          {/* Load More Button */}
          {hasMore && (
            <div className="flex justify-center pt-4">
              <button
                onClick={handleLoadMore}
                disabled={isLoading}
                className="px-6 py-3 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
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
