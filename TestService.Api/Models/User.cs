using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TestService.Api.Models;

/// <summary>
/// User model for authentication and authorization
/// </summary>
public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("firstName")]
    public string? FirstName { get; set; }

    [BsonElement("lastName")]
    public string? LastName { get; set; }

    [BsonElement("role")]
    public UserRole Role { get; set; } = UserRole.Contributor;

    [BsonElement("customPermissions")]
    public List<string> CustomPermissions { get; set; } = new();

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// User roles for role-based access control
/// </summary>
public enum UserRole
{
    Contributor = 0,
    Admin = 1
}

/// <summary>
/// DTO for user creation
/// </summary>
public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public UserRole Role { get; set; } = UserRole.Contributor;
    public List<string>? CustomPermissions { get; set; }
}

/// <summary>
/// DTO for user update
/// </summary>
public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? CustomPermissions { get; set; }
}

/// <summary>
/// DTO for password change
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// DTO for login
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO for login response
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Reason for login failure when credentials are invalid
/// </summary>
public enum LoginFailureReason
{
    /// <summary>User not found or account is inactive</summary>
    UserNotFoundOrInactive,
    /// <summary>User exists but password is wrong</summary>
    InvalidPassword
}

/// <summary>
/// Result of a login attempt; either success with response or failure with reason
/// </summary>
public class LoginResult
{
    public bool IsSuccess => Response != null;
    public LoginResponse? Response { get; set; }
    public LoginFailureReason? FailureReason { get; set; }

    public static LoginResult Success(LoginResponse response) =>
        new() { Response = response };

    public static LoginResult Fail(LoginFailureReason reason) =>
        new() { FailureReason = reason };
}

/// <summary>
/// DTO for user response (without sensitive data)
/// </summary>
public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public UserRole Role { get; set; }
    public List<string> Permissions { get; set; } = new();
    public List<string> CustomPermissions { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public static UserResponse FromUser(User user)
    {
        return new UserResponse
        {
            Id = user.Id ?? string.Empty,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Permissions = PermissionDefinitions.GetEffectivePermissions(user).ToList(),
            CustomPermissions = user.CustomPermissions,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}

public class PermissionDescriptor
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

public static class PermissionDefinitions
{
    public const string DashboardRead = "dashboard.read";
    public const string SchemasRead = "schemas.read";
    public const string SchemasWrite = "schemas.write";
    public const string SchemasDelete = "schemas.delete";
    public const string EntitiesRead = "entities.read";
    public const string EntitiesWrite = "entities.write";
    public const string EntitiesDelete = "entities.delete";
    public const string EntitiesReset = "entities.reset";
    public const string EnvironmentsRead = "environments.read";
    public const string EnvironmentsWrite = "environments.write";
    public const string EnvironmentsDelete = "environments.delete";
    public const string ActivityRead = "activity.read";
    public const string SettingsRead = "settings.read";
    public const string SettingsWrite = "settings.write";
    public const string ApiKeysRead = "settings.api_keys.read";
    public const string ApiKeysCreate = "settings.api_keys.create";
    public const string ApiKeysDelete = "settings.api_keys.delete";
    public const string UsersRead = "users.read";
    public const string UsersCreate = "users.create";
    public const string UsersUpdate = "users.update";
    public const string UsersDelete = "users.delete";
    public const string UsersPermissionsManage = "users.permissions.manage";
    public const string MocksRead = "mocks.read";
    public const string MocksWrite = "mocks.write";
    public const string MocksVerify = "mocks.verify";
    public const string MocksLogsRead = "mocks.logs.read";
    public const string MocksLogsDelete = "mocks.logs.delete";

    private static readonly List<PermissionDescriptor> Catalog =
    [
        new() { Key = DashboardRead, Description = "View dashboard and system overview", Group = "General" },
        new() { Key = SchemasRead, Description = "View schema definitions", Group = "Schemas" },
        new() { Key = SchemasWrite, Description = "Create and update schemas", Group = "Schemas" },
        new() { Key = SchemasDelete, Description = "Delete schemas and schema entities", Group = "Schemas" },
        new() { Key = EntitiesRead, Description = "View entities", Group = "Entities" },
        new() { Key = EntitiesWrite, Description = "Create and update entities", Group = "Entities" },
        new() { Key = EntitiesDelete, Description = "Delete entities", Group = "Entities" },
        new() { Key = EntitiesReset, Description = "Reset consumed entities", Group = "Entities" },
        new() { Key = EnvironmentsRead, Description = "View environments", Group = "Environments" },
        new() { Key = EnvironmentsWrite, Description = "Create and update environments", Group = "Environments" },
        new() { Key = EnvironmentsDelete, Description = "Delete environments", Group = "Environments" },
        new() { Key = ActivityRead, Description = "View activity log", Group = "Activity" },
        new() { Key = SettingsRead, Description = "View settings", Group = "Settings" },
        new() { Key = SettingsWrite, Description = "Update settings", Group = "Settings" },
        new() { Key = ApiKeysRead, Description = "View API keys", Group = "Settings" },
        new() { Key = ApiKeysCreate, Description = "Create API keys", Group = "Settings" },
        new() { Key = ApiKeysDelete, Description = "Delete API keys", Group = "Settings" },
        new() { Key = UsersRead, Description = "View users", Group = "Users" },
        new() { Key = UsersCreate, Description = "Create users", Group = "Users" },
        new() { Key = UsersUpdate, Description = "Update users", Group = "Users" },
        new() { Key = UsersDelete, Description = "Delete users", Group = "Users" },
        new() { Key = UsersPermissionsManage, Description = "Manage user custom permissions", Group = "Users" },
        new() { Key = MocksRead, Description = "View mock expectations", Group = "Mocks" },
        new() { Key = MocksWrite, Description = "Create/update/delete mock expectations", Group = "Mocks" },
        new() { Key = MocksVerify, Description = "Verify mock requests", Group = "Mocks" },
        new() { Key = MocksLogsRead, Description = "View mock request logs", Group = "Mocks" },
        new() { Key = MocksLogsDelete, Description = "Delete mock request logs", Group = "Mocks" }
    ];

    private static readonly HashSet<string> ValidPermissionKeys = Catalog.Select(x => x.Key).ToHashSet();

    public static IEnumerable<PermissionDescriptor> GetCatalog() => Catalog;

    public static IEnumerable<string> GetRolePermissions(UserRole role)
    {
        if (role == UserRole.Admin)
        {
            return ValidPermissionKeys;
        }

        return
        [
            DashboardRead,
            SchemasRead,
            SchemasWrite,
            SchemasDelete,
            EntitiesRead,
            EntitiesWrite,
            EntitiesDelete,
            EntitiesReset,
            EnvironmentsRead,
            ActivityRead
        ];
    }

    public static IEnumerable<string> SanitizeCustomPermissions(IEnumerable<string>? customPermissions)
    {
        if (customPermissions == null) return [];

        return customPermissions
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(ValidPermissionKeys.Contains)
            .Distinct()
            .OrderBy(x => x);
    }

    public static IEnumerable<string> GetEffectivePermissions(User user)
    {
        var rolePermissions = GetRolePermissions(user.Role);
        var customPermissions = SanitizeCustomPermissions(user.CustomPermissions);
        return rolePermissions.Union(customPermissions).Distinct().OrderBy(x => x);
    }
}
