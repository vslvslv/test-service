import React from 'react';
import { formatDistanceToNow } from 'date-fns';
import {
  Activity as ActivityIcon,
  CheckCircle,
  Database,
  Edit,
  Plus,
  RefreshCw,
  Settings,
  Trash2,
  User,
} from 'lucide-react';
import type { Activity } from '../types';

interface ActivityTimelineProps {
  activities: Activity[];
}

const ActivityTimeline: React.FC<ActivityTimelineProps> = ({ activities }) => {
  const getActivityIcon = (type: string, action: string) => {
    if (action === 'created') return <Plus className="h-4 w-4" />;
    if (action === 'updated') return <Edit className="h-4 w-4" />;
    if (action === 'deleted') return <Trash2 className="h-4 w-4" />;
    if (action === 'consumed') return <CheckCircle className="h-4 w-4" />;
    if (action === 'reset' || action === 'bulk-reset') return <RefreshCw className="h-4 w-4" />;
    if (type === 'user') return <User className="h-4 w-4" />;
    if (type === 'schema') return <Database className="h-4 w-4" />;
    if (type === 'environment') return <Settings className="h-4 w-4" />;
    return <ActivityIcon className="h-4 w-4" />;
  };

  const getActivityColor = (action: string) => {
    switch (action) {
      case 'created':
        return 'border-emerald-500/25 bg-emerald-500/10 text-emerald-300';
      case 'updated':
        return 'border-blue-500/25 bg-blue-500/10 text-blue-300';
      case 'deleted':
        return 'border-red-500/25 bg-red-500/10 text-red-300';
      case 'consumed':
        return 'border-violet-500/25 bg-violet-500/10 text-violet-300';
      case 'reset':
      case 'bulk-reset':
        return 'border-amber-500/25 bg-amber-500/10 text-amber-300';
      case 'logged-in':
      case 'logged-out':
        return 'border-cyan-500/25 bg-cyan-500/10 text-cyan-300';
      default:
        return 'border-slate-700 bg-slate-900/70 text-slate-300';
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

  const groupActivitiesByDate = (items: Activity[]) => {
    const groups: Record<string, Activity[]> = {};

    items.forEach((activity) => {
      const date = new Date(activity.timestamp);
      const today = new Date();
      const yesterday = new Date();
      yesterday.setDate(today.getDate() - 1);

      let groupKey: string;
      if (date.toDateString() === today.toDateString()) {
        groupKey = 'Today';
      } else if (date.toDateString() === yesterday.toDateString()) {
        groupKey = 'Yesterday';
      } else {
        groupKey = date.toLocaleDateString('en-US', {
          weekday: 'long',
          month: 'short',
          day: 'numeric',
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
        <section key={dateLabel} className="space-y-4">
          <div className="flex items-center gap-4">
            <div className="h-px flex-1 bg-slate-800" />
            <h3 className="text-xs font-semibold uppercase tracking-[0.22em] text-slate-500">{dateLabel}</h3>
            <div className="h-px flex-1 bg-slate-800" />
          </div>

          <div className="relative space-y-4 pl-8">
            <div className="absolute left-2 top-0 bottom-0 w-px bg-slate-800" />

            {dateActivities.map((activity) => {
              const colorClasses = getActivityColor(activity.action);
              const timeAgo = formatDistanceToNow(new Date(activity.timestamp), { addSuffix: true });

              return (
                <div key={activity.id} className="relative">
                  <div className={`absolute -left-6 top-6 z-10 flex h-4 w-4 items-center justify-center rounded-full border-2 ${colorClasses}`}>
                    <div className="h-2 w-2 rounded-full bg-current" />
                  </div>

                  <div className="panel p-5 transition-colors hover:bg-slate-900/70">
                    <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                      <div className="min-w-0 flex-1">
                        <div className="mb-3 flex flex-wrap items-center gap-2">
                          <div className={`inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-medium ${colorClasses}`}>
                            {getActivityIcon(activity.type, activity.action)}
                            <span>{getActivityLabel(activity.action)}</span>
                          </div>
                          {activity.entityType && <span className="badge-soft">{activity.entityType}</span>}
                          {activity.environment && <span className="badge-soft border-blue-500/25 bg-blue-500/10 text-blue-300">{activity.environment}</span>}
                        </div>

                        <p className="text-sm leading-6 text-slate-200">{activity.description}</p>

                        <div className="mt-4 flex flex-wrap items-center gap-3 text-xs text-slate-500">
                          <span className="inline-flex items-center gap-1.5">
                            <User className="h-3.5 w-3.5" />
                            {activity.user}
                          </span>
                          {activity.entityId && (
                            <span className="inline-flex items-center gap-1.5">
                              <Database className="h-3.5 w-3.5" />
                              <code className="rounded bg-slate-950/70 px-2 py-1 text-slate-300">{activity.entityId.substring(0, 8)}...</code>
                            </span>
                          )}
                          {activity.details?.count !== undefined && (
                            <span className="badge-soft">{activity.details.count} items</span>
                          )}
                        </div>
                      </div>

                      <div className="text-xs uppercase tracking-[0.18em] text-slate-500">{timeAgo}</div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
};

export default ActivityTimeline;
