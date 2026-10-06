using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShareSync.Application;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Application.Interfaces;
using ShareSync.Infrastructure;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Services;
using ShareSync.Web.Middleware;
using ShareSync.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers & HttpContextAccessor
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 2. Add Layer Dependencies & Validate Configuration
var isDev = builder.Environment.IsDevelopment();
var connectionString = builder.Configuration.GetConnectionString("MySqlConnection");

if (!isDev)
{
    if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_PASSWORD") || connectionString.Contains("YOUR_DB_PASSWORD"))
    {
        throw new InvalidOperationException("Production configuration error: 'ConnectionStrings:MySqlConnection' is missing or contains placeholder values.");
    }

    var configuredSecret = builder.Configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(configuredSecret) || configuredSecret.Length < 32 || configuredSecret.Contains("YOUR_STRONG_SECRET"))
    {
        throw new InvalidOperationException("Production configuration error: 'Jwt:Secret' must be provided via environment variables or secrets and be at least 32 characters long.");
    }
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Add HttpClient with safe timeout for live DSE price synchronization
builder.Services.AddHttpClient<IDsePriceService, DsePriceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
});
builder.Services.AddHostedService<DsePriceBackgroundService>();

// 3. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (isDev)
    {
        jwtSecret = "ShareSyncDevSecretKeyForAcademicProjectSecurity2026#LongEnoughKey";
    }
    else
    {
        throw new InvalidOperationException("Required configuration 'Jwt:Secret' is missing in production environment.");
    }
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ShareSyncServer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ShareSyncClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. Configure Swagger with JWT Bearer Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShareSync API",
        Version = "v1",
        Description = "ASP.NET Core Web API for ShareSync Portfolio Tracker backed by MySQL Database"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 5. Configure CORS (Controlled origins policy)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5000", "https://localhost:5001", "http://127.0.0.1:5000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ShareSyncCorsPolicy", policy =>
    {
        if (isDev)
        {
            policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

var app = builder.Build();

// 6. Database Initialization on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ShareSyncDbContext>();
        await DbInitializer.InitializeAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization warning/error: {Message}", ex.Message);
    }
}

// 7. Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ShareSync API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("ShareSyncCorsPolicy");

var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend"));
if (Directory.Exists(frontendPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
        RequestPath = ""
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
