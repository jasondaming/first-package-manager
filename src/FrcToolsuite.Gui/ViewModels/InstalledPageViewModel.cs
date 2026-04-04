using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
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
        LoadMockData();

        if (_packageManager != null)
        {
            _ = LoadInstalledPackagesAsync();
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
        if (_packageManager == null)
        {
            return;
        }

        try
        {
            var installed = await _packageManager.GetInstalledPackagesAsync();
            if (installed.Count > 0)
            {
                InstalledPackages.Clear();
                long totalBytes = 0;

                foreach (var pkg in installed)
                {
                    long pkgBytes = 0;
                    if (Directory.Exists(pkg.InstallPath))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(pkg.InstallPath);
                            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                pkgBytes += file.Length;
                            }
                        }
                        catch
                        {
                            // Skip directories we cannot enumerate
                        }
                    }

                    totalBytes += pkgBytes;

                    InstalledPackages.Add(new InstalledPackageViewModel
                    {
                        Name = pkg.PackageId,
                        Version = pkg.Version,
                        InstalledDate = pkg.InstalledAt.ToString("yyyy-MM-dd"),
                        Size = FormatBytes(pkgBytes)
                    });
                }

                TotalSize = FormatBytes(totalBytes);
            }
        }
        catch
        {
            // Keep mock data as fallback
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
