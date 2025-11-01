using System.Text.Json;
using Dapper;
using MediaServer.Server.Configuration;
using MediaServer.Server.Devices;
using MediaServer.Server.Scanning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Data;

public sealed class MediaRepository
{
    private readonly AppPaths _paths;
    private readonly ILogger<MediaRepository> _logger;

    public MediaRepository(AppPaths paths, ILogger<MediaRepository> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        connection.Open();
        return connection;
    }

    public async Task<IReadOnlyList<LibraryDto>> GetLibrariesAsync()
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<LibraryDto>("SELECT id, name, path, kind FROM library ORDER BY name");
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MediaItemDto>> GetMediaItemsAsync(long libraryId, int skip, int take)
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<MediaItemDto>(
            @"SELECT id, library_id AS LibraryId, title, file_name AS FileName, file_path AS FilePath, ext, status, duration, video_codec AS VideoCodec, audio_codec AS AudioCodec, created_at AS CreatedAt
              FROM media_item
             WHERE library_id = @LibraryId
             ORDER BY created_at DESC
             LIMIT @Take OFFSET @Skip",
            new { LibraryId = libraryId, Skip = skip, Take = take });
        return rows.ToList();
    }

    public async Task<MediaItemDto?> GetMediaItemAsync(long id)
    {
        await using var connection = CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<MediaItemDto>(
            @"SELECT id, library_id AS LibraryId, title, file_name AS FileName, file_path AS FilePath, ext, status, duration, video_codec AS VideoCodec, audio_codec AS AudioCodec, created_at AS CreatedAt
              FROM media_item WHERE id = @Id",
            new { Id = id });
        return row;
    }

    public async Task<long> UpsertMediaItemAsync(long libraryId, string filePath, string? title, string extension)
    {
        await using var connection = CreateConnection();
        await using var tx = await connection.BeginTransactionAsync();
        var id = await connection.ExecuteScalarAsync<long?>(
            "SELECT id FROM media_item WHERE file_path = @FilePath", new { FilePath = filePath }, tx);
        if (id.HasValue)
        {
            await connection.ExecuteAsync(
                "UPDATE media_item SET title = COALESCE(@Title, title), file_name = @FileName, ext = @Ext WHERE id = @Id",
                new
                {
                    Title = title,
                    FileName = Path.GetFileName(filePath),
                    Ext = extension,
                    Id = id.Value
                }, tx);
            await tx.CommitAsync();
            return id.Value;
        }

        var insertId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO media_item(library_id, title, file_name, file_path, ext, status)
              VALUES(@LibraryId, @Title, @FileName, @FilePath, @Ext, 'local');
              SELECT last_insert_rowid();",
            new
            {
                LibraryId = libraryId,
                Title = title,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                Ext = extension
            }, tx);
        await tx.CommitAsync();
        return insertId;
    }

    public async Task UpdateMediaMetadataAsync(long id, MediaTechnicalInfo info)
    {
        await using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE media_item SET duration = @Duration, video_codec = @VideoCodec, audio_codec = @AudioCodec WHERE id = @Id",
            new { Duration = info.DurationSeconds, info.VideoCodec, info.AudioCodec, Id = id });
    }

    public async Task UpdateMediaStatusAsync(long id, string status, string? title = null)
    {
        await using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE media_item SET status = @Status, title = COALESCE(@Title, title) WHERE id = @Id",
            new { Status = status, Title = title, Id = id });
    }

    public async Task<IReadOnlyList<PendingMetadataItem>> GetItemsPendingMetadataAsync(int take)
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<PendingMetadataItem>(
            @"SELECT id, title, file_name AS FileName, file_path AS FilePath
              FROM media_item
             WHERE status = 'local'
             ORDER BY created_at ASC
             LIMIT @Take",
            new { Take = take });
        return rows.ToList();
    }

    public async Task SaveMetadataAsync(long id, string? title, string? status)
    {
        await using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE media_item SET title = COALESCE(@Title, title), status = COALESCE(@Status, status) WHERE id = @Id",
            new { Title = title, Status = status, Id = id });
    }

    public async Task SaveArtworkAsync(long mediaItemId, string kind, string filePath)
    {
        await using var connection = CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO artwork(media_item_id, kind, file_path)
              VALUES(@MediaItemId, @Kind, @FilePath)
              ON CONFLICT(media_item_id, kind) DO UPDATE SET file_path = excluded.file_path",
            new { MediaItemId = mediaItemId, Kind = kind, FilePath = filePath });
    }

    public async Task<string?> GetArtworkAsync(string name)
    {
        await using var connection = CreateConnection();
        var row = await connection.ExecuteScalarAsync<string?>(
            "SELECT file_path FROM artwork WHERE file_path LIKE '%' || @Name ORDER BY id DESC LIMIT 1",
            new { Name = name });
        return row;
    }

    public async Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync()
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<DeviceRecord>(
            "SELECT id, name, ip_address AS IpAddress, user_agent AS UserAgent, status, last_seen AS LastSeen, created_at AS CreatedAt FROM device ORDER BY created_at DESC");
        return rows.ToList();
    }

    public async Task<DeviceRecord> UpsertDeviceAsync(string? ipAddress, string? userAgent, DeviceRegistrationRequest request, bool autoApprove)
    {
        await using var connection = CreateConnection();
        await using var tx = await connection.BeginTransactionAsync();
        var existing = await connection.QuerySingleOrDefaultAsync<DeviceRecord>(
            "SELECT id, name, ip_address AS IpAddress, user_agent AS UserAgent, status, last_seen AS LastSeen, created_at AS CreatedAt FROM device WHERE coalesce(ip_address, '') = coalesce(@Ip, '') AND coalesce(user_agent, '') = coalesce(@UserAgent, '')",
            new { Ip = ipAddress, UserAgent = userAgent }, tx);
        if (existing is not null)
        {
            await connection.ExecuteAsync(
                "UPDATE device SET name = COALESCE(@Name, name), last_seen = @LastSeen WHERE id = @Id",
                new { request.Name, LastSeen = DateTimeOffset.UtcNow, Id = existing.Id }, tx);
            await tx.CommitAsync();
            return existing with
            {
                Name = request.Name ?? existing.Name,
                LastSeen = DateTimeOffset.UtcNow
            };
        }

        var status = autoApprove ? "approved" : "pending";
        var insertedId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO device(name, ip_address, user_agent, status, last_seen)
              VALUES(@Name, @Ip, @UserAgent, @Status, @LastSeen);
              SELECT last_insert_rowid();",
            new
            {
                request.Name,
                Ip = ipAddress,
                UserAgent = userAgent ?? request.UserAgent,
                Status = status,
                LastSeen = DateTimeOffset.UtcNow
            }, tx);
        await tx.CommitAsync();
        return new DeviceRecord
        {
            Id = insertedId,
            Name = request.Name,
            IpAddress = ipAddress,
            UserAgent = userAgent ?? request.UserAgent,
            Status = status,
            LastSeen = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<bool> SetDeviceStatusAsync(long id, string status)
    {
        await using var connection = CreateConnection();
        var rows = await connection.ExecuteAsync("UPDATE device SET status = @Status WHERE id = @Id", new { Status = status, Id = id });
        return rows > 0;
    }
}

public sealed record LibraryDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
}

public sealed record MediaItemDto
{
    public long Id { get; init; }
    public long LibraryId { get; init; }
    public string? Title { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string? Ext { get; init; }
    public string Status { get; init; } = "local";
    public double? Duration { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed record PendingMetadataItem
{
    public long Id { get; init; }
    public string? Title { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

public sealed record DeviceRecord
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string Status { get; init; } = "pending";
    public DateTimeOffset? LastSeen { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
