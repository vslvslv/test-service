export const Permissions = {
  DashboardRead: 'dashboard.read',
  SchemasRead: 'schemas.read',
  SchemasWrite: 'schemas.write',
  SchemasDelete: 'schemas.delete',
  EntitiesRead: 'entities.read',
  EntitiesWrite: 'entities.write',
  EntitiesDelete: 'entities.delete',
  EntitiesReset: 'entities.reset',
  EnvironmentsRead: 'environments.read',
  EnvironmentsWrite: 'environments.write',
  EnvironmentsDelete: 'environments.delete',
  ActivityRead: 'activity.read',
  SettingsRead: 'settings.read',
  SettingsWrite: 'settings.write',
  ApiKeysRead: 'settings.api_keys.read',
  ApiKeysCreate: 'settings.api_keys.create',
  ApiKeysDelete: 'settings.api_keys.delete',
  UsersRead: 'users.read',
  UsersCreate: 'users.create',
  UsersUpdate: 'users.update',
  UsersDelete: 'users.delete',
  UsersPermissionsManage: 'users.permissions.manage',
} as const;

export type PermissionKey = (typeof Permissions)[keyof typeof Permissions];

export type RoleName = 'Contributor' | 'Admin';

export const normalizeRole = (role: unknown): RoleName => {
  if (role === 1 || role === '1' || role === 'Admin') return 'Admin';
  return 'Contributor';
};

const contributorDefaults: string[] = [
  Permissions.DashboardRead,
  Permissions.SchemasRead,
  Permissions.SchemasWrite,
  Permissions.SchemasDelete,
  Permissions.EntitiesRead,
  Permissions.EntitiesWrite,
  Permissions.EntitiesDelete,
  Permissions.EntitiesReset,
  Permissions.EnvironmentsRead,
  Permissions.ActivityRead
];

const adminDefaults: string[] = Object.values(Permissions);

export const getDefaultPermissionsForRole = (role: unknown): string[] => {
  return normalizeRole(role) === 'Admin' ? adminDefaults : contributorDefaults;
};

export const normalizePermissions = (permissions: unknown, role: unknown): string[] => {
  if (Array.isArray(permissions) && permissions.length > 0) {
    return Array.from(new Set(permissions.filter((p): p is string => typeof p === 'string' && p.trim().length > 0)));
  }
  return getDefaultPermissionsForRole(role);
};
