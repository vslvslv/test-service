import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  AlertCircle,
  ArrowLeft,
  GripVertical,
  Layers,
  Plus,
  Save,
  Trash2
} from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage } from '../types';

interface SchemaField {
  id: string;
  name: string;
  type: string;
  required: boolean;
  isUnique: boolean;
  defaultValue?: string;
}

const fieldTypes = [
  { value: 'string', label: 'String' },
  { value: 'number', label: 'Number' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'date', label: 'Date' },
  { value: 'array', label: 'Array' },
  { value: 'object', label: 'Object' }
];

const newFieldId = (): string =>
  typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
    ? crypto.randomUUID()
    : `field-${Math.random().toString(36).slice(2)}-${Date.now()}`;

const emptyField = (): SchemaField => ({
  id: newFieldId(),
  name: '',
  type: 'string',
  required: false,
  isUnique: false,
  defaultValue: ''
});

const CreateSchema: React.FC = () => {
  const navigate = useNavigate();
  const [schemaName, setSchemaName] = useState('');
  const [excludeOnFetch, setExcludeOnFetch] = useState(false);
  const [fields, setFields] = useState<SchemaField[]>([emptyField()]);
  const [error, setError] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  const updateField = (index: number, field: Partial<SchemaField>) => {
    setFields((current) => current.map((item, itemIndex) => (itemIndex === index ? { ...item, ...field } : item)));
  };

  const addField = () => {
    setFields((current) => [...current, emptyField()]);
  };

  const removeField = (index: number) => {
    if (fields.length === 1) {
      alert('Schema must have at least one field');
      return;
    }
    setFields((current) => current.filter((_, itemIndex) => itemIndex !== index));
  };

  const validateForm = (): string | null => {
    if (!schemaName.trim()) return 'Schema name is required';
    if (!/^[a-zA-Z0-9-_]+$/.test(schemaName)) {
      return 'Schema name can only contain letters, numbers, hyphens, and underscores';
    }
    if (fields.length === 0) return 'At least one field is required';

    for (let index = 0; index < fields.length; index += 1) {
      const field = fields[index];
      if (!field.name.trim()) return `Field ${index + 1}: Field name is required`;
      if (!/^[a-zA-Z0-9_]+$/.test(field.name)) {
        return `Field ${index + 1}: Field name can only contain letters, numbers, and underscores`;
      }
    }

    const names = fields.map((field) => field.name.toLowerCase());
    const duplicate = names.find((name, index) => names.indexOf(name) !== index);
    if (duplicate) return `Duplicate field name: ${duplicate}`;

    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const validationError = validateForm();
    if (validationError) {
      setError(validationError);
      return;
    }

    setIsCreating(true);
    try {
      await apiService.createSchema({
        entityName: schemaName.trim(),
        fields: fields.map((field) => ({
          name: field.name.trim(),
          type: field.type,
          required: field.required,
          isUnique: field.isUnique,
          ...(field.defaultValue ? { defaultValue: field.defaultValue } : {})
        })),
        excludeOnFetch
      });
      navigate('/schemas');
    } catch (err: unknown) {
      setError(getErrorMessage(err));
      console.error('Failed to create schema:', err);
    } finally {
      setIsCreating(false);
    }
  };

  const handleCancel = () => {
    if (schemaName || fields.some((field) => field.name || field.defaultValue)) {
      if (!confirm('Are you sure you want to cancel? All changes will be lost.')) {
        return;
      }
    }
    navigate('/schemas');
  };

  return (
    <div className="app-page">
      <section className="page-hero">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="max-w-3xl">
            <div className="inline-flex items-center gap-3">
              <button
                type="button"
                onClick={handleCancel}
                className="page-hero-icon text-slate-300 transition-colors hover:text-white"
                title="Back to schemas"
              >
                <ArrowLeft className="h-5 w-5" />
              </button>
              <div>
                <p className="eyebrow">Create Schema</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">Design a new entity contract</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Define field shape, validation intent, and auto-consume behavior before the schema starts receiving data.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-3 lg:min-w-[440px]">
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Fields</p>
              <p className="mt-3 text-3xl font-semibold text-white">{fields.length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Required</p>
              <p className="mt-3 text-3xl font-semibold text-white">{fields.filter((field) => field.required).length}</p>
            </div>
            <div className="stat-card">
              <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Unique</p>
              <p className="mt-3 text-3xl font-semibold text-white">{fields.filter((field) => field.isUnique).length}</p>
            </div>
          </div>
        </div>
      </section>

      {error && (
        <div className="rounded-2xl border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          <div className="flex items-center gap-2">
            <AlertCircle className="h-4 w-4" />
            <span>{error}</span>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="panel p-5">
          <div className="mb-5">
            <p className="eyebrow">Definition</p>
            <h2 className="mt-2 text-xl font-semibold text-white">Schema details</h2>
          </div>

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
            <div>
              <label className="mb-2 block text-sm font-medium text-slate-300">Schema name</label>
              <input
                type="text"
                value={schemaName}
                onChange={(e) => setSchemaName(e.target.value)}
                className="field-shell"
                placeholder="e.g. user-account, test-agent"
                required
              />
              <p className="mt-2 text-xs text-slate-500">Use letters, numbers, hyphens, and underscores only.</p>
            </div>

            <div className="rounded-2xl border border-slate-800 bg-slate-950/35 p-4">
              <div className="flex items-start gap-3">
                <input
                  id="excludeOnFetch"
                  type="checkbox"
                  checked={excludeOnFetch}
                  onChange={(e) => setExcludeOnFetch(e.target.checked)}
                  className="mt-1 h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                />
                <div>
                  <label htmlFor="excludeOnFetch" className="text-sm font-medium text-white">
                    Auto-consume on fetch
                  </label>
                  <p className="mt-2 text-sm leading-6 text-slate-400">
                    Mark entities as consumed immediately after retrieval. Use this for one-time credentials or single-use test records.
                  </p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="panel-strong p-5">
          <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="eyebrow">Fields</p>
              <h2 className="mt-2 text-xl font-semibold text-white">Structure definition</h2>
            </div>
            <button type="button" onClick={addField} className="button-secondary">
              <Plus className="h-4 w-4" />
              Add Field
            </button>
          </div>

          <div className="space-y-4">
            {fields.map((field, index) => (
              <div key={field.id} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
                <div className="mb-4 flex items-center justify-between">
                  <div className="inline-flex items-center gap-3">
                    <div className="rounded-2xl border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400">
                      <GripVertical className="h-4 w-4" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-white">Field {index + 1}</p>
                      <p className="text-xs text-slate-500">Configure name, type, and validation rules.</p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => removeField(index)}
                    className="rounded-full border border-red-500/20 bg-red-500/10 p-2 text-red-200 transition-colors hover:bg-red-500/15"
                    title="Remove field"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>

                <div className="grid gap-4 xl:grid-cols-2">
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-300">Field name</label>
                    <input
                      type="text"
                      value={field.name}
                      onChange={(e) => updateField(index, { name: e.target.value })}
                      className="field-shell"
                      placeholder="e.g. username"
                      required
                    />
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-300">Field type</label>
                    <select
                      aria-label="Field type"
                      value={field.type}
                      onChange={(e) => updateField(index, { type: e.target.value })}
                      className="field-shell"
                    >
                      {fieldTypes.map((type) => (
                        <option key={type.value} value={type.value}>
                          {type.label}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-300">Default value</label>
                    <input
                      type="text"
                      value={field.defaultValue || ''}
                      onChange={(e) => updateField(index, { defaultValue: e.target.value })}
                      className="field-shell"
                      placeholder="Optional default value"
                    />
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <label className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-4 py-3 text-sm text-slate-300">
                      <input
                        type="checkbox"
                        checked={field.required}
                        onChange={(e) => updateField(index, { required: e.target.checked })}
                        className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                      />
                      <span>Required field</span>
                    </label>
                    <label className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-4 py-3 text-sm text-slate-300">
                      <input
                        type="checkbox"
                        checked={field.isUnique}
                        onChange={(e) => updateField(index, { isUnique: e.target.checked })}
                        className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                      />
                      <span>Unique field</span>
                    </label>
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="mt-6 rounded-[24px] border border-blue-500/20 bg-blue-500/10 p-5">
            <div className="inline-flex items-center gap-3">
              <div className="rounded-2xl border border-blue-500/20 bg-slate-950/40 p-3">
                <Layers className="h-5 w-5 text-blue-300" />
              </div>
              <div>
                <h3 className="text-base font-semibold text-white">Schema design guidance</h3>
                <p className="mt-1 text-sm text-blue-100/80">
                  Prefer stable field names, reserve unique constraints for true business identifiers, and enable auto-consume only where reuse creates risk.
                </p>
              </div>
            </div>
          </div>
        </section>

        <div className="sticky bottom-6 flex flex-wrap justify-end gap-3 rounded-[24px] border border-slate-800 bg-slate-950/90 p-4 backdrop-blur">
          <button type="button" onClick={handleCancel} className="button-secondary">
            Cancel
          </button>
          <button type="submit" disabled={isCreating} className="button-primary disabled:cursor-not-allowed disabled:opacity-60">
            <Save className="h-4 w-4" />
            {isCreating ? 'Creating...' : 'Create Schema'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default CreateSchema;
