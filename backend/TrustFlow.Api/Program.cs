using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Exceptions;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Auth;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TrustFlow.Api.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry =>
                entry.Value is not null &&
                entry.Value.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error =>
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The submitted value is invalid."
                            : error.ErrorMessage)
                    .ToArray()
            );

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = "Check the errors field for details.",
            Instance = context.HttpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problemDetails);
    };
});

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler
>();

var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Database connection string is missing."
    );

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(
        JwtSettings.SectionName
    )
);

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT settings are missing."
    );

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
{
    throw new InvalidOperationException(
        "JWT issuer is missing."
    );
}

if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
{
    throw new InvalidOperationException(
        "JWT audience is missing."
    );
}

if (string.IsNullOrWhiteSpace(jwtSettings.Key))
{
    throw new InvalidOperationException(
        "JWT secret key is missing."
    );
}

if (jwtSettings.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT expiration minutes must be greater than zero."
    );
}
if (jwtSettings.RefreshTokenExpirationDays <= 0)
{
    throw new InvalidOperationException(
        "JWT refresh token expiration days must be greater than zero."
    );
}

var jwtKeyBytes = Encoding.UTF8.GetBytes(
    jwtSettings.Key
);

if (jwtKeyBytes.Length < 32)
{
    throw new InvalidOperationException(
        "JWT secret key must be at least 32 bytes."
    );
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(jwtKeyBytes),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
     .WithOrigins(allowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials();
        }
    );
});

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            var response =
                context.HttpContext.Response;

            response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            response.ContentType =
                "application/problem+json";

            if (context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter,
                    out var retryAfter))
            {
                response.Headers["Retry-After"] =
                    Math.Ceiling(
                            retryAfter.TotalSeconds
                        )
                        .ToString(
                            CultureInfo.InvariantCulture
                        );
            }

            var problemDetails = new ProblemDetails
            {
                Status =
                    StatusCodes.Status429TooManyRequests,

                Title = "Too many requests.",

                Detail =
                    "The request limit has been exceeded. Try again later.",

                Instance =
                    context.HttpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] =
                context.HttpContext.TraceIdentifier;

            await response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken
            );
        };

    options.AddPolicy(
        RateLimitPolicies.Login,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 10,
                        window: TimeSpan.FromMinutes(1)
                    )
            )
    );

    options.AddPolicy(
        RateLimitPolicies.Register,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 5,
                        window: TimeSpan.FromMinutes(10)
                    )
            )
    );

    options.AddPolicy(
        RateLimitPolicies.RefreshToken,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 30,
                        window: TimeSpan.FromMinutes(1)
                    )
            )
    );

    options.AddPolicy(
        RateLimitPolicies.PublicMarketplace,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 60,
                        window: TimeSpan.FromMinutes(1)
                    )
            )
    );
});

var app = builder.Build();

if (app.Environment.IsEnvironment("Testing"))
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();
}

await app.SeedRolesAsync();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("Frontend");

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();



static string GetClientRateLimitKey(
    HttpContext httpContext)
{
    return httpContext.Connection
               .RemoteIpAddress?
               .ToString()
           ?? "unknown-client";
}

static FixedWindowRateLimiterOptions
    CreateFixedWindowOptions(
        int permitLimit,
        TimeSpan window)
{
    return new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,

        Window = window,

        QueueLimit = 0,

        QueueProcessingOrder =
            QueueProcessingOrder.OldestFirst,

        AutoReplenishment = true
    };
}


public partial class Program;
