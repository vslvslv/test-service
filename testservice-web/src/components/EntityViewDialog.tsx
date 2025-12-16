import React from 'react';
import { 
  X,
  Calendar,
  Package,
  CheckCircle,
  XCircle,
  Copy,
  Download,
  Edit,
  RefreshCw
} from 'lucide-react';

interface EntityViewDialogProps {
  isOpen: boolean;
  onClose: () => void;
  entity: any;
  schema: any;
  onReset?: () => void;
  onEdit?: () => void;
}

const EntityViewDialog: React.FC<EntityViewDialogProps> = ({
  isOpen,
  onClose,
  entity,
  schema,
  onReset,
  onEdit
}) => {
  if (!isOpen || !entity) return null;

  const handleCopyField = (value: string) => {
    navigator.clipboard.writeText(value);
    // Could add a toast notification here
  };

  const handleCopyAll = () => {
    const allFields = JSON.stringify(entity.fields, null, 2);
    navigator.clipboard.writeText(allFields);
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

  return (
    <>
      {/* Backdrop */}
      <div 
        className="fixed inset-0 bg-black/50 z-40 animate-fadeIn"
        onClick={onClose}
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
              <div className="p-2 bg-purple-500/10 rounded-lg border border-purple-500/20">
                <Package className="w-6 h-6 text-purple-500" />
              </div>
              <div>
                <h2 className="text-xl font-bold text-white">{entity.entityType}</h2>
                <p className="text-sm text-gray-400 font-mono">{entity.id}</p>
              </div>
            </div>
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-700 rounded-lg transition-colors"
            >
              <X className="w-5 h-5 text-gray-400 hover:text-white" />
            </button>
          </div>

          {/* Content */}
          <div className="p-6 overflow-y-auto max-h-[calc(90vh-180px)]">
            {/* Status and Metadata */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
              {/* Status */}
              <div className="bg-gray-700/50 rounded-lg border border-gray-600 p-4">
                <p className="text-xs text-gray-400 mb-2">Status</p>
                {entity.isConsumed ? (
                  <div className="flex items-center gap-2">
                    <XCircle className="w-5 h-5 text-orange-400" />
                    <span className="text-orange-400 font-medium">Consumed</span>
                  </div>
                ) : (
                  <div className="flex items-center gap-2">
                    <CheckCircle className="w-5 h-5 text-green-400" />
                    <span className="text-green-400 font-medium">Available</span>
                  </div>
                )}
              </div>

              {/* Environment */}
              {entity.environment && (
                <div className="bg-gray-700/50 rounded-lg border border-gray-600 p-4">
                  <p className="text-xs text-gray-400 mb-2">Environment</p>
                  <p className="text-white font-medium">{entity.environment}</p>
                </div>
              )}

              {/* Created At */}
              {entity.createdAt && (
                <div className="bg-gray-700/50 rounded-lg border border-gray-600 p-4">
                  <p className="text-xs text-gray-400 mb-2 flex items-center gap-1">
                    <Calendar className="w-3 h-3" />
                    Created
                  </p>
                  <p className="text-white text-sm">
                    {new Date(entity.createdAt).toLocaleString()}
                  </p>
                </div>
              )}

              {/* Updated At */}
              {entity.updatedAt && (
                <div className="bg-gray-700/50 rounded-lg border border-gray-600 p-4">
                  <p className="text-xs text-gray-400 mb-2 flex items-center gap-1">
                    <Calendar className="w-3 h-3" />
                    Updated
                  </p>
                  <p className="text-white text-sm">
                    {new Date(entity.updatedAt).toLocaleString()}
                  </p>
                </div>
              )}
            </div>

            {/* Fields */}
            <div className="bg-gray-700/50 rounded-lg border border-gray-600 p-4">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-lg font-semibold text-white">Fields</h3>
                <button
                  onClick={handleCopyAll}
                  className="flex items-center gap-2 px-3 py-1.5 text-sm bg-gray-600 hover:bg-gray-500 text-white rounded-lg transition-colors"
                >
                  <Copy className="w-4 h-4" />
                  Copy All
                </button>
              </div>

              <div className="space-y-3">
                {schema?.fields?.map((field: any, index: number) => {
                  const value = entity.fields[field.name];
                  const hasValue = value !== undefined && value !== null && value !== '';

                  return (
                    <div
                      key={index}
                      className="flex items-start justify-between p-3 bg-gray-800 rounded-lg border border-gray-600 hover:border-gray-500 transition-colors group"
                    >
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <p className="text-sm font-medium text-gray-300">
                            {field.name}
                            {field.required && (
                              <span className="text-red-400 ml-1">*</span>
                            )}
                          </p>
                          <span className="text-xs px-1.5 py-0.5 bg-blue-500/20 text-blue-400 rounded border border-blue-500/30">
                            {field.type}
                          </span>
                        </div>
                        {field.description && (
                          <p className="text-xs text-gray-500 mb-2">{field.description}</p>
                        )}
                        <div className="flex items-center gap-2">
                          {hasValue ? (
                            <p className="text-white font-mono text-sm break-all">
                              {typeof value === 'object' 
                                ? JSON.stringify(value) 
                                : String(value)}
                            </p>
                          ) : (
                            <p className="text-gray-500 text-sm italic">
                              {field.defaultValue ? `Default: ${field.defaultValue}` : 'No value'}
                            </p>
                          )}
                        </div>
                      </div>
                      {hasValue && (
                        <button
                          onClick={() => handleCopyField(String(value))}
                          className="p-1.5 opacity-0 group-hover:opacity-100 hover:bg-gray-700 rounded transition-all ml-2"
                          title="Copy value"
                        >
                          <Copy className="w-4 h-4 text-gray-400 hover:text-white" />
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>

            {/* Raw JSON View */}
            <details className="mt-4 bg-gray-700/50 rounded-lg border border-gray-600">
              <summary className="p-4 cursor-pointer hover:bg-gray-700/70 transition-colors text-sm font-medium text-gray-300">
                View Raw JSON
              </summary>
              <div className="p-4 pt-0">
                <pre className="bg-gray-900 rounded p-4 overflow-x-auto text-xs text-gray-300 border border-gray-700">
                  {JSON.stringify(entity, null, 2)}
                </pre>
              </div>
            </details>
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-between gap-3 p-6 border-t border-gray-700 bg-gray-800/50">
            <div className="flex gap-2">
              <button
                onClick={handleExportJson}
                className="flex items-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors text-sm"
              >
                <Download className="w-4 h-4" />
                Export JSON
              </button>
            </div>
            <div className="flex gap-2">
              {entity.isConsumed && schema?.excludeOnFetch && onReset && (
                <button
                  onClick={onReset}
                  className="flex items-center gap-2 px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors"
                >
                  <RefreshCw className="w-4 h-4" />
                  Reset
                </button>
              )}
              {onEdit && (
                <button
                  onClick={onEdit}
                  className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                >
                  <Edit className="w-4 h-4" />
                  Edit
                </button>
              )}
              <button
                onClick={onClose}
                className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
};

export default EntityViewDialog;
