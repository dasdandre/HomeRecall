# Project Context: HomeRecall

## Overview
HomeRecall is a backup solution for IoT devices (Tasmota, WLED, Shelly) built as an **ASP.NET Core Blazor Server** application. It is designed to run primarily as a **Home Assistant Add-on** behind Ingress, but also supports local execution.
It features a multi-language UI (German/English) and seamless Home Assistant integration.

## Tech Stack
- **Framework:** .NET 10 (Stable/LTS)
- **UI Framework:** MudBlazor (v8+)
- **Architecture:** Blazor Server (Interactive Server)
- **Database:** SQLite (EF Core)
- **Infrastructure:** Docker (Alpine based), S6 Overlay (via HA Base Image)
- **Localization:** ASP.NET Core Localization (IStringLocalizer) with Cookie Culture Provider

## Critical Architectural Decisions & Fixes

### 1. Home Assistant Ingress Compatibility
This was the most complex part of the setup. The app runs behind a dynamic path (`/api/hassio_ingress/{token}/`).

*   **PathBase Middleware:** We manually check the `X-Ingress-Path` header in `Program.cs` and set `context.Request.PathBase`.
    *   *Rule:* This Middleware MUST be registered **before** `app.UseStaticFiles()`.
*   **Static Files:** `app.UseStaticFiles()` strips the PathBase to find files in `wwwroot`. If the middleware order is wrong, CSS/JS will return 404.
*   **Blazor Base URI:** `NavigationManager.BaseUri` is unreliable behind proxies/ingress.
    *   *Solution:* We inject `IHttpContextAccessor` into `App.razor` and manually set `<base href="..." />` using the `X-Ingress-Path` header.

### 2. Theming & Styling
*   **Library:** MudBlazor.
*   **Integration:** The app mimics Home Assistant's theme.
*   **Mechanism:** `wwwroot/js/ha-theme.js` accesses `window.parent` (the HA frontend) to read CSS variables (e.g., `--primary-color`).
*   **Reactivity:** A `MutationObserver` in JS detects Dark Mode switches in HA and notifies `MainLayout.razor` via JS Interop to update the `MudTheme` dynamically.

### 3. Localization (Multi-language Support)
*   **Strategy:** Standard ASP.NET Core `IStringLocalizer`.
*   **Resources:** `SharedResource.en.resx` (English) and `SharedResource.de.resx` (German) located in `homerecall/Resources`.
*   **Switching:** 
    *   A `CultureController` handles setting the `AspNetCore.Culture` cookie.
    *   Users can select language in Settings (Auto/EN/DE).
    *   "Auto" mode respects the browser's language settings.
*   **Constraint:** German translation avoids English technical terms (except "Backup" and "Download").

### 4. Blazor Interactivity
*   **Mode:** `InteractiveServerRenderMode(prerender: false)`.
*   **Reason:** Prerendering caused issues with the Router resolving parameters and with timing of JS Interop for the theming.
*   **Configuration:** Applied in `App.razor` to `<Routes>` and `<HeadOutlet>`.

### 5. Docker & S6 Overlay
*   **Base Image:** Standard Microsoft SDK for build, but HA Base Image (Alpine) for runtime.
*   **Runtime:** We install ASP.NET Core Runtime manually via `dotnet-install.sh` in the Dockerfile because Alpine repos often lag behind.
*   **Startup:** We use a `run.sh` script (`CMD ["/run.sh"]`) to be compatible with the S6 Overlay init system used by Home Assistant. Direct `dotnet` entrypoints fail with PID 1 errors.

### 6. Backup Strategy Pattern
*   **Problem:** The `BackupService` became a monolith with many `if/else` blocks for different device types.
*   **Solution:** Refactored into a Strategy Pattern (`IDeviceStrategy`).
*   **Implementation:** 
    *   Each device type (Tasmota, WLED, Shelly, etc.) has its own class in `Services/Strategies`.
    *   `BackupService` simply iterates through injected strategies to find the right one.
    *   **Deduplication:** 
        *   Logic: If the *content* hash matches the *last* backup of the same device, the existing file on disk is reused.
        *   Naming: Files are named `YYYY-MM-DD_Name_Type_Hash.zip`.
        *   UI: A visual indicator shows if content has changed compared to the previous backup.

## Directory Structure
*   `homerecall/Components/Pages`: Blazor pages (Home, Backups).
*   `homerecall/Components/Layout`: MainLayout (AppBar, Theming Logic).
*   `homerecall/Resources`: Resx files for translations.
*   `homerecall/Services/Strategies`: Device specific backup implementations.
*   `homerecall/wwwroot`: Static assets (JS for theming, custom CSS).
*   `homerecall/data`: SQLite DB location (mapped to `/config` or `./data`).
*   `homerecall/backups`: Backup storage (mapped to `/backup` or `./backups`).

## Development
*   **Local Run:** `dotnet watch run` works thanks to `Properties/launchSettings.json`, which simulates the environment variables (`persist_path`, `backup_path`) and sets `ASPNETCORE_ENVIRONMENT=Development`. Local run uses default HA colors as fallback.

## Important Code Snippets (Do not regress)

**Program.cs (Middleware Order):**
```csharp
// 1. PathBase Handling (First!)
app.Use(async (context, next) => {
    if (context.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath)) {
        context.Request.PathBase = new PathString(ingressPath);
    }
    await next();
});

// 2. Static Files (After PathBase!)
app.UseStaticFiles();

// 3. Localization
var supportedCultures = new[] { "en", "de" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

// 4. Routing & Antiforgery
app.UseRouting();
app.UseAntiforgery();
app.MapControllers(); // For Download Controller and Culture Controller
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
```

**App.razor (Base Href):**
```razor
@{
    var baseHref = "/";
    if (HttpContextAccessor.HttpContext?.Request.Headers.TryGetValue("X-Ingress-Path", out var ingressPath) == true)
    {
        baseHref = ingressPath.ToString().TrimEnd('/') + "/";
    }
    else 
    {
         baseHref = NavigationManager.BaseUri;
    }
}
<base href="@baseHref" />
```