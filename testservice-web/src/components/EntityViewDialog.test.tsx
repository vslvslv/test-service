import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import EntityViewDialog from './EntityViewDialog';
import { ToastProvider } from '../contexts/ToastContext';
import { copyToClipboard } from '../utils/clipboard';
import type { Entity, Schema } from '../types';

vi.mock('../utils/clipboard', () => ({
  copyToClipboard: vi.fn().mockResolvedValue(true),
}));
vi.mock('../services/api', () => ({
  apiService: { getEnvironments: vi.fn().mockResolvedValue([]) },
}));

const mockedCopy = vi.mocked(copyToClipboard);

const schema: Schema = {
  entityName: 'User',
  fields: [{ name: 'Username', type: 'string', required: true }],
  excludeOnFetch: false,
};

const entity: Entity = {
  id: 'e1',
  entityType: 'User',
  fields: { Username: 'AutomationMgr_5' },
  isConsumed: false,
};

function renderDialog() {
  return render(
    <ToastProvider>
      <EntityViewDialog isOpen onClose={() => {}} entity={entity} schema={schema} />
    </ToastProvider>,
  );
}

describe('EntityViewDialog copy actions', () => {
  beforeEach(() => {
    mockedCopy.mockClear();
    mockedCopy.mockResolvedValue(true);
  });

  it('copies a single field value when its copy button is clicked', async () => {
    renderDialog();

    fireEvent.click(screen.getByTitle('Copy value'));

    await waitFor(() => expect(mockedCopy).toHaveBeenCalledWith('AutomationMgr_5'));
  });

  it('copies all field values as JSON when Copy All is clicked', async () => {
    renderDialog();

    fireEvent.click(screen.getByRole('button', { name: /copy all/i }));

    await waitFor(() =>
      expect(mockedCopy).toHaveBeenCalledWith(JSON.stringify(entity.fields, null, 2)),
    );
  });

  it('surfaces a success toast after a successful copy', async () => {
    renderDialog();

    fireEvent.click(screen.getByTitle('Copy value'));

    expect(await screen.findByText('Copied')).toBeInTheDocument();
  });
});
