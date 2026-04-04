using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Gui.ViewModels;

public class InstalledPackageViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string InstalledDate { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4E6";
}

public partial class InstalledPageViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;

    public ObservableCollection<InstalledPackageViewModel> InstalledPackages { get; } = new();

    [ObservableProperty]
    private string _totalSize = "0 MB";

    [ObservableProperty]
    private bool _isUninstalling;

    public InstalledPageViewModel()
        : this(null)
    {
    }

    public InstalledPageViewModel(IPackageManager? packageManager)
    {
        _packageManager = packageManager;

        if (_packageManager != null)
        {
            _ = LoadInstalledPackagesAsync();
        }
        else
        {
            LoadMockData();
        }
    }

    private void LoadMockData()
    {
        InstalledPackages.Add(new InstalledPackageViewModel
        {
            Name = "WPILib",
            Publisher = "WPI",
            Version = "2026.1.1",
            InstalledDate = "2026-01-15",
            Size = "1.2 GB",
            Icon = "\U0001F916"
        });
        InstalledPackages.Add(new InstalledPackageViewModel
        {
            Name = "FRC Driver Station",
            Publisher = "NI / FIRST",
            Version = "25.0.1",
            InstalledDate = "2026-01-15",
            Size = "320 MB",
            Icon = "\U0001F3AE"
        });
        InstalledPackages.Add(new InstalledPackageViewModel
        {
            Name = "AdvantageScope",
            Publisher = "Mechanical Advantage",
            Version = "4.0.0",
            InstalledDate = "2026-02-10",
            Size = "95 MB",
            Icon = "\U0001F4CA"
        });
        TotalSize = "1.6 GB";
    }

    private async Task LoadInstalledPackagesAsync()
    {
        try
        {
            var allInstalled = new List<InstalledPackageViewModel>();

            // Get packages installed via our package manager
            if (_packageManager != null)
            {
                var installed = await _packageManager.GetInstalledPackagesAsync();
                foreach (var pkg in installed)
                {
                    allInstalled.Add(new InstalledPackageViewModel
                    {
                        Name = pkg.PackageId,
                        Version = pkg.Version,
                        InstalledDate = pkg.InstalledAt.ToString("yyyy-MM-dd"),
                        Size = GetDirectorySize(pkg.InstallPath)
                    });
                }
            }

            // Also detect legacy WPILib installations
            var legacyIds = LegacyInstallDetector.DetectInstalledPackages();
            var alreadyListed = new HashSet<string>(
                allInstalled.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

            var wpilibBase = @"C:\Users\Public\wpilib\2026";
            foreach (var id in legacyIds)
            {
                if (alreadyListed.Contains(id))
                {
                    continue;
                }

                var legacyDir = id switch
                {
                    "wpilib.jdk" => Path.Combine(wpilibBase, "jdk"),
                    "wpilib.vscode" => Path.Combine(wpilibBase, "vscode"),
                    "wpilib.tools" => Path.Combine(wpilibBase, "tools"),
                    "wpilib.gradlerio" => Path.Combine(wpilibBase, "maven"),
                    "wpilib.advantagescope" => Path.Combine(wpilibBase, "advantagescope"),
                    "wpilib.elastic" => Path.Combine(wpilibBase, "elastic"),
                    _ => null
                };

                allInstalled.Add(new InstalledPackageViewModel
                {
                    Name = id,
                    Version = "(legacy)",
                    InstalledDate = "(WPILib installer)",
                    Size = legacyDir != null ? GetDirectorySize(legacyDir) : ""
                });
            }

            if (allInstalled.Count > 0)
            {
                InstalledPackages.Clear();
                foreach (var pkg in allInstalled)
                {
                    InstalledPackages.Add(pkg);
                }
                TotalSize = $"{allInstalled.Count} packages";
            }
        }
        catch
        {
            // Keep mock data as fallback
        }
    }

    private static string GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
        {
            return "";
        }

        try
        {
            long bytes = 0;
            foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                bytes += file.Length;
            }
            return FormatBytes(bytes);
        }
        catch
        {
            return "";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824)
        {
            return $"{bytes / 1_073_741_824.0:F1} GB";
        }

        if (bytes >= 1_048_576)
        {
            return $"{bytes / 1_048_576.0:F0} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F0} KB";
        }

        return $"{bytes} B";
    }

    [RelayCommand]
    private async Task UninstallPackageAsync(string packageName)
    {
        if (_packageManager == null)
        {
            return;
        }

        IsUninstalling = true;
        try
        {
            var plan = await _packageManager.PlanUninstallAsync(new[] { packageName });
            if (plan.Steps.Count > 0)
            {
                await _packageManager.ExecutePlanAsync(plan);
            }

            var item = InstalledPackages.FirstOrDefault(p => p.Name == packageName);
            if (item != null)
            {
                InstalledPackages.Remove(item);
            }
        }
        catch
        {
            // Uninstall failed; keep item in list so user can retry
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    public string ExportStateJson()
    {
        var state = new
        {
            InstalledCount = InstalledPackages.Count,
            TotalSize
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
