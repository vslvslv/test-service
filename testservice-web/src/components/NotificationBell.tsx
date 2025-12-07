import React, { useState, useEffect, useRef, forwardRef, useImperativeHandle } from 'react';
import { Bell, X, CheckCircle, AlertCircle, Info, AlertTriangle, Trash2 } from 'lucide-react';
import { Notification } from '../services/notificationService';

interface NotificationBellProps {
  onNotification?: (notification: Notification) => void;
}

export interface NotificationBellRef {
  addNotification: (notification: Notification) => void;
}

const NotificationBell = forwardRef<NotificationBellRef, NotificationBellProps>(({ onNotification }, ref) => {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // Load notifications from localStorage on mount
    const saved = localStorage.getItem('notification_history');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        setNotifications(parsed);
        // Count unread notifications
        const unread = parsed.filter((n: Notification & { read?: boolean }) => !n.read).length;
        setUnreadCount(unread);
      } catch (err) {
        console.error('Failed to parse notification history:', err);
      }
    }
  }, []);

  useEffect(() => {
    // Close dropdown when clicking outside
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  const addNotification = (notification: Notification) => {
    console.log('?? Adding notification to bell:', notification);
    
    setNotifications(prev => {
      // Add new notification at the beginning, keep only last 5
      const updated = [{ ...notification, read: false }, ...prev].slice(0, 5);
      
      // Save to localStorage
      localStorage.setItem('notification_history', JSON.stringify(updated));
      
      console.log('?? Updated notifications:', updated);
      return updated;
    });

    // Increment unread count
    setUnreadCount(prev => Math.min(prev + 1, 5));

    // Call parent callback
    if (onNotification) {
      onNotification(notification);
    }
  };

  // Expose addNotification method via ref
  useImperativeHandle(ref, () => ({
    addNotification
  }));

  const markAllAsRead = () => {
    setNotifications(prev => {
      const updated = prev.map(n => ({ ...n, read: true }));
      localStorage.setItem('notification_history', JSON.stringify(updated));
      return updated;
    });
    setUnreadCount(0);
  };

  const clearNotification = (index: number) => {
    setNotifications(prev => {
      const updated = prev.filter((_, i) => i !== index);
      localStorage.setItem('notification_history', JSON.stringify(updated));
      
      // Update unread count
      if (!(prev[index] as any).read) {
        setUnreadCount(count => Math.max(0, count - 1));
      }
      
      return updated;
    });
  };

  const clearAll = () => {
    setNotifications([]);
    setUnreadCount(0);
    localStorage.removeItem('notification_history');
  };

  const handleBellClick = () => {
    setIsOpen(!isOpen);
    if (!isOpen) {
      // Mark all as read when opening
      markAllAsRead();
    }
  };

  const getIcon = (type: Notification['type']) => {
    const iconClass = "w-4 h-4 flex-shrink-0";
    switch (type) {
      case 'schema_created':
      case 'entity_created':
        return <CheckCircle className={`${iconClass} text-green-400`} />;
      case 'schema_updated':
      case 'entity_updated':
        return <Info className={`${iconClass} text-blue-400`} />;
      case 'schema_deleted':
      case 'entity_deleted':
        return <AlertTriangle className={`${iconClass} text-yellow-400`} />;
      default:
        return <AlertCircle className={`${iconClass} text-gray-400`} />;
    }
  };

  const getNotificationMessage = (notification: Notification) => {
    switch (notification.type) {
      case 'schema_created':
        return `Schema "${notification.schemaName}" created`;
      case 'schema_updated':
        return `Schema "${notification.schemaName}" updated`;
      case 'schema_deleted':
        return `Schema "${notification.schemaName}" deleted`;
      case 'entity_created':
        return `New ${notification.entityType} entity created`;
      case 'entity_updated':
        return `${notification.entityType} entity updated`;
      case 'entity_deleted':
        return `${notification.entityType} entity deleted`;
      default:
        return 'Notification';
    }
  };

  const getTimeAgo = (timestamp: string) => {
    const now = new Date();
    const notificationTime = new Date(timestamp);
    const diffMs = now.getTime() - notificationTime.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  };

  return (
    <div className="relative" ref={dropdownRef}>
      {/* Bell Button */}
      <button
        onClick={handleBellClick}
        className="p-2 hover:bg-gray-700 rounded-lg transition-colors text-gray-400 hover:text-white relative"
        aria-label="Notifications"
      >
        <Bell className="w-5 h-5" />
        {unreadCount > 0 && (
          <span className="absolute top-1 right-1 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center text-white text-xs font-bold">
            {unreadCount}
          </span>
        )}
      </button>

      {/* Dropdown */}
      {isOpen && (
        <div className="absolute right-0 mt-2 w-96 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-50 animate-slideDown">
          {/* Header */}
          <div className="flex items-center justify-between p-4 border-b border-gray-700">
            <h3 className="text-white font-semibold">Notifications</h3>
            {notifications.length > 0 && (
              <button
                onClick={clearAll}
                className="text-xs text-gray-400 hover:text-red-400 transition-colors flex items-center gap-1"
                title="Clear all"
              >
                <Trash2 className="w-3 h-3" />
                Clear all
              </button>
            )}
          </div>

          {/* Notification List */}
          <div className="max-h-96 overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="p-8 text-center">
                <Bell className="w-12 h-12 text-gray-600 mx-auto mb-3" />
                <p className="text-gray-400 text-sm">No notifications yet</p>
              </div>
            ) : (
              <div className="divide-y divide-gray-700">
                {notifications.map((notification, index) => (
                  <div
                    key={index}
                    className={`p-4 hover:bg-gray-700/50 transition-colors group ${
                      !(notification as any).read ? 'bg-gray-700/30' : ''
                    }`}
                  >
                    <div className="flex items-start gap-3">
                      <div className="mt-0.5">
                        {getIcon(notification.type)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm text-white font-medium mb-1">
                          {getNotificationMessage(notification)}
                        </p>
                        <p className="text-xs text-gray-400">
                          {getTimeAgo(notification.timestamp)}
                        </p>
                      </div>
                      <button
                        onClick={() => clearNotification(index)}
                        className="opacity-0 group-hover:opacity-100 p-1 hover:bg-gray-600 rounded transition-all"
                        title="Dismiss"
                      >
                        <X className="w-4 h-4 text-gray-400 hover:text-white" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Footer */}
          {notifications.length > 0 && (
            <div className="p-3 border-t border-gray-700 bg-gray-800/50">
              <p className="text-xs text-gray-500 text-center">
                Showing last {notifications.length} notification{notifications.length !== 1 ? 's' : ''}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
});

NotificationBell.displayName = 'NotificationBell';

export default NotificationBell;

// Export hook for components to add notifications
export const useNotificationBell = () => {
  const [bellRef, setBellRef] = useState<{ addNotification: (n: Notification) => void } | null>(null);

  return {
    setBellRef,
    addNotification: (notification: Notification) => {
      if (bellRef) {
        bellRef.addNotification(notification);
      }
    }
  };
};
