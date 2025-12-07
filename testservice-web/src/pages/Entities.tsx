import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Package,
  Search,
  Database,
  ChevronRight,
  Layers,
  AlertCircle,
  Filter,
  CheckCircle,
  XCircle
} from 'lucide-react';
import { apiService } from '../services/api';

interface Schema {
  id?: string;
  entityName: string;
  fields: any[];
  filterableFields?: string[];
  excludeOnFetch: boolean;
  createdAt?: string;
  updatedAt?: string;
}

interface EntityTypeStats {
  entityName: string;
  schema: Schema;
  totalCount: number;
  availableCount: number;
  consumedCount: number;
}

const Entities: React.FC = () => {
  const navigate = useNavigate();
  const [schemas, setSchemas] = useState<Schema[]>([]);
  const [entityStats, setEntityStats] = useState<Map<string, EntityTypeStats>>(new Map());
  const [isLoading, setIsLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [error, setError] = useState('');
  const [showAutoConsumeOnly, setShowAutoConsumeOnly] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setIsLoading(true);
    setError('');
    try {
      // Load all schemas
      const schemasData = await apiService.getSchemas();
      setSchemas(schemasData);

      // Load entity counts for each schema
      const statsMap = new Map<string, EntityTypeStats>();
      
      for (const schema of schemasData) {
        try {
          const entities = await apiService.getEntities(schema.entityName);
          const totalCount = entities.length;
          const consumedCount = entities.filter((e: any) => e.consumed).length;
          const availableCount = totalCount - consumedCount;

          statsMap.set(schema.entityName, {
            entityName: schema.entityName,
            schema,
            totalCount,
            availableCount,
            consumedCount
          });
        } catch (err) {
          console.error(`Failed to load entities for ${schema.entityName}:`, err);
          // Set zero counts if loading fails
          statsMap.set(schema.entityName, {
            entityName: schema.entityName,
            schema,
            totalCount: 0,
            availableCount: 0,
            consumedCount: 0
          });
        }
      }

      setEntityStats(statsMap);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to load data');
      console.error('Failed to load data:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleEntityTypeClick = (entityName: string) => {
    navigate(`/entities/${entityName}`);
  };

  // Filter schemas
  let filteredSchemas = schemas.filter(schema =>
    schema.entityName?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  if (showAutoConsumeOnly) {
    filteredSchemas = filteredSchemas.filter(s => s.excludeOnFetch);
  }

  // Calculate totals
  const totalEntities = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.totalCount, 0);
  const totalAvailable = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.availableCount, 0);
  const totalConsumed = Array.from(entityStats.values()).reduce((sum, stat) => sum + stat.consumedCount, 0);

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
            <Database className="w-8 h-8" />
            Entities
          </h1>
          <p className="text-gray-400 mt-1">Browse and manage test data entities</p>
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
              placeholder="Search entity types..."
              className="w-full pl-10 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            />
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
              Show only auto-consume entity types
            </span>
          </label>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Entity Types</p>
          <p className="text-2xl font-bold text-white mt-1">{schemas.length}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm">Total Entities</p>
          <p className="text-2xl font-bold text-white mt-1">{totalEntities}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm flex items-center gap-2">
            <CheckCircle className="w-4 h-4 text-green-400" />
            Available
          </p>
          <p className="text-2xl font-bold text-white mt-1">{totalAvailable}</p>
        </div>
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-4">
          <p className="text-gray-400 text-sm flex items-center gap-2">
            <XCircle className="w-4 h-4 text-orange-400" />
            Consumed
          </p>
          <p className="text-2xl font-bold text-white mt-1">{totalConsumed}</p>
        </div>
      </div>

      {/* Entity Types List */}
      {filteredSchemas.length > 0 ? (
        <div className="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
          <div className="divide-y divide-gray-700">
            {filteredSchemas.map((schema, index) => {
              const stats = entityStats.get(schema.entityName);
              const availablePercent = stats && stats.totalCount > 0 
                ? Math.round((stats.availableCount / stats.totalCount) * 100) 
                : 0;

              return (
                <button
                  key={index}
                  onClick={() => handleEntityTypeClick(schema.entityName)}
                  className="w-full p-6 hover:bg-gray-700 transition-colors text-left group"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-4 flex-1">
                      <div className="p-3 bg-purple-500/10 rounded-lg border border-purple-500/20">
                        <Package className="w-6 h-6 text-purple-500" />
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
                        <div className="flex items-center gap-4 text-sm text-gray-400">
                          <span>{schema.fields?.length || 0} fields</span>
                          {stats && (
                            <>
                              <span>•</span>
                              <span className="flex items-center gap-1">
                                <Database className="w-3 h-3" />
                                {stats.totalCount} total
                              </span>
                              <span>•</span>
                              <span className="flex items-center gap-1 text-green-400">
                                <CheckCircle className="w-3 h-3" />
                                {stats.availableCount} available
                              </span>
                              {stats.consumedCount > 0 && (
                                <>
                                  <span>•</span>
                                  <span className="flex items-center gap-1 text-orange-400">
                                    <XCircle className="w-3 h-3" />
                                    {stats.consumedCount} consumed
                                  </span>
                                </>
                              )}
                            </>
                          )}
                        </div>
                        {stats && stats.totalCount > 0 && (
                          <div className="mt-2">
                            <div className="flex items-center gap-2">
                              <div className="flex-1 h-2 bg-gray-700 rounded-full overflow-hidden">
                                <div
                                  className="h-full bg-green-500 transition-all"
                                  style={{ width: `${availablePercent}%` }}
                                />
                              </div>
                              <span className="text-xs text-gray-400 min-w-[3rem] text-right">
                                {availablePercent}% free
                              </span>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                    <ChevronRight className="w-5 h-5 text-gray-500 group-hover:text-white transition-colors" />
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      ) : (
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-12 text-center">
          <Layers className="w-16 h-16 mx-auto mb-4 opacity-50 text-gray-500" />
          <h3 className="text-lg font-medium text-gray-400 mb-2">
            {searchTerm || showAutoConsumeOnly ? 'No entity types found' : 'No entity types yet'}
          </h3>
          <p className="text-sm text-gray-500 mb-4">
            {searchTerm || showAutoConsumeOnly
              ? 'Try adjusting your search or filters'
              : 'Create a schema first to start managing entities'}
          </p>
          {!searchTerm && !showAutoConsumeOnly && (
            <button
              onClick={() => navigate('/schemas/new')}
              className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors font-medium"
            >
              <Layers className="w-5 h-5" />
              Create Schema
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default Entities;
