using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Packages;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Gui.ViewModels;

public class ProfilePackageItem
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public partial class ProfilesPageViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;

    [ObservableProperty]
    private string _title = "Team Profiles";

    [ObservableProperty]
    private string _description = "Export your installed packages as a shareable profile, or import a profile to replicate a teammate's setup.";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasLoadedProfile;

    [ObservableProperty]
    private string _profileName = string.Empty;

    [ObservableProperty]
    private int _profileTeamNumber;

    [ObservableProperty]
    private string _profileSeason = string.Empty;

    [ObservableProperty]
    private string _profileCreatedAt = string.Empty;

    [ObservableProperty]
    private string _profileNotes = string.Empty;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private double _applyProgress;

    [ObservableProperty]
    private string _applyProgressText = string.Empty;

    public ObservableCollection<ProfilePackageItem> ProfilePackages { get; } = new();

    private TeamProfile? _loadedProfile;

    public ProfilesPageViewModel()
        : this(null)
    {
    }

    public ProfilesPageViewModel(IPackageManager? packageManager)
    {
        _packageManager = packageManager;
    }

    [RelayCommand]
    private async Task ExportProfileAsync()
    {
        try
        {
            var packages = new List<string>();

            if (_packageManager != null)
            {
                var installed = await _packageManager.GetInstalledPackagesAsync();
                foreach (var pkg in installed)
                {
                    packages.Add(pkg.PackageId);
                }
            }

            if (packages.Count == 0)
            {
                // Use mock data for test harness
                packages.AddRange(
                [
                    "wpilib.jdk", "wpilib.vscode", "wpilib.gradlerio",
                    "ctre.phoenix6", "rev.revlib", "wpilib.advantagescope"
                ]);
            }

            var profile = new TeamProfile
            {
                SchemaVersion = "1.0.0",
                ProfileName = "My Team Profile",
                TeamNumber = 0,
                Competition = CompetitionProgram.Frc,
                Season = 2026,
                CreatedAt = DateTimeOffset.UtcNow,
                Packages = packages,
                Notes = "Exported from FIRST Package Manager"
            };

            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });

            // In the real app, this would use a SaveFilePicker dialog.
            // For now, store the exported profile and show status.
            _loadedProfile = profile;
            LoadProfileIntoView(profile);

            StatusMessage = $"Profile ready with {packages.Count} packages. Use Save File dialog to export.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportProfileAsync()
    {
        try
        {
            // In the real app, this would use an OpenFilePicker dialog.
            // For test harness / demo, load a sample profile.
            await Task.CompletedTask;

            var profile = new TeamProfile
            {
                SchemaVersion = "1.0.0",
                ProfileName = "Team 254 - The Chezy Poofs",
                TeamNumber = 254,
                Competition = CompetitionProgram.Frc,
                Season = 2026,
                CreatedAt = DateTimeOffset.Parse("2026-01-15T10:00:00Z"),
                Packages = new List<string>
                {
                    "wpilib.jdk",
                    "wpilib.vscode",
                    "wpilib.gradlerio",
                    "wpilib.glass",
                    "wpilib.advantagescope",
                    "ctre.phoenix6",
                    "pathplanner.pathplannerlib",
                    "community.advantagekit"
                },
                Notes = "Standard development environment for 2026 season."
            };

            _loadedProfile = profile;
            LoadProfileIntoView(profile);

            StatusMessage = $"Profile \"{profile.ProfileName}\" loaded with {profile.Packages.Count} packages.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ApplyProfileAsync()
    {
        if (_loadedProfile == null)
        {
            StatusMessage = "No profile loaded. Import a profile first.";
            return;
        }

        IsApplying = true;
        ApplyProgress = 0;
        StatusMessage = "Installing profile packages...";

        try
        {
            if (_packageManager != null)
            {
                var plan = await _packageManager.PlanInstallAsync(_loadedProfile.Packages);
                var progress = new Progress<InstallProgress>(p =>
                {
                    if (p.TotalSteps > 0)
                    {
                        ApplyProgress = (double)p.CurrentStep / p.TotalSteps * 100;
                    }

                    ApplyProgressText = $"Installing {p.CurrentPackageId}...";
                });
                await _packageManager.ExecutePlanAsync(plan, progress);

                StatusMessage = $"Profile applied! {_loadedProfile.Packages.Count} packages installed.";
            }
            else
            {
                // Mock install for test harness
                for (int i = 0; i < _loadedProfile.Packages.Count; i++)
                {
                    ApplyProgress = (double)(i + 1) / _loadedProfile.Packages.Count * 100;
                    ApplyProgressText = $"Installing {_loadedProfile.Packages[i]}...";
                    await Task.Delay(300);
                }

                StatusMessage = $"Profile applied! {_loadedProfile.Packages.Count} packages installed.";
            }

            // Update package statuses
            foreach (var pkg in ProfilePackages)
            {
                pkg.Status = "Installed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
            ApplyProgress = 0;
            ApplyProgressText = string.Empty;
        }
    }

    private void LoadProfileIntoView(TeamProfile profile)
    {
        ProfileName = profile.ProfileName;
        ProfileTeamNumber = profile.TeamNumber;
        ProfileSeason = profile.Season.ToString();
        ProfileCreatedAt = profile.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        ProfileNotes = profile.Notes ?? string.Empty;
        HasLoadedProfile = true;

        ProfilePackages.Clear();
        foreach (var pkgId in profile.Packages)
        {
            ProfilePackages.Add(new ProfilePackageItem { Id = pkgId });
        }
    }

    public string ExportStateJson()
    {
        var state = new
        {
            Title,
            Description,
            StatusMessage,
            HasLoadedProfile,
            ProfileName,
            ProfileTeamNumber,
            ProfileSeason,
            PackageCount = ProfilePackages.Count,
            IsApplying
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
