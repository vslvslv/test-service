import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { 
  Database, 
  Server, 
  Layers, 
  Activity,
  TrendingUp,
  AlertCircle,
  CheckCircle,
  Package,
  ChevronRight
} from 'lucide-react';
import { apiService } from '../services/api';
import type { Schema } from '../types';

interface Stats {
  totalSchemas: number;
  totalEntities: number;
  totalEnvironments: number;
  availableEntities: number;
  consumedEntities: number;
}

const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [stats, setStats] = useState<Stats>({
    totalSchemas: 0,
    totalEntities: 0,
    totalEnvironments: 0,
    availableEntities: 0,
    consumedEntities: 0,
  });
  const [isLoading, setIsLoading] = useState(true);
  const [recentSchemas, setRecentSchemas] = useState<Schema[]>([]);

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    setIsLoading(true);
    try {
      // Load schemas
      const schemas = await apiService.getSchemas();
      setRecentSchemas(schemas.slice(0, 5));

      // Load environments
      const environments = await apiService.getEnvironments();

      // Calculate entity statistics across all schemas
      let totalEntities = 0;
      let availableEntities = 0;
      let consumedEntities = 0;

      for (const schema of schemas) {
        try {
          const entities = await apiService.getEntities(schema.entityName);
          totalEntities += entities.length;
          
          // Count available vs consumed
          entities.forEach((entity: any) => {
            if (entity.isConsumed) {
              consumedEntities++;
            } else {
              availableEntities++;
            }
          });
        } catch (err) {
          console.error(`Failed to load entities for ${schema.entityName}:`, err);
        }
      }

      setStats({
        totalSchemas: schemas.length,
        totalEntities,
        totalEnvironments: environments.length,
        availableEntities,
        consumedEntities,
      });
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCreateSchema = () => {
    navigate('/schemas/new');
  };

  const handleManageEnvironments = () => {
    navigate('/environments');
  };

  const handleViewActivity = () => {
    navigate('/activity');
  };

  const handleViewAllSchemas = () => {
    navigate('/schemas');
  };

  const handleSchemaClick = (schemaName: string) => {
    navigate(`/schemas/${schemaName}`);
  };

  const statCards = [
    {
      title: 'Total Schemas',
      value: stats.totalSchemas,
      icon: Layers,
      color: 'blue',
      trend: '+12%',
      onClick: () => navigate('/schemas'),
    },
    {
      title: 'Environments',
      value: stats.totalEnvironments,
      icon: Server,
      color: 'green',
      trend: '+5%',
      onClick: () => navigate('/environments'),
    },
    {
      title: 'Available Entities',
      value: stats.availableEntities,
      icon: CheckCircle,
      color: 'purple',
      trend: '87%',
      onClick: () => navigate('/entities'),
    },
    {
      title: 'Consumed Entities',
      value: stats.consumedEntities,
      icon: Activity,
      color: 'orange',
      trend: '13%',
      onClick: () => navigate('/entities'),
    },
  ];

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Welcome Section */}
      <div className="bg-gradient-to-r from-blue-600 to-blue-700 rounded-lg p-6 text-white">
        <h1 className="text-2xl font-bold mb-2">Welcome back, {user?.username}!</h1>
        <p className="text-blue-100">Here's what's happening with your test data today.</p>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {statCards.map((stat, index) => {
          const Icon = stat.icon;
          const colorClasses = {
            blue: 'bg-blue-500/10 text-blue-500 border-blue-500/20',
            green: 'bg-green-500/10 text-green-500 border-green-500/20',
            purple: 'bg-purple-500/10 text-purple-500 border-purple-500/20',
            orange: 'bg-orange-500/10 text-orange-500 border-orange-500/20',
          };

          return (
            <button
              key={index}
              onClick={stat.onClick}
              className="bg-gray-800 rounded-lg border border-gray-700 p-6 hover:border-gray-600 hover:bg-gray-750 transition-all text-left cursor-pointer"
            >
              <div className="flex items-center justify-between mb-4">
                <div className={`p-3 rounded-lg ${colorClasses[stat.color as keyof typeof colorClasses]}`}>
                  <Icon className="w-6 h-6" />
                </div>
                <span className="text-sm text-green-400 font-medium">{stat.trend}</span>
              </div>
              <div>
                <p className="text-gray-400 text-sm mb-1">{stat.title}</p>
                <p className="text-3xl font-bold text-white">{stat.value}</p>
              </div>
            </button>
          );
        })}
      </div>

      {/* Content Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Schemas */}
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-6">
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-lg font-semibold text-white flex items-center gap-2">
              <Layers className="w-5 h-5" />
              Recent Schemas
            </h2>
            <button 
              onClick={handleViewAllSchemas}
              className="text-blue-400 hover:text-blue-300 text-sm font-medium flex items-center gap-1 transition-colors"
            >
              View All
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
          <div className="space-y-2">
            {recentSchemas.length > 0 ? (
              recentSchemas.map((schema, index) => (
                <button
                  key={index}
                  onClick={() => handleSchemaClick(schema.entityName)}
                  className="w-full p-4 bg-gray-750 rounded-lg border border-gray-700 hover:border-gray-600 hover:bg-gray-700 transition-all text-left group"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex-1 min-w-0">
                      <h3 className="text-white font-medium mb-1 group-hover:text-blue-400 transition-colors">
                        {schema.entityName}
                      </h3>
                      <p className="text-sm text-gray-400">
                        {schema.fields?.length || 0} fields
                      </p>
                    </div>
                    <div className="flex items-center gap-3 ml-4">
                      {schema.excludeOnFetch && (
                        <span className="text-xs px-2.5 py-1 bg-orange-500/15 text-orange-400 rounded-md border border-orange-500/30 font-medium whitespace-nowrap">
                          Auto-consume
                        </span>
                      )}
                      <div className="flex items-center gap-2 text-gray-500 group-hover:text-gray-400 transition-colors">
                        <Package className="w-5 h-5" />
                        <ChevronRight className="w-4 h-4" />
                      </div>
                    </div>
                  </div>
                </button>
              ))
            ) : (
              <div className="text-center py-12 text-gray-500">
                <Layers className="w-12 h-12 mx-auto mb-3 opacity-50" />
                <p className="mb-4 text-gray-400">No schemas found</p>
                <button
                  onClick={handleCreateSchema}
                  className="text-blue-400 hover:text-blue-300 text-sm font-medium transition-colors"
                >
                  Create your first schema
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Quick Actions */}
        <div className="bg-gray-800 rounded-lg border border-gray-700 p-6">
          <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
            <TrendingUp className="w-5 h-5" />
            Quick Actions
          </h2>
          <div className="space-y-3">
            <button 
              onClick={handleCreateSchema}
              className="w-full p-4 bg-blue-600 hover:bg-blue-700 rounded-lg text-white font-medium transition-colors text-left group"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white/10 rounded-lg">
                    <Database className="w-5 h-5" />
                  </div>
                  <div>
                    <p className="font-semibold">Create New Schema</p>
                    <p className="text-sm text-blue-100">Define a new entity type</p>
                  </div>
                </div>
                <ChevronRight className="w-5 h-5 opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
            </button>
            
            <button 
              onClick={handleManageEnvironments}
              className="w-full p-4 bg-gray-700 hover:bg-gray-600 rounded-lg text-white font-medium transition-colors text-left group"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white/5 rounded-lg">
                    <Server className="w-5 h-5" />
                  </div>
                  <div>
                    <p className="font-semibold">Manage Environments</p>
                    <p className="text-sm text-gray-300">Configure test environments</p>
                  </div>
                </div>
                <ChevronRight className="w-5 h-5 opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
            </button>
            
            <button 
              onClick={handleViewActivity}
              className="w-full p-4 bg-gray-700 hover:bg-gray-600 rounded-lg text-white font-medium transition-colors text-left group"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="p-2 bg-white/5 rounded-lg">
                    <Activity className="w-5 h-5" />
                  </div>
                  <div>
                    <p className="font-semibold">View Activity</p>
                    <p className="text-sm text-gray-300">Check recent operations</p>
                  </div>
                </div>
                <ChevronRight className="w-5 h-5 opacity-0 group-hover:opacity-100 transition-opacity" />
              </div>
            </button>
          </div>
        </div>
      </div>

      {/* System Status */}
      <div className="bg-gray-800 rounded-lg border border-gray-700 p-6">
        <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
          <AlertCircle className="w-5 h-5" />
          System Status
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="flex items-center gap-3 p-3 bg-green-500/10 rounded-lg border border-green-500/20">
            <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></div>
            <div>
              <p className="text-white font-medium">API Service</p>
              <p className="text-sm text-green-400">Operational</p>
            </div>
          </div>
          
          <div className="flex items-center gap-3 p-3 bg-green-500/10 rounded-lg border border-green-500/20">
            <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></div>
            <div>
              <p className="text-white font-medium">Database</p>
              <p className="text-sm text-green-400">Connected</p>
            </div>
          </div>
          
          <div className="flex items-center gap-3 p-3 bg-green-500/10 rounded-lg border border-green-500/20">
            <div className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></div>
            <div>
              <p className="text-white font-medium">Message Bus</p>
              <p className="text-sm text-green-400">Active</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Dashboard;
