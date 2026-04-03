import React, { useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  CheckCircle,
  Loader,
  Package,
  Plus,
  X,
} from 'lucide-react';
import { apiService } from '../services/api';
import type { Environment } from '../types';

interface EntityCreateDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  schema: any;
  entityType: string;
}

const EntityCreateDialog: React.FC<EntityCreateDialogProps> = ({
  isOpen,
  onClose,
  onSuccess,
  schema,
  entityType,
}) => {
  const [formData, setFormData] = useState<Record<string, any>>({});
  const [environment, setEnvironment] = useState('');
  const [availableEnvironments, setAvailableEnvironments] = useState<Environment[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');

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
    return Array.from(new Set(environment.trim() ? [...names, environment] : names));
  }, [availableEnvironments, environment]);

  if (!isOpen || !schema) return null;

  const coerceFieldValue = (field: any, rawValue: string) => {
    if (rawValue === '') return '';

    switch (field.type) {
      case 'number': {
        const parsed = Number(rawValue);
        return Number.isNaN(parsed) ? rawValue : parsed;
      }
      case 'boolean':
        return rawValue === 'true';
      default:
        return rawValue;
    }
  };

  const handleFieldChange = (fieldName: string, value: any) => {
    setFormData((prev) => ({ ...prev, [fieldName]: value }));
    if (errors[fieldName]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[fieldName];
        return next;
      });
    }
  };

  const validateForm = () => {
    const nextErrors: Record<string, string> = {};
    schema.fields.forEach((field: any) => {
      if (field.required) {
        const value = formData[field.name];
        if (value === undefined || value === null || value === '') {
          nextErrors[field.name] = 'This field is required';
        }
      }
    });
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  };

  const resetState = () => {
    setFormData({});
    setEnvironment('');
    setErrors({});
    setSubmitError('');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitError('');

    if (!validateForm()) return;

    setIsSubmitting(true);
    try {
      const entityData: Record<string, any> = { fields: formData };
      if (environment) {
        entityData.environment = environment;
      }

      await apiService.createEntity(entityType, entityData);
      resetState();
      onSuccess();
      onClose();
    } catch (err: any) {
      setSubmitError(err.response?.data?.message || 'Failed to create entity');
      console.error('Failed to create entity:', err);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancel = () => {
    if (Object.keys(formData).length > 0 || environment) {
      if (!confirm('Are you sure you want to cancel? All entered data will be lost.')) {
        return;
      }
    }
    resetState();
    onClose();
  };

  const getInputType = (fieldType: string) => {
    switch (fieldType) {
      case 'number':
        return 'number';
      case 'date':
        return 'date';
      case 'datetime':
        return 'datetime-local';
      case 'boolean':
        return 'checkbox';
      default:
        return 'text';
    }
  };

  const renderField = (field: any) => {
    const value = formData[field.name] ?? field.defaultValue ?? '';
    const hasError = !!errors[field.name];

    if (field.type === 'boolean') {
      return (
        <div key={field.name} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <label className="flex cursor-pointer items-start gap-3">
            <input
              type="checkbox"
              checked={!!formData[field.name]}
              onChange={(e) => handleFieldChange(field.name, e.target.checked)}
              className="mt-1 h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
            />
            <div className="flex-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium text-white">
                  {field.name}
                  {field.required && <span className="ml-1 text-red-300">*</span>}
                </span>
                <span className="badge-soft border-violet-500/25 bg-violet-500/10 text-violet-300">{field.type}</span>
              </div>
              {field.description && <p className="mt-2 text-xs leading-5 text-slate-500">{field.description}</p>}
            </div>
          </label>
          {hasError && (
            <p className="mt-3 flex items-center gap-1 text-xs text-red-300">
              <AlertCircle className="h-3.5 w-3.5" />
              {errors[field.name]}
            </p>
          )}
        </div>
      );
    }

    if (field.type === 'array' || field.type === 'object') {
      return (
        <div key={field.name} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <span className="text-sm font-medium text-white">
              {field.name}
              {field.required && <span className="ml-1 text-red-300">*</span>}
            </span>
            <span className="badge-soft border-amber-500/25 bg-amber-500/10 text-amber-300">{field.type}</span>
          </div>
          {field.description && <p className="mb-3 text-xs leading-5 text-slate-500">{field.description}</p>}
          <textarea
            value={typeof value === 'string' ? value : JSON.stringify(value, null, 2)}
            onChange={(e) => {
              try {
                handleFieldChange(field.name, JSON.parse(e.target.value));
              } catch {
                handleFieldChange(field.name, e.target.value);
              }
            }}
            placeholder={`Enter JSON ${field.type}`}
            rows={5}
            className={`field-shell min-h-[140px] font-mono text-sm ${hasError ? '!border-red-500' : ''}`}
          />
          {hasError && (
            <p className="mt-3 flex items-center gap-1 text-xs text-red-300">
              <AlertCircle className="h-3.5 w-3.5" />
              {errors[field.name]}
            </p>
          )}
        </div>
      );
    }

    return (
      <div key={field.name} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium text-white">
            {field.name}
            {field.required && <span className="ml-1 text-red-300">*</span>}
          </span>
          <span className={`badge-soft ${
            field.type === 'string'
              ? 'border-blue-500/25 bg-blue-500/10 text-blue-300'
              : field.type === 'number'
                ? 'border-emerald-500/25 bg-emerald-500/10 text-emerald-300'
                : field.type === 'date'
                  ? 'border-amber-500/25 bg-amber-500/10 text-amber-300'
                  : 'border-slate-700 bg-slate-900/70 text-slate-300'
          }`}>
            {field.type}
          </span>
        </div>
        {field.description && <p className="mb-3 text-xs leading-5 text-slate-500">{field.description}</p>}
        <input
          type={getInputType(field.type)}
          value={value}
          onChange={(e) => handleFieldChange(field.name, coerceFieldValue(field, e.target.value))}
          placeholder={field.defaultValue ? `Default: ${field.defaultValue}` : `Enter ${field.name}`}
          className={`field-shell ${hasError ? '!border-red-500' : ''}`}
        />
        {hasError && (
          <p className="mt-3 flex items-center gap-1 text-xs text-red-300">
            <AlertCircle className="h-3.5 w-3.5" />
            {errors[field.name]}
          </p>
        )}
      </div>
    );
  };

  return (
    <div className="modal-backdrop" onClick={handleCancel}>
      <div className="modal-shell max-h-[90vh] max-w-4xl overflow-hidden" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-slate-800 px-6 py-5">
          <div className="flex items-center gap-3">
            <div className="page-hero-icon !p-2.5">
              <Plus className="h-5 w-5 text-blue-300" />
            </div>
            <div>
              <h2 className="text-xl font-semibold text-white">Create {entityType}</h2>
              <p className="mt-1 text-sm text-slate-400">Define field values and assign an optional environment before adding this record.</p>
            </div>
          </div>
          <button type="button" onClick={handleCancel} className="rounded-xl p-2 text-slate-400 transition-colors hover:bg-slate-800 hover:text-white" disabled={isSubmitting}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="max-h-[calc(90vh-170px)] overflow-y-auto px-6 py-5">
            {submitError && (
              <div className="mb-5 rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
                <div className="flex items-center gap-2">
                  <AlertCircle className="h-4 w-4" />
                  <span>{submitError}</span>
                </div>
              </div>
            )}

            <div className="mb-5 rounded-[24px] border border-slate-800 bg-slate-950/35 p-5">
              <div className="mb-3 flex items-center gap-2">
                <Package className="h-4 w-4 text-slate-400" />
                <p className="text-sm font-medium text-white">Allocation context</p>
              </div>
              <label className="block text-sm text-slate-300">
                Environment
                <select value={environment} onChange={(e) => setEnvironment(e.target.value)} className="field-shell mt-2">
                  <option value="">No environment</option>
                  {environmentOptions.map((envName) => (
                    <option key={envName} value={envName}>{envName}</option>
                  ))}
                </select>
                <span className="mt-2 block text-xs text-slate-500">Attach the entity to a target environment if this pool is segmented.</span>
              </label>
            </div>

            <div>
              <div className="mb-4 flex items-center gap-2">
                <Package className="h-5 w-5 text-slate-400" />
                <h3 className="text-lg font-semibold text-white">Entity fields</h3>
              </div>
              <div className="space-y-4">
                {schema.fields.map((field: any) => renderField(field))}
              </div>
            </div>

            <div className="mt-5 rounded-[24px] border border-blue-500/25 bg-blue-500/10 p-4 text-sm leading-6 text-blue-100/80">
              Fields marked with <span className="text-red-300">*</span> are required.
              {schema.excludeOnFetch && ' This schema uses auto-consume, so created records will participate in one-time allocation flows.'}
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 border-t border-slate-800 px-6 py-5">
            <button type="button" onClick={handleCancel} disabled={isSubmitting} className="button-secondary disabled:cursor-not-allowed disabled:opacity-60">
              Cancel
            </button>
            <button type="submit" disabled={isSubmitting} className="button-primary disabled:cursor-not-allowed disabled:opacity-60">
              {isSubmitting ? (
                <>
                  <Loader className="h-4 w-4 animate-spin" />
                  Creating...
                </>
              ) : (
                <>
                  <CheckCircle className="h-4 w-4" />
                  Create Entity
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default EntityCreateDialog;
