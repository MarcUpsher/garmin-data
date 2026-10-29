using Garmin.Api.Endpoints;
using Garmin.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://127.0.0.1:5080");
var defaultDataDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "garmin-data");
var databasePath = Path.GetFullPath(Environment.GetEnvironmentVariable("GARMIN_DB_PATH")
    ?? Path.Combine(defaultDataDirectory, "activities.db"));
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddDbContext<GarminDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddCors(options => options.AddPolicy("local-angular", policy => policy
    .WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();
app.UseCors("local-angular");
// Published containers include Angular's production build in wwwroot. The
// separate Angular development server remains usable when those files are absent.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (File.Exists(Path.Combine(webRoot, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}
// Unknown API routes must return 404 instead of the Angular entry page.
app.Map("/api/{**path}", () => Results.NotFound());

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GarminDbContext>();
    await db.Database.MigrateAsync();
}

using var syncLock = new SemaphoreSlim(1, 1);
app.MapActivitiesEndpoints();
app.MapTrendsEndpoints(syncLock);
app.MapWellnessEndpoints(syncLock);
app.MapSyncEndpoints(syncLock);
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
