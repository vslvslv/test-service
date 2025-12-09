import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Layers, 
  Plus, 
  Edit, 
  Trash2,
  AlertCircle,
  Search,
  List,
  Grid3x3,
  Filter,
  Package,
  ChevronRight,
  Type,
  Hash,
  CheckSquare,
  Calendar
} from 'lucide-react';
import { apiService } from '../services/api';
import { getErrorMessage, type Schema } from '../types';

type SortOption = 'name' | 'fields' | 'recent';

const Schemas: React.FC = () => {
  const navigate = useNavigate();
  const [schemas, setSchemas] = useState<Schema[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [viewMode, setViewMode] = useState<'list' | 'grid'>('list');
  const [sortBy, setSortBy] = useState<'name' | 'fields' | 'recent'>('name');
  const [showAutoConsumeOnly, setShowAutoConsumeOnly] = useState(false);

  useEffect(() => {
    loadSchemas();
  }, []);

  const loadSchemas = async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await apiService.getSchemas();
      console.log('API returned schemas:', data);
      console.log('Number of schemas:', data?.length);
      console.log('First schema:', data?.[0]);
      setSchemas(data);
    } catch (err) {
      setError(getErrorMessage(err));
      console.error('Failed to load schemas:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateNew = () => {
    navigate('/schemas/new');
  };

  const handleSchemaClick = (schemaEntityName: string) => {
    navigate(`/schemas/${schemaEntityName}`);
  };

  const handleDelete = async (schemaEntityName: string, e: React.MouseEvent) => {
    e.stopPropagation();
    
    if (!confirm(`Are you sure you want to delete the schema "${schemaEntityName}"?\n\nThis will also affect any entities using this schema.`)) {
      return;
    }

    try {
      await apiService.request({
        method: 'DELETE',
        url: `/api/schemas/${schemaEntityName}`
      });
      
      await loadSchemas();
    } catch (err) {
      alert(getErrorMessage(err));
    }
  };

  const getFieldTypeIcon = (type: string) => {
    switch (type) {
      case 'string': return <Type className="w-3 h-3" />;
      case 'number': return <Hash className="w-3 h-3" />;
      case 'boolean': return <CheckSquare className="w-3 h-3" />;
      case 'date': return <Calendar className="w-3 h-3" />;
      default: return <Package className="w-3 h-3" />;
    }
  };

  const getFieldTypeColor = (type: string) => {
    switch (type) {
      case 'string': return 'bg-blue-500/10 text-blue-400 border-blue-500/30';
      case 'number': return 'bg-green-500/10 text-green-400 border-green-500/30';
      case 'boolean': return 'bg-purple-500/10 text-purple-400 border-purple-500/30';
      case 'date': return 'bg-orange-500/10 text-orange-400 border-orange-500/30';
      case 'array': return 'bg-pink-500/10 text-pink-400 border-pink-500/30';
      case 'object': return 'bg-yellow-500/10 text-yellow-400 border-yellow-500/30';
      default: return 'bg-gray-500/10 text-gray-400 border-gray-500/30';
    }
  };

  // Filter and sort schemas
  let filteredSchemas = schemas.filter(schema => {
    // Skip schemas without entityName
    if (!schema.entityName) return false;
    
    // If there's a search term, filter by entityName
    if (searchTerm) {
      return schema.entityName.toLowerCase().includes(searchTerm.toLowerCase());
    }
    
    // No search term, include all schemas with entityName
    return true;
  });

  console.log('After filtering:', filteredSchemas.length, 'schemas');
  console.log('Search term:', searchTerm);
  console.log('Show auto-consume only:', showAutoConsumeOnly);

  if (showAutoConsumeOnly) {
    filteredSchemas = filteredSchemas.filter(s => s.excludeOnFetch);
  }

  // Sort schemas
  const sortedSchemas = [...filteredSchemas].sort((a, b) => {
    switch (sortBy) {
      case 'name':
        return (a.entityName || '').localeCompare(b.entityName || '');
      case 'fields':
        return (b.fields?.length || 0) - (a.fields?.length || 0);
      case 'recent':
        return (b.createdAt || '').localeCompare(a.createdAt || '');
      default:
        return 0;
    }
  });

  console.log('After sorting:', sortedSchemas.length, 'schemas');
  console.log('Sorted schemas:', sortedSchemas.map(s => s.entityName));

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white flex items-center gap-2">
            <Layers className="w-8 h-8" />
            Schemas
          </h1>
          <p className="text-gray-400 mt-1">Manage entity type definitions</p>
        </div>
        <button
          onClick={handleCreateNew}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
        >
          <Plus className="w-5 h-5" />
          Create Schema
        </button>
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
              placeholder="Search schemas by name..."
              className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
          </div>

          {/* Sort */}
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as SortOption)}
            className="px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="name">Sort by Name</option>
            <option value="fields">Sort by Fields Count</option>
            <option value="recent">Sort by Recent</option>
          </select>

          {/* View Mode Toggle */}
          <div className="flex items-center gap-1 bg-gray-700 rounded-lg p-1">
            <button
              onClick={() => setViewMode('list')}
              className={`p-2 rounded transition-colors ${
                viewMode === 'list' ? 'bg-blue-600 text-white' : 'text-gray-400 hover:text-white'
              }`}
              title="List view"
            >
              <List className="w-4 h-4" />
            </button>
            <button
              onClick={() => setViewMode('grid')}
              className={`p-2 rounded transition-colors ${
                viewMode === 'grid' ? 'bg-blue-600 text-white' : 'text-gray-400 hover:text-white'
              }`}
              title="Grid view"
            >
              <Grid3x3 className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Auto-consume filter */}
        <div className="mt-3 pt-3 border-t border-gray-700">
          <label className="flex items-center gap-2 cursor-pointer w-fit">
            <input
              type="checkbox"
              checked={showAutoConsumeOnly}
              onChange={(e) => setShowAutoConsumeOnly(e.target.checked)}
              className="w-4 h-4 bg-gray-700 border-gray-600 rounded focus:ring-2 focus:ring-blue-500"
            />
            <span className="text-sm text-gray-300 flex items-center gap-2">
              <Filter className="w-4 h-4" />
              Show only auto-consume schemas
            </span>
          </label>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Total Schemas</p>
          <p className="text-2xl font-bold text-white mt-1">{schemas.length}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Auto-Consume</p>
          <p className="text-2xl font-bold text-white mt-1">
            {schemas.filter(s => s.excludeOnFetch).length}
          </p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Total Fields</p>
          <p className="text-2xl font-bold text-white mt-1">
            {schemas.reduce((sum, s) => sum + (s.fields?.length || 0), 0)}
          </p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Filtered Results</p>
          <p className="text-2xl font-bold text-white mt-1">{sortedSchemas.length}</p>
        </div>
      </div>

      {/* Schema List/Grid */}
      {sortedSchemas.length > 0 ? (
        viewMode === 'list' ? (
          // List View
          <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
            <div className="divide-y divide-gray-700">
              {sortedSchemas.map((schema, index) => (
                <div
                  key={index}
                  className="p-6 hover:bg-gray-700 transition-colors group"
                >
                  <div className="flex items-start justify-between mb-3">
                    <div className="flex items-center gap-4 flex-1">
                      <div className="p-3 bg-blue-500/10 rounded-lg border border-blue-500/20">
                        <Package className="w-6 h-6 text-blue-500" />
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center gap-2 mb-1">
                          <h3 className="text-lg font-semibold text-white">{schema.entityName}</h3>
                          {schema.excludeOnFetch && (
                            <span className="text-xs px-2 py-0.5 bg-orange-500/20 text-orange-400 rounded border border-orange-500/30">
                              Auto-consume
                            </span>
                          )}
                        </div>
                        <p className="text-sm text-gray-400">
                          {schema.fields?.length || 0} fields • {schema.fields?.filter(f => f.required).length || 0} required
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => handleSchemaClick(schema.entityName)}
                        className="p-2 hover:bg-gray-600 rounded-lg transition-colors"
                        title="View/Edit"
                      >
                        <Edit className="w-5 h-5 text-gray-400 hover:text-white" />
                      </button>
                      <button
                        onClick={(e) => handleDelete(schema.entityName, e)}
                        className="p-2 hover:bg-red-500/10 rounded-lg transition-colors"
                        title="Delete"
                      >
                        <Trash2 className="w-5 h-5 text-gray-400 hover:text-red-400" />
                      </button>
                      <button
                        onClick={() => handleSchemaClick(schema.entityName)}
                        className="p-2 hover:bg-gray-600 rounded-lg transition-colors"
                      >
                        <ChevronRight className="w-5 h-5 text-gray-500 group-hover:text-white transition-colors" />
                      </button>
                    </div>
                  </div>
                  
                  {/* Fields Preview */}
                  {schema.fields && schema.fields.length > 0 && (
                    <div className="flex flex-wrap gap-2 mt-3 pt-3 border-t border-gray-700">
                      {schema.fields.slice(0, 6).map((field, fieldIndex) => (
                        field && field.name ? (
                          <div
                            key={fieldIndex}
                            className={`flex items-center gap-1.5 px-2 py-1 rounded border text-xs ${getFieldTypeColor(field.type || 'string')}`}
                          >
                            {getFieldTypeIcon(field.type || 'string')}
                            <span className="font-medium">{field.name}</span>
                            {field.required && <span className="text-red-400">*</span>}
                          </div>
                        ) : null
                      ))}
                      {schema.fields.length > 6 && (
                        <div className="px-2 py-1 text-xs text-gray-400">
                          +{schema.fields.length - 6} more
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))}
            </div>
          </div>
        ) : (
          // Grid View
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {sortedSchemas.map((schema, index) => (
              <div
                key={index}
                className="bg-gray-800 rounded-lg border border-gray-700 p-6 hover:border-blue-500/50 transition-all group cursor-pointer"
                onClick={() => handleSchemaClick(schema.entityName)}
              >
                <div className="flex items-start justify-between mb-4">
                  <div className="p-3 bg-blue-500/10 rounded-lg border border-blue-500/20">
                    <Package className="w-6 h-6 text-blue-500" />
                  </div>
                  <div className="flex gap-1">
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleSchemaClick(schema.entityName);
                      }}
                      className="p-1.5 hover:bg-gray-700 rounded transition-colors"
                      title="Edit"
                    >
                      <Edit className="w-4 h-4 text-gray-400 hover:text-white" />
                    </button>
                    <button
                      onClick={(e) => handleDelete(schema.entityName, e)}
                      className="p-1.5 hover:bg-red-500/10 rounded transition-colors"
                      title="Delete"
                    >
                      <Trash2 className="w-4 h-4 text-gray-400 hover:text-red-400" />
                    </button>
                  </div>
                </div>

                <h3 className="text-lg font-semibold text-white mb-2 truncate">
                  {schema.entityName}
                </h3>

                {schema.excludeOnFetch && (
                  <span className="inline-block text-xs px-2 py-0.5 bg-orange-500/20 text-orange-400 rounded border border-orange-500/30 mb-3">
                    Auto-consume
                  </span>
                )}

                <div className="space-y-2 mb-4">
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-400">Fields</span>
                    <span className="text-white font-medium">{schema.fields?.length || 0}</span>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-gray-400">Required</span>
                    <span className="text-white font-medium">
                      {schema.fields?.filter(f => f.required).length || 0}
                    </span>
                  </div>
                </div>

                {/* Field Types Summary */}
                {schema.fields && schema.fields.length > 0 && (
                  <div className="flex flex-wrap gap-1 pt-3 border-t border-gray-700">
                    {Array.from(new Set(schema.fields.map(f => f?.type || 'string'))).slice(0, 4).map((type, i) => (
                      <div
                        key={i}
                        className={`flex items-center gap-1 px-2 py-0.5 rounded border text-xs ${getFieldTypeColor(type)}`}
                      >
                        {getFieldTypeIcon(type)}
                        <span>{type}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        )
      ) : (
        // Empty State
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-12 text-center">
          <Layers className="w-16 h-16 mx-auto mb-4 opacity-50 text-gray-500" />
          <h3 className="text-lg font-medium text-gray-400 mb-2">
            {searchTerm || showAutoConsumeOnly ? 'No schemas found' : 'No schemas yet'}
          </h3>
          <p className="text-sm text-gray-500 mb-4">
            {searchTerm || showAutoConsumeOnly
              ? 'Try adjusting your search or filters'
              : 'Create your first schema to define entity types'}
          </p>
          {!searchTerm && !showAutoConsumeOnly && (
            <button
              onClick={handleCreateNew}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
            >
              <Plus className="w-5 h-5" />
              Create Schema
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default Schemas;
