// Helper utilities for common patterns

import { getErrorMessage } from '../types';

/**
 * Standard error handler for try-catch blocks
 * @param error - The caught error
 * @param setError - State setter for error message
 * @param fallbackMessage - Optional fallback message
 */
export function handleCatchError(
  error: unknown,
  setError: (message: string) => void,
  fallbackMessage?: string
): void {
  const message = getErrorMessage(error);
  setError(fallbackMessage || message);
}

/**
 * Async error handler that returns the error message
 * @param error - The caught error
 * @returns Error message string
 */
export function getAsyncErrorMessage(error: unknown): string {
  return getErrorMessage(error);
}
