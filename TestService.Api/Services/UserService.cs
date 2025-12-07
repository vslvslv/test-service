using TestService.Api.Models;
using TestService.Api.Configuration;

namespace TestService.Api.Services;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(string id);
    Task<UserResponse?> GetByUsernameAsync(string username);
    Task<IEnumerable<UserResponse>> GetAllAsync();
    Task<UserResponse> CreateAsync(CreateUserRequest request);
    Task<bool> UpdateAsync(string id, UpdateUserRequest request);
    Task<bool> DeleteAsync(string id);
    Task<bool> ChangePasswordAsync(string id, ChangePasswordRequest request);
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task InitializeDefaultAdminAsync();
}

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<UserService> _logger;
    private readonly JwtSettings _jwtSettings;

    public UserService(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<UserService> logger,
        JwtSettings jwtSettings)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
        _jwtSettings = jwtSettings;
    }

    public async Task<UserResponse?> GetByIdAsync(string id)
    {
        var user = await _repository.GetByIdAsync(id);
        return user != null ? UserResponse.FromUser(user) : null;
    }

    public async Task<UserResponse?> GetByUsernameAsync(string username)
    {
        var user = await _repository.GetByUsernameAsync(username);
        return user != null ? UserResponse.FromUser(user) : null;
    }

    public async Task<IEnumerable<UserResponse>> GetAllAsync()
    {
        var users = await _repository.GetAllAsync();
        return users.Select(UserResponse.FromUser);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request)
    {
        // Validate username and email uniqueness
        if (await _repository.UsernameExistsAsync(request.Username))
        {
            throw new InvalidOperationException($"Username '{request.Username}' already exists");
        }

        if (await _repository.EmailExistsAsync(request.Email))
        {
            throw new InvalidOperationException($"Email '{request.Email}' already exists");
        }

        // Validate password strength
        ValidatePassword(request.Password);

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role,
            IsActive = true
        };

        var created = await _repository.CreateAsync(user);
        _logger.LogInformation("User created: {Username} with role {Role}", created.Username, created.Role);

        return UserResponse.FromUser(created);
    }

    public async Task<bool> UpdateAsync(string id, UpdateUserRequest request)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
        {
            return false;
        }

        // Check email uniqueness if changed
        if (request.Email != null && request.Email != user.Email)
        {
            if (await _repository.EmailExistsAsync(request.Email))
            {
                throw new InvalidOperationException($"Email '{request.Email}' already exists");
            }
            user.Email = request.Email;
        }

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Role.HasValue) user.Role = request.Role.Value;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        var result = await _repository.UpdateAsync(id, user);
        
        if (result)
        {
            _logger.LogInformation("User updated: {Username}", user.Username);
        }

        return result;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
        {
            return false;
        }

        // Prevent deleting the last admin
        if (user.Role == UserRole.Admin)
        {
            var allUsers = await _repository.GetAllAsync();
            var adminCount = allUsers.Count(u => u.Role == UserRole.Admin && u.IsActive);
            if (adminCount <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last admin user");
            }
        }

        var result = await _repository.DeleteAsync(id);
        
        if (result)
        {
            _logger.LogInformation("User deleted: {Username}", user.Username);
        }

        return result;
    }

    public async Task<bool> ChangePasswordAsync(string id, ChangePasswordRequest request)
    {
        var user = await _repository.GetByIdAsync(id);
        if (user == null)
        {
            return false;
        }

        // Verify current password
        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Current password is incorrect");
        }

        // Validate new password
        ValidatePassword(request.NewPassword);

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        var result = await _repository.UpdateAsync(id, user);

        if (result)
        {
            _logger.LogInformation("Password changed for user: {Username}", user.Username);
        }

        return result;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _repository.GetByUsernameAsync(request.Username);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Username}", request.Username);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user: {Username}", request.Username);
            return null;
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", request.Username);
            return null;
        }

        // Update last login
        await _repository.UpdateLastLoginAsync(user.Id!);

        // Generate token
        var token = _tokenService.GenerateToken(user);

        _logger.LogInformation("User logged in: {Username}", user.Username);

        return new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes)
        };
    }

    public async Task InitializeDefaultAdminAsync()
    {
        // Check if any users exist
        var users = await _repository.GetAllAsync();
        if (users.Any())
        {
            _logger.LogInformation("Users already exist, skipping default admin creation");
            return;
        }

        // Create default admin
        var defaultAdmin = new User
        {
            Username = "admin",
            Email = "admin@testservice.local",
            PasswordHash = _passwordHasher.HashPassword("Admin@123"),
            FirstName = "System",
            LastName = "Administrator",
            Role = UserRole.Admin,
            IsActive = true
        };

        await _repository.CreateAsync(defaultAdmin);
        _logger.LogInformation("Default admin user created - Username: admin, Password: Admin@123");
        _logger.LogWarning("??  IMPORTANT: Change the default admin password immediately!");
    }

    private void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password cannot be empty");
        }

        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            throw new ArgumentException("Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            throw new ArgumentException("Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            throw new ArgumentException("Password must contain at least one digit");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new ArgumentException("Password must contain at least one special character");
        }
    }
}
