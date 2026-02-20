using HomeRecall.Extensions;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------------------------------------
// Service Registration
// --------------------------------------------------------------------------------------
// Add persistence layer (SQLite, EF Core)
builder.Services.AddPersistence();

// Add application interfaces, background services, and UI components
builder.Services.AddApplicationServices();

// Configure Logging based on Addon options
builder.Logging.ConfigureLogging();

// Build the application host
var app = builder.Build();

// --------------------------------------------------------------------------------------
// Application Initialization & Pipeline
// --------------------------------------------------------------------------------------
// Ensure database is created and migrated
app.EnsureDatabaseCreated();

// Configure the HTTP request pipeline (Middleware, Localization, Routing, etc.)
app.UseHomeRecallMiddleware();

app.Run();