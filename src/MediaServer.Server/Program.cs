using System.Net;
using MediaServer.Server.Configuration;
using MediaServer.Server.Data;
using MediaServer.Server.Metadata;
using MediaServer.Server.Middleware;
using MediaServer.Server.Networking;
using MediaServer.Server.Scanning;
using MediaServer.Server.Streaming;
using MediaServer.Server.Devices;
using MediaServer.Server.Utilities;
using MediaServer.Server.Discovery;
using MediaServer.Server.Hosting;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var port = builder.Configuration.GetValue("MediaServerPort", builder.Configuration.GetValue("PORT", 8090));

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, port);
});

builder.Host.UseWindowsService();

builder.Services.AddRouting();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<MediaRepository>();
builder.Services.AddSingleton<LanAccessEvaluator>();
builder.Services.AddSingleton<ScanService>();
builder.Services.AddSingleton<FfprobeService>();
builder.Services.AddSingleton<StreamService>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddSingleton<MetadataWorker>();
builder.Services.AddSingleton<DiscoveryService>();
builder.Services.AddHostedService<StartupInitializer>();
builder.Services.AddHostedService<MetadataBackgroundService>();
builder.Services.AddHostedService<DiscoveryBackgroundService>();

var app = builder.Build();

var configService = app.Services.GetRequiredService<ConfigService>();
var appPaths = app.Services.GetRequiredService<AppPaths>();
var discoveryService = app.Services.GetRequiredService<DiscoveryService>();

discoveryService.Configure(app.Configuration["ServerName"] ?? "Local Media Server", port);

app.UseMiddleware<LanAccessMiddleware>();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/streams",
    FileProvider = new PhysicalFileProvider(appPaths.StreamsPath)
});

app.MapFallbackToFile("/media-center/{*path}", "media-center/index.html");

app.MapGet("/api/status", (MediaRepository repository, ScanService scanService, ConfigService cfg) =>
{
    return Results.Ok(new
    {
        service = "MediaServer",
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev",
        scanning = new
        {
            scanService.IsScanning,
            scanService.LastScanStarted,
            scanService.LastScanCompleted
        },
        config = new
        {
            libraries = cfg.Current.Libraries.Select(l => new { l.Name, l.Path, l.Kind })
        }
    });
});

app.MapGet("/api/libraries", async (MediaRepository repository) =>
{
    var libraries = await repository.GetLibrariesAsync();
    return Results.Ok(libraries);
});

app.MapGet("/api/library/{id:long}/items", async (long id, int? skip, int? take, MediaRepository repository) =>
{
    var items = await repository.GetMediaItemsAsync(id, skip ?? 0, take ?? 100);
    return Results.Ok(items);
});

app.MapGet("/api/item/{id:long}", async (long id, MediaRepository repository) =>
{
    var item = await repository.GetMediaItemAsync(id);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapGet("/api/item/{id:long}/file", async (long id, MediaRepository repository) =>
{
    var item = await repository.GetMediaItemAsync(id);
    if (item is null)
    {
        return Results.NotFound();
    }

    if (!File.Exists(item.FilePath))
    {
        return Results.Problem($"File not found: {item.FilePath}", statusCode: (int)HttpStatusCode.Gone);
    }

    var contentType = MimeTypes.GetMimeType(item.FilePath);
    return Results.File(item.FilePath, contentType, enableRangeProcessing: true);
});

app.MapGet("/api/item/{id:long}/stream", async (long id, StreamService streamService, MediaRepository repository) =>
{
    var item = await repository.GetMediaItemAsync(id);
    if (item is null)
    {
        return Results.NotFound();
    }

    var manifest = await streamService.CreateHlsStreamAsync(item);
    return Results.Ok(new { manifest });
});

app.MapGet("/api/devices", async (DeviceService service) => Results.Ok(await service.GetDevicesAsync()));

app.MapPost("/api/devices/register", async (HttpContext context, DeviceRegistrationRequest? request, DeviceService service) =>
{
    request ??= new DeviceRegistrationRequest();
    var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    var ua = context.Request.Headers.UserAgent.ToString();
    var result = await service.RegisterDeviceAsync(ip, ua, request);
    return Results.Ok(result);
});

app.MapPost("/api/devices/approve", async (DeviceApprovalRequest request, DeviceService service) =>
{
    var success = await service.SetDeviceStatusAsync(request.DeviceId, request.Status);
    return success ? Results.Ok() : Results.NotFound();
});

app.MapPost("/api/scan", async (ScanService scanService) =>
{
    await scanService.QueueFullScanAsync();
    return Results.Accepted();
});

app.MapGet("/api/art/{name}", async (string name, MediaRepository repository, AppPaths paths) =>
{
    var art = await repository.GetArtworkAsync(name);
    var path = art ?? Path.Combine(paths.ArtworkPath, name);
    if (!File.Exists(path))
    {
        return Results.NotFound();
    }

    var contentType = MimeTypes.GetMimeType(path);
    return Results.File(path, contentType);
});

await app.RunAsync();

namespace MediaServer.Server
{
    internal partial class Program;
}
