using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Data;
using TrustFlow.Api.Exceptions;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Ai;
using TrustFlow.Api.Services.Auth;
using TrustFlow.Api.Services.Email;
using TrustFlow.Api.Services.Files;
using TrustFlow.Api.Services.Identity;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Services.Security;

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

// Handlers run in order: AI failures get their own answers first.
builder.Services.AddExceptionHandler<AiExceptionHandler>();

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

builder.Services.Configure<BlockchainOptions>(
    builder.Configuration.GetSection(
        BlockchainOptions.SectionName
    )
);

var blockchainOptions = builder.Configuration
    .GetSection(BlockchainOptions.SectionName)
    .Get<BlockchainOptions>()
    ?? throw new InvalidOperationException(
        "Blockchain settings are missing."
    );

if (blockchainOptions.ChainId <= 0)
{
    throw new InvalidOperationException(
        "Blockchain chain id must be greater than zero."
    );
}

if (string.IsNullOrWhiteSpace(blockchainOptions.RpcUrl))
{
    throw new InvalidOperationException(
        "Blockchain RPC URL is missing."
    );
}

if (string.IsNullOrWhiteSpace(blockchainOptions.TokenAddress) ||
    blockchainOptions.TokenAddress.Length != 42 ||
    !blockchainOptions.TokenAddress.StartsWith(
        "0x",
        StringComparison.OrdinalIgnoreCase) ||
    !blockchainOptions.TokenAddress
        .AsSpan(2)
        .ToString()
        .All(Uri.IsHexDigit))
{
    throw new InvalidOperationException(
        "Blockchain token address is missing or invalid."
    );
}

if (string.IsNullOrWhiteSpace(blockchainOptions.ArbiterAddress) ||
    blockchainOptions.ArbiterAddress.Length != 42 ||
    !blockchainOptions.ArbiterAddress.StartsWith(
        "0x",
        StringComparison.OrdinalIgnoreCase) ||
    !blockchainOptions.ArbiterAddress
        .AsSpan(2)
        .ToString()
        .All(Uri.IsHexDigit))
{
    throw new InvalidOperationException(
        "Blockchain arbiter address is missing or invalid."
    );
}

if (string.IsNullOrWhiteSpace(blockchainOptions.DeployerPrivateKey))
{
    throw new InvalidOperationException(
        "Blockchain deployer private key is missing."
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
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var network = context.HttpContext.RequestServices.GetRequiredService<AdminNetworkAccess>();
                if (context.Principal?.HasClaim(LocalAdminPreview.Claim, "true") == true)
                {
                    if (!await LocalAdminPreview.ValidateAsync(context.HttpContext, context.Principal, users,
                        context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>(),
                        context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
                        context.Fail("Local administrator preview is unavailable.");
                    return;
                }
                if (context.Principal is null
                    || (context.Principal.IsInRole(AppRoles.Admin) && !network.IsAllowed(context.HttpContext))
                    || !await AdminSessionSecurity.ValidateAsync(context.Principal, users))
                    context.Fail("Administrator multi-factor verification is required.");
            }
        };
    });

builder.Services.AddSingleton<AdminNetworkAccess>();
builder.Services.AddScoped<AdminMfaService>();
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

var emailOptions = builder.Configuration
    .GetSection(EmailOptions.SectionName)
    .Get<EmailOptions>()
    ?? new EmailOptions();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

if (emailOptions.IsConfigured)
{
    builder.Services.AddSingleton<IEmailDelivery, SmtpEmailDelivery>();
}
else if (builder.Environment.IsDevelopment() ||
         builder.Environment.IsEnvironment("Testing"))
{
    // Nothing is sent: the message, code included, goes to the log.
    builder.Services.AddSingleton<IEmailDelivery, LogEmailDelivery>();
}
else
{
    // Accounts cannot be verified or recovered without a mail server, so
    // a deployment without one should not start.
    throw new InvalidOperationException(
        "Email settings are missing. Set Email__Host and " +
        "Email__FromAddress, plus Email__User and Email__Password " +
        "if the server needs a login."
    );
}

var aiOptions = builder.Configuration
    .GetSection(AiOptions.SectionName)
    .Get<AiOptions>()
    ?? new AiOptions();

builder.Services.Configure<AiOptions>(
    builder.Configuration.GetSection(AiOptions.SectionName));

if (aiOptions.IsMock)
{
    builder.Services.AddSingleton<IAiModel, MockAiModel>();
}
else if (string.Equals(
             aiOptions.Provider,
             AiOptions.AnthropicProvider,
             StringComparison.OrdinalIgnoreCase))
{
    if (aiOptions.Enabled &&
        string.IsNullOrWhiteSpace(aiOptions.ApiKey))
    {
        throw new InvalidOperationException(
            "Ai__ApiKey is missing. Set it, or set Ai__Provider=Mock."
        );
    }

    builder.Services.AddHttpClient<IAiModel, AnthropicAiModel>(
        client => client.Timeout =
            TimeSpan.FromSeconds(aiOptions.TimeoutSeconds));
}
else
{
    throw new InvalidOperationException(
        $"Ai__Provider {aiOptions.Provider} is not supported. " +
        "Use Mock or Anthropic."
    );
}

builder.Services.AddScoped<AiEvidenceBuilder>();
builder.Services.AddScoped<IAiGateway, AiGateway>();
builder.Services.AddScoped<ProjectDraftService>();
builder.Services.AddScoped<MilestoneReviewService>();
builder.Services.AddScoped<FreelancerCoachService>();
builder.Services.AddScoped<DisputeService>();
builder.Services.AddScoped<CaseAdviceService>();
builder.Services.AddScoped<IAuthCodeService, AuthCodeService>();
builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IEscrowChainDeployer, EscrowChainDeployer>();
builder.Services.AddScoped<IEscrowDeploymentOrchestrator, EscrowDeploymentOrchestrator>();
builder.Services.AddScoped<IEscrowChainSyncOrchestrator, EscrowChainSyncOrchestrator>();
builder.Services.Configure<MalwareScannerOptions>(
    builder.Configuration.GetSection(MalwareScannerOptions.SectionName));
builder.Services.AddScoped<IMalwareScanner, ClamAvMalwareScanner>();
builder.Services.AddSingleton<PdfResumeValidator>();
builder.Services.AddSingleton<MilestoneDeliveryFileValidator>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ProfilePhotoProcessor>();
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
        RateLimitPolicies.AccountRecovery,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 10,
                        window: TimeSpan.FromMinutes(10)
                    )
            )
    );

    // Per signed-in user, not per address: a model call costs money, and
    // one office sharing an address should not share a budget.
    options.AddPolicy(
        RateLimitPolicies.Ai,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.User.FindFirstValue(
                        ClaimTypes.NameIdentifier)
                    ?? GetClientRateLimitKey(httpContext),

                factory: _ =>
                    CreateFixedWindowOptions(
                        permitLimit: 20,
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

    options.AddPolicy(
        RateLimitPolicies.ResumeUpload,
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: GetClientRateLimitKey(httpContext),
                factory: _ => CreateFixedWindowOptions(
                    permitLimit: 6,
                    window: TimeSpan.FromMinutes(10))));

    options.AddPolicy(
        RateLimitPolicies.ProfilePhotoUpload,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientRateLimitKey(httpContext),
            factory: _ => CreateFixedWindowOptions(
                permitLimit: 4,
                window: TimeSpan.FromMinutes(30))));

    // Per signed-in user, so a flood from one account stops there and a
    // shared office address does not share a limit.
    options.AddPolicy(
        RateLimitPolicies.Chat,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? GetClientRateLimitKey(httpContext),
            factory: _ => CreateFixedWindowOptions(
                permitLimit: 30,
                window: TimeSpan.FromMinutes(1))));

    options.AddPolicy(
        RateLimitPolicies.MilestoneDelivery,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientRateLimitKey(httpContext),
            factory: _ => CreateFixedWindowOptions(
                permitLimit: 10,
                window: TimeSpan.FromMinutes(10))));
});

var app = builder.Build();
_ = app.Services.GetRequiredService<AdminNetworkAccess>();

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
await ReviewerAccess.SeedAsync(app);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("Frontend");

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}
app.Use(async (context, next) =>
{
    if (AdminNetworkAccess.IsManagementPath(context.Request.Path)
        && !context.RequestServices.GetRequiredService<AdminNetworkAccess>().IsAllowed(context))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Administrator access requires an approved private connection." });
        return;
    }
    await next(context);
});
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
