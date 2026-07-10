import { afterEach, describe, expect, it, vi } from 'vitest';
import { copyToClipboard } from './clipboard';

describe('copyToClipboard', () => {
  const originalClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
  const originalExecCommand = document.execCommand;

  const setClipboard = (value: unknown) => {
    Object.defineProperty(navigator, 'clipboard', {
      value,
      configurable: true,
      writable: true,
    });
  };

  afterEach(() => {
    if (originalClipboard) {
      Object.defineProperty(navigator, 'clipboard', originalClipboard);
    } else {
      setClipboard(undefined);
    }
    document.execCommand = originalExecCommand;
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  it('uses the async Clipboard API when available in a secure context', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    setClipboard({ writeText });
    const execCommand = vi.fn().mockReturnValue(true);
    document.execCommand = execCommand;

    const result = await copyToClipboard('hello');

    expect(result).toBe(true);
    expect(writeText).toHaveBeenCalledWith('hello');
    expect(execCommand).not.toHaveBeenCalled();
  });

  it('falls back to execCommand when the Clipboard API rejects (e.g. permission denied)', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    setClipboard({ writeText });
    const execCommand = vi.fn().mockReturnValue(true);
    document.execCommand = execCommand;

    const result = await copyToClipboard('hello');

    expect(result).toBe(true);
    expect(writeText).toHaveBeenCalled();
    expect(execCommand).toHaveBeenCalledWith('copy');
  });

  it('falls back to execCommand when the Clipboard API is unavailable (served over HTTP)', async () => {
    setClipboard(undefined); // navigator.clipboard is undefined in an insecure context
    const execCommand = vi.fn().mockReturnValue(true);
    document.execCommand = execCommand;

    const result = await copyToClipboard('over-http');

    expect(result).toBe(true);
    expect(execCommand).toHaveBeenCalledWith('copy');
  });

  it('returns false when both the API and the fallback fail', async () => {
    setClipboard(undefined);
    document.execCommand = vi.fn().mockReturnValue(false);

    const result = await copyToClipboard('nope');

    expect(result).toBe(false);
  });

  it('cleans up the temporary textarea after the legacy copy', async () => {
    setClipboard(undefined);
    document.execCommand = vi.fn().mockReturnValue(true);

    await copyToClipboard('cleanup');

    expect(document.querySelectorAll('textarea')).toHaveLength(0);
  });
});
