using System.Text;
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

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<SuperAdminOptions>(builder.Configuration.GetSection(SuperAdminOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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

app.MapHealthChecks("/health");
app.MapControllers();
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = SetStaticFileCacheHeaders
});

app.Run();

static void SetStaticFileCacheHeaders(StaticFileResponseContext context)
{
    var headers = context.Context.Response.Headers;

    if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
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
