using System.Net;
using MediaServer.Server.Configuration;
using MediaServer.Server.Data;
using MediaServer.Server.Networking;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Devices;

public sealed class DeviceService
{
    private readonly MediaRepository _repository;
    private readonly ConfigService _config;
    private readonly LanAccessEvaluator _evaluator;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(MediaRepository repository, ConfigService config, LanAccessEvaluator evaluator, ILogger<DeviceService> logger)
    {
        _repository = repository;
        _config = config;
        _evaluator = evaluator;
        _logger = logger;
    }

    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync() => _repository.GetDevicesAsync();

    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(string? ip, string? userAgent, DeviceRegistrationRequest request)
    {
        IPAddress? parsed = null;
        if (!string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out var address))
        {
            parsed = address;
        }

        var autoApprove = _config.Current.AutoApproveLanDevices && (parsed is null || _evaluator.IsAllowed(parsed));
        var device = await _repository.UpsertDeviceAsync(parsed?.ToString(), userAgent ?? request.UserAgent, request, autoApprove);
        _logger.LogInformation("Device {DeviceId} registered with status {Status}", device.Id, device.Status);
        return new DeviceRegistrationResponse(device.Id, device.Status);
    }

    public Task<bool> SetDeviceStatusAsync(long deviceId, string status) => _repository.SetDeviceStatusAsync(deviceId, status);
}

public sealed record DeviceRegistrationRequest
{
    public string? Name { get; init; }
    public string? UserAgent { get; init; }
}

public sealed record DeviceRegistrationResponse(long DeviceId, string Status);

public sealed record DeviceApprovalRequest
{
    public long DeviceId { get; init; }
    public string Status { get; init; } = "approved";
}
