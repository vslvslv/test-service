import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import AboutDialog from './AboutDialog';
import type { BackendInfo } from '../types';
import { apiService } from '../services/api';

vi.mock('../services/api', () => ({
  apiService: { getInfo: vi.fn() },
}));

const mockedGetInfo = vi.mocked(apiService.getInfo);

const backendInfo: BackendInfo = {
  name: 'Test Service API',
  version: '1.2.3',
  commit: 'abcdef1234567',
  buildDateUtc: '2026-07-01T08:00:00Z',
  environment: 'Production',
  apiVersion: 'v1',
  runtime: '.NET 10.0.1',
  serverTimeUtc: '2026-07-09T10:22:05Z',
  uptime: '3d 4h 5m',
  uptimeSeconds: 273900,
};

describe('AboutDialog', () => {
  beforeEach(() => {
    mockedGetInfo.mockReset();
  });

  it('renders nothing and does not fetch when closed', () => {
    mockedGetInfo.mockResolvedValue(backendInfo);
    const { container } = render(<AboutDialog isOpen={false} onClose={() => {}} />);
    expect(container).toBeEmptyDOMElement();
    expect(mockedGetInfo).not.toHaveBeenCalled();
  });

  it('shows the frontend section immediately when opened', () => {
    mockedGetInfo.mockResolvedValue(backendInfo);
    render(<AboutDialog isOpen onClose={() => {}} />);
    expect(screen.getByRole('dialog', { name: /about test service/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /application \(frontend\)/i })).toBeInTheDocument();
  });

  it('fetches and displays backend info fields when opened', async () => {
    mockedGetInfo.mockResolvedValue(backendInfo);
    render(<AboutDialog isOpen onClose={() => {}} />);

    expect(await screen.findByText('Production')).toBeInTheDocument();
    expect(screen.getByText('.NET 10.0.1')).toBeInTheDocument();
    expect(screen.getByText('3d 4h 5m')).toBeInTheDocument();
    // Backend commit is shortened to 7 chars for display.
    expect(screen.getByText('abcdef1')).toBeInTheDocument();
    expect(mockedGetInfo).toHaveBeenCalledTimes(1);
  });

  it('shows an error message when the backend info request fails', async () => {
    mockedGetInfo.mockRejectedValue(new Error('network down'));
    render(<AboutDialog isOpen onClose={() => {}} />);
    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/unable to load server information/i);
  });

  it('calls onClose when the Close button is clicked', async () => {
    mockedGetInfo.mockResolvedValue(backendInfo);
    const onClose = vi.fn();
    render(<AboutDialog isOpen onClose={onClose} />);
    // Let the fetch settle first so its state updates stay inside act().
    await screen.findByText('Production');
    fireEvent.click(screen.getByRole('button', { name: /^close$/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when Escape is pressed', async () => {
    mockedGetInfo.mockResolvedValue(backendInfo);
    const onClose = vi.fn();
    render(<AboutDialog isOpen onClose={onClose} />);
    await screen.findByText('Production');
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('refetches on reopen and does not flash stale backend data', async () => {
    mockedGetInfo.mockResolvedValueOnce(backendInfo);
    const { rerender } = render(<AboutDialog isOpen onClose={() => {}} />);
    expect(await screen.findByText('Production')).toBeInTheDocument();

    // Close, then queue a slow second fetch and reopen.
    rerender(<AboutDialog isOpen={false} onClose={() => {}} />);
    let resolveSecond: (value: BackendInfo) => void = () => {};
    mockedGetInfo.mockReturnValueOnce(
      new Promise<BackendInfo>((resolve) => {
        resolveSecond = resolve;
      }),
    );
    rerender(<AboutDialog isOpen onClose={() => {}} />);

    // Backend state was reset: loading shows, previous data is gone, a refetch fired.
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.queryByText('Production')).not.toBeInTheDocument();
    expect(mockedGetInfo).toHaveBeenCalledTimes(2);

    resolveSecond(backendInfo);
    expect(await screen.findByText('Production')).toBeInTheDocument();
  });
});
