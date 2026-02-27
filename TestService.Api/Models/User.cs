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
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
