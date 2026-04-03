using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TestService.Api.Models;

namespace TestService.Api.Services;

public interface IEntityImportExportService
{
    Task<byte[]> ExportAsJsonAsync(string entityType, string? environment = null);
    Task<byte[]> ExportAsCsvAsync(string entityType, string? environment = null);
    Task<EntityImportResult> ImportAsync(string entityType, Stream fileStream, string? fileName, string? environment, string mode, string? user = null);
}

public class EntityImportExportService : IEntityImportExportService
{
    private const int MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private const int MaxRows = 5000;

    private readonly IDynamicEntityService _entityService;
    private readonly IEntitySchemaRepository _schemaRepository;
    private readonly IActivityService _activityService;
    private readonly ILogger<EntityImportExportService> _logger;

    public EntityImportExportService(
        IDynamicEntityService entityService,
        IEntitySchemaRepository schemaRepository,
        IActivityService activityService,
        ILogger<EntityImportExportService> logger)
    {
        _entityService = entityService;
        _schemaRepository = schemaRepository;
        _activityService = activityService;
        _logger = logger;
    }

    public async Task<byte[]> ExportAsJsonAsync(string entityType, string? environment = null)
    {
        var entities = (await _entityService.GetAllAsync(entityType, environment)).ToList();
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Encoding.UTF8.GetBytes(json);
    }

    public async Task<byte[]> ExportAsCsvAsync(string entityType, string? environment = null)
    {
        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema == null)
            throw new ArgumentException($"Entity type '{entityType}' not found.");

        var entities = (await _entityService.GetAllAsync(entityType, environment)).ToList();
        var fieldNames = schema.Fields.Select(f => f.Name).ToList();
        var hasEnvironment = entities.Any(e => !string.IsNullOrEmpty(e.Environment));
        if (hasEnvironment)
            fieldNames.Add("environment");

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", fieldNames.Select(EscapeCsv)));

        foreach (var entity in entities)
        {
            var values = new List<string>();
            foreach (var name in schema.Fields.Select(f => f.Name))
            {
                var v = entity.Fields.TryGetValue(name, out var val) && val != null ? val.ToString() ?? "" : "";
                values.Add(EscapeCsv(v));
            }
            if (hasEnvironment)
                values.Add(EscapeCsv(entity.Environment ?? ""));
            sb.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<EntityImportResult> ImportAsync(string entityType, Stream fileStream, string? fileName, string? environment, string mode, string? user = null)
    {
        var result = new EntityImportResult();
        var isUpsert = string.Equals(mode, "upsert", StringComparison.OrdinalIgnoreCase);

        var schema = await _schemaRepository.GetSchemaByNameAsync(entityType);
        if (schema == null)
        {
            result.Errors.Add(new EntityImportError { Row = 0, Message = $"Entity type '{entityType}' not found." });
            return result;
        }

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        var length = ms.Length;
        if (length > MaxFileSizeBytes)
        {
            result.Errors.Add(new EntityImportError { Row = 0, Message = $"File size exceeds maximum of {MaxFileSizeBytes / 1024 / 1024} MB." });
            return result;
        }

        ms.Position = 0;
        List<Dictionary<string, object?>> rows;
        var isJson = fileName != null && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        if (isJson)
        {
            try
            {
                rows = await ParseJsonRowsAsync(ms);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new EntityImportError { Row = 0, Message = $"Invalid JSON: {ex.Message}" });
                return result;
            }
        }
        else
        {
            try
            {
                rows = ParseCsvRows(ms, schema);
            }
            catch (Exception ex)
            {
                result.Errors.Add(new EntityImportError { Row = 0, Message = $"Invalid CSV: {ex.Message}" });
                return result;
            }
        }

        if (rows.Count > MaxRows)
        {
            result.Errors.Add(new EntityImportError { Row = 0, Message = $"Row count exceeds maximum of {MaxRows}." });
            return result;
        }

        IEnumerable<DynamicEntity>? existingForUpsert = null;
        if (isUpsert && !string.IsNullOrEmpty(environment))
            existingForUpsert = (await _entityService.GetAllAsync(entityType, environment)).ToList();

        var uniqueFieldNames = schema.Fields.Where(f => f.IsUnique).Select(f => f.Name).ToList();
        var compoundUnique = schema.UseCompoundUnique && schema.UniqueFields.Count > 0 ? schema.UniqueFields : null;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowIndex = i + 1;
            try
            {
                var row = rows[i];
                var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                string? rowEnv = environment;

                if (row.TryGetValue("environment", out var envVal) && envVal != null && !string.IsNullOrWhiteSpace(envVal.ToString()))
                    rowEnv = envVal.ToString()!.Trim();

                foreach (var fieldDef in schema.Fields)
                {
                    if (!row.TryGetValue(fieldDef.Name, out var cell))
                        continue;
                    if (cell == null || (cell is string s && string.IsNullOrWhiteSpace(s)))
                    {
                        if (fieldDef.Required)
                        {
                            result.Errors.Add(new EntityImportError { Row = rowIndex, Message = $"Required field '{fieldDef.Name}' is missing or empty." });
                            result.Skipped++;
                            goto nextRow;
                        }
                        continue;
                    }
                    var parsed = ParseFieldValue(cell, fieldDef.Type);
                    if (parsed == null && fieldDef.Required)
                    {
                        result.Errors.Add(new EntityImportError { Row = rowIndex, Message = $"Invalid value for required field '{fieldDef.Name}'." });
                        result.Skipped++;
                        goto nextRow;
                    }
                    if (parsed != null)
                        fields[fieldDef.Name] = parsed;
                }

                foreach (var required in schema.Fields.Where(f => f.Required))
                {
                    if (!fields.ContainsKey(required.Name))
                    {
                        result.Errors.Add(new EntityImportError { Row = rowIndex, Message = $"Required field '{required.Name}' is missing." });
                        result.Skipped++;
                        goto nextRow;
                    }
                }

                var entity = new DynamicEntity
                {
                    EntityType = entityType,
                    Environment = rowEnv,
                    Fields = fields
                };

                if (!await _entityService.ValidateEntityAsync(entityType, entity))
                {
                    result.Errors.Add(new EntityImportError { Row = rowIndex, Message = "Entity does not match schema (e.g. duplicate unique value or invalid type)." });
                    result.Skipped++;
                    goto nextRow;
                }

                if (isUpsert && existingForUpsert != null)
                {
                    DynamicEntity? match = null;
                    if (compoundUnique != null && compoundUnique.Count > 0)
                    {
                        match = existingForUpsert.FirstOrDefault(e =>
                            compoundUnique.All(uf => FieldsEqual(e.Fields.GetValueOrDefault(uf), entity.Fields.GetValueOrDefault(uf))));
                    }
                    else if (uniqueFieldNames.Count > 0)
                    {
                        var firstUnique = uniqueFieldNames[0];
                        if (entity.Fields.TryGetValue(firstUnique, out var keyVal))
                            match = existingForUpsert.FirstOrDefault(e => FieldsEqual(e.Fields.GetValueOrDefault(firstUnique), keyVal));
                    }

                    if (match != null && !string.IsNullOrEmpty(match.Id))
                    {
                        entity.Id = match.Id;
                        entity.IsConsumed = match.IsConsumed;
                        entity.CreatedAt = match.CreatedAt;
                        await _entityService.UpdateAsync(entityType, match.Id, entity);
                        result.Updated++;
                    }
                    else
                    {
                        await _entityService.CreateAsync(entityType, entity);
                        result.Created++;
                    }
                }
                else
                {
                    await _entityService.CreateAsync(entityType, entity);
                    result.Created++;
                }
            }
            catch (ArgumentException ex)
            {
                result.Errors.Add(new EntityImportError { Row = rowIndex, Message = ex.Message });
                result.Skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Import row {Row} failed", rowIndex);
                result.Errors.Add(new EntityImportError { Row = rowIndex, Message = ex.Message });
                result.Skipped++;
            }

        nextRow: ;
        }

        if (result.Created + result.Updated > 0)
        {
            try
            {
                var activityUser = user ?? "bulk-import";
                await _activityService.LogActivityAsync(
                    ActivityType.Entity,
                    ActivityAction.Created,
                    activityUser,
                    $"Bulk import: {result.Created} created, {result.Updated} updated for {entityType}",
                    entityType,
                    null,
                    environment,
                    new ActivityDetails { Count = result.Created + result.Updated });
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Activity logging failed after bulk import");
            }
        }

        return result;
    }

    private static bool FieldsEqual(object? a, object? b)
    {
        var sa = a?.ToString() ?? "";
        var sb = b?.ToString() ?? "";
        return string.Equals(sa, sb, StringComparison.Ordinal);
    }

    private static async Task<List<Dictionary<string, object?>>> ParseJsonRowsAsync(Stream stream)
    {
        using var doc = await JsonDocument.ParseAsync(stream);
        var list = new List<Dictionary<string, object?>>();
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            var single = FlattenJsonObject(doc.RootElement);
            if (single.Count > 0)
                list.Add(single);
            return list;
        }
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dict = FlattenJsonObject(item);
            list.Add(dict);
        }
        return list;
    }

    private static Dictionary<string, object?> FlattenJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
            return dict;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Equals("fields", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in prop.Value.EnumerateObject())
                    dict[sub.Name] = JsonElementToObject(sub.Value);
                continue;
            }
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }

    private List<Dictionary<string, object?>> ParseCsvRows(Stream stream, EntitySchema schema)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        if (lines.Count < 1)
            return new List<Dictionary<string, object?>>();

        var headers = ParseCsvLine(lines[0]);

        var rows = new List<Dictionary<string, object?>>();
        for (var i = 1; i < lines.Count; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Count; j++)
            {
                var header = headers[j];
                var value = j < values.Count ? values[j] : "";
                if (!string.IsNullOrWhiteSpace(header))
                    row[header] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                    inQuotes = !inQuotes;
            }
            else if ((c == ',' && !inQuotes) || c == '\n' || c == '\r')
            {
                list.Add(current.ToString().Trim());
                current.Clear();
                if (c == '\r' && i + 1 < line.Length && line[i + 1] == '\n')
                    i++;
            }
            else
                current.Append(c);
        }
        list.Add(current.ToString().Trim());
        return list;
    }

    private static object? ParseFieldValue(object cell, string fieldType)
    {
        var s = cell?.ToString()?.Trim();
        if (string.IsNullOrEmpty(s))
            return null;

        return fieldType.ToLowerInvariant() switch
        {
            "number" or "integer" => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            "boolean" or "bool" => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1" ? true : s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0" ? false : null,
            "date" or "datetime" => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt) ? dt : null,
            _ => s
        };
    }

    private static string EscapeCsv(string value)
    {
        if (value == null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
