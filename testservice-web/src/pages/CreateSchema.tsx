import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Plus, 
  Save,
  ArrowLeft,
  AlertCircle,
  Trash2,
  GripVertical
} from 'lucide-react';
import { apiService } from '../services/api';

interface SchemaField {
  name: string;
  type: string;
  required: boolean;
  defaultValue?: string;
}

const fieldTypes = [
  { value: 'string', label: 'String (Text)' },
  { value: 'number', label: 'Number' },
  { value: 'boolean', label: 'Boolean (True/False)' },
  { value: 'date', label: 'Date' },
  { value: 'array', label: 'Array (List)' },
  { value: 'object', label: 'Object (JSON)' },
];

const CreateSchema: React.FC = () => {
  const navigate = useNavigate();
  const [schemaName, setSchemaName] = useState('');
  const [excludeOnFetch, setExcludeOnFetch] = useState(false);
  const [fields, setFields] = useState<SchemaField[]>([
    { name: '', type: 'string', required: false, defaultValue: '' }
  ]);
  const [error, setError] = useState('');
  const [isCreating, setIsCreating] = useState(false);

  const addField = () => {
    setFields([...fields, { name: '', type: 'string', required: false, defaultValue: '' }]);
  };

  const removeField = (index: number) => {
    if (fields.length === 1) {
      alert('Schema must have at least one field');
      return;
    }
    setFields(fields.filter((_, i) => i !== index));
  };

  const updateField = (index: number, field: Partial<SchemaField>) => {
    const newFields = [...fields];
    newFields[index] = { ...newFields[index], ...field };
    setFields(newFields);
  };

  const validateForm = (): string | null => {
    if (!schemaName.trim()) {
      return 'Schema name is required';
    }

    if (!/^[a-zA-Z0-9-_]+$/.test(schemaName)) {
      return 'Schema name can only contain letters, numbers, hyphens, and underscores';
    }

    if (fields.length === 0) {
      return 'At least one field is required';
    }

    for (let i = 0; i < fields.length; i++) {
      if (!fields[i].name.trim()) {
        return `Field ${i + 1}: Field name is required`;
      }
      if (!/^[a-zA-Z0-9_]+$/.test(fields[i].name)) {
        return `Field ${i + 1}: Field name can only contain letters, numbers, and underscores`;
      }
    }

    // Check for duplicate field names
    const fieldNames = fields.map(f => f.name.toLowerCase());
    const duplicates = fieldNames.filter((name, index) => fieldNames.indexOf(name) !== index);
    if (duplicates.length > 0) {
      return `Duplicate field name: ${duplicates[0]}`;
    }

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
      // Prepare schema data
      const schemaData = {
        entityName: schemaName.trim(),
        fields: fields.map(f => ({
          name: f.name.trim(),
          type: f.type,
          required: f.required,
          ...(f.defaultValue && { defaultValue: f.defaultValue })
        })),
        excludeOnFetch
      };

      await apiService.createSchema(schemaData);
      
      // Navigate to schemas list on success
      navigate('/schemas');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to create schema');
      console.error('Failed to create schema:', err);
    } finally {
      setIsCreating(false);
    }
  };

  const handleCancel = () => {
    if (schemaName || fields.some(f => f.name)) {
      if (confirm('Are you sure you want to cancel? All changes will be lost.')) {
        navigate('/schemas');
      }
    } else {
      navigate('/schemas');
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <button
            onClick={handleCancel}
            className="p-2 hover:bg-gray-700 rounded-lg transition-colors"
            title="Back to schemas"
          >
            <ArrowLeft className="w-6 h-6 text-gray-400 hover:text-white" />
          </button>
          <div>
            <h1 className="text-2xl font-bold text-white">Create New Schema</h1>
            <p className="text-gray-400 mt-1">Define a new entity type with custom fields</p>
          </div>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/50 rounded-lg flex items-center gap-2 text-red-400">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Form */}
      <form onSubmit={handleSubmit} className="space-y-6">
        {/* Schema Details Card */}
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-6">
          <h2 className="text-lg font-semibold text-white mb-4">Schema Details</h2>
          
          <div className="space-y-4">
            {/* Schema Name */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                Schema Name *
              </label>
              <input
                type="text"
                value={schemaName}
                onChange={(e) => setSchemaName(e.target.value)}
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="e.g., test-agent, user-account"
                required
              />
              <p className="mt-1 text-xs text-gray-400">
                Use lowercase letters, numbers, hyphens, and underscores only
              </p>
            </div>

            {/* Exclude On Fetch */}
            <div className="flex items-start gap-3">
              <input
                type="checkbox"
                id="excludeOnFetch"
                checked={excludeOnFetch}
                onChange={(e) => setExcludeOnFetch(e.target.checked)}
                className="mt-1 w-4 h-4 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-blue-500"
              />
              <div className="flex-1">
                <label htmlFor="excludeOnFetch" className="text-sm font-medium text-gray-300 cursor-pointer">
                  Auto-consume on fetch
                </label>
                <p className="mt-1 text-xs text-gray-400">
                  Automatically mark entities as consumed when fetched. Useful for test data that should only be used once.
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Fields Card */}
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Fields</h2>
            <button
              type="button"
              onClick={addField}
              className="flex items-center gap-2 px-3 py-1.5 bg-blue-600 hover:bg-blue-700 text-white text-sm rounded-lg transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Field
            </button>
          </div>

          <div className="space-y-3">
            {fields.map((field, index) => (
              <div
                key={index}
                className="bg-gray-700/50 border border-gray-600 rounded-lg p-4"
              >
                <div className="flex items-start gap-3">
                  {/* Drag Handle */}
                  <div className="pt-2 cursor-move opacity-50 hover:opacity-100">
                    <GripVertical className="w-5 h-5 text-gray-400" />
                  </div>

                  {/* Field Configuration */}
                  <div className="flex-1 grid grid-cols-1 md:grid-cols-2 gap-3">
                    {/* Field Name */}
                    <div>
                      <label className="block text-xs font-medium text-gray-400 mb-1">
                        Field Name *
                      </label>
                      <input
                        type="text"
                        value={field.name}
                        onChange={(e) => updateField(index, { name: e.target.value })}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        placeholder="e.g., username, email"
                        required
                      />
                    </div>

                    {/* Field Type */}
                    <div>
                      <label className="block text-xs font-medium text-gray-400 mb-1">
                        Field Type *
                      </label>
                      <select
                        value={field.type}
                        onChange={(e) => updateField(index, { type: e.target.value })}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                      >
                        {fieldTypes.map(type => (
                          <option key={type.value} value={type.value}>
                            {type.label}
                          </option>
                        ))}
                      </select>
                    </div>

                    {/* Default Value */}
                    <div>
                      <label className="block text-xs font-medium text-gray-400 mb-1">
                        Default Value (Optional)
                      </label>
                      <input
                        type="text"
                        value={field.defaultValue || ''}
                        onChange={(e) => updateField(index, { defaultValue: e.target.value })}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                        placeholder="Optional default value"
                      />
                    </div>

                    {/* Required Checkbox */}
                    <div className="flex items-center">
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={field.required}
                          onChange={(e) => updateField(index, { required: e.target.checked })}
                          className="w-4 h-4 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-blue-500"
                        />
                        <span className="text-sm text-gray-300">Required field</span>
                      </label>
                    </div>
                  </div>

                  {/* Delete Button */}
                  <button
                    type="button"
                    onClick={() => removeField(index)}
                    className="p-2 hover:bg-red-500/10 rounded-lg transition-colors mt-5"
                    title="Remove field"
                  >
                    <Trash2 className="w-4 h-4 text-gray-400 hover:text-red-400" />
                  </button>
                </div>
              </div>
            ))}
          </div>

          {fields.length === 0 && (
            <div className="text-center py-8 text-gray-500">
              <p className="mb-3">No fields added yet</p>
              <button
                type="button"
                onClick={addField}
                className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
              >
                <Plus className="w-4 h-4" />
                Add First Field
              </button>
            </div>
          )}
        </div>

        {/* Action Buttons */}
        <div className="flex gap-3 justify-end sticky bottom-6 bg-gray-900 p-4 rounded-lg border border-gray-700">
          <button
            type="button"
            onClick={handleCancel}
            className="px-6 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors font-medium"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isCreating}
            className="flex items-center gap-2 px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Save className="w-4 h-4" />
            {isCreating ? 'Creating...' : 'Create Schema'}
          </button>
        </div>
      </form>

      {/* Help Section */}
      <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-4">
        <h3 className="text-sm font-semibold text-blue-400 mb-2">?? Tips</h3>
        <ul className="space-y-1 text-xs text-blue-300">
          <li>• Schema names should be descriptive and follow a naming convention (e.g., kebab-case)</li>
          <li>• Required fields must be provided when creating entities</li>
          <li>• Default values are used when a field is not specified during entity creation</li>
          <li>• Auto-consume is useful for test data that should only be used once (e.g., unique accounts)</li>
        </ul>
      </div>
    </div>
  );
};

export default CreateSchema;
