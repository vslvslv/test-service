import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  ArrowLeft,
  GripVertical,
  Layers,
  Loader,
  Plus,
  Save,
  Trash2
} from 'lucide-react';
import { apiService } from '../services/api';

interface SchemaField {
  name: string;
  type: string;
  required: boolean;
  defaultValue?: string;
  description?: string;
}

interface Schema {
  id?: string;
  entityName: string;
  fields: SchemaField[];
  filterableFields?: string[];
  excludeOnFetch: boolean;
  createdAt?: string;
  updatedAt?: string;
}

const fieldTypes = [
  { value: 'string', label: 'String' },
  { value: 'number', label: 'Number' },
  { value: 'boolean', label: 'Boolean' },
  { value: 'date', label: 'Date' },
  { value: 'datetime', label: 'DateTime' },
  { value: 'array', label: 'Array' },
  { value: 'object', label: 'Object' }
];

const emptyField = (): SchemaField => ({
  name: '',
  type: 'string',
  required: false,
  defaultValue: '',
  description: ''
});

const EditSchema: React.FC = () => {
  const navigate = useNavigate();
  const { name } = useParams<{ name: string }>();
  const [schema, setSchema] = useState<Schema | null>(null);
  const [excludeOnFetch, setExcludeOnFetch] = useState(false);
  const [fields, setFields] = useState<SchemaField[]>([]);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (name) {
      loadSchema(name);
    }
  }, [name]);

  const loadSchema = async (schemaName: string) => {
    setIsLoading(true);
    setError('');
    try {
      const data = await apiService.getSchema(schemaName);
      setSchema(data);
      setExcludeOnFetch(data.excludeOnFetch || false);
      const loadedFields = (data.fields || []).map((field: SchemaField) => ({
        name: field.name || '',
        type: field.type || 'string',
        required: !!field.required,
        defaultValue: field.defaultValue ?? '',
        description: field.description ?? ''
      }));
      setFields(loadedFields.length > 0 ? loadedFields : [emptyField()]);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to load schema');
      console.error('Failed to load schema:', err);
    } finally {
      setIsLoading(false);
    }
  };

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
    if (fields.length === 0) return 'At least one field is required';

    for (let index = 0; index < fields.length; index += 1) {
      const field = fields[index];
      if (!field.name.trim()) return `Field ${index + 1}: Field name is required`;
      if (!/^[a-zA-Z0-9_]+$/.test(field.name)) {
        return `Field ${index + 1}: Field name can only contain letters, numbers, and underscores`;
      }
    }

    const names = fields.map((field) => field.name.toLowerCase());
    const duplicate = names.find((fieldName, index) => names.indexOf(fieldName) !== index);
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

    setIsSaving(true);
    try {
      await apiService.updateSchema(schema!.entityName, {
        ...schema,
        entityName: schema!.entityName,
        fields: fields.map((field) => ({
          name: field.name.trim(),
          type: field.type,
          required: field.required,
          ...(field.defaultValue ? { defaultValue: field.defaultValue } : {}),
          ...(field.description ? { description: field.description } : {})
        })),
        excludeOnFetch
      });
      navigate('/schemas');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to update schema');
      console.error('Failed to update schema:', err);
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm(`Are you sure you want to delete the schema "${schema?.entityName}"?\n\nThis will also delete all entities of this type.`)) {
      return;
    }

    try {
      await apiService.deleteSchema(schema!.entityName);
      navigate('/schemas');
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to delete schema');
    }
  };

  const handleCancel = () => {
    if (!confirm('Are you sure you want to cancel? All unsaved changes will be lost.')) {
      return;
    }
    navigate('/schemas');
  };

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <Loader className="h-12 w-12 animate-spin text-blue-500" />
          <p className="text-slate-400">Loading schema...</p>
        </div>
      </div>
    );
  }

  if (!schema) {
    return (
      <div className="panel p-12 text-center">
        <AlertCircle className="mx-auto h-14 w-14 text-red-400" />
        <h2 className="mt-4 text-xl font-semibold text-white">Schema not found</h2>
        <p className="mt-2 text-sm text-slate-400">The schema "{name}" could not be found.</p>
        <div className="mt-6">
          <button type="button" onClick={() => navigate('/schemas')} className="button-primary">
            Back to Schemas
          </button>
        </div>
      </div>
    );
  }

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
                <p className="eyebrow">Edit Schema</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">{schema.entityName}</h1>
              </div>
            </div>
            <p className="mt-4 max-w-2xl text-sm leading-6 text-slate-300">
              Update the active field contract carefully. Existing entities may already depend on these names and types.
            </p>
          </div>

          <div className="flex flex-wrap gap-3">
            <button type="button" onClick={handleDelete} className="inline-flex items-center justify-center gap-2 rounded-full border border-red-500/30 bg-red-500/10 px-4 py-2 text-sm text-red-200 transition-colors hover:bg-red-500/15">
              <Trash2 className="h-4 w-4" />
              Delete Schema
            </button>
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
            <h2 className="mt-2 text-xl font-semibold text-white">Schema metadata</h2>
          </div>

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1.1fr)_minmax(340px,0.9fr)]">
            <div className="space-y-4">
              <div>
                <label className="mb-2 block text-sm font-medium text-slate-300">Schema name</label>
                <input type="text" value={schema.entityName} disabled className="field-shell cursor-not-allowed opacity-70" />
                <p className="mt-2 text-xs text-slate-500">Schema name is immutable after creation.</p>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Created</p>
                  <p className="mt-2 text-sm text-white">{schema.createdAt ? new Date(schema.createdAt).toLocaleString() : 'Unknown'}</p>
                </div>
                <div className="rounded-2xl border border-slate-800 bg-slate-950/35 px-4 py-4">
                  <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Last Updated</p>
                  <p className="mt-2 text-sm text-white">{schema.updatedAt ? new Date(schema.updatedAt).toLocaleString() : 'Unknown'}</p>
                </div>
              </div>
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
                    Keep this enabled only for records that should be consumed once. Changing it affects how future allocations behave.
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
              <h2 className="mt-2 text-xl font-semibold text-white">Contract fields</h2>
            </div>
            <button type="button" onClick={addField} className="button-secondary">
              <Plus className="h-4 w-4" />
              Add Field
            </button>
          </div>

          <div className="space-y-4">
            {fields.map((field, index) => (
              <div key={`${field.name}-${index}`} className="rounded-[24px] border border-slate-800 bg-slate-950/35 p-4">
                <div className="mb-4 flex items-center justify-between">
                  <div className="inline-flex items-center gap-3">
                    <div className="rounded-2xl border border-slate-700/70 bg-slate-900/70 p-2 text-slate-400">
                      <GripVertical className="h-4 w-4" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-white">Field {index + 1}</p>
                      <p className="text-xs text-slate-500">Adjust structure, helper text, and validation.</p>
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
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-300">Description</label>
                    <input
                      type="text"
                      value={field.description || ''}
                      onChange={(e) => updateField(index, { description: e.target.value })}
                      className="field-shell"
                      placeholder="Optional field description"
                    />
                  </div>
                  <div className="xl:col-span-2">
                    <label className="flex items-center gap-3 rounded-2xl border border-slate-800 bg-slate-900/60 px-4 py-3 text-sm text-slate-300">
                      <input
                        type="checkbox"
                        checked={field.required}
                        onChange={(e) => updateField(index, { required: e.target.checked })}
                        className="h-4 w-4 rounded border-slate-600 bg-slate-800 text-blue-500 focus:ring-blue-500"
                      />
                      <span>Required field</span>
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
                <h3 className="text-base font-semibold text-white">Change management note</h3>
                <p className="mt-1 text-sm text-blue-100/80">
                  Renaming or removing fields can break imports, downstream consumers, or entity validation. Favor additive changes where possible.
                </p>
              </div>
            </div>
          </div>
        </section>

        <div className="sticky bottom-6 flex flex-wrap justify-end gap-3 rounded-[24px] border border-slate-800 bg-slate-950/90 p-4 backdrop-blur">
          <button type="button" onClick={handleCancel} className="button-secondary">
            Cancel
          </button>
          <button type="submit" disabled={isSaving} className="button-primary disabled:cursor-not-allowed disabled:opacity-60">
            <Save className="h-4 w-4" />
            {isSaving ? 'Saving...' : 'Save Changes'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default EditSchema;
