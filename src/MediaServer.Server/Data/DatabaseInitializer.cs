using Dapper;
using MediaServer.Server.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Data;

public sealed class DatabaseInitializer
{
    private readonly AppPaths _paths;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(AppPaths paths, ILogger<DatabaseInitializer> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task InitializeAsync(AppConfig config, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_paths.DatabasePath)!);

        await using var connection = new SqliteConnection($"Data Source={_paths.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS library (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    path TEXT NOT NULL UNIQUE,
    kind TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS media_item (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    library_id INTEGER NOT NULL REFERENCES library(id) ON DELETE CASCADE,
    title TEXT,
    file_name TEXT NOT NULL,
    file_path TEXT NOT NULL UNIQUE,
    ext TEXT,
    status TEXT NOT NULL DEFAULT 'local',
    duration REAL,
    video_codec TEXT,
    audio_codec TEXT,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS artwork (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    media_item_id INTEGER NOT NULL REFERENCES media_item(id) ON DELETE CASCADE,
    kind TEXT NOT NULL,
    file_path TEXT NOT NULL,
    UNIQUE(media_item_id, kind)
);
CREATE TABLE IF NOT EXISTS device (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT,
    ip_address TEXT,
    user_agent TEXT,
    status TEXT NOT NULL DEFAULT 'pending',
    last_seen TEXT,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_media_item_library ON media_item(library_id);
CREATE INDEX IF NOT EXISTS idx_media_item_status ON media_item(status);
CREATE INDEX IF NOT EXISTS idx_device_status ON device(status);
");

        await SyncLibrariesAsync(connection, config.Libraries);
    }

    private async Task SyncLibrariesAsync(SqliteConnection connection, IEnumerable<LibraryConfig> libraries)
    {
        foreach (var library in libraries)
        {
            await connection.ExecuteAsync(@"
INSERT INTO library(name, path, kind)
SELECT @Name, @Path, @Kind
WHERE NOT EXISTS(SELECT 1 FROM library WHERE path = @Path);
", new { library.Name, Path = library.NormalizedPath, library.Kind });

            await connection.ExecuteAsync(
                "UPDATE library SET name = @Name, kind = @Kind, updated_at = CURRENT_TIMESTAMP WHERE path = @Path",
                new { library.Name, Path = library.NormalizedPath, library.Kind });
        }
    }
}
