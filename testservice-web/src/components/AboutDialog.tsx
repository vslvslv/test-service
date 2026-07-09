import { useEffect, useRef, useState } from 'react';
import { Info, X, Loader2, AlertCircle } from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage, type BackendInfo } from '../types';
import { frontendBuildInfo, shortCommit, formatBuildDate } from '../utils/version';

interface AboutDialogProps {
  isOpen: boolean;
  onClose: () => void;
}

interface InfoRow {
  label: string;
  value: string;
  mono?: boolean;
}

const FOCUSABLE_SELECTOR =
  'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';

function InfoList({ rows }: { rows: InfoRow[] }) {
  return (
    <dl className="mt-3 space-y-2">
      {rows.map((row) => (
        <div key={row.label} className="flex items-center justify-between gap-4">
          <dt className="text-sm text-slate-400">{row.label}</dt>
          <dd className={`text-right text-sm text-white ${row.mono ? 'font-mono' : ''}`}>{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function AboutDialog({ isOpen, onClose }: AboutDialogProps) {
  const [backendInfo, setBackendInfo] = useState<BackendInfo | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const previouslyFocused = useRef<HTMLElement | null>(null);

  // Fetch backend info whenever the dialog opens. `cancelled` guards against a
  // response landing after the dialog has closed/unmounted.
  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    setBackendInfo(null);
    setIsLoading(true);
    setError(null);
    apiService
      .getInfo()
      .then((data) => {
        if (!cancelled) setBackendInfo(data);
      })
      .catch((err: unknown) => {
        // Friendly prefix + underlying detail (technical users), and log for diagnostics.
        console.error('Failed to load server info:', err);
        if (!cancelled) setError(`Unable to load server information: ${getErrorMessage(err)}`);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen]);

  // Focus management: remember the invoking control, move focus into the dialog on
  // open, and restore it on close. Keyed on `isOpen` only so unrelated re-renders
  // (e.g. a background toast re-rendering the parent) never re-steal focus.
  useEffect(() => {
    if (!isOpen) return;
    previouslyFocused.current = document.activeElement as HTMLElement | null;
    closeButtonRef.current?.focus();
    return () => {
      previouslyFocused.current?.focus?.();
    };
  }, [isOpen]);

  // Close on Escape and trap Tab focus inside the dialog.
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
        return;
      }
      if (e.key !== 'Tab' || !dialogRef.current) return;

      const focusables = Array.from(
        dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
      ).filter((el) => !el.hasAttribute('disabled'));
      if (focusables.length === 0) return;

      const first = focusables[0];
      const last = focusables[focusables.length - 1];
      const active = document.activeElement;

      if (e.shiftKey && active === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && active === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const frontendRows: InfoRow[] = [
    { label: 'Version', value: frontendBuildInfo.version, mono: true },
    { label: 'Commit', value: shortCommit(frontendBuildInfo.commit), mono: true },
    { label: 'Build date', value: formatBuildDate(frontendBuildInfo.buildDate) },
  ];

  const backendRows: InfoRow[] = backendInfo
    ? [
        { label: 'Version', value: backendInfo.version, mono: true },
        { label: 'Commit', value: shortCommit(backendInfo.commit), mono: true },
        { label: 'Build date', value: formatBuildDate(backendInfo.buildDateUtc) },
        { label: 'Environment', value: backendInfo.environment },
        { label: 'API version', value: backendInfo.apiVersion, mono: true },
        { label: 'Runtime', value: backendInfo.runtime },
        { label: 'Uptime', value: backendInfo.uptime },
        { label: 'Server time (UTC)', value: backendInfo.serverTimeUtc, mono: true },
      ]
    : [];

  return (
    <div
      className="modal-backdrop"
      role="presentation"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="about-dialog-title"
        className="modal-shell max-h-[90vh] max-w-2xl overflow-hidden"
      >
        <div className="flex items-center justify-between border-b border-slate-800 px-6 py-5">
          <div className="flex items-center gap-3">
            <div className="page-hero-icon !p-2.5">
              <Info className="h-5 w-5 text-blue-300" />
            </div>
            <div>
              <h2 id="about-dialog-title" className="text-xl font-semibold text-white">
                About Test Service
              </h2>
              <p className="mt-1 text-sm text-slate-400">Version and runtime information</p>
            </div>
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            onClick={onClose}
            aria-label="Close about dialog"
            className="rounded-xl p-2 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="max-h-[calc(90vh-170px)] space-y-5 overflow-y-auto px-6 py-5">
          <section className="panel p-5" aria-labelledby="about-frontend-heading">
            <h3 className="eyebrow" id="about-frontend-heading">
              Application (Frontend)
            </h3>
            <InfoList rows={frontendRows} />
          </section>

          <section className="panel p-5" aria-labelledby="about-backend-heading">
            <h3 className="eyebrow" id="about-backend-heading">
              Server (Backend)
            </h3>
            {isLoading ? (
              <div role="status" className="mt-4 flex items-center gap-2 text-sm text-slate-400">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading server information…
              </div>
            ) : error ? (
              <div role="alert" className="mt-4 flex items-center gap-2 text-sm text-rose-300">
                <AlertCircle className="h-4 w-4" />
                {error}
              </div>
            ) : (
              <InfoList rows={backendRows} />
            )}
          </section>
        </div>

        <div className="flex items-center justify-end border-t border-slate-800 px-6 py-5">
          <button type="button" onClick={onClose} className="button-secondary">
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

export default AboutDialog;
