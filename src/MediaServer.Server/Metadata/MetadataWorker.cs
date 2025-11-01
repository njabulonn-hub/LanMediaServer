using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using MediaServer.Server.Configuration;
using MediaServer.Server.Data;
using Microsoft.Extensions.Logging;

namespace MediaServer.Server.Metadata;

public sealed class MetadataWorker
{
    private readonly MediaRepository _repository;
    private readonly ConfigService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppPaths _paths;
    private readonly ILogger<MetadataWorker> _logger;

    public MetadataWorker(MediaRepository repository, ConfigService configService, IHttpClientFactory httpClientFactory, AppPaths paths, ILogger<MetadataWorker> logger)
    {
        _repository = repository;
        _configService = configService;
        _httpClientFactory = httpClientFactory;
        _paths = paths;
        _logger = logger;
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var config = _configService.Current;
        if (string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            return 0;
        }

        var pending = await _repository.GetItemsPendingMetadataAsync(10);
        if (pending.Count == 0)
        {
            return 0;
        }

        Directory.CreateDirectory(_paths.MetadataPath);
        Directory.CreateDirectory(_paths.ArtworkPath);

        var client = _httpClientFactory.CreateClient();
        var processed = 0;
        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = await FetchMetadataAsync(client, config.TmdbApiKey!, item, cancellationToken);
                if (metadata is null)
                {
                    continue;
                }

                await SaveMetadataAsync(item.Id, metadata);
                await _repository.SaveMetadataAsync(item.Id, metadata.Title ?? item.Title, "ok");

                if (!string.IsNullOrEmpty(metadata.PosterPath))
                {
                    var posterFile = await DownloadPosterAsync(client, metadata.PosterPath!, item.Id, cancellationToken);
                    if (posterFile is not null)
                    {
                        await _repository.SaveArtworkAsync(item.Id, "poster", posterFile);
                    }
                }

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich metadata for media item {ItemId}", item.Id);
            }
        }

        return processed;
    }

    private async Task SaveMetadataAsync(long itemId, TmdbMetadata metadata)
    {
        var path = Path.Combine(_paths.MetadataPath, $"{itemId}.json");
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions.OptionsIndented);
    }

    private async Task<TmdbMetadata?> FetchMetadataAsync(HttpClient client, string apiKey, PendingMetadataItem item, CancellationToken cancellationToken)
    {
        var query = item.Title ?? Path.GetFileNameWithoutExtension(item.FileName);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var uri = new Uri($"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={Uri.EscapeDataString(query)}");
        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TMDB request failed with {Status}", response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<TmdbSearchResponse>(cancellationToken: cancellationToken);
        var match = payload?.Results?.FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        return new TmdbMetadata
        {
            Title = match.Title,
            Overview = match.Overview,
            PosterPath = match.PosterPath,
            ReleaseDate = match.ReleaseDate
        };
    }

    private async Task<string?> DownloadPosterAsync(HttpClient client, string posterPath, long itemId, CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri($"https://image.tmdb.org/t/p/w500{posterPath}");
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var extension = Path.GetExtension(posterPath);
            var fileName = $"{itemId}{extension}";
            var filePath = Path.Combine(_paths.ArtworkPath, fileName);
            await using var stream = File.Create(filePath);
            await response.Content.CopyToAsync(stream, cancellationToken);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to download poster for {ItemId}", itemId);
            return null;
        }
    }
}

public sealed class TmdbMetadata
{
    public string? Title { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? ReleaseDate { get; init; }
}

public sealed class TmdbSearchResponse
{
    public List<TmdbSearchResult>? Results { get; init; }
}

public sealed class TmdbSearchResult
{
    public string? Title { get; init; }
    public string? Overview { get; init; }
    public string? PosterPath { get; init; }
    public string? ReleaseDate { get; init; }
}
