using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DimToOff.Services;

internal sealed class UpdateCheckService : IDisposable
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/lingmulongtai/DimToOff-windows/releases/latest";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LogService log;
    private readonly HttpClient httpClient;
    private bool disposed;

    public UpdateCheckService(LogService log)
    {
        this.log = log;
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DimToOff", GetCurrentVersionText()));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(LatestReleaseApiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.Failed("GitHub returned an empty release response.");
            }

            Version? latestVersion = TryParseVersion(release.TagName);
            Version currentVersion = GetCurrentVersion();
            if (latestVersion is null)
            {
                return UpdateCheckResult.Failed($"Could not parse release version '{release.TagName}'.");
            }

            ReleaseAsset? installer = release.Assets
                .FirstOrDefault(asset => asset.Name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase));
            ReleaseAsset? installerChecksum = release.Assets
                .FirstOrDefault(asset => installer is not null &&
                                         string.Equals(asset.Name, $"{installer.Name}.sha256", StringComparison.OrdinalIgnoreCase));

            var update = new AvailableUpdate(
                release.TagName,
                latestVersion,
                release.HtmlUrl,
                installer?.BrowserDownloadUrl,
                installerChecksum?.BrowserDownloadUrl);

            if (latestVersion.CompareTo(currentVersion) > 0)
            {
                return UpdateCheckResult.UpdateAvailable(update);
            }

            return UpdateCheckResult.NoUpdate(update);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Error("Update check failed", ex);
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    public static Version GetCurrentVersion()
    {
        string versionText = GetCurrentVersionText();
        return TryParseVersion(versionText) ?? new Version(0, 0, 0);
    }

    public static string GetCurrentVersionText()
    {
        Assembly assembly = typeof(UpdateCheckService).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion ?? "0.0.0";
    }

    private static Version? TryParseVersion(string versionText)
    {
        string normalized = versionText.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int metadataIndex = normalized.IndexOf('+');
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        int prereleaseIndex = normalized.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        return Version.TryParse(normalized, out Version? version) ? version : null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        httpClient.Dispose();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "https://github.com/lingmulongtai/DimToOff-windows/releases";

        [JsonPropertyName("assets")]
        public List<ReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class ReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

internal sealed record AvailableUpdate(
    string TagName,
    Version Version,
    string ReleaseUrl,
    string? InstallerDownloadUrl,
    string? InstallerChecksumUrl)
{
    public bool HasInstaller => !string.IsNullOrWhiteSpace(InstallerDownloadUrl);
}

internal sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    AvailableUpdate? Update,
    string? ErrorMessage)
{
    public static UpdateCheckResult UpdateAvailable(AvailableUpdate update) =>
        new(UpdateCheckStatus.UpdateAvailable, update, null);

    public static UpdateCheckResult NoUpdate(AvailableUpdate latestRelease) =>
        new(UpdateCheckStatus.NoUpdate, latestRelease, null);

    public static UpdateCheckResult Failed(string message) =>
        new(UpdateCheckStatus.Failed, null, message);
}

internal enum UpdateCheckStatus
{
    NoUpdate,
    UpdateAvailable,
    Failed
}
