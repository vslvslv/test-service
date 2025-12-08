import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { 
  Plus,
  Search,
  ArrowLeft,
  Eye,
  Trash2,
  RefreshCw,
  AlertCircle,
  CheckCircle,
  XCircle,
  Download,
  Filter
} from 'lucide-react';
import { apiService } from '../services/api';
import EntityViewDialog from '../components/EntityViewDialog';
import EntityCreateDialog from '../components/EntityCreateDialog';

interface Entity {
  id: string;
  entityType: string;
  fields: Record<string, any>;
  consumed: boolean;
  environment?: string;
  createdAt?: string;
  updatedAt?: string;
}

interface Schema {
  entityName: string;
  fields: any[];
  filterableFields?: string[];
  excludeOnFetch: boolean;
}

const EntityList: React.FC = () => {
  const navigate = useNavigate();
  const { entityType } = useParams<{ entityType: string }>();
  const [entities, setEntities] = useState<Entity[]>([]);
  const [schema, setSchema] = useState<Schema | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [showConsumedOnly, setShowConsumedOnly] = useState(false);
  const [selectedEnvironment, setSelectedEnvironment] = useState<string>('all');
  const [selectedEntity, setSelectedEntity] = useState<Entity | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);

  useEffect(() => {
    if (entityType) {
      loadData();
    }
  }, [entityType]);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      // Load schema
      const schemaData = await apiService.getSchema(entityType!);
      setSchema(schemaData);

      // Load entities
      const entitiesData = await apiService.request({
        method: 'GET',
        url: `/api/entities/${entityType}`
      });
      setEntities(entitiesData);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to load entities');
      console.error('Failed to load entities:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateNew = () => {
    setIsCreateDialogOpen(true);
  };

  const handleCloseCreateDialog = () => {
    setIsCreateDialogOpen(false);
  };

  const handleCreateSuccess = async () => {
    await loadData();
  };

  const handleViewEntity = (entity: Entity) => {
    setSelectedEntity(entity);
    setIsViewDialogOpen(true);
  };

  const handleCloseDialog = () => {
    setIsViewDialogOpen(false);
    setSelectedEntity(null);
  };

  const handleDeleteEntity = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    
    if (!confirm('Are you sure you want to delete this entity?')) {
      return;
    }

    try {
      await apiService.deleteEntity(entityType!, id);
      await loadData();
      if (selectedEntity?.id === id) {
        handleCloseDialog();
      }
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to delete entity');
    }
  };

  const handleResetEntity = async (id: string, e?: React.MouseEvent) => {
    if (e) {
      e.stopPropagation();
    }
    
    try {
      await apiService.request({
        method: 'POST',
        url: `/api/entities/${entityType}/${id}/reset`
      });
      await loadData();
      // Update selected entity if it's the one being reset
      if (selectedEntity?.id === id) {
        const updatedEntity = entities.find(e => e.id === id);
        if (updatedEntity) {
          setSelectedEntity({ ...updatedEntity, consumed: false });
        }
      }
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to reset entity');
    }
  };

  const handleResetAll = async () => {
    if (!confirm('Are you sure you want to reset all consumed entities? This will make them available again.')) {
      return;
    }

    try {
      await apiService.request({
        method: 'POST',
        url: `/api/entities/${entityType}/reset-all`
      });
      await loadData();
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to reset entities');
    }
  };

  const handleGetNext = async () => {
    try {
      const entity = await apiService.getNextAvailable(entityType!);
      if (entity) {
        await loadData();
        handleViewEntity(entity);
      } else {
        alert('No available entities found');
      }
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to get next entity');
    }
  };

  const handleResetFromDialog = async () => {
    if (selectedEntity) {
      await handleResetEntity(selectedEntity.id);
    }
  };

  const handleEditFromDialog = () => {
    if (selectedEntity) {
      handleCloseDialog();
      navigate(`/entities/${entityType}/${selectedEntity.id}/edit`);
    }
  };

  // Filter entities
  const filteredEntities = entities.filter(entity => {
    // Search filter
    if (searchTerm) {
      const searchLower = searchTerm.toLowerCase();
      const matchesSearch = Object.values(entity.fields).some(value =>
        String(value).toLowerCase().includes(searchLower)
      );
      if (!matchesSearch) return false;
    }

    // Consumed filter
    if (showConsumedOnly && !entity.consumed) return false;

    // Environment filter
    if (selectedEnvironment !== 'all' && entity.environment !== selectedEnvironment) return false;

    return true;
  });

  const availableCount = entities.filter(e => !e.consumed).length;
  const consumedCount = entities.filter(e => e.consumed).length;
  const environments = Array.from(new Set(entities.map(e => e.environment).filter(Boolean)));

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  if (!schema) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <AlertCircle className="w-16 h-16 text-red-500 mx-auto mb-4" />
          <h2 className="text-xl font-semibold text-white mb-2">Entity Type Not Found</h2>
          <p className="text-gray-400 mb-4">The entity type "{entityType}" could not be found.</p>
          <button
            onClick={() => navigate('/entities')}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            Back to Entities
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate('/entities')}
            className="p-2 hover:bg-gray-700 rounded-lg transition-colors"
            title="Back to entities"
          >
            <ArrowLeft className="w-6 h-6 text-gray-400 hover:text-white" />
          </button>
          <div>
            <div className="flex items-center gap-2 flex-wrap">
              <h1 className="text-2xl font-bold text-white">{entityType}</h1>
              {schema.excludeOnFetch && (
                <span className="text-xs px-2 py-0.5 bg-orange-500/20 text-orange-400 rounded border border-orange-500/30 font-medium">
                  Auto-consume
                </span>
              )}
            </div>
            <p className="text-gray-400 mt-1 text-sm">{entities.length} entities • {schema.fields.length} fields</p>
          </div>
        </div>
        
        {/* Action Buttons Group */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Secondary Actions (Conditional) */}
          {schema.excludeOnFetch && consumedCount > 0 && (
            <button
              onClick={handleResetAll}
              className="flex items-center gap-2 px-3 py-2 bg-gray-700 hover:bg-gray-600 text-white text-sm rounded-lg transition-colors border border-gray-600"
              title="Reset all consumed entities"
            >
              <RefreshCw className="w-4 h-4" />
              <span className="hidden sm:inline">Reset All</span>
            </button>
          )}
          
          {/* Primary Action (Get Next) */}
          {schema.excludeOnFetch && availableCount > 0 && (
            <button
              onClick={handleGetNext}
              className="flex items-center gap-2 px-4 py-2 bg-green-600 hover:bg-green-700 text-white text-sm font-medium rounded-lg transition-colors shadow-sm hover:shadow-md"
              title="Get next available entity"
            >
              <Download className="w-4 h-4" />
              <span>Get Next</span>
            </button>
          )}
          
          {/* Primary Action (Create) */}
          <button
            onClick={handleCreateNew}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition-colors shadow-sm hover:shadow-md"
            title="Create new entity"
          >
            <Plus className="w-4 h-4" />
            <span>Create New</span>
          </button>
        </div>
      </div>

      {/* Error Message */}
      {error && (
        <div className="p-4 bg-red-500/10 border border-red-500/50 rounded-lg flex items-center gap-2 text-red-400">
          <AlertCircle className="w-5 h-5 flex-shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Search and Filters */}
      <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
        <div className="flex flex-col md:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 w-5 h-5 text-gray-500" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Search entities..."
              className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          {/* Environment Filter */}
          {environments.length > 0 && (
            <select
              value={selectedEnvironment}
              onChange={(e) => setSelectedEnvironment(e.target.value)}
              className="px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="all">All Environments</option>
              {environments.map(env => (
                <option key={env} value={env}>{env}</option>
              ))}
            </select>
          )}
        </div>

        {/* Consumed filter */}
        {schema.excludeOnFetch && (
          <div className="mt-3 pt-3 border-t border-gray-700">
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input
                type="checkbox"
                checked={showConsumedOnly}
                onChange={(e) => setShowConsumedOnly(e.target.checked)}
                className="w-4 h-4 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-blue-500"
              />
              <span className="text-sm text-gray-300 flex items-center gap-2">
                <Filter className="w-4 h-4" />
                Show only consumed entities
              </span>
            </label>
          </div>
        )}
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Total Entities</p>
          <p className="text-2xl font-bold text-white mt-1">{entities.length}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm flex items-center gap-2">
            <CheckCircle className="w-4 h-4 text-green-400" />
            Available
          </p>
          <p className="text-2xl font-bold text-white mt-1">{availableCount}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm flex items-center gap-2">
            <XCircle className="w-4 h-4 text-orange-400" />
            Consumed
          </p>
          <p className="text-2xl font-bold text-white mt-1">{consumedCount}</p>
        </div>
      </div>

      {/* Entities List */}
      {filteredEntities.length > 0 ? (
        <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead className="bg-gray-700/50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                    Status
                  </th>
                  {schema.fields.slice(0, 3).map((field, idx) => (
                    <th key={idx} className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                      {field.name}
                    </th>
                  ))}
                  {entities.some(e => e.environment) && (
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-400 uppercase tracking-wider">
                      Environment
                    </th>
                  )}
                  <th className="px-6 py-3 text-right text-xs font-medium text-gray-400 uppercase tracking-wider">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-700">
                {filteredEntities.map((entity) => (
                  <tr
                    key={entity.id}
                    className="hover:bg-gray-700/30 cursor-pointer"
                    onClick={() => handleViewEntity(entity)}
                  >
                    <td className="px-6 py-4 whitespace-nowrap">
                      {entity.consumed ? (
                        <span className="flex items-center gap-1 text-orange-400 text-sm">
                          <XCircle className="w-4 h-4" />
                          Consumed
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-green-400 text-sm">
                          <CheckCircle className="w-4 h-4" />
                          Available
                        </span>
                      )}
                    </td>
                    {schema.fields.slice(0, 3).map((field, idx) => (
                      <td key={idx} className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                        {String(entity.fields[field.name] || '-')}
                      </td>
                    ))}
                    {entities.some(e => e.environment) && (
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                        {entity.environment || '-'}
                      </td>
                    )}
                    <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            handleViewEntity(entity);
                          }}
                          className="p-1.5 hover:bg-gray-600 rounded transition-colors"
                          title="View"
                        >
                          <Eye className="w-4 h-4 text-gray-400 hover:text-white" />
                        </button>
                        {entity.consumed && schema.excludeOnFetch && (
                          <button
                            onClick={(e) => handleResetEntity(entity.id, e)}
                            className="p-1.5 hover:bg-green-500/10 rounded transition-colors"
                            title="Reset"
                          >
                            <RefreshCw className="w-4 h-4 text-gray-400 hover:text-green-400" />
                          </button>
                        )}
                        <button
                          onClick={(e) => handleDeleteEntity(entity.id, e)}
                          className="p-1.5 hover:bg-red-500/10 rounded transition-colors"
                          title="Delete"
                        >
                          <Trash2 className="w-4 h-4 text-gray-400 hover:text-red-400" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-12 text-center">
          <AlertCircle className="w-16 h-16 mx-auto mb-4 opacity-50 text-gray-500" />
          <h3 className="text-lg font-medium text-gray-400 mb-2">
            {searchTerm || showConsumedOnly ? 'No entities found' : 'No entities yet'}
          </h3>
          <p className="text-sm text-gray-500 mb-4">
            {searchTerm || showConsumedOnly
              ? 'Try adjusting your search or filters'
              : `Create your first ${entityType} entity to get started`}
          </p>
          {!searchTerm && !showConsumedOnly && (
            <button
              onClick={handleCreateNew}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
            >
              <Plus className="w-5 h-5" />
              Create Entity
            </button>
          )}
        </div>
      )}

      {/* Entity View Dialog */}
      <EntityViewDialog
        isOpen={isViewDialogOpen}
        onClose={handleCloseDialog}
        entity={selectedEntity}
        schema={schema}
        onReset={handleResetFromDialog}
        onEdit={handleEditFromDialog}
      />

      {/* Entity Create Dialog */}
      <EntityCreateDialog
        isOpen={isCreateDialogOpen}
        onClose={handleCloseCreateDialog}
        onSuccess={handleCreateSuccess}
        schema={schema}
        entityType={entityType!}
      />
    </div>
  );
};

export default EntityList;
