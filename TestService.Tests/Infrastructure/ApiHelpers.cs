using System.Net.Http.Json;
using TestService.Api.Models;

namespace TestService.Tests.Infrastructure;

/// <summary>
/// Helper methods for API operations
/// </summary>
public static class ApiHelpers
{
    /// <summary>
    /// Creates a schema and returns the created schema
    /// </summary>
    public static async Task<EntitySchema?> CreateSchemaAsync(HttpClient client, EntitySchema schema)
    {
        var response = await client.PostAsJsonAsync("/api/schemas", schema);
        
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // Schema already exists, get it
            var getResponse = await client.GetAsync($"/api/schemas/{schema.EntityName}");
            return await getResponse.Content.ReadFromJsonAsync<EntitySchema>();
        }
        
        return await response.Content.ReadFromJsonAsync<EntitySchema>();
    }

    /// <summary>
    /// Creates an entity and returns the created entity
    /// </summary>
    public static async Task<DynamicEntity?> CreateEntityAsync(HttpClient client, string entityType, DynamicEntity entity)
    {
        var response = await client.PostAsJsonAsync($"/api/entities/{entityType}", entity);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DynamicEntity>();
    }

    /// <summary>
    /// Creates multiple entities
    /// </summary>
    public static async Task<List<DynamicEntity>> CreateEntitiesAsync(HttpClient client, string entityType, params DynamicEntity[] entities)
    {
        var created = new List<DynamicEntity>();
        foreach (var entity in entities)
        {
            var result = await CreateEntityAsync(client, entityType, entity);
            if (result != null)
            {
                created.Add(result);
            }
        }
        return created;
    }

    /// <summary>
    /// Resets all consumed entities of a type
    /// </summary>
    public static async Task<int> ResetAllConsumedAsync(HttpClient client, string entityType)
    {
        var response = await client.PostAsync($"/api/entities/{entityType}/reset-all", null);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        if (result != null && result.TryGetValue("resetCount", out var count))
        {
            return Convert.ToInt32(count);
        }
        return 0;
    }

    /// <summary>
    /// Deletes a schema if it exists
    /// </summary>
    public static async Task<bool> DeleteSchemaIfExistsAsync(HttpClient client, string entityName)
    {
        var response = await client.DeleteAsync($"/api/schemas/{entityName}");
        return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
    }

    /// <summary>
    /// Gets all entities of a type
    /// </summary>
    public static async Task<List<DynamicEntity>?> GetAllEntitiesAsync(HttpClient client, string entityType)
    {
        var response = await client.GetAsync($"/api/entities/{entityType}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<List<DynamicEntity>>();
    }

    /// <summary>
    /// Gets an entity by ID
    /// </summary>
    public static async Task<DynamicEntity?> GetEntityByIdAsync(HttpClient client, string entityType, string id)
    {
        var response = await client.GetAsync($"/api/entities/{entityType}/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<DynamicEntity>();
    }

    /// <summary>
    /// Updates an entity
    /// </summary>
    public static async Task<bool> UpdateEntityAsync(HttpClient client, string entityType, string id, DynamicEntity entity)
    {
        var response = await client.PutAsJsonAsync($"/api/entities/{entityType}/{id}", entity);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Deletes an entity
    /// </summary>
    public static async Task<bool> DeleteEntityAsync(HttpClient client, string entityType, string id)
    {
        var response = await client.DeleteAsync($"/api/entities/{entityType}/{id}");
        return response.IsSuccessStatusCode;
    }
}
