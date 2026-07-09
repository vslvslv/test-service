import React, { useEffect, useMemo, useState } from 'react';
import {
  Calendar,
  CheckCircle,
  Copy,
  Download,
  Edit,
  Eye,
  EyeOff,
  Package,
  RefreshCw,
  Save,
  X,
  XCircle,
} from 'lucide-react';
import { apiService } from '../services/api';
import type { Entity, Environment, Schema, SchemaField } from '../types';

interface EntityViewDialogProps {
  isOpen: boolean;
  onClose: () => void;
  entity: Entity;
  schema: Schema;
  onReset?: () => void;
  onEdit?: (updatedEntity: { fields: Record<string, unknown>; environment?: string }) => Promise<void> | void;
}

const EntityViewDialog: React.FC<EntityViewDialogProps> = ({
  isOpen,
  onClose,
  entity,
  schema,
  onReset,
  onEdit,
}) => {
  const [isEditing, setIsEditing] = useState(false);
  const [draftFields, setDraftFields] = useState<Record<string, unknown>>({});
  const [draftEnvironment, setDraftEnvironment] = useState('');
  const [availableEnvironments, setAvailableEnvironments] = useState<Environment[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [showRawJson, setShowRawJson] = useState(false);

  useEffect(() => {
    if (!isOpen || !entity) return;
    setIsEditing(false);
    setDraftFields({ ...(entity.fields || {}) });
    setDraftEnvironment(entity.environment || '');
  }, [isOpen, entity]);

  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    const loadEnvironments = async () => {
      try {
        const data = await apiService.getEnvironments();
        if (!cancelled) setAvailableEnvironments(data || []);
      } catch {
        if (!cancelled) setAvailableEnvironments([]);
      }
    };
    loadEnvironments();
    return () => {
      cancelled = true;
    };
  }, [isOpen]);

  const environmentOptions = useMemo(() => {
    const names = availableEnvironments
      .map((env) => env.name)
      .filter((name): name is string => !!name && name.trim().length > 0);
    return Array.from(new Set(draftEnvironment.trim() ? [...names, draftEnvironment] : names));
  }, [availableEnvironments, draftEnvironment]);

  if (!isOpen || !entity) return null;

  const handleCopyField = (value: string) => {
    navigator.clipboard.writeText(value);
  };

  const handleCopyAll = () => {
    navigator.clipboard.writeText(JSON.stringify(entity.fields, null, 2));
  };

  const handleExportJson = () => {
    const dataStr = JSON.stringify(entity, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${entity.entityType}_${entity.id}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const handleFieldChange = (fieldName: string, value: string, fieldType?: string) => {
    let parsedValue: string | number | boolean = value;
    if (fieldType === 'number') {
      parsedValue = value === '' ? '' : Number(value);
    } else if (fieldType === 'boolean') {
      parsedValue = value === 'true';
    } else if (fieldType === 'array' || fieldType === 'object') {
      try {
        parsedValue = value === '' ? '' : JSON.parse(value);
      } catch {
        parsedValue = value;
      }
    }
    setDraftFields((prev) => ({ ...prev, [fieldName]: parsedValue }));
  };

  const handleSave = async () => {
    if (!onEdit) return;
    setIsSaving(true);
    try {
      await onEdit({
        fields: draftFields,
        environment: draftEnvironment || undefined,
      });
      setIsEditing(false);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancelEdit = () => {
    setDraftFields({ ...(entity.fields || {}) });
    setDraftEnvironment(entity.environment || '');
    setIsEditing(false);
  };

  const rawJson = JSON.stringify(
    {
      ...entity,
      fields: isEditing ? draftFields : entity.fields,
      environment: isEditing ? draftEnvironment || undefined : entity.environment,
    },
    null,
    2
  );

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-shell max-h-[90vh] max-w-5xl overflow-hidden" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-slate-800 px-6 py-5">
          <div className="flex items-center gap-3">
            <div className="page-hero-icon !p-2.5">
              <Package className="h-5 w-5 text-blue-300" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-white">{entity.entityType}</h2>
              <p className="mt-1 font-mono text-sm text-slate-400">{entity.id}</p>
            </div>
          </div>
          <button type="button" onClick={onClose} className="rounded-xl p-2 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="max-h-[calc(90vh-170px)] overflow-y-auto px-6 py-5">
          <div className="mb-5 grid gap-4 lg:grid-cols-2 xl:grid-cols-4">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Status</p>
              <div className="mt-3">
                {entity.isConsumed ? (
                  <span className="inline-flex items-center gap-2 text-amber-300">
                    <XCircle className="h-4 w-4" />
                    Consumed
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-2 text-emerald-300">
                    <CheckCircle className="h-4 w-4" />
                    Available
                  </span>
                )}
              </div>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Environment</p>
              <div className="mt-3">
                {isEditing ? (
                  <select value={draftEnvironment} onChange={(e) => setDraftEnvironment(e.target.value)} className="field-shell">
                    <option value="">No environment</option>
                    {environmentOptions.map((envName) => (
                      <option key={envName} value={envName}>{envName}</option>
                    ))}
                  </select>
                ) : (
                  <p className="text-white">{entity.environment || '-'}</p>
                )}
              </div>
            </div>
            {entity.createdAt && (
              <div className="stat-card">
                <p className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.18em] text-slate-500">
                  <Calendar className="h-3.5 w-3.5" />
                  Created
                </p>
                <p className="mt-3 text-sm text-white">{new Date(entity.createdAt).toLocaleString()}</p>
              </div>
            )}
            {entity.updatedAt && (
              <div className="stat-card">
                <p className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.18em] text-slate-500">
                  <Calendar className="h-3.5 w-3.5" />
                  Updated
                </p>
                <p className="mt-3 text-sm text-white">{new Date(entity.updatedAt).toLocaleString()}</p>
              </div>
            )}
          </div>

          <div className="panel p-5">
            <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="eyebrow">Field Values</p>
                <h3 className="mt-2 text-lg font-semibold text-white">Entity payload</h3>
              </div>
              <div className="flex flex-wrap gap-2">
                <button type="button" onClick={handleCopyAll} className="button-secondary">
                  <Copy className="h-4 w-4" />
                  Copy All
                </button>
                <button type="button" onClick={handleExportJson} className="button-secondary">
                  <Download className="h-4 w-4" />
                  Export JSON
                </button>
                <button type="button" onClick={() => setShowRawJson((prev) => !prev)} className="button-secondary">
                  {showRawJson ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  {showRawJson ? 'Hide Raw JSON' : 'Show Raw JSON'}
                </button>
              </div>
            </div>

            <div className="space-y-3">
              {schema?.fields?.map((field: SchemaField) => {
                const value = isEditing ? draftFields[field.name] : entity.fields[field.name];
                const hasValue = value !== undefined && value !== null && value !== '';

                return (
                  <div key={field.name} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                      <div className="min-w-0 flex-1">
                        <div className="mb-2 flex flex-wrap items-center gap-2">
                          <p className="text-sm font-medium text-white">
                            {field.name}
                            {field.required && <span className="ml-1 text-red-300">*</span>}
                          </p>
                          <span className="badge-soft border-blue-500/25 bg-blue-500/10 text-blue-300">{field.type}</span>
                        </div>
                        {field.description && <p className="mb-3 text-xs leading-5 text-slate-500">{field.description}</p>}
                        {isEditing ? (
                          field.type === 'boolean' ? (
                            <select
                              value={String(Boolean(value))}
                              onChange={(e) => handleFieldChange(field.name, e.target.value, field.type)}
                              className="field-shell"
                            >
                              <option value="false">false</option>
                              <option value="true">true</option>
                            </select>
                          ) : (
                            <input
                              type={field.type === 'number' ? 'number' : 'text'}
                              value={typeof value === 'object' ? JSON.stringify(value) : (value ?? '')}
                              onChange={(e) => handleFieldChange(field.name, e.target.value, field.type)}
                              className="field-shell font-mono text-sm"
                            />
                          )
                        ) : hasValue ? (
                          <p className="break-all font-mono text-sm text-slate-200">
                            {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                          </p>
                        ) : (
                          <p className="text-sm italic text-slate-500">
                            {field.defaultValue ? `Default: ${field.defaultValue}` : 'No value'}
                          </p>
                        )}
                      </div>
                      {!isEditing && hasValue && (
                        <button
                          type="button"
                          onClick={() => handleCopyField(typeof value === 'object' ? JSON.stringify(value) : String(value))}
                          className="rounded-full border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400 transition-colors hover:text-white"
                          title="Copy value"
                        >
                          <Copy className="h-4 w-4" />
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>

            {showRawJson && (
              <div className="mt-4 rounded-[24px] border border-slate-800 bg-slate-950/60 p-4">
                <pre className="overflow-x-auto text-xs text-slate-300">{rawJson}</pre>
              </div>
            )}
          </div>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-800 px-6 py-5">
          <div className="flex gap-2">
            {entity.isConsumed && schema?.excludeOnFetch && onReset && (
              <button type="button" onClick={onReset} className="button-secondary">
                <RefreshCw className="h-4 w-4" />
                Reset
              </button>
            )}
          </div>
          <div className="flex flex-wrap gap-3">
            {onEdit && !isEditing && (
              <button type="button" onClick={() => setIsEditing(true)} className="button-primary">
                <Edit className="h-4 w-4" />
                Edit
              </button>
            )}
            {onEdit && isEditing && (
              <>
                <button type="button" onClick={handleCancelEdit} disabled={isSaving} className="button-secondary disabled:cursor-not-allowed disabled:opacity-60">
                  Cancel
                </button>
                <button type="button" onClick={handleSave} disabled={isSaving} className="button-primary disabled:cursor-not-allowed disabled:opacity-60">
                  <Save className="h-4 w-4" />
                  {isSaving ? 'Saving...' : 'Save'}
                </button>
              </>
            )}
            <button type="button" onClick={onClose} className="button-secondary">
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default EntityViewDialog;
