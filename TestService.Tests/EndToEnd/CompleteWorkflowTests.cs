using System.Net;
using System.Net.Http.Json;
using TestService.Api.Models;
using TestService.Tests.Infrastructure;

namespace TestService.Tests.EndToEnd;

/// <summary>
/// Complete end-to-end scenarios testing full workflows
/// </summary>
[TestFixture]
public class CompleteWorkflowTests : IntegrationTestBase
{
    [Test]
    public async Task E2E_CreateSchemaAndEntities_PerformCrud_DeleteAll()
    {
        // Step 1: Create Schema
        var entityType = CreateUniqueName("E2E_Product");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(entityType)
            .WithField("name", "string", required: true)
            .WithField("price", "number", required: true)
            .WithField("category", "string")
            .WithField("inStock", "boolean")
            .WithFilterableFields("category", "inStock")
            .Build();

        var createSchemaResponse = await Client.PostAsJsonAsync("/api/schemas", schema);
        AssertStatusCode(createSchemaResponse, HttpStatusCode.Created);
        var createdSchema = await createSchemaResponse.Content.ReadFromJsonAsync<EntitySchema>();
        Assert.That(createdSchema!.EntityName, Is.EqualTo(entityType));

        // Step 2: Create Multiple Entities
        var productIds = new List<string>();
        var products = new[]
        {
            ("Laptop", 999.99, "Electronics", true),
            ("Mouse", 29.99, "Electronics", true),
            ("Desk", 299.99, "Furniture", false),
            ("Chair", 199.99, "Furniture", true)
        };

        foreach (var (name, price, category, inStock) in products)
        {
            var product = new DynamicEntityBuilder()
                .WithField("name", name)
                .WithField("price", price)
                .WithField("category", category)
                .WithField("inStock", inStock)
                .Build();

            var createResponse = await Client.PostAsJsonAsync($"/api/entities/{entityType}", product);
            AssertStatusCode(createResponse, HttpStatusCode.Created);
            
            var created = await createResponse.Content.ReadFromJsonAsync<DynamicEntity>();
            productIds.Add(created!.Id!);
        }

        // Step 3: Retrieve All
        var getAllResponse = await Client.GetAsync($"/api/entities/{entityType}");
        var allProducts = await getAllResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(allProducts!.Count, Is.GreaterThanOrEqualTo(4));

        // Step 4: Filter by Category
        var filterResponse = await Client.GetAsync($"/api/entities/{entityType}/filter/category/Electronics");
        var electronics = await filterResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(electronics!.Count, Is.EqualTo(2));
        Assert.That(electronics.All(p => GetFieldString(p, "category") == "Electronics"), Is.True);

        // Step 5: Update a Product
        var productToUpdate = allProducts.First(p => GetFieldString(p, "name") == "Laptop");
        productToUpdate.Fields["price"] = 899.99; // Price drop!
        productToUpdate.Fields["inStock"] = false; // Out of stock

        var updateResponse = await Client.PutAsJsonAsync(
            $"/api/entities/{entityType}/{productToUpdate.Id}", 
            productToUpdate);
        AssertStatusCode(updateResponse, HttpStatusCode.NoContent);

        // Verify Update
        var updatedProduct = await ApiHelpers.GetEntityByIdAsync(Client, entityType, productToUpdate.Id!);
        Assert.That(GetFieldValue<double>(updatedProduct!, "price"), Is.EqualTo(899.99));
        Assert.That(GetFieldValue<bool>(updatedProduct!, "inStock"), Is.EqualTo(false));

        // Step 6: Delete Products
        foreach (var id in productIds)
        {
            var deleteResponse = await Client.DeleteAsync($"/api/entities/{entityType}/{id}");
            AssertStatusCode(deleteResponse, HttpStatusCode.NoContent);
        }

        // Step 7: Verify Deletion
        var finalGetAllResponse = await Client.GetAsync($"/api/entities/{entityType}");
        var remainingProducts = await finalGetAllResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(remainingProducts!.All(p => !productIds.Contains(p.Id!)), Is.True);

        // Step 8: Delete Schema
        var deleteSchemaResponse = await Client.DeleteAsync($"/api/schemas/{entityType}");
        AssertStatusCode(deleteSchemaResponse, HttpStatusCode.NoContent);
    }

    [Test]
    public async Task E2E_ParallelTestExecution_SimulateRealTestSuite()
    {
        // Scenario: Multiple tests running in parallel need unique test data
        
        // Step 1: Create Schema with ExcludeOnFetch
        var entityType = CreateUniqueName("E2E_TestUser");
        var schema = new EntitySchemaBuilder()
            .WithEntityName(entityType)
            .WithField("username", "string", required: true)
            .WithField("password", "string", required: true)
            .WithField("testCase", "string")
            .WithFilterableField("testCase")
            .WithExcludeOnFetch(true)
            .Build();

        await Client.PostAsJsonAsync("/api/schemas", schema);

        // Step 2: Create Pool of Test Users
        var testCaseId = CreateUniqueId();
        for (int i = 0; i < 10; i++)
        {
            var user = new DynamicEntityBuilder()
                .WithField("username", $"testuser_{i}")
                .WithField("password", "Test@123")
                .WithField("testCase", testCaseId)
                .Build();
            
            await ApiHelpers.CreateEntityAsync(Client, entityType, user);
        }

        // Step 3: Simulate 5 Parallel Tests
        var parallelTests = new List<Task<bool>>();
        for (int i = 0; i < 5; i++)
        {
            parallelTests.Add(SimulateTestAsync(entityType, i));
        }

        var results = await Task.WhenAll(parallelTests);

        // Assert all tests succeeded
        Assert.That(results.All(r => r), Is.True);

        // Step 4: Cleanup - Reset all consumed entities
        var resetResponse = await Client.PostAsync($"/api/entities/{entityType}/reset-all", null);
        AssertStatusCode(resetResponse, HttpStatusCode.OK);

        // Verify all entities are available again
        var availableResponse = await Client.GetAsync($"/api/entities/{entityType}");
        var available = await availableResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(available!.Count, Is.GreaterThanOrEqualTo(10));
    }

    private async Task<bool> SimulateTestAsync(string entityType, int testNumber)
    {
        try
        {
            // Get unique test user
            var response = await Client.GetAsync($"/api/entities/{entityType}/next");
            if (!response.IsSuccessStatusCode)
                return false;

            var user = await response.Content.ReadFromJsonAsync<DynamicEntity>();
            if (user == null)
                return false;

            // Simulate test using this user
            await Task.Delay(100); // Simulate test work

            // Verify user was marked as consumed
            return user.IsConsumed;
        }
        catch
        {
            return false;
        }
    }

    [Test]
    public async Task E2E_MultiTypeDataManagement_ComplexScenario()
    {
        // Scenario: Manage multiple related entity types (Order, Customer, Product)
        
        var uniqueId = CreateUniqueId();
        
        // Step 1: Create Customer Schema and Customer
        var customerType = $"Customer_{uniqueId}";
        var customerSchema = new EntitySchemaBuilder()
            .WithEntityName(customerType)
            .WithField("name", "string", required: true)
            .WithField("email", "string", required: true)
            .WithFilterableField("email")
            .Build();
        
        await Client.PostAsJsonAsync("/api/schemas", customerSchema);
        
        var customer = new DynamicEntityBuilder()
            .WithField("name", "John Doe")
            .WithField("email", "john@example.com")
            .Build();
        
        var createdCustomer = await ApiHelpers.CreateEntityAsync(Client, customerType, customer);

        // Step 2: Create Product Schema and Products
        var productType = $"Product_{uniqueId}";
        var productSchema = new EntitySchemaBuilder()
            .WithEntityName(productType)
            .WithField("name", "string", required: true)
            .WithField("price", "number", required: true)
            .WithFilterableField("name")
            .Build();
        
        await Client.PostAsJsonAsync("/api/schemas", productSchema);
        
        var products = new List<DynamicEntity>();
        foreach (var (name, price) in new[] { ("Widget", 19.99), ("Gadget", 29.99) })
        {
            var product = new DynamicEntityBuilder()
                .WithField("name", name)
                .WithField("price", price)
                .Build();
            
            products.Add((await ApiHelpers.CreateEntityAsync(Client, productType, product))!);
        }

        // Step 3: Create Order Schema and Order
        var orderType = $"Order_{uniqueId}";
        var orderSchema = new EntitySchemaBuilder()
            .WithEntityName(orderType)
            .WithField("customerId", "string", required: true)
            .WithField("productIds", "string", required: true)
            .WithField("totalAmount", "number", required: true)
            .WithFilterableField("customerId")
            .Build();
        
        await Client.PostAsJsonAsync("/api/schemas", orderSchema);
        
        var order = new DynamicEntityBuilder()
            .WithField("customerId", createdCustomer!.Id!)
            .WithField("productIds", string.Join(",", products.Select(p => p.Id)))
            .WithField("totalAmount", 49.98)
            .Build();
        
        var createdOrder = await ApiHelpers.CreateEntityAsync(Client, orderType, order);

        // Step 4: Query Order by Customer
        var orderResponse = await Client.GetAsync(
            $"/api/entities/{orderType}/filter/customerId/{createdCustomer.Id}");
        var customerOrders = await orderResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        
        Assert.That(customerOrders!.Count, Is.GreaterThan(0));
        Assert.That(GetFieldValue<double>(customerOrders[0], "totalAmount"), Is.EqualTo(49.98));

        // Step 5: Cleanup
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, orderType);
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, productType);
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, customerType);
    }

    [Test]
    public async Task E2E_SchemaEvolution_UpdateFieldsAndMigrate()
    {
        // Scenario: Schema evolves over time, adding new fields
        
        var entityType = CreateUniqueName("E2E_Evolution");
        
        // Step 1: Create Initial Schema (V1)
        var schemaV1 = new EntitySchemaBuilder()
            .WithEntityName(entityType)
            .WithField("name", "string", required: true)
            .WithField("version", "number")
            .Build();
        
        await Client.PostAsJsonAsync("/api/schemas", schemaV1);

        // Step 2: Create Entities with V1 Schema
        var v1Entities = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"Entity_{i}")
                .WithField("version", 1)
                .Build();
            
            var created = await ApiHelpers.CreateEntityAsync(Client, entityType, entity);
            v1Entities.Add(created!.Id!);
        }

        // Step 3: Update Schema (V2) - Add new fields
        schemaV1.Fields.Add(new FieldDefinition { Name = "description", Type = "string" });
        schemaV1.Fields.Add(new FieldDefinition { Name = "createdBy", Type = "string" });
        schemaV1.FilterableFields.Add("version");
        
        var updateResponse = await Client.PutAsJsonAsync($"/api/schemas/{entityType}", schemaV1);
        AssertStatusCode(updateResponse, HttpStatusCode.NoContent);

        // Step 4: Create New Entities with V2 Schema
        var v2Entity = new DynamicEntityBuilder()
            .WithField("name", "V2_Entity")
            .WithField("version", 2)
            .WithField("description", "New field added")
            .WithField("createdBy", "admin")
            .Build();
        
        var createdV2 = await ApiHelpers.CreateEntityAsync(Client, entityType, v2Entity);

        // Step 5: Migrate V1 Entities to V2
        foreach (var id in v1Entities)
        {
            var entity = await ApiHelpers.GetEntityByIdAsync(Client, entityType, id);
            entity!.Fields["version"] = 2;
            entity.Fields["description"] = "Migrated from V1";
            entity.Fields["createdBy"] = "migration-script";
            
            await ApiHelpers.UpdateEntityAsync(Client, entityType, id, entity);
        }

        // Step 6: Verify All Entities Are V2
        var allEntities = await ApiHelpers.GetAllEntitiesAsync(Client, entityType);
        Assert.That(allEntities!.All(e => GetFieldValue<int>(e, "version") == 2), Is.True);
        Assert.That(allEntities.All(e => e.Fields.ContainsKey("description")), Is.True);

        // Cleanup
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, entityType);
    }

    [Test]
    public async Task E2E_BulkDataOperations_CreateFilterUpdateDelete()
    {
        // Scenario: Bulk operations for test data management
        
        var entityType = CreateUniqueName("E2E_Bulk");
        
        // Step 1: Create Schema
        var schema = new EntitySchemaBuilder()
            .WithEntityName(entityType)
            .WithField("name", "string", required: true)
            .WithField("status", "string")
            .WithField("priority", "number")
            .WithFilterableFields("status", "priority")
            .Build();
        
        await Client.PostAsJsonAsync("/api/schemas", schema);

        // Step 2: Bulk Create (50 entities)
        var entityIds = new List<string>();
        var statuses = new[] { "active", "pending", "completed" };
        
        for (int i = 0; i < 50; i++)
        {
            var entity = new DynamicEntityBuilder()
                .WithField("name", $"BulkEntity_{i}")
                .WithField("status", statuses[i % 3])
                .WithField("priority", (i % 5) + 1)
                .Build();
            
            var created = await ApiHelpers.CreateEntityAsync(Client, entityType, entity);
            entityIds.Add(created!.Id!);
        }

        // Step 3: Filter by Status
        var activeResponse = await Client.GetAsync($"/api/entities/{entityType}/filter/status/active");
        var activeEntities = await activeResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(activeEntities!.Count, Is.GreaterThan(0));

        // Step 4: Bulk Update (Change all 'pending' to 'active')
        var pendingResponse = await Client.GetAsync($"/api/entities/{entityType}/filter/status/pending");
        var pendingEntities = await pendingResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        
        foreach (var entity in pendingEntities!)
        {
            entity.Fields["status"] = "active";
            await ApiHelpers.UpdateEntityAsync(Client, entityType, entity.Id!, entity);
        }

        // Step 5: Verify Update
        var verifyResponse = await Client.GetAsync($"/api/entities/{entityType}/filter/status/pending");
        var remainingPending = await verifyResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(remainingPending!.Count, Is.EqualTo(0));

        // Step 6: Bulk Delete (Delete first 25)
        foreach (var id in entityIds.Take(25))
        {
            await ApiHelpers.DeleteEntityAsync(Client, entityType, id);
        }

        // Step 7: Verify Remaining Count
        var finalResponse = await Client.GetAsync($"/api/entities/{entityType}");
        var finalEntities = await finalResponse.Content.ReadFromJsonAsync<List<DynamicEntity>>();
        Assert.That(finalEntities!.Count, Is.GreaterThanOrEqualTo(25));

        // Cleanup
        await ApiHelpers.DeleteSchemaIfExistsAsync(Client, entityType);
    }
}
