using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using TestService.Api.Configuration;
using TestService.Api.Services;
using TestService.Api.Hubs;
using TestService.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure MongoDB serialization to always serialize boolean fields
if (!BsonClassMap.IsClassMapRegistered(typeof(FieldDefinition)))
{
    BsonClassMap.RegisterClassMap<FieldDefinition>(cm =>
    {
        cm.AutoMap();
        cm.MapMember(c => c.IsUnique)
          .SetDefaultValue(false)
          .SetShouldSerializeMethod(obj => true); // Always serialize
    });
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebUI", policy =>
    {
        var allowedOrigins = new List<string>
        {
            "http://localhost:3000", 
            "http://localhost:5173",
            "http://qa2-env01.cloudad.local",
            "https://vslvslv.github.io",
            "https://vslvslv.github.io/" // Also allow with trailing slash
        };
        
        // Allow custom origin from env var (e.g., other GitHub Pages or custom domains)
        var customOrigin = System.Environment.GetEnvironmentVariable("ALLOWED_ORIGIN");
        if (!string.IsNullOrWhiteSpace(customOrigin))
        {
            allowedOrigins.Add(customOrigin);
        }
        
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("*");
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add configuration
var mongoDbSettings = new MongoDbSettings();
builder.Configuration.GetSection("MongoDbSettings").Bind(mongoDbSettings);

// Log environment for debugging
Console.WriteLine("[INFO] MongoDB Configuration:");
Console.WriteLine($"[INFO]   Default ConnectionString: {(mongoDbSettings.ConnectionString?.Substring(0, 20) ?? "null")}...");
Console.WriteLine($"[INFO]   Database: {mongoDbSettings.DatabaseName}");

// Allow platform-provided env vars (e.g., Railway) to override the connection string
var mongoConnFromEnv = System.Environment.GetEnvironmentVariable("MONGODB_URL")
    ?? System.Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
    ?? System.Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
    ?? System.Environment.GetEnvironmentVariable("MONGO_URL")
    ?? System.Environment.GetEnvironmentVariable("MongoDbSettings__ConnectionString");

Console.WriteLine($"[INFO]   Env var MONGODB_URL: {(System.Environment.GetEnvironmentVariable("MONGODB_URL") != null ? "SET" : "NOT SET")}");
Console.WriteLine($"[INFO]   Env var MONGO_URL: {(System.Environment.GetEnvironmentVariable("MONGO_URL") != null ? "SET" : "NOT SET")}");
Console.WriteLine($"[INFO]   Env var MONGO_CONNECTION_STRING: {(System.Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING") != null ? "SET" : "NOT SET")}");

if (!string.IsNullOrWhiteSpace(mongoConnFromEnv))
{
    mongoDbSettings.ConnectionString = mongoConnFromEnv;
    Console.WriteLine("[INFO]   ✓ Using environment variable for MongoDB connection");
}
else
{
    Console.WriteLine("[WARN]   ✗ No MongoDB connection env var found - using appsettings default (localhost)");
}

// Allow platform env vars for database name
var mongoDbNameFromEnv = System.Environment.GetEnvironmentVariable("MONGODB_DATABASE")
    ?? System.Environment.GetEnvironmentVariable("MONGO_INITDB_DATABASE")
    ?? System.Environment.GetEnvironmentVariable("MongoDbSettings__DatabaseName");
if (!string.IsNullOrWhiteSpace(mongoDbNameFromEnv))
{
    mongoDbSettings.DatabaseName = mongoDbNameFromEnv;
}

builder.Services.AddSingleton(mongoDbSettings);

// Register MongoDB database
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var settings = sp.GetRequiredService<MongoDbSettings>();
    var client = new MongoClient(settings.ConnectionString);
    return client.GetDatabase(settings.DatabaseName);
});

var rabbitMqSettings = new RabbitMqSettings();
builder.Configuration.GetSection("RabbitMqSettings").Bind(rabbitMqSettings);
builder.Services.AddSingleton(rabbitMqSettings);

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // Configure SignalR to accept tokens from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificationHub"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Test Service API", 
        Version = "v1",
        Description = "Dynamic test data management API with schema-based entities and JWT authentication"
    });
    
    // Disable XML comments temporarily due to compatibility issues
    // var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    // var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    // if (File.Exists(xmlPath))
    // {
    //     c.IncludeXmlComments(xmlPath);
    // }
});

// Register User Management services
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService, UserService>();

// Register Environment Management services
builder.Services.AddSingleton<IEnvironmentRepository, EnvironmentRepository>();
builder.Services.AddScoped<IEnvironmentService, EnvironmentService>();

// Register TestData services (legacy)
builder.Services.AddSingleton<ITestDataRepository, TestDataRepository>();
builder.Services.AddScoped<ITestDataService, TestDataService>();

// Register Dynamic Entity services (new)
builder.Services.AddSingleton<IEntitySchemaRepository, EntitySchemaRepository>();
builder.Services.AddSingleton<IDynamicEntityRepository, DynamicEntityRepository>();
builder.Services.AddScoped<IDynamicEntityService, DynamicEntityService>();

// Register Message Bus service (shared)
builder.Services.AddSingleton<IMessageBusService, MessageBusService>();

// Register Notification service
builder.Services.AddSingleton<INotificationService, NotificationService>();

// Register Settings services
builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();

// Register Activity services
builder.Services.AddSingleton<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IActivityService, ActivityService>();

// Register background services
builder.Services.AddHostedService<TestService.Api.BackgroundServices.DataCleanupService>();
builder.Services.AddHostedService<TestService.Api.BackgroundServices.ActivityCleanupService>();

var app = builder.Build();
Console.WriteLine("[INFO] App built successfully, starting configuration...");


// Initialize default admin user and environments in background to avoid blocking startup
// If initialization fails, the app will still start and can be accessed via API
_ = Task.Run(async () =>
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            await userService.InitializeDefaultAdminAsync();
            
            // Initialize default environments
            var environmentService = scope.ServiceProvider.GetRequiredService<IEnvironmentService>();
            await environmentService.InitializeDefaultEnvironmentsAsync();
        }
    }
    catch (Exception ex)
    {
        // Log initialization errors but don't crash the app
        Console.WriteLine($"[WARN] Database initialization failed: {ex.Message}");
        Console.WriteLine($"[WARN] Retrying in 10 seconds...");
        // Retry after delay
        await Task.Delay(TimeSpan.FromSeconds(10));
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                await userService.InitializeDefaultAdminAsync();
                
                var environmentService = scope.ServiceProvider.GetRequiredService<IEnvironmentService>();
                await environmentService.InitializeDefaultEnvironmentsAsync();
                Console.WriteLine("[INFO] Database initialization succeeded on retry");
            }
        }
        catch (Exception retryEx)
        {
            Console.WriteLine($"[ERROR] Database initialization failed after retry: {retryEx.Message}");
        }
    }
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors("AllowWebUI");

// Don't use HTTPS redirection in containerized environment behind nginx
// app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Simple health check endpoint for Railway
app.MapGet("/health", () => 
{
    try
    {
        return Results.Ok(new { status = "healthy" });
    }
    catch
    {
        return Results.StatusCode(500);
    }
})
.WithName("Health")
.Produces(200)
.Produces(500);

app.MapControllers();

// Map SignalR hub
app.MapHub<NotificationHub>("/notificationHub");

try
{
    Console.WriteLine("[INFO] Application is starting...");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"[ERROR] Application failed: {ex.Message}");
    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
    throw;
}

public partial class Program { }
