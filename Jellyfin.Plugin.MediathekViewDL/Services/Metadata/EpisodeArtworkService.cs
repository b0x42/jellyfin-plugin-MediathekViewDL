using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewDL.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewDL.Services.Metadata;

/// <summary>
/// Default implementation of <see cref="IEpisodeArtworkService"/>. Scrapes the <c>og:image</c>
/// meta tag from a broadcaster's episode page and downloads that image as episode artwork.
/// </summary>
public partial class EpisodeArtworkService : IEpisodeArtworkService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EpisodeArtworkService> _logger;
    private readonly IConfigurationProvider _configurationProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeArtworkService"/> class.
    /// </summary>
    /// <param name="httpClient">The http client.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationProvider">The configuration provider.</param>
    public EpisodeArtworkService(HttpClient httpClient, ILogger<EpisodeArtworkService> logger, IConfigurationProvider configurationProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configurationProvider = configurationProvider;
    }

    /// <inheritdoc />
    public async Task<bool> DownloadArtworkAsync(EpisodeArtworkDTO item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.WebsiteUrl) || string.IsNullOrWhiteSpace(item.FilePath))
        {
            return false;
        }

        var pluginConfig = _configurationProvider.ConfigurationOrNull;
        if (pluginConfig is null)
        {
            _logger.LogWarning("Plugin configuration is not available, skipping episode artwork for '{WebsiteUrl}'.", item.WebsiteUrl);
            return false;
        }

        if (!IsDomainAllowed(item.WebsiteUrl, pluginConfig))
        {
            _logger.LogWarning("Website domain is not allowed, skipping episode artwork for '{WebsiteUrl}'.", item.WebsiteUrl);
            return false;
        }

        try
        {
            var imageUrl = await ExtractOgImageUrlAsync(item.WebsiteUrl, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _logger.LogInformation("No og:image found on '{WebsiteUrl}', skipping episode artwork.", item.WebsiteUrl);
                return false;
            }

            if (!IsDomainAllowed(imageUrl, pluginConfig))
            {
                _logger.LogWarning("Image domain is not allowed, skipping episode artwork for '{ImageUrl}'.", imageUrl);
                return false;
            }

            using var response = await _httpClient.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download episode artwork from '{ImageUrl}': {StatusCode}", imageUrl, response.StatusCode);
                return false;
            }

            var directory = Path.GetDirectoryName(item.FilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(item.FilePath, bytes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully downloaded episode artwork to '{FilePath}'.", item.FilePath);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to download episode artwork for '{WebsiteUrl}': {Message}", item.WebsiteUrl, ex.Message);
            return false;
        }
    }

    private async Task<string?> ExtractOgImageUrlAsync(string websiteUrl, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(websiteUrl, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch website page '{WebsiteUrl}': {StatusCode}", websiteUrl, response.StatusCode);
            return null;
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var match = OgImageRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        return System.Net.WebUtility.HtmlDecode(content);
    }

    private bool IsDomainAllowed(string url, PluginConfiguration pluginConfig)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("Invalid or non-HTTPS URL: {Url}", url);
            return false;
        }

        var hostParts = uri.Host.Split('.');
        if (hostParts.Length < 2)
        {
            _logger.LogWarning("Invalid host in URL: {Host}", uri.Host);
            return false;
        }

        var topDomain = string.Join('.', hostParts[^2..]);
        return pluginConfig.AllowedDomains.Contains(topDomain) || pluginConfig.Network.AllowUnknownDomains;
    }

    // Matches <meta property="og:image" content="..."> regardless of attribute order
    // (property before content, or content before property) and quote style.
    [GeneratedRegex(
        "<meta[^>]*(?:property=[\"']og:image[\"'][^>]*content=[\"']([^\"']+)[\"']|content=[\"']([^\"']+)[\"'][^>]*property=[\"']og:image[\"'])",
        RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();
}
