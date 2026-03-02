namespace TestService.Api.Models;

/// <summary>
/// Result of a bulk entity import operation.
/// </summary>
public class EntityImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<EntityImportError> Errors { get; set; } = new();
}

/// <summary>
/// Per-row error from entity import.
/// </summary>
public class EntityImportError
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}
