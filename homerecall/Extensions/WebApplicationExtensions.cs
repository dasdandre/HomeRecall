using HomeRecall.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using HomeRecall.Components;

namespace HomeRecall.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline and middleware.
    /// Includes Exception Handling, HSTS, Ingress support, Static Files, Localization, Routing, and Razor Components.
    /// </summary>
    public static WebApplication UseHomeRecallMiddleware(this WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        // Path Base handling for Ingress
        // HA Ingress sets HTTP_X_INGRESS_PATH header. We MUST use this to set PathBase.
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath))
            {
                // Ensure PathBase starts with slash
                var pathBase = new PathString(ingressPath);
                context.Request.PathBase = pathBase;
            }
            await next();
        });

        // Ensure Static Files are served correctly even behind Ingress paths
        app.UseStaticFiles();

        var supportedCultures = new[] { "en", "de" };
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture("en")
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        app.UseRequestLocalization(localizationOptions);

        app.UseRouting();
        app.UseAntiforgery();
        app.MapControllers();

        // Ingress Setup:
        // Home Assistant Ingress sends requests to the root, but we need to handle the base path correctly if we were using it directly.
        // However, Ingress usually proxies it nicely. We might need X-Forwarded-For headers etc.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>
    /// Ensures the database is created and all pending migrations are applied.
    /// Also configures SQLite Write-Ahead Logging (WAL) mode for performance.
    /// </summary>
    public static WebApplication EnsureDatabaseCreated(this WebApplication app)
    {
        // Ensure DB created
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BackupContext>();
            db.Database.Migrate();
            // Enable Write-Ahead Logging (WAL) for better performance and resilience against power loss
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        }
        return app;
    }
}
