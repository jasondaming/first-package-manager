using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
using FrcToolsuite.Core.Install;
using FrcToolsuite.Core.Packages;

namespace FrcToolsuite.Gui.ViewModels;

public partial class WizardPackageItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _inclusion = "Optional";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private bool _requiresAdmin;

    [ObservableProperty]
    private bool _isAlreadyInstalled;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _downloadProgress;

    public string SizeDisplay => Size switch
    {
        >= 1_000_000_000 => $"{Size / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{Size / 1_000_000.0:F0} MB",
        >= 1_000 => $"{Size / 1_000.0:F0} KB",
        _ => $"{Size} B"
    };
}

public partial class UseCaseItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class VendorPackageItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private bool _isAlreadyInstalled;

    public ObservableCollection<VendorSubItem> SubItems { get; } = new();

    public string SizeDisplay => Size switch
    {
        >= 1_000_000_000 => $"{Size / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{Size / 1_000_000.0:F0} MB",
        >= 1_000 => $"{Size / 1_000.0:F0} KB",
        _ => $"{Size} B"
    };
}

public partial class VendorSubItem : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private long _size;

    public string SizeDisplay => Size switch
    {
        >= 1_000_000_000 => $"{Size / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{Size / 1_000_000.0:F0} MB",
        >= 1_000 => $"{Size / 1_000.0:F0} KB",
        _ => $"{Size} B"
    };
}

public partial class PreviousYearItem : ObservableObject
{
    [ObservableProperty]
    private int _year;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private long _sizeBytes;

    [ObservableProperty]
    private bool _isRemoved;

    [ObservableProperty]
    private bool _isRemoving;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public string SizeDisplay => SizeBytes switch
    {
        >= 1_000_000_000 => $"{SizeBytes / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{SizeBytes / 1_000_000.0:F0} MB",
        >= 1_000 => $"{SizeBytes / 1_000.0:F0} KB",
        _ => $"{SizeBytes} B"
    };
}

public partial class FirstRunWizardViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;
    private readonly Action? _dismissWizard;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _totalSteps = 4;

    [ObservableProperty]
    private string _selectedProgram = "FRC";

    [ObservableProperty]
    private string _stepTitle = "Welcome";

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _showInstallButton;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgress;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private bool _canSkipWizard = true;

    [ObservableProperty]
    private bool _installComplete;

    [ObservableProperty]
    private bool _hasPreviousYears;

    [ObservableProperty]
    private bool _previousYearsDismissed;

    public ObservableCollection<PreviousYearItem> PreviousYears { get; } = new();

    public string InstallPath
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return @"C:\Users\Public\wpilib\2026";
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "wpilib", "2026");
            }
        }
    }

    public ObservableCollection<string> Programs { get; } = new() { "FRC", "FTC", "Both" };

    public ObservableCollection<UseCaseItem> UseCases { get; } = new();

    public ObservableCollection<VendorPackageItem> VendorPackages { get; } = new();

    // Kept for review step and install progress tracking
    public ObservableCollection<WizardPackageItem> SelectedPackages { get; } = new();

    public string SelectedUseCase => UseCases.FirstOrDefault(u => u.IsSelected)?.Id ?? "team-dev";

    public string TotalDownloadSize
    {
        get
        {
            long total = 0;
            // WPILib core base size (JDK + VS Code + GradleRIO + Tools)
            total += 714_000_000;
            // Add vendor packages
            foreach (var vp in VendorPackages.Where(v => v.IsSelected))
            {
                total += vp.Size;
                foreach (var sub in vp.SubItems.Where(s => s.IsSelected))
                {
                    total += sub.Size;
                }
            }
            return total switch
            {
                >= 1_000_000_000 => $"{total / 1_000_000_000.0:F1} GB",
                >= 1_000_000 => $"{total / 1_000_000.0:F0} MB",
                _ => $"{total / 1_000.0:F0} KB"
            };
        }
    }

    public int SelectedVendorCount => VendorPackages.Count(v => v.IsSelected);

    public FirstRunWizardViewModel()
        : this(null, null)
    {
    }

    public FirstRunWizardViewModel(IPackageManager? packageManager, Action? dismissWizard)
    {
        _packageManager = packageManager;
        _dismissWizard = dismissWizard;
        LoadUseCases();
        LoadVendorPackages();
        DetectPreviousYears();
        UpdateStepState();
    }

    partial void OnCurrentStepChanged(int value)
    {
        UpdateStepState();
        if (value == 3)
        {
            BuildReviewPackageList();
        }
    }

    partial void OnSelectedProgramChanged(string value)
    {
        // Vendor packages are the same for FRC/FTC/Both in this simplified flow
    }

    private void UpdateStepState()
    {
        CanGoBack = CurrentStep > 1 && !IsInstalling;
        CanGoNext = CurrentStep < 3 && !IsInstalling;
        ShowInstallButton = CurrentStep == 3;
        CanSkipWizard = !IsInstalling && CurrentStep < 4;

        StepTitle = CurrentStep switch
        {
            1 => "Welcome",
            2 => "Choose Setup",
            3 => "Review",
            4 => "Installing",
            _ => "Welcome"
        };
    }

    private void LoadUseCases()
    {
        UseCases.Clear();
        UseCases.Add(new UseCaseItem
        {
            Id = "team-dev",
            Title = "Team Development",
            Description = "Full development environment with VS Code, build tools, and libraries. This is what most teams need.",
            Icon = "\U0001F4BB",
            IsSelected = true
        });
        UseCases.Add(new UseCaseItem
        {
            Id = "driver-station",
            Title = "Driver Station Computer",
            Description = "Minimal install for a dedicated Driver Station machine.",
            Icon = "\U0001F3AE",
            IsSelected = false
        });
        UseCases.Add(new UseCaseItem
        {
            Id = "usb-offline",
            Title = "Create USB Offline Stick",
            Description = "Download everything to a USB drive for offline installs at events.",
            Icon = "\U0001F4BE",
            IsSelected = false
        });
        UseCases.Add(new UseCaseItem
        {
            Id = "csa-volunteer",
            Title = "CSA / Event Volunteer",
            Description = "Diagnostic and support toolkit for CSAs and event volunteers.",
            Icon = "\U0001F6E0",
            IsSelected = false
        });

        foreach (var uc in UseCases)
        {
            uc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UseCaseItem.IsSelected) && uc.IsSelected)
                {
                    // Ensure only one use case is selected at a time
                    foreach (var other in UseCases)
                    {
                        if (other != uc && other.IsSelected)
                        {
                            other.IsSelected = false;
                        }
                    }
                    OnPropertyChanged(nameof(SelectedUseCase));
                }
            };
        }
    }

    private void LoadVendorPackages()
    {
        VendorPackages.Clear();

        var ctre = new VendorPackageItem
        {
            Id = "ctre.phoenix6",
            Name = "CTRE Phoenix 6",
            Description = "Vendor library for CTRE motor controllers and sensors",
            Size = 85_000_000,
            IsSelected = false
        };
        ctre.SubItems.Add(new VendorSubItem
        {
            Id = "ctre.phoenix-tuner-x",
            Name = "Phoenix Tuner X",
            Size = 120_000_000,
            IsSelected = false
        });

        var rev = new VendorPackageItem
        {
            Id = "rev.revlib",
            Name = "REV",
            Description = "Vendor library for REV motor controllers and sensors",
            Size = 45_000_000,
            IsSelected = false
        };
        rev.SubItems.Add(new VendorSubItem
        {
            Id = "rev.hardware-client",
            Name = "REV Hardware Client",
            Size = 95_000_000,
            IsSelected = false
        });

        var pathplanner = new VendorPackageItem
        {
            Id = "pathplanner.pathplannerlib",
            Name = "PathPlannerLib",
            Description = "Autonomous path planning library",
            Size = 12_000_000,
            IsSelected = false
        };

        var photon = new VendorPackageItem
        {
            Id = "photonvision.photonlib",
            Name = "PhotonVision",
            Description = "Vision processing library for AprilTag detection",
            Size = 18_000_000,
            IsSelected = false
        };

        var yagsl = new VendorPackageItem
        {
            Id = "yagsl.yagsl",
            Name = "YAGSL",
            Description = "Yet Another Generic Swerve Library",
            Size = 8_000_000,
            IsSelected = false
        };

        var advantagekit = new VendorPackageItem
        {
            Id = "advantagekit.advantagekit",
            Name = "AdvantageKit",
            Description = "Log-replay framework for robot code testing",
            Size = 15_000_000,
            IsSelected = false
        };

        VendorPackages.Add(ctre);
        VendorPackages.Add(rev);
        VendorPackages.Add(pathplanner);
        VendorPackages.Add(photon);
        VendorPackages.Add(yagsl);
        VendorPackages.Add(advantagekit);

        foreach (var vp in VendorPackages)
        {
            vp.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(VendorPackageItem.IsSelected))
                {
                    OnPropertyChanged(nameof(TotalDownloadSize));
                    OnPropertyChanged(nameof(SelectedVendorCount));
                }
            };
            foreach (var sub in vp.SubItems)
            {
                sub.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(VendorSubItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(TotalDownloadSize));
                    }
                };
            }
        }
    }

    private void BuildReviewPackageList()
    {
        SelectedPackages.Clear();

        // WPILib core (always included, shown as informational)
        AddReviewPackage("wpilib.jdk", "JDK 17", "Eclipse Adoptium JDK 17", 189_000_000, true);
        AddReviewPackage("wpilib.vscode", "VS Code", "Visual Studio Code editor", 105_000_000, true);
        AddReviewPackage("wpilib.gradlerio", "GradleRIO", "Gradle build system and WPILib plugins", 140_000_000, true);
        AddReviewPackage("wpilib.tools", "WPILib Tools", "Glass, Shuffleboard, SysId, and more", 280_000_000, true);

        // Selected vendor packages
        foreach (var vp in VendorPackages.Where(v => v.IsSelected))
        {
            AddReviewPackage(vp.Id, vp.Name, vp.Description, vp.Size, false);
            foreach (var sub in vp.SubItems.Where(s => s.IsSelected))
            {
                AddReviewPackage(sub.Id, sub.Name, "", sub.Size, false);
            }
        }

        OnPropertyChanged(nameof(TotalDownloadSize));
    }

    private void AddReviewPackage(string id, string name, string description, long size, bool isCore)
    {
        var item = new WizardPackageItem
        {
            Id = id,
            Name = name,
            Description = description,
            Inclusion = isCore ? "Core" : "Vendor",
            IsRequired = isCore,
            IsSelected = true,
            Size = size,
        };
        SelectedPackages.Add(item);
    }

    private void DetectPreviousYears()
    {
        PreviousYears.Clear();
        var detected = LegacyYearDetector.DetectPreviousYears();
        foreach (var install in detected)
        {
            PreviousYears.Add(new PreviousYearItem
            {
                Year = install.Year,
                Path = install.Path,
                SizeBytes = install.SizeBytes
            });
        }

        HasPreviousYears = PreviousYears.Count > 0;
        PreviousYearsDismissed = false;
    }

    [RelayCommand]
    private async Task UninstallYearAsync(PreviousYearItem item)
    {
        if (item.IsRemoving || item.IsRemoved)
        {
            return;
        }

        item.IsRemoving = true;
        item.StatusText = "Removing...";

        try
        {
            var progress = new Progress<string>(message =>
            {
                item.StatusText = message;
            });

            await LegacyYearDetector.UninstallYearAsync(item.Year, progress);

            item.IsRemoved = true;
            item.IsRemoving = false;
            item.StatusText = "Removed";

            // Update HasPreviousYears if all are removed
            if (PreviousYears.All(y => y.IsRemoved))
            {
                HasPreviousYears = false;
            }
        }
        catch (Exception ex)
        {
            item.IsRemoving = false;
            item.StatusText = $"Failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DismissPreviousYears()
    {
        PreviousYearsDismissed = true;
    }

    [RelayCommand]
    private void SelectUseCase(string useCaseId)
    {
        foreach (var uc in UseCases)
        {
            uc.IsSelected = uc.Id == useCaseId;
        }
        OnPropertyChanged(nameof(SelectedUseCase));
    }

    [RelayCommand]
    private void SkipWizard()
    {
        _dismissWizard?.Invoke();
    }

    [RelayCommand]
    private void GoNext()
    {
        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private async Task BeginInstallAsync()
    {
        CurrentStep = 4;
        IsInstalling = true;
        InstallComplete = false;
        InstallProgress = 0;
        InstallStatus = "Preparing downloads...";
        UpdateStepState();

        await ExecuteInstallAsync();
    }

    private async Task ExecuteInstallAsync()
    {
        if (_packageManager == null)
        {
            // Design mode / test harness: simulate progress
            var packages = SelectedPackages.ToList();
            for (int i = 0; i < packages.Count; i++)
            {
                var pkg = packages[i];
                pkg.StatusText = $"Downloading... 0 MB / {pkg.SizeDisplay}";
                InstallStatus = $"Downloading {pkg.Name}...";
                InstallProgress = (double)i / packages.Count * 100;
                await Task.Delay(50);

                pkg.StatusText = "Extracting...";
                await Task.Delay(30);

                pkg.StatusText = "Configuring...";
                await Task.Delay(20);

                pkg.StatusText = "Installed";
                pkg.DownloadProgress = 100;
            }

            InstallProgress = 100;
            InstallStatus = "Installation Complete!";
            InstallComplete = true;
            IsInstalling = false;
            return;
        }

        try
        {
            var selectedIds = VendorPackages
                .Where(v => v.IsSelected)
                .Select(v => v.Id)
                .ToList();

            InstallStatus = "Planning installation...";
            var plan = await _packageManager.PlanBundleInstallAsync("frc-base-2026", selectedIds);

            if (plan.Steps.Count == 0)
            {
                InstallProgress = 100;
                InstallStatus = "Everything is already installed.";
                InstallComplete = true;
                IsInstalling = false;
                _dismissWizard?.Invoke();
                return;
            }

            var progress = new Progress<InstallProgress>(p =>
            {
                if (p.TotalSteps > 0)
                {
                    InstallProgress = (double)p.CurrentStep / p.TotalSteps * 100;
                }

                var phase = p.Phase switch
                {
                    InstallPhase.Downloading => "Downloading",
                    InstallPhase.Extracting => "Extracting",
                    InstallPhase.Configuring => "Configuring",
                    InstallPhase.AwaitingAdmin => "Requesting administrator access for",
                    _ => "Processing"
                };
                InstallStatus = $"{phase} {p.CurrentPackageId}...";
            });

            await _packageManager.ExecutePlanAsync(plan, progress);

            InstallProgress = 100;
            InstallStatus = "Installation Complete!";
            InstallComplete = true;
            IsInstalling = false;
            _dismissWizard?.Invoke();
        }
        catch (Exception ex)
        {
            InstallStatus = $"Installation failed: {ex.Message}";
            IsInstalling = false;
        }
    }

    public string ExportStateJson()
    {
        var state = new
        {
            CurrentStep,
            TotalSteps,
            SelectedProgram,
            InstallPath,
            StepTitle,
            CanGoBack,
            CanGoNext,
            ShowInstallButton,
            IsInstalling,
            InstallProgress,
            CanSkipWizard,
            TotalDownloadSize,
            SelectedUseCase,
            SelectedVendorCount,
            HasPreviousYears,
            PreviousYearsDismissed,
            PreviousYears = PreviousYears.Select(y => new
            {
                y.Year,
                y.Path,
                y.SizeBytes,
                y.SizeDisplay,
                y.IsRemoved,
                y.IsRemoving,
                y.StatusText
            }).ToArray(),
            UseCases = UseCases.Select(u => new
            {
                u.Id,
                u.Title,
                u.Description,
                u.IsSelected
            }).ToArray(),
            VendorPackages = VendorPackages.Select(v => new
            {
                v.Id,
                v.Name,
                v.IsSelected,
                v.SizeDisplay,
                SubItems = v.SubItems.Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.IsSelected,
                    s.SizeDisplay
                }).ToArray()
            }).ToArray(),
            ReviewPackages = SelectedPackages.Select(p => new
            {
                p.Id,
                p.Name,
                p.Inclusion,
                p.IsSelected,
                p.IsRequired,
                p.SizeDisplay
            }).ToArray()
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
