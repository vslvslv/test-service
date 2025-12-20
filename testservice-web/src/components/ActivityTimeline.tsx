import React from 'react';
import { formatDistanceToNow } from 'date-fns';
import { 
  CheckCircle, 
  XCircle, 
  Edit, 
  Trash2, 
  RefreshCw, 
  Plus,
  User,
  Database,
  Settings,
  Activity as ActivityIcon
} from 'lucide-react';
import type { Activity } from '../types';

interface ActivityTimelineProps {
  activities: Activity[];
}

const ActivityTimeline: React.FC<ActivityTimelineProps> = ({ activities }) => {
  const getActivityIcon = (type: string, action: string) => {
    if (action === 'created') return <Plus className="w-4 h-4" />;
    if (action === 'updated') return <Edit className="w-4 h-4" />;
    if (action === 'deleted') return <Trash2 className="w-4 h-4" />;
    if (action === 'consumed') return <CheckCircle className="w-4 h-4" />;
    if (action === 'reset' || action === 'bulk-reset') return <RefreshCw className="w-4 h-4" />;
    if (type === 'user') return <User className="w-4 h-4" />;
    if (type === 'schema') return <Database className="w-4 h-4" />;
    if (type === 'environment') return <Settings className="w-4 h-4" />;
    return <ActivityIcon className="w-4 h-4" />;
  };

  const getActivityColor = (action: string) => {
    switch (action) {
      case 'created':
        return 'bg-green-500/20 border-green-500/30 text-green-400';
      case 'updated':
        return 'bg-blue-500/20 border-blue-500/30 text-blue-400';
      case 'deleted':
        return 'bg-red-500/20 border-red-500/30 text-red-400';
      case 'consumed':
        return 'bg-purple-500/20 border-purple-500/30 text-purple-400';
      case 'reset':
      case 'bulk-reset':
        return 'bg-yellow-500/20 border-yellow-500/30 text-yellow-400';
      case 'logged-in':
      case 'logged-out':
        return 'bg-cyan-500/20 border-cyan-500/30 text-cyan-400';
      default:
        return 'bg-gray-500/20 border-gray-500/30 text-gray-400';
    }
  };

  const getActivityLabel = (action: string) => {
    switch (action) {
      case 'created':
        return 'Created';
      case 'updated':
        return 'Updated';
      case 'deleted':
        return 'Deleted';
      case 'consumed':
        return 'Consumed';
      case 'reset':
        return 'Reset';
      case 'bulk-reset':
        return 'Bulk Reset';
      case 'logged-in':
        return 'Logged In';
      case 'logged-out':
        return 'Logged Out';
      default:
        return action;
    }
  };

  const groupActivitiesByDate = (activities: Activity[]) => {
    const groups: { [key: string]: Activity[] } = {};
    
    activities.forEach((activity) => {
      const date = new Date(activity.timestamp);
      const today = new Date();
      const yesterday = new Date(today);
      yesterday.setDate(yesterday.getDate() - 1);
      
      let groupKey: string;
      if (date.toDateString() === today.toDateString()) {
        groupKey = 'Today';
      } else if (date.toDateString() === yesterday.toDateString()) {
        groupKey = 'Yesterday';
      } else {
        groupKey = date.toLocaleDateString('en-US', { 
          weekday: 'long', 
          month: 'short', 
          day: 'numeric' 
        });
      }
      
      if (!groups[groupKey]) {
        groups[groupKey] = [];
      }
      groups[groupKey].push(activity);
    });
    
    return groups;
  };

  const groupedActivities = groupActivitiesByDate(activities);

  return (
    <div className="space-y-8">
      {Object.entries(groupedActivities).map(([dateLabel, dateActivities]) => (
        <div key={dateLabel} className="space-y-4">
          {/* Date Header */}
          <div className="flex items-center gap-3">
            <div className="h-px bg-gray-700 flex-1" />
            <h3 className="text-sm font-semibold text-gray-400 uppercase tracking-wider">
              {dateLabel}
            </h3>
            <div className="h-px bg-gray-700 flex-1" />
          </div>

          {/* Activities for this date */}
          <div className="relative space-y-4 pl-8">
            {/* Timeline line */}
            <div className="absolute left-2 top-0 bottom-0 w-px bg-gray-700" />

            {dateActivities.map((activity, index) => {
              const colorClasses = getActivityColor(activity.action);
              const icon = getActivityIcon(activity.type, activity.action);
              const timeAgo = formatDistanceToNow(new Date(activity.timestamp), { addSuffix: true });
              
              return (
                <div key={activity.id} className="relative">
                  {/* Timeline dot */}
                  <div className={`absolute -left-6 top-3 w-4 h-4 rounded-full border-2 ${colorClasses} flex items-center justify-center z-10`}>
                    <div className="w-2 h-2 rounded-full bg-current" />
                  </div>

                  {/* Activity Card */}
                  <div className="bg-gray-800 border border-gray-700 rounded-lg p-4 hover:border-gray-600 transition-colors">
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex-1">
                        {/* Header with icon and label */}
                        <div className="flex items-center gap-3 mb-2">
                          <div className={`p-2 rounded-lg border ${colorClasses}`}>
                            {icon}
                          </div>
                          <div className="flex-1">
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className={`text-xs font-semibold px-2 py-1 rounded-md border ${colorClasses}`}>
                                {getActivityLabel(activity.action)}
                              </span>
                              {activity.entityType && (
                                <span className="text-xs px-2 py-1 bg-gray-700 text-gray-300 rounded-md border border-gray-600">
                                  {activity.entityType}
                                </span>
                              )}
                              {activity.environment && (
                                <span className="text-xs px-2 py-1 bg-blue-500/10 text-blue-400 rounded-md border border-blue-500/30">
                                  {activity.environment}
                                </span>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Description */}
                        <p className="text-gray-300 text-sm mb-2">
                          {activity.description}
                        </p>

                        {/* Metadata */}
                        <div className="flex items-center gap-4 text-xs text-gray-500">
                          <div className="flex items-center gap-1">
                            <User className="w-3 h-3" />
                            <span>{activity.user}</span>
                          </div>
                          {activity.entityId && (
                            <div className="flex items-center gap-1">
                              <Database className="w-3 h-3" />
                              <span className="font-mono">{activity.entityId.substring(0, 8)}...</span>
                            </div>
                          )}
                          {activity.details?.count !== undefined && (
                            <div className="flex items-center gap-1">
                              <span className="font-semibold">{activity.details.count}</span>
                              <span>items</span>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Timestamp */}
                      <div className="text-xs text-gray-500 whitespace-nowrap">
                        {timeAgo}
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
};

export default ActivityTimeline;
