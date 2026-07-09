import { describe, expect, it } from 'vitest'
import {
  Permissions,
  getDefaultPermissionsForRole,
  normalizePermissions,
  normalizeRole,
} from './permissions'

describe('normalizeRole', () => {
  it('returns Admin for numeric 1', () => {
    expect(normalizeRole(1)).toBe('Admin')
  })

  it('returns Admin for string "1"', () => {
    expect(normalizeRole('1')).toBe('Admin')
  })

  it('returns Admin for case-insensitive "admin" string', () => {
    expect(normalizeRole('Admin')).toBe('Admin')
    expect(normalizeRole(' ADMIN ')).toBe('Admin')
  })

  it('returns Contributor for any other value', () => {
    expect(normalizeRole('contributor')).toBe('Contributor')
    expect(normalizeRole(0)).toBe('Contributor')
    expect(normalizeRole(null)).toBe('Contributor')
    expect(normalizeRole(undefined)).toBe('Contributor')
    expect(normalizeRole({})).toBe('Contributor')
  })
})

describe('getDefaultPermissionsForRole', () => {
  it('returns the full Permissions list for Admin', () => {
    const result = getDefaultPermissionsForRole('Admin')
    expect(result).toEqual(Object.values(Permissions))
  })

  it('returns the contributor subset (does not include settings.write)', () => {
    const result = getDefaultPermissionsForRole('Contributor')
    expect(result).toContain(Permissions.DashboardRead)
    expect(result).toContain(Permissions.SchemasRead)
    expect(result).toContain(Permissions.EntitiesWrite)
    expect(result).not.toContain(Permissions.SettingsWrite)
    expect(result).not.toContain(Permissions.UsersDelete)
  })
})

describe('normalizePermissions', () => {
  it('returns role defaults when permissions is not an array', () => {
    expect(normalizePermissions(undefined, 'Admin')).toEqual(Object.values(Permissions))
    expect(normalizePermissions(null, 'Contributor')).toEqual(getDefaultPermissionsForRole('Contributor'))
  })

  it('returns role defaults when permissions is an empty array', () => {
    expect(normalizePermissions([], 'Admin')).toEqual(Object.values(Permissions))
  })

  it('preserves user-supplied permissions when non-empty', () => {
    const supplied = [Permissions.DashboardRead, Permissions.MocksRead]
    expect(normalizePermissions(supplied, 'Contributor')).toEqual(supplied)
  })

  it('deduplicates supplied permissions', () => {
    const supplied = [Permissions.DashboardRead, Permissions.DashboardRead, Permissions.MocksRead]
    expect(normalizePermissions(supplied, 'Contributor')).toEqual([
      Permissions.DashboardRead,
      Permissions.MocksRead,
    ])
  })

  it('filters out non-string and empty entries', () => {
    const supplied = [Permissions.DashboardRead, '', '   ', 42, null, undefined] as unknown[]
    expect(normalizePermissions(supplied, 'Contributor')).toEqual([Permissions.DashboardRead])
  })
})
