using FluentValidation;
using Prepared.Business.Extensions;
using Prepared.Data.Extensions;
using Prepared.Client.Middleware;
using Sentry;

namespace Prepared.Client;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Initialize Sentry
        var sentryDsn = builder.Configuration["Sentry:Dsn"];
        if (!string.IsNullOrEmpty(sentryDsn))
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = sentryDsn;
                options.TracesSampleRate = builder.Configuration.GetValue<double>("Sentry:TracesSampleRate", 1.0);
                options.Environment = builder.Configuration["Sentry:Environment"] ?? builder.Environment.EnvironmentName;
            });
        }

        // Add services to the container.
        builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

        // Add FluentValidation
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();

        // Add SignalR for real-time updates
        builder.Services.AddSignalR();

        // Add anti-forgery token services for CSRF protection
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "X-CSRF-TOKEN";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        // Add session support
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(2);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        ConfigureInfrastructure(builder);

        builder.Services.AddBusinessServices(builder.Configuration, builder.Environment);
        builder.Services.AddDataServices(builder.Configuration);

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        // Add session middleware (must be before UseAuthentication/UseAuthorization)
        app.UseSession();

        // Add comprehensive security middleware
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorsMiddleware>();
        app.UseMiddleware<EnhancedRateLimitingMiddleware>();
        app.UseMiddleware<ErrorHandlerMiddleware>();

        // Add SEO and Performance Middleware
        app.UseResponseCaching();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Cache static files for 1 year
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000,immutable");
                
                // Ensure proper MIME types for common file types
                var fileExtension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
                switch (fileExtension)
                {
                    case ".css":
                        ctx.Context.Response.ContentType = "text/css";
                        break;
                    case ".js":
                        ctx.Context.Response.ContentType = "application/javascript";
                        break;
                    case ".woff2":
                        ctx.Context.Response.ContentType = "font/woff2";
                        break;
                    case ".woff":
                        ctx.Context.Response.ContentType = "font/woff";
                        break;
                    case ".ttf":
                        ctx.Context.Response.ContentType = "font/ttf";
                        break;
                    case ".eot":
                        ctx.Context.Response.ContentType = "application/vnd.ms-fontobject";
                        break;
                }
                
                // Add compression for text-based files
                if (ctx.File.Name.EndsWith(".css") || ctx.File.Name.EndsWith(".js") || ctx.File.Name.EndsWith(".html"))
                {
                    ctx.Context.Response.Headers.Append("Content-Encoding", "gzip");
                }
            }
        });

        app.UseAuthorization();
        app.MapHealthChecks("api/health");
        app.MapStaticAssets();
        
        // Map SignalR hubs
        // app.MapHub<TranscriptHub>("/hubs/transcript");
        
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.Run();
    }

    private static void ConfigureInfrastructure(WebApplicationBuilder builder)
    {
        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365 * 2);
        });

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.AddServerHeader = false;
        });

        builder.Services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = 443;
        });

        builder.Services.AddHttpClient();

        // Infrastructure services
        builder.Services.AddResponseCaching();
        builder.Services.AddRouting(options => options.LowercaseUrls = true);
        builder.Services.AddMemoryCache();
    }
}
