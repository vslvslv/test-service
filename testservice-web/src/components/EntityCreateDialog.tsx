import React, { useState } from 'react';
import { 
  X,
  Plus,
  AlertCircle,
  Loader,
  Package,
  CheckCircle
} from 'lucide-react';

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
  entityType
}) => {
  const [formData, setFormData] = useState<Record<string, any>>({});
  const [environment, setEnvironment] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');

  if (!isOpen || !schema) return null;

  const handleFieldChange = (fieldName: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [fieldName]: value
    }));
    
    // Clear error for this field
    if (errors[fieldName]) {
      setErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[fieldName];
        return newErrors;
      });
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};
    
    // Check required fields
    schema.fields.forEach((field: any) => {
      if (field.required) {
        const value = formData[field.name];
        if (value === undefined || value === null || value === '') {
          newErrors[field.name] = 'This field is required';
        }
      }
    });

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitError('');

    if (!validateForm()) {
      return;
    }

    setIsSubmitting(true);

    try {
      const { apiService } = await import('../services/api');
      
      const entityData: any = {
        fields: formData
      };

      if (environment) {
        entityData.environment = environment;
      }

      await apiService.createEntity(entityType, entityData);
      
      // Reset form
      setFormData({});
      setEnvironment('');
      setErrors({});
      
      // Notify success
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
    
    setFormData({});
    setEnvironment('');
    setErrors({});
    setSubmitError('');
    onClose();
  };

  const getInputType = (fieldType: string): string => {
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
    const inputType = getInputType(field.type);
    const value = formData[field.name] ?? field.defaultValue ?? '';
    const hasError = !!errors[field.name];

    if (field.type === 'boolean') {
      return (
        <div key={field.name} className="space-y-2">
          <label className="flex items-center gap-2 cursor-pointer">
            <input
              type="checkbox"
              checked={!!formData[field.name]}
              onChange={(e) => handleFieldChange(field.name, e.target.checked)}
              className="w-4 h-4 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-blue-500"
            />
            <div className="flex-1">
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-gray-300">
                  {field.name}
                  {field.required && <span className="text-red-400 ml-1">*</span>}
                </span>
                <span className="text-xs px-1.5 py-0.5 bg-purple-500/20 text-purple-400 rounded border border-purple-500/30">
                  {field.type}
                </span>
              </div>
              {field.description && (
                <p className="text-xs text-gray-500 mt-0.5">{field.description}</p>
              )}
            </div>
          </label>
          {hasError && (
            <p className="text-xs text-red-400 flex items-center gap-1">
              <AlertCircle className="w-3 h-3" />
              {errors[field.name]}
            </p>
          )}
        </div>
      );
    }

    if (field.type === 'array' || field.type === 'object') {
      return (
        <div key={field.name} className="space-y-2">
          <label className="block">
            <div className="flex items-center gap-2 mb-2">
              <span className="text-sm font-medium text-gray-300">
                {field.name}
                {field.required && <span className="text-red-400 ml-1">*</span>}
              </span>
              <span className="text-xs px-1.5 py-0.5 bg-yellow-500/20 text-yellow-400 rounded border border-yellow-500/30">
                {field.type}
              </span>
            </div>
            {field.description && (
              <p className="text-xs text-gray-500 mb-2">{field.description}</p>
            )}
            <textarea
              value={typeof value === 'string' ? value : JSON.stringify(value, null, 2)}
              onChange={(e) => {
                try {
                  const parsed = JSON.parse(e.target.value);
                  handleFieldChange(field.name, parsed);
                } catch {
                  handleFieldChange(field.name, e.target.value);
                }
              }}
              placeholder={`Enter JSON ${field.type}`}
              rows={4}
              className={`w-full px-3 py-2 bg-gray-700 border ${
                hasError ? 'border-red-500' : 'border-gray-600'
              } rounded-lg text-white placeholder-gray-400 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-blue-500`}
            />
          </label>
          {hasError && (
            <p className="text-xs text-red-400 flex items-center gap-1">
              <AlertCircle className="w-3 h-3" />
              {errors[field.name]}
            </p>
          )}
        </div>
      );
    }

    return (
      <div key={field.name} className="space-y-2">
        <label className="block">
          <div className="flex items-center gap-2 mb-2">
            <span className="text-sm font-medium text-gray-300">
              {field.name}
              {field.required && <span className="text-red-400 ml-1">*</span>}
            </span>
            <span className={`text-xs px-1.5 py-0.5 rounded border ${
              field.type === 'string' ? 'bg-blue-500/20 text-blue-400 border-blue-500/30' :
              field.type === 'number' ? 'bg-green-500/20 text-green-400 border-green-500/30' :
              field.type === 'date' ? 'bg-orange-500/20 text-orange-400 border-orange-500/30' :
              'bg-gray-500/20 text-gray-400 border-gray-500/30'
            }`}>
              {field.type}
            </span>
          </div>
          {field.description && (
            <p className="text-xs text-gray-500 mb-2">{field.description}</p>
          )}
          <input
            type={inputType}
            value={value}
            onChange={(e) => handleFieldChange(field.name, e.target.value)}
            placeholder={field.defaultValue ? `Default: ${field.defaultValue}` : `Enter ${field.name}`}
            className={`w-full px-3 py-2 bg-gray-700 border ${
              hasError ? 'border-red-500' : 'border-gray-600'
            } rounded-lg text-white placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500`}
          />
        </label>
        {hasError && (
          <p className="text-xs text-red-400 flex items-center gap-1">
            <AlertCircle className="w-3 h-3" />
            {errors[field.name]}
          </p>
        )}
      </div>
    );
  };

  return (
    <>
      {/* Backdrop */}
      <div 
        className="fixed inset-0 bg-black/50 z-40 animate-fadeIn"
        onClick={handleCancel}
      />
      
      {/* Dialog */}
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <div 
          className="bg-gray-800 rounded-lg border border-gray-700 max-w-3xl w-full max-h-[90vh] overflow-hidden animate-slideUp"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-700">
            <div className="flex items-center gap-3">
              <div className="p-2 bg-blue-500/10 rounded-lg border border-blue-500/20">
                <Plus className="w-6 h-6 text-blue-500" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-white">Create {entityType}</h2>
                <p className="text-sm text-gray-400">Fill in the fields to create a new entity</p>
              </div>
            </div>
            <button
              onClick={handleCancel}
              className="p-2 hover:bg-gray-700 rounded-lg transition-colors"
              disabled={isSubmitting}
            >
              <X className="w-5 h-5 text-gray-400 hover:text-white" />
            </button>
          </div>

          {/* Content */}
          <form onSubmit={handleSubmit}>
            <div className="p-6 overflow-y-auto max-h-[calc(90vh-180px)]">
              {/* Submit Error */}
              {submitError && (
                <div className="mb-4 p-4 bg-red-500/10 border border-red-500/50 rounded-lg flex items-center gap-2 text-red-400">
                  <AlertCircle className="w-5 h-5 flex-shrink-0" />
                  <span>{submitError}</span>
                </div>
              )}

              {/* Environment Field (Optional) */}
              <div className="mb-6 p-4 bg-gray-700/50 rounded-lg border border-gray-600">
                <label className="block">
                  <div className="flex items-center gap-2 mb-2">
                    <span className="text-sm font-medium text-gray-300">Environment</span>
                    <span className="text-xs text-gray-500">(Optional)</span>
                  </div>
                  <input
                    type="text"
                    value={environment}
                    onChange={(e) => setEnvironment(e.target.value)}
                    placeholder="e.g., dev, qa, staging, prod"
                    className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                </label>
              </div>

              {/* Schema Fields */}
              <div className="space-y-4">
                <div className="flex items-center gap-2 mb-4">
                  <Package className="w-5 h-5 text-gray-400" />
                  <h3 className="text-lg font-semibold text-white">Entity Fields</h3>
                </div>
                {schema.fields.map((field: any) => renderField(field))}
              </div>

              {/* Helper Text */}
              <div className="mt-6 p-3 bg-blue-500/10 border border-blue-500/20 rounded-lg">
                <p className="text-xs text-blue-300">
                  <strong>Tip:</strong> Fields marked with <span className="text-red-400">*</span> are required. 
                  {schema.excludeOnFetch && ' This entity will be marked as available and can be consumed by tests.'}
                </p>
              </div>
            </div>

            {/* Footer Actions */}
            <div className="flex items-center justify-end gap-3 p-6 border-t border-gray-700 bg-gray-800/50">
              <button
                type="button"
                onClick={handleCancel}
                disabled={isSubmitting}
                className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="flex items-center gap-2 px-6 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium disabled:opacity-50 disabled:cursor-not-allowed shadow-sm hover:shadow-md"
              >
                {isSubmitting ? (
                  <>
                    <Loader className="w-4 h-4 animate-spin" />
                    Creating...
                  </>
                ) : (
                  <>
                    <CheckCircle className="w-4 h-4" />
                    Create Entity
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </>
  );
};

export default EntityCreateDialog;
