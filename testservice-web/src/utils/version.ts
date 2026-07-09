// Frontend build metadata and shared formatting helpers for the About dialog.
// The `__*__` constants are replaced at build time by Vite's `define` (see
// vite.config.ts). The `typeof` guards keep this safe if a runtime ever evaluates
// the module without those constants defined (e.g. certain test setups).

export interface BuildInfo {
  version: string;
  commit: string;
  buildDate: string;
}

const rawVersion = typeof __APP_VERSION__ !== 'undefined' ? __APP_VERSION__ : '0.0.0';
const rawCommit = typeof __GIT_SHA__ !== 'undefined' ? __GIT_SHA__ : 'dev';
const rawBuildDate = typeof __BUILD_DATE__ !== 'undefined' ? __BUILD_DATE__ : '';

export const frontendBuildInfo: BuildInfo = {
  version: rawVersion || '0.0.0',
  commit: rawCommit || 'dev',
  buildDate: rawBuildDate || '',
};

const SHORT_COMMIT_LENGTH = 7;

/**
 * Shortens a git commit SHA to 7 characters for display. Returns 'dev' when the
 * commit is unknown (empty, null, or the 'dev' sentinel).
 */
export function shortCommit(commit: string | null | undefined): string {
  if (!commit || commit === 'dev') {
    return 'dev';
  }
  return commit.slice(0, SHORT_COMMIT_LENGTH);
}

/**
 * Formats an ISO-8601 date string as a human-readable local date/time. Returns
 * 'local build' when the value is empty and the raw string when it cannot be parsed.
 */
export function formatBuildDate(value: string | null | undefined): string {
  if (!value) {
    return 'local build';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}
