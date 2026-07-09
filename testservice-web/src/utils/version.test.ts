import { describe, expect, it } from 'vitest';
import { shortCommit, formatBuildDate, frontendBuildInfo } from './version';

describe('shortCommit', () => {
  it('truncates a long sha to 7 characters', () => {
    expect(shortCommit('85de620e316404f3e931b3cc16b807ddc88e90dc')).toBe('85de620');
  });

  it('returns "dev" for empty, null, undefined, or the dev sentinel', () => {
    expect(shortCommit('')).toBe('dev');
    expect(shortCommit(null)).toBe('dev');
    expect(shortCommit(undefined)).toBe('dev');
    expect(shortCommit('dev')).toBe('dev');
  });

  it('returns a commit shorter than 7 chars unchanged', () => {
    expect(shortCommit('abc12')).toBe('abc12');
  });
});

describe('formatBuildDate', () => {
  it('returns "local build" for empty or null values', () => {
    expect(formatBuildDate('')).toBe('local build');
    expect(formatBuildDate(null)).toBe('local build');
    expect(formatBuildDate(undefined)).toBe('local build');
  });

  it('formats a valid ISO date into a readable string containing the year', () => {
    const output = formatBuildDate('2026-07-09T10:00:00Z');
    expect(output).not.toBe('local build');
    expect(output).toContain('2026');
  });

  it('returns the raw string when the value cannot be parsed', () => {
    expect(formatBuildDate('not-a-date')).toBe('not-a-date');
  });
});

describe('frontendBuildInfo', () => {
  it('exposes non-empty version, commit, and buildDate strings', () => {
    expect(typeof frontendBuildInfo.version).toBe('string');
    expect(frontendBuildInfo.version.length).toBeGreaterThan(0);
    expect(typeof frontendBuildInfo.commit).toBe('string');
    expect(frontendBuildInfo.commit.length).toBeGreaterThan(0);
    expect(typeof frontendBuildInfo.buildDate).toBe('string');
  });
});
