using System.Text;

namespace TestService.Unit;

[TestFixture]
public class PostmanImportServiceTests
{
    private static IReadOnlyList<MockExpectation> Parse(string json) =>
        new PostmanImportService().ParseCollection(new MemoryStream(Encoding.UTF8.GetBytes(json)), "env");

    [Test]
    public void ParseCollection_ItemWithSavedResponse_UsesItsStatusHeadersAndBody()
    {
        var expectations = Parse("""
            {
              "info": { "name": "c", "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json" },
              "item": [
                {
                  "name": "Create Foo",
                  "request": { "method": "POST", "url": { "raw": "https://example.com/foo", "path": ["foo"] } },
                  "response": [
                    {
                      "name": "created",
                      "code": 201,
                      "header": [ { "key": "X-Custom", "value": "yes" } ],
                      "body": "{\"id\":7}"
                    }
                  ]
                }
              ]
            }
            """);

        Assert.That(expectations, Has.Count.EqualTo(1));
        var template = expectations[0].ResponseTemplate;
        Assert.That(template.Status, Is.EqualTo(201));
        Assert.That(template.Body, Is.EqualTo("{\"id\":7}"));
        Assert.That(template.Headers["X-Custom"], Is.EqualTo("yes"));
    }

    [Test]
    public void ParseCollection_SavedResponseTransportHeaders_AreNotCopied()
    {
        var expectations = Parse("""
            {
              "item": [
                {
                  "name": "Get Foo",
                  "request": { "method": "GET", "url": { "raw": "https://example.com/foo", "path": ["foo"] } },
                  "response": [
                    {
                      "code": 200,
                      "header": [
                        { "key": "Content-Type", "value": "application/json" },
                        { "key": "content-length", "value": "123" },
                        { "key": "Content-Encoding", "value": "gzip" },
                        { "key": "Transfer-Encoding", "value": "chunked" },
                        { "key": "Connection", "value": "keep-alive" },
                        { "key": "X-Custom", "value": "yes" }
                      ],
                      "body": "{}"
                    }
                  ]
                }
              ]
            }
            """);

        Assert.That(expectations[0].ResponseTemplate.Headers.Keys, Is.EquivalentTo(new[] { "Content-Type", "X-Custom" }));
    }

    [Test]
    public void ParseCollection_ItemWithoutSavedResponse_Defaults200AndEmptyBody()
    {
        var expectations = Parse("""
            {
              "item": [
                { "name": "Get Foo", "request": { "method": "GET", "url": { "raw": "https://example.com/foo", "path": ["foo"] } } }
              ]
            }
            """);

        Assert.That(expectations, Has.Count.EqualTo(1));
        var template = expectations[0].ResponseTemplate;
        Assert.That(template.Status, Is.EqualTo(200));
        Assert.That(template.Body, Is.Empty);
        Assert.That(template.Headers, Is.Empty);
    }
}
