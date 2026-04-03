using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using FrcToolsuite.Core.Download;
using FrcToolsuite.Core.Platform;

namespace FrcToolsuite.Core.Update;

/// <summary>
/// Checks for new versions of the package manager via a GitHub-style releases
/// endpoint, downloads the update, and orchestrates an in-place upgrade via a
/// helper script that waits for the current process to exit.
/// </summary>
public sealed class SelfUpdater : ISelfUpdater
{
    private const string UpdateCheckUrl =
        "https://api.github.com/repos/first-toolsuite/first-package-manager/releases/latest";

    private readonly HttpClient _http;
    private readonly IDownloadManager _downloadManager;
    private readonly IPlatformService _platformService;

    public SelfUpdater(HttpClient http, IDownloadManager downloadManager, IPlatformService platformService)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _downloadManager = downloadManager ?? throw new ArgumentNullException(nameof(downloadManager));
        _platformService = platformService ?? throw new ArgumentNullException(nameof(platformService));
    }

    public string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            return ver is not null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "0.1.0";
        }
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateCheckUrl);
            request.Headers.Add("User-Agent", "FrcToolsuite-PackageManager");
            request.Headers.Add("Accept", "application/vnd.github+json");

            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var latestVersion = tagName.TrimStart('v');
            var releaseNotes = root.TryGetProperty("body", out var bodyEl)
                ? bodyEl.GetString()
                : null;

            if (!IsNewerVersion(latestVersion, CurrentVersion))
            {
                return null;
            }

            var platformId = _platformService.GetPlatformId();
            var (downloadUrl, sha256, size) = FindPlatformAsset(root, platformId);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                return null;
            }

            return new UpdateInfo(latestVersion, downloadUrl, sha256, size, releaseNotes);
        }
        catch
        {
            // Network errors, JSON parse errors, etc. -- not fatal
            return null;
        }
    }

    public async Task DownloadAndApplyUpdateAsync(
        UpdateInfo update,
        IProgress<DownloadProgress> progress,
        CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"frc-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var fileName = Path.GetFileName(new Uri(update.DownloadUrl).AbsolutePath);
        var downloadPath = Path.Combine(tempDir, fileName);

        var downloadRequest = new DownloadRequest(
            update.DownloadUrl,
            downloadPath,
            string.IsNullOrEmpty(update.Sha256) ? null : update.Sha256,
            update.Size > 0 ? update.Size : null);

        var result = await _downloadManager.DownloadAsync(downloadRequest, progress, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Failed to download update: {result.Error}");
        }

        var currentExe = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine current executable path.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            WriteWindowsUpdateScript(tempDir, downloadPath, currentExe);
        }
        else
        {
            WriteUnixUpdateScript(tempDir, downloadPath, currentExe);
        }
    }

    private static void WriteWindowsUpdateScript(string tempDir, string downloadPath, string currentExe)
    {
        var scriptPath = Path.Combine(tempDir, "apply-update.bat");
        var pid = Environment.ProcessId;

        var script = $"""
            @echo off
            echo Waiting for FIRST Package Manager to exit...
            :waitloop
            tasklist /FI "PID eq {pid}" 2>NUL | find /I "{pid}" >NUL
            if not errorlevel 1 (
                timeout /t 1 /nobreak >NUL
                goto waitloop
            )
            echo Applying update...
            copy /Y "{downloadPath}" "{currentExe}" >NUL
            echo Update applied. Restarting...
            start "" "{currentExe}"
            del /Q "{scriptPath}"
            """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static void WriteUnixUpdateScript(string tempDir, string downloadPath, string currentExe)
    {
        var scriptPath = Path.Combine(tempDir, "apply-update.sh");
        var pid = Environment.ProcessId;

        var script = $"""
            #!/bin/bash
            echo "Waiting for FIRST Package Manager to exit..."
            while kill -0 {pid} 2>/dev/null; do
                sleep 1
            done
            echo "Applying update..."
            cp "{downloadPath}" "{currentExe}"
            chmod +x "{currentExe}"
            echo "Update applied. Restarting..."
            "{currentExe}" &
            rm -f "{scriptPath}"
            """;

        File.WriteAllText(scriptPath, script);

        // Make the script executable
        Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        })?.WaitForExit(5000);

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        var latestParts = ParseVersionParts(latest);
        var currentParts = ParseVersionParts(current);

        for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
        {
            var l = i < latestParts.Length ? latestParts[i] : 0;
            var c = i < currentParts.Length ? currentParts[i] : 0;
            if (l > c) return true;
            if (l < c) return false;
        }

        return false;
    }

    private static int[] ParseVersionParts(string version)
    {
        return version
            .Split('.', '-')
            .Select(p => int.TryParse(p, out var n) ? n : 0)
            .ToArray();
    }

    private static (string Url, string Sha256, long Size) FindPlatformAsset(
        JsonElement root, string platformId)
    {
        if (!root.TryGetProperty("assets", out var assets))
        {
            return ("", "", 0);
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains(platformId, StringComparison.OrdinalIgnoreCase))
            {
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var size = asset.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;

                // SHA-256 may be provided in a companion .sha256 asset or in release body.
                // For now, pass empty and rely on download manager verification if available.
                return (url, "", size);
            }
        }

        return ("", "", 0);
    }
}
