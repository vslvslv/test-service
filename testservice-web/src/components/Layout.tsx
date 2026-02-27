import React, { useState, useRef, useEffect, useLayoutEffect, useMemo } from 'react';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import NotificationBell, { NotificationBellRef } from './NotificationBell';
import { apiService } from '../services/api';
import {
  Home,
  Database,
  Layers,
  Server,
  Users,
  Settings,
  LogOut,
  Menu,
  X,
  Search,
  KeyRound,
  User as UserIcon,
  ChevronRight
} from 'lucide-react';

interface SearchSchema {
  entityName: string;
  fields?: Array<{ name?: string; required?: boolean }>;
  filterableFields?: string[];
  excludeOnFetch?: boolean;
}

interface SearchEnvironment {
  id: string;
  name: string;
  displayName?: string;
  description?: string;
}

interface SearchUser {
  id: string;
  username: string;
  email?: string;
  role?: string;
}

interface SearchApiKey {
  id: string;
  name: string;
  expiresAt?: string | null;
}

type SearchCategory = 'navigation' | 'schema' | 'entity' | 'environment' | 'settings' | 'api-key' | 'user';

interface SearchItem {
  id: string;
  category: SearchCategory;
  label: string;
  description: string;
  keywords: string[];
  path: string;
  icon: React.ComponentType<{ className?: string }>;
  iconClass: string;
}

const Layout: React.FC = () => {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [showSearchResults, setShowSearchResults] = useState(false);
  const [schemas, setSchemas] = useState<SearchSchema[]>([]);
  const [environments, setEnvironments] = useState<SearchEnvironment[]>([]);
  const [users, setUsers] = useState<SearchUser[]>([]);
  const [apiKeys, setApiKeys] = useState<SearchApiKey[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const searchContainerRef = useRef<HTMLDivElement>(null);
  const { user, logout } = useAuth();
  const { setBellCallback } = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const bellRef = useRef<NotificationBellRef>(null);

  // Load data for global search index
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setSearchLoading(true);
      try {
        const [schemasResult, envsResult, usersResult, apiKeysResult] = await Promise.allSettled([
          apiService.getSchemas(),
          apiService.getEnvironments(),
          user?.role === 'Admin' ? apiService.getUsers() : Promise.resolve([]),
          user?.role === 'Admin' ? apiService.getApiKeys() : Promise.resolve([])
        ]);

        const schemasData = schemasResult.status === 'fulfilled' ? schemasResult.value : [];
        const envsData = envsResult.status === 'fulfilled' ? envsResult.value : [];
        const usersData = usersResult.status === 'fulfilled' ? usersResult.value : [];
        const apiKeysData = apiKeysResult.status === 'fulfilled' ? apiKeysResult.value : [];

        if (!cancelled) {
          setSchemas(schemasData as SearchSchema[]);
          setEnvironments(envsData as SearchEnvironment[]);
          setUsers(Array.isArray(usersData) ? (usersData as SearchUser[]) : []);
          setApiKeys(Array.isArray(apiKeysData) ? (apiKeysData as SearchApiKey[]) : []);
        }
      } catch {
        if (!cancelled) {
          setSchemas([]);
          setEnvironments([]);
          setUsers([]);
          setApiKeys([]);
        }
      } finally {
        if (!cancelled) setSearchLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [user?.role]);

  // Click outside to close search results
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (searchContainerRef.current && !searchContainerRef.current.contains(e.target as Node)) {
        setShowSearchResults(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Reset global search when navigating (sidebar, or after create/edit actions that navigate)
  useEffect(() => {
    setSearchQuery('');
    setShowSearchResults(false);
  }, [location.pathname]);

  const query = searchQuery.trim().toLowerCase();
  const hasQuery = query.length > 0;

  const allSearchItems = useMemo<SearchItem[]>(() => {
    const navigationItems: SearchItem[] = [
      {
        id: 'nav-dashboard',
        category: 'navigation',
        label: 'Dashboard',
        description: 'Overview and system metrics',
        keywords: ['home', 'overview', 'stats'],
        path: '/',
        icon: Home,
        iconClass: 'text-blue-400'
      },
      {
        id: 'nav-entities',
        category: 'navigation',
        label: 'Entities',
        description: 'Browse and manage entity data',
        keywords: ['entity', 'data', 'records'],
        path: '/entities',
        icon: Database,
        iconClass: 'text-purple-400'
      },
      {
        id: 'nav-schemas',
        category: 'navigation',
        label: 'Schemas',
        description: 'Manage schema definitions',
        keywords: ['schema', 'model', 'fields'],
        path: '/schemas',
        icon: Layers,
        iconClass: 'text-sky-400'
      },
      {
        id: 'nav-environments',
        category: 'navigation',
        label: 'Environments',
        description: 'Manage test environments',
        keywords: ['env', 'deployment', 'target'],
        path: '/environments',
        icon: Server,
        iconClass: 'text-emerald-400'
      },
      {
        id: 'nav-settings',
        category: 'settings',
        label: 'Settings',
        description: 'System configuration and API keys',
        keywords: ['config', 'retention', 'api keys'],
        path: '/settings',
        icon: Settings,
        iconClass: 'text-amber-400'
      }
    ];

    if (user?.role === 'Admin') {
      navigationItems.push({
        id: 'nav-users',
        category: 'navigation',
        label: 'Users',
        description: 'User management and access control',
        keywords: ['accounts', 'roles', 'permissions'],
        path: '/users',
        icon: Users,
        iconClass: 'text-indigo-400'
      });
      navigationItems.push({
        id: 'settings-api-keys',
        category: 'settings',
        label: 'API Keys',
        description: 'Create, list, and revoke API keys',
        keywords: ['tokens', 'credentials', 'authentication'],
        path: '/settings',
        icon: KeyRound,
        iconClass: 'text-amber-400'
      });
    }

    const schemaItems: SearchItem[] = schemas.map((schema) => ({
      id: `schema-${schema.entityName}`,
      category: 'schema',
      label: schema.entityName,
      description: `${schema.fields?.length ?? 0} fields${schema.excludeOnFetch ? ' • auto-consume' : ''}`,
      keywords: [
        schema.entityName,
        ...(schema.fields?.map((f) => f.name || '').filter(Boolean) || []),
        ...(schema.filterableFields || []),
        schema.excludeOnFetch ? 'auto consume' : ''
      ].filter(Boolean),
      path: `/schemas/${schema.entityName}`,
      icon: Layers,
      iconClass: 'text-sky-400'
    }));

    const entityTypeItems: SearchItem[] = schemas.map((schema) => ({
      id: `entity-${schema.entityName}`,
      category: 'entity',
      label: schema.entityName,
      description: `Entity type${schema.excludeOnFetch ? ' • auto-consume enabled' : ''}`,
      keywords: [
        schema.entityName,
        'entity',
        ...(schema.fields?.map((f) => f.name || '').filter(Boolean) || [])
      ],
      path: `/entities/${schema.entityName}`,
      icon: Database,
      iconClass: 'text-purple-400'
    }));

    const environmentItems: SearchItem[] = environments.map((env) => ({
      id: `env-${env.id}`,
      category: 'environment',
      label: env.displayName || env.name,
      description: env.description || env.name,
      keywords: [env.name, env.displayName || '', env.description || '', 'environment'].filter(Boolean),
      path: '/environments',
      icon: Server,
      iconClass: 'text-emerald-400'
    }));

    const userItems: SearchItem[] = users.map((searchUser) => ({
      id: `user-${searchUser.id}`,
      category: 'user',
      label: searchUser.username,
      description: `${searchUser.role || 'User'}${searchUser.email ? ` • ${searchUser.email}` : ''}`,
      keywords: [searchUser.username, searchUser.email || '', searchUser.role || '', 'user'].filter(Boolean),
      path: '/users',
      icon: Users,
      iconClass: 'text-indigo-400'
    }));

    const apiKeyItems: SearchItem[] = apiKeys.map((key) => ({
      id: `api-key-${key.id}`,
      category: 'api-key',
      label: key.name,
      description: key.expiresAt ? `Expires ${new Date(key.expiresAt).toLocaleDateString()}` : 'No expiration',
      keywords: [key.name, 'api', 'key', 'settings'],
      path: '/settings',
      icon: KeyRound,
      iconClass: 'text-amber-400'
    }));

    return [...navigationItems, ...schemaItems, ...entityTypeItems, ...environmentItems, ...userItems, ...apiKeyItems];
  }, [schemas, environments, users, apiKeys, user?.role]);

  const filteredItems = useMemo(() => {
    if (!hasQuery) return [];
    const tokens = query.split(/\s+/).filter(Boolean);

    const scoreItem = (item: SearchItem) => {
      const normalizedLabel = item.label.toLowerCase();
      const normalizedDescription = item.description.toLowerCase();
      const normalizedKeywords = item.keywords.join(' ').toLowerCase();
      let score = 0;

      if (normalizedLabel === query) score += 120;
      if (normalizedLabel.startsWith(query)) score += 70;
      if (normalizedLabel.includes(query)) score += 35;
      if (normalizedDescription.includes(query)) score += 20;
      if (normalizedKeywords.includes(query)) score += 15;
      if (item.category === 'navigation') score += 5;
      return score;
    };

    const results = allSearchItems
      .filter((item) => {
        const haystack = `${item.label} ${item.description} ${item.keywords.join(' ')}`.toLowerCase();
        return tokens.every((token) => haystack.includes(token));
      })
      .map((item) => ({ item, score: scoreItem(item) }))
      .sort((a, b) => b.score - a.score)
      .slice(0, 30)
      .map((entry) => entry.item);

    return results;
  }, [allSearchItems, hasQuery, query]);

  const groupedResults = useMemo(() => {
    const order: SearchCategory[] = ['navigation', 'schema', 'entity', 'environment', 'user', 'settings', 'api-key'];
    return order
      .map((category) => ({
        category,
        items: filteredItems.filter((item) => item.category === category)
      }))
      .filter((group) => group.items.length > 0);
  }, [filteredItems]);

  const categoryLabels: Record<SearchCategory, string> = {
    navigation: 'Navigation',
    schema: 'Schemas',
    entity: 'Entity Types',
    environment: 'Environments',
    user: 'Users',
    settings: 'Settings',
    'api-key': 'API Keys'
  };

  const quickSuggestions = allSearchItems
    .filter((item) => item.category === 'navigation' || item.category === 'settings')
    .slice(0, 8);

  const hasResults = groupedResults.length > 0;
  const showDropdown = showSearchResults;

  // Use useLayoutEffect to register callback synchronously after render
  useLayoutEffect(() => {
    // Use a small delay to ensure ref is populated
    const timer = setTimeout(() => {
      if (bellRef.current?.addNotification) {
        console.log('? Registering bell callback (useLayoutEffect)');
        setBellCallback(bellRef.current.addNotification);
      } else {
        console.log('? Bell ref not ready in useLayoutEffect, will retry...');
      }
    }, 50);

    return () => clearTimeout(timer);
  }, []); // Run once on mount

  // Also register on any changes to ensure it's set
  useEffect(() => {
    if (bellRef.current?.addNotification) {
      console.log('? Re-registering bell callback (useEffect)');
      setBellCallback(bellRef.current.addNotification);
    } else {
      console.log('? Bell ref still not ready in useEffect');
      // Keep trying
      const retryTimer = setInterval(() => {
        if (bellRef.current?.addNotification) {
          console.log('? Bell callback registered after retry');
          setBellCallback(bellRef.current.addNotification);
          clearInterval(retryTimer);
        }
      }, 100);

      // Stop trying after 5 seconds
      setTimeout(() => clearInterval(retryTimer), 5000);

      return () => clearInterval(retryTimer);
    }
  }, [setBellCallback]);

  const handleLogout = () => {
    logout();
    navigate('login');
  };

  const menuItems = [
    { icon: Home, label: 'Dashboard', path: '/' },
    { icon: Database, label: 'Entities', path: '/entities' },
    { icon: Layers, label: 'Schemas', path: '/schemas' },
    { icon: Server, label: 'Environments', path: '/environments' },
    { icon: Users, label: 'Users', path: '/users', adminOnly: true },
  ];

  const isActive = (path: string) => {
    return location.pathname === path;
  };

  return (
    <div className="min-h-screen bg-gray-900">
      {/* Top Navigation Bar */}
      <header className="bg-gray-800 border-b border-gray-700 fixed top-0 left-0 right-0 z-30 h-16">
        <div className="flex items-center justify-between h-full px-4">
          {/* Left side */}
          <div className="flex items-center gap-4">
            <button
              onClick={() => setIsSidebarOpen(!isSidebarOpen)}
              className="p-2 hover:bg-gray-700 rounded-lg transition-colors text-gray-400 hover:text-white"
            >
              {isSidebarOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
            </button>
            
            <div className="flex items-center gap-2">
              <Database className="w-6 h-6 text-blue-500" />
              <span className="text-white font-semibold text-lg">Test Service</span>
            </div>
          </div>

          {/* Search Bar */}
          <div className="hidden md:flex flex-1 max-w-2xl mx-8" ref={searchContainerRef}>
            <div className="relative w-full">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500 pointer-events-none" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                onFocus={() => setShowSearchResults(true)}
                placeholder="Search entities, schemas, environments, users, settings..."
                className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              />
              {showDropdown && (
                <div className="absolute top-full left-0 right-0 mt-1 py-2 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-50 max-h-80 overflow-y-auto">
                  {searchLoading ? (
                    <div className="px-4 py-4 text-center text-gray-400 text-sm">
                      Building global search index...
                    </div>
                  ) : !hasQuery ? (
                    <div className="px-3 py-1.5">
                      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Quick Access</p>
                      {quickSuggestions.map((item) => {
                        const Icon = item.icon;
                        return (
                          <button
                            key={item.id}
                            type="button"
                            onClick={() => {
                              navigate(item.path);
                              setSearchQuery('');
                              setShowSearchResults(false);
                            }}
                            className="w-full flex items-center gap-2 px-2 py-2 text-left text-white hover:bg-gray-700 rounded-lg transition-colors"
                          >
                            <Icon className={`w-4 h-4 flex-shrink-0 ${item.iconClass}`} />
                            <div className="min-w-0">
                              <p className="truncate">{item.label}</p>
                              <p className="text-xs text-gray-400 truncate">{item.description}</p>
                            </div>
                            <ChevronRight className="w-4 h-4 text-gray-500 ml-auto" />
                          </button>
                        );
                      })}
                      <p className="text-xs text-gray-500 px-2 pt-2">Type to search across the system. Activity history is intentionally excluded.</p>
                    </div>
                  ) : hasResults ? (
                    <>
                      {groupedResults.map((group, groupIndex) => (
                        <div
                          key={group.category}
                          className={`px-3 py-1.5 ${groupIndex > 0 ? 'border-t border-gray-700' : ''}`}
                        >
                          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">
                            {categoryLabels[group.category]}
                          </p>
                          {group.items.map((item) => {
                            const Icon = item.icon;
                            return (
                              <button
                                key={item.id}
                                type="button"
                                onClick={() => {
                                  navigate(item.path);
                                  setSearchQuery('');
                                  setShowSearchResults(false);
                                }}
                                className="w-full flex items-center gap-2 px-2 py-2 text-left text-white hover:bg-gray-700 rounded-lg transition-colors"
                              >
                                <Icon className={`w-4 h-4 flex-shrink-0 ${item.iconClass}`} />
                                <div className="min-w-0">
                                  <p className="truncate">{item.label}</p>
                                  <p className="text-xs text-gray-400 truncate">{item.description}</p>
                                </div>
                                <ChevronRight className="w-4 h-4 text-gray-500 ml-auto" />
                              </button>
                            );
                          })}
                        </div>
                      ))}
                    </>
                  ) : (
                    <div className="px-4 py-4 text-center text-gray-400 text-sm">
                      No results found for &quot;{searchQuery.trim()}&quot;.
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Right side */}
          <div className="flex items-center gap-3">
            <NotificationBell ref={bellRef} />
            
            <div className="flex items-center gap-3 pl-3 border-l border-gray-700">
              <div className="flex items-center gap-2">
                <div className="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center">
                  <UserIcon className="w-4 h-4 text-white" />
                </div>
                <div className="hidden lg:block">
                  <p className="text-sm text-white font-medium">{user?.username}</p>
                  <p className="text-xs text-gray-400">{user?.role}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </header>

      {/* Sidebar */}
      <aside
        className={`fixed left-0 top-16 bottom-0 bg-gray-800 border-r border-gray-700 transition-all duration-300 z-20 ${
          isSidebarOpen ? 'w-64' : 'w-0'
        } overflow-hidden`}
      >
        <nav className="p-4 space-y-1">
          {menuItems.map((item) => {
            if (item.adminOnly && user?.role !== 'Admin') return null;
            
            const Icon = item.icon;
            const active = isActive(item.path);

            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                  active
                    ? 'bg-blue-600 text-white'
                    : 'text-gray-400 hover:bg-gray-700 hover:text-white'
                }`}
              >
                <Icon className="w-5 h-5 flex-shrink-0" />
                <span className="font-medium">{item.label}</span>
              </Link>
            );
          })}

          {/* Bottom Section */}
          <div className="absolute bottom-4 left-4 right-4 space-y-1 pt-4 border-t border-gray-700">
            <Link
              to="/settings"
              className={`flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                isActive('/settings')
                  ? 'bg-blue-600 text-white'
                  : 'text-gray-400 hover:bg-gray-700 hover:text-white'
              }`}
            >
              <Settings className="w-5 h-5 flex-shrink-0" />
              <span className="font-medium">Settings</span>
            </Link>

            <button
              onClick={handleLogout}
              className="w-full flex items-center gap-3 px-4 py-3 rounded-lg text-gray-400 hover:bg-red-600/10 hover:text-red-400 transition-colors"
            >
              <LogOut className="w-5 h-5 flex-shrink-0" />
              <span className="font-medium">Logout</span>
            </button>
          </div>
        </nav>
      </aside>

      {/* Main Content */}
      <main
        className={`transition-all duration-300 pt-16 ${
          isSidebarOpen ? 'ml-64' : 'ml-0'
        }`}
      >
        <div className="p-6">
          <Outlet />
        </div>
      </main>
    </div>
  );
};

export default Layout;
