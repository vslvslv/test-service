namespace TestService.Api.Configuration;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "TestServiceDb";
    public string CollectionName { get; set; } = "TestData";
}
