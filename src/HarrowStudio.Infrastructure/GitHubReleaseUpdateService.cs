using System.Net.Http.Headers;
using System.Text.Json;
using HarrowStudio.Application;

namespace HarrowStudio.Infrastructure;

public sealed class GitHubReleaseUpdateService : IUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/JUANJOPROGRAM/HarrowPROsftw/releases/latest";
    private const string InstallerAssetName = "HarrowStudioSetup.exe";
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<AppRelease?> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(7));

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("HarrowStudio", Normalize(currentVersion).ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        try
        {
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var root = document.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagProperty)) return null;

            var tagName = tagProperty.GetString();
            if (string.IsNullOrWhiteSpace(tagName)) return null;
            var versionText = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(versionText, out var parsedVersion)) return null;

            var releaseVersion = Normalize(parsedVersion);
            if (releaseVersion <= Normalize(currentVersion)) return null;
            if (!root.TryGetProperty("html_url", out var pageProperty) ||
                !Uri.TryCreate(pageProperty.GetString(), UriKind.Absolute, out var pageUri)) return null;

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
            var installer = assets.EnumerateArray().FirstOrDefault(asset =>
                asset.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), InstallerAssetName, StringComparison.OrdinalIgnoreCase));
            if (installer.ValueKind != JsonValueKind.Object ||
                !installer.TryGetProperty("browser_download_url", out var downloadProperty) ||
                !Uri.TryCreate(downloadProperty.GetString(), UriKind.Absolute, out var installerUri) ||
                installerUri.Scheme != Uri.UriSchemeHttps) return null;

            var notes = root.TryGetProperty("body", out var bodyProperty) ? bodyProperty.GetString() ?? string.Empty : string.Empty;
            return new AppRelease(releaseVersion, tagName, pageUri, installerUri, notes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }

    public async Task DownloadInstallerAsync(AppRelease release, string destinationPath, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));

        using var response = await Client.GetAsync(release.InstallerUri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, useAsync: true);
        await source.CopyToAsync(destination, 131072, timeout.Token);
        await destination.FlushAsync(timeout.Token);

        if (response.Content.Headers.ContentLength is long expectedLength && new FileInfo(destinationPath).Length != expectedLength)
        {
            File.Delete(destinationPath);
            throw new IOException("La descarga del instalador quedó incompleta.");
        }
    }

    private static Version Normalize(Version version) => new(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build));
}
