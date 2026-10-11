using System.Text;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NestHub.Api.Domain.Entities;
using NestHub.Api.Infrastructure.Configuration;
using NestHub.Api.Infrastructure.Extensions;
using NestHub.Api.Infrastructure.MultiTenancy;
using NestHub.Api.Infrastructure.Persistence;
using NestHub.Api.Infrastructure.Security;
using NestHub.Api.Services;

if (args.Contains("--migrate-mysql-to-sqlite"))
{
    var migrationConfiguration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Production.json", optional: true)
        .AddEnvironmentVariables()
        .Build();
    Environment.ExitCode = await DatabaseMigrationService.RunAsync(migrationConfiguration);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection(SuperAdminOptions.SectionName));

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("share-public", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        await context.HttpContext.Response.WriteAsJsonAsync(new { message = "访问过于频繁，请稍后再试。" }, cancellationToken);
    };
});

builder.Services.AddSingleton<TenantContextAccessor>();
builder.Services.AddSingleton<IPasswordHasher<Tenant>, PasswordHasher<Tenant>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AppInfoService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<BookmarkService>();
builder.Services.AddScoped<BookmarkHtmlGenerator>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<FolderService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<NavigationLinkService>();
builder.Services.AddScoped<PortalService>();
builder.Services.AddSingleton<SiteIconService>();
builder.Services.AddSingleton<SystemUpdateService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<ShareService>();
builder.Services.AddScoped<SiteSettingService>();
builder.Services.AddScoped<SuperAdminService>();
builder.Services.AddScoped<TransitionSettingService>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddFreeSql();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApp", policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

await app.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Compress public frontend assets, never authentication/API responses or already compressed downloads.
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api")
    && !context.Request.Path.StartsWithSegments("/uploads")
    && !context.Request.Path.StartsWithSegments("/downloads")
    && !context.Request.Path.StartsWithSegments("/health"), branch => branch.UseResponseCompression());
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/share")) {
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = SetStaticFileCacheHeaders
});

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "AppData", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.ContentRootPath, "AppData", "uploads")),
    RequestPath = "/uploads"
});
app.UseCors("WebApp");
app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapGet("/health/version", () => Results.Json(new { revision = app.Configuration["NESTHUB_BUILD_REVISION"] ?? "local" })).AllowAnonymous();
app.MapControllers();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = SetStaticFileCacheHeaders
});

app.Run();

static void SetStaticFileCacheHeaders(StaticFileResponseContext context)
{
    var headers = context.Context.Response.Headers;

    if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase)
        || context.Context.Request.Path.StartsWithSegments("/downloads"))
    {
        headers.CacheControl = "no-cache, no-store, must-revalidate";
        headers.Pragma = "no-cache";
        headers.Expires = "0";
        return;
    }

    if (context.Context.Request.Path.StartsWithSegments("/assets"))
    {
        headers.CacheControl = "public, max-age=31536000, immutable";
    }
}
