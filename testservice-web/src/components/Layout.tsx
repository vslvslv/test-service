import React, { useState, useRef, useEffect, useLayoutEffect, useMemo } from 'react';
import { Outlet, Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useToast } from '../contexts/ToastContext';
import NotificationBell, { NotificationBellRef } from './NotificationBell';
import { apiService } from '../services/api';
import { Permissions } from '../utils/permissions';
import {
  Home,
  Database,
  Layers,
  Server,
  Activity,
  Users,
  Settings,
  LogOut,
  Menu,
  X,
  Search,
  KeyRound,
  User as UserIcon,
  ChevronRight,
  Boxes
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
  const { user, logout, hasPermission } = useAuth();
  const { setBellCallback } = useToast();
  const location = useLocation();
  const navigate = useNavigate();
  const bellRef = useRef<NotificationBellRef>(null);
  const canSearchUsers = hasPermission(Permissions.UsersRead);
  const canSearchApiKeys = hasPermission(Permissions.ApiKeysRead);
  const canReadSettings = hasPermission(Permissions.SettingsRead);
  const canReadMocks = hasPermission(Permissions.MocksRead);

  // Load data for global search index
  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setSearchLoading(true);
      try {
        const [schemasResult, envsResult, usersResult, apiKeysResult] = await Promise.allSettled([
          apiService.getSchemas(),
          apiService.getEnvironments(),
          canSearchUsers ? apiService.getUsers() : Promise.resolve([]),
          canSearchApiKeys ? apiService.getApiKeys() : Promise.resolve([])
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
  }, [canSearchUsers, canSearchApiKeys]);

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
    ];

    if (canReadSettings) {
      navigationItems.push({
        id: 'nav-settings',
        category: 'settings',
        label: 'Settings',
        description: 'System configuration and API keys',
        keywords: ['config', 'retention', 'api keys'],
        path: '/settings',
        icon: Settings,
        iconClass: 'text-amber-400'
      });
    }

    if (canSearchUsers) {
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
    }
    if (canReadMocks) {
      navigationItems.push({
        id: 'nav-mocks',
        category: 'navigation',
        label: 'Mocks',
        description: 'Manage mock expectations and request matching',
        keywords: ['mock', 'expectations', 'stub', 'verify'],
        path: '/mocks',
        icon: Boxes,
        iconClass: 'text-cyan-400'
      });
    }
    if (canSearchApiKeys) {
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
  }, [schemas, environments, users, apiKeys, canSearchUsers, canSearchApiKeys, canReadSettings, canReadMocks]);

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

  useLayoutEffect(() => {
    const timer = setTimeout(() => {
      if (bellRef.current?.addNotification) {
        setBellCallback(bellRef.current.addNotification);
      }
    }, 50);

    return () => clearTimeout(timer);
  }, [setBellCallback]);

  useEffect(() => {
    if (bellRef.current?.addNotification) {
      setBellCallback(bellRef.current.addNotification);
      return;
    }

    const retryTimer = setInterval(() => {
      if (bellRef.current?.addNotification) {
        setBellCallback(bellRef.current.addNotification);
        clearInterval(retryTimer);
      }
    }, 100);

    const giveUpTimer = setTimeout(() => clearInterval(retryTimer), 5000);

    return () => {
      clearInterval(retryTimer);
      clearTimeout(giveUpTimer);
    };
  }, [setBellCallback]);

  const handleLogout = () => {
    logout();
    navigate('login');
  };

  const handleNavItemClick = () => {
    if (typeof window !== 'undefined' && window.innerWidth < 1024) {
      setIsSidebarOpen(false);
    }
  };

  const menuItems = [
    { icon: Home, label: 'Dashboard', path: '/' },
    { icon: Database, label: 'Entities', path: '/entities' },
    { icon: Layers, label: 'Schemas', path: '/schemas' },
    { icon: Server, label: 'Environments', path: '/environments' },
    { icon: Activity, label: 'Activity', path: '/activity' },
    { icon: Users, label: 'Users', path: '/users', requiredPermission: Permissions.UsersRead },
    { icon: Boxes, label: 'Mocks', path: '/mocks', requiredPermission: Permissions.MocksRead },
  ];

  const isActive = (path: string) => {
    if (path === '/') return location.pathname === '/';
    return location.pathname === path || location.pathname.startsWith(`${path}/`);
  };

  const currentItem = menuItems.find((item) => isActive(item.path))
    || (location.pathname.startsWith('/settings') ? { label: 'Settings' } : null);

  return (
    <div className="min-h-screen bg-transparent text-slate-100">
      {isSidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation overlay"
          onClick={() => setIsSidebarOpen(false)}
          className="fixed inset-0 z-20 bg-black/50 lg:hidden"
        />
      )}

      <aside
        className={`fixed inset-y-0 left-0 z-30 w-80 max-w-[85vw] border-r border-slate-700/70 bg-[linear-gradient(180deg,rgba(2,6,23,0.97),rgba(15,23,42,0.98))] shadow-[0_20px_80px_rgba(2,6,23,0.38)] transition-[width,transform] duration-300 ${
          isSidebarOpen ? 'translate-x-0 lg:w-80' : '-translate-x-full lg:translate-x-0 lg:w-24'
        }`}
      >
        <div className="flex h-full flex-col">
          <div className={`border-b border-slate-800 py-5 ${isSidebarOpen ? 'px-5' : 'px-3 lg:px-4'}`}>
            <div className="flex items-start justify-between gap-3">
              <div className={isSidebarOpen ? '' : 'w-full'}>
                <div className={`inline-flex items-center ${isSidebarOpen ? 'gap-3' : 'w-full justify-center'}`}>
                  <div className="rounded-2xl border border-blue-400/25 bg-blue-500/10 p-3">
                    <Database className="h-6 w-6 text-blue-300" />
                  </div>
                  {isSidebarOpen && (
                    <div>
                      <p className="eyebrow">Enterprise Test Data</p>
                      <h1 className="mt-2 text-xl font-semibold text-white">Test Service</h1>
                    </div>
                  )}
                </div>
                {isSidebarOpen && (
                  <p className="mt-4 text-sm leading-6 text-slate-400">
                    Unified workspace for schemas, entities, mocks, activity, and operational controls.
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={() => setIsSidebarOpen(false)}
                aria-label="Close navigation"
                className="rounded-xl border border-slate-700 bg-slate-900/70 p-2 text-slate-400 hover:bg-slate-800 hover:text-white lg:hidden"
              >
                <X className="h-4 w-4" />
              </button>
            </div>
          </div>

          <div className={`flex-1 overflow-y-auto py-5 ${isSidebarOpen ? 'px-4' : 'px-3 lg:px-2.5'}`}>
            {isSidebarOpen && (
              <div className="mb-5 rounded-2xl border border-slate-800 bg-slate-900/60 px-4 py-3">
                <p className="eyebrow">Current Context</p>
                <p className="mt-2 text-lg font-semibold text-white">{currentItem?.label || 'Workspace'}</p>
                <p className="mt-1 text-sm text-slate-400">Use global search or jump directly between operational surfaces.</p>
              </div>
            )}

            <nav className="space-y-1">
              {menuItems.map((item) => {
                if (item.requiredPermission && !hasPermission(item.requiredPermission)) return null;

                const Icon = item.icon;
                const active = isActive(item.path);

                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    onClick={handleNavItemClick}
                    title={!isSidebarOpen ? item.label : undefined}
                    className={`group flex items-center rounded-2xl transition-all ${
                      active
                        ? 'border border-blue-400/25 bg-blue-500/15 text-white shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]'
                        : 'border border-transparent text-slate-400 hover:border-slate-700 hover:bg-slate-900/80 hover:text-white'
                    } ${isSidebarOpen ? 'gap-3 px-4 py-3' : 'justify-center px-2 py-3 lg:px-0'}`}
                  >
                    <div className={`rounded-xl p-2 ${active ? 'bg-blue-500/20 text-blue-200' : 'bg-slate-800/80 text-slate-400 group-hover:text-slate-200'}`}>
                      <Icon className="h-4 w-4" />
                    </div>
                    {isSidebarOpen && (
                      <div className="min-w-0">
                        <p className="font-medium">{item.label}</p>
                        <p className="text-xs text-slate-500">{item.label === 'Dashboard' ? 'Overview and status' : 'Manage and inspect'}</p>
                      </div>
                    )}
                  </Link>
                );
              })}
            </nav>
          </div>

          <div className={`border-t border-slate-800 ${isSidebarOpen ? 'p-4' : 'px-3 py-4 lg:px-2.5'}`}>
            <div className={`mb-3 rounded-2xl border border-slate-800 bg-slate-900/70 ${isSidebarOpen ? 'px-4 py-3' : 'px-2 py-3 lg:px-0'}`}>
              <div className={`flex items-center ${isSidebarOpen ? 'gap-3' : 'justify-center'}`}>
                <div className="flex h-10 w-10 items-center justify-center rounded-full bg-blue-500/20 text-blue-200">
                  <UserIcon className="h-4 w-4" />
                </div>
                {isSidebarOpen && (
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-white">{user?.username}</p>
                    <p className="truncate text-xs text-slate-400">{user?.role}</p>
                  </div>
                )}
              </div>
            </div>
            <div className="space-y-1">
              {hasPermission(Permissions.SettingsRead) && (
                <Link
                  to="/settings"
                  onClick={handleNavItemClick}
                  title={!isSidebarOpen ? 'Settings' : undefined}
                  className={`flex items-center rounded-2xl transition-colors ${
                    isActive('/settings')
                      ? 'border border-blue-400/25 bg-blue-500/15 text-white'
                      : 'text-slate-400 hover:bg-slate-900/80 hover:text-white'
                  } ${isSidebarOpen ? 'gap-3 px-4 py-3' : 'justify-center px-2 py-3 lg:px-0'}`}
                >
                  <Settings className="h-4 w-4" />
                  {isSidebarOpen && <span className="font-medium">Settings</span>}
                </Link>
              )}

              <button
                onClick={handleLogout}
                title={!isSidebarOpen ? 'Logout' : undefined}
                className={`flex w-full items-center rounded-2xl text-slate-400 transition-colors hover:bg-rose-500/10 hover:text-rose-300 ${
                  isSidebarOpen ? 'gap-3 px-4 py-3' : 'justify-center px-2 py-3 lg:px-0'
                }`}
              >
                <LogOut className="h-4 w-4" />
                {isSidebarOpen && <span className="font-medium">Logout</span>}
              </button>
            </div>
          </div>
        </div>
      </aside>

      <div className={`min-h-screen transition-[padding] duration-300 ${isSidebarOpen ? 'lg:pl-80' : 'lg:pl-24'}`}>
        <header className="sticky top-0 z-10 border-b border-slate-800/80 bg-[rgba(7,17,31,0.82)] backdrop-blur-xl">
          <div className="flex items-center gap-4 px-4 py-4 sm:px-6">
            <button
              type="button"
              onClick={() => setIsSidebarOpen((prev) => !prev)}
              aria-label={isSidebarOpen ? 'Collapse navigation' : 'Expand navigation'}
              aria-expanded={isSidebarOpen}
              className="rounded-2xl border border-slate-700 bg-slate-900/70 p-3 text-slate-400 hover:bg-slate-800 hover:text-white"
            >
              {isSidebarOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
            </button>

            <div className="hidden min-w-0 flex-1 md:flex" ref={searchContainerRef}>
              <div className="relative w-full max-w-3xl">
                <Search className="pointer-events-none absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-slate-500" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onFocus={() => setShowSearchResults(true)}
                  placeholder="Search entities, schemas, environments, users, mocks, settings..."
                  className="w-full rounded-2xl border border-slate-700 bg-slate-900/80 py-3 pl-12 pr-4 text-white placeholder-slate-500 outline-none transition-all focus:border-blue-400/60 focus:ring-4 focus:ring-blue-500/10"
                />
                {showDropdown && (
                  <div className="absolute left-0 right-0 top-full z-50 mt-2 max-h-96 overflow-y-auto rounded-[24px] border border-slate-700 bg-slate-950/95 py-2 shadow-[0_24px_80px_rgba(2,6,23,0.45)]">
                    {searchLoading ? (
                      <div className="px-4 py-4 text-center text-sm text-slate-400">
                        Building global search index...
                      </div>
                    ) : !hasQuery ? (
                      <div className="px-3 py-1.5">
                        <p className="eyebrow mb-2 px-2">Quick Access</p>
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
                              className="flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left text-white transition-colors hover:bg-slate-900"
                            >
                              <Icon className={`h-4 w-4 flex-shrink-0 ${item.iconClass}`} />
                              <div className="min-w-0">
                                <p className="truncate">{item.label}</p>
                                <p className="truncate text-xs text-slate-400">{item.description}</p>
                              </div>
                              <ChevronRight className="ml-auto h-4 w-4 text-slate-500" />
                            </button>
                          );
                        })}
                      </div>
                    ) : hasResults ? (
                      <>
                        {groupedResults.map((group, groupIndex) => (
                          <div
                            key={group.category}
                            className={`px-3 py-1.5 ${groupIndex > 0 ? 'border-t border-slate-800' : ''}`}
                          >
                            <p className="eyebrow mb-2 px-2">{categoryLabels[group.category]}</p>
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
                                  className="flex w-full items-center gap-3 rounded-2xl px-3 py-3 text-left text-white transition-colors hover:bg-slate-900"
                                >
                                  <Icon className={`h-4 w-4 flex-shrink-0 ${item.iconClass}`} />
                                  <div className="min-w-0">
                                    <p className="truncate">{item.label}</p>
                                    <p className="truncate text-xs text-slate-400">{item.description}</p>
                                  </div>
                                  <ChevronRight className="ml-auto h-4 w-4 text-slate-500" />
                                </button>
                              );
                            })}
                          </div>
                        ))}
                      </>
                    ) : (
                      <div className="px-4 py-4 text-center text-sm text-slate-400">
                        No results found for &quot;{searchQuery.trim()}&quot;.
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>

            <div className="ml-auto flex items-center gap-3">
              <NotificationBell ref={bellRef} />
              <div className="hidden rounded-2xl border border-slate-700 bg-slate-900/70 px-4 py-2 sm:block">
                <p className="text-xs uppercase tracking-[0.24em] text-slate-500">Workspace</p>
                <p className="mt-1 text-sm font-medium text-white">{currentItem?.label || 'Dashboard'}</p>
              </div>
            </div>
          </div>
        </header>

        <main className="px-4 py-6 sm:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default Layout;
