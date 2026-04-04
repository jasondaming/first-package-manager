using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrcToolsuite.Core;
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

    public string SizeDisplay => Size switch
    {
        >= 1_000_000_000 => $"{Size / 1_000_000_000.0:F1} GB",
        >= 1_000_000 => $"{Size / 1_000_000.0:F0} MB",
        >= 1_000 => $"{Size / 1_000.0:F0} KB",
        _ => $"{Size} B"
    };
}

public partial class FirstRunWizardViewModel : ObservableObject, IStateExportable
{
    private readonly IPackageManager? _packageManager;
    private readonly Action? _dismissWizard;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _totalSteps = 5;

    [ObservableProperty]
    private string _selectedProgram = "FRC";

    [ObservableProperty]
    private string _selectedBundle = "FRC Java Starter Kit";

    [ObservableProperty]
    private string _installPath = @"C:\frc";

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

    public ObservableCollection<string> Programs { get; } = new() { "FRC", "FTC" };

    public ObservableCollection<string> Bundles { get; } = new()
    {
        "FRC Java Starter Kit",
        "FRC C++ Starter Kit",
        "CSA USB Toolkit"
    };

    public ObservableCollection<WizardPackageItem> SelectedPackages { get; } = new();

    public string TotalDownloadSize
    {
        get
        {
            var total = SelectedPackages.Where(p => p.IsSelected).Sum(p => p.Size);
            return total switch
            {
                >= 1_000_000_000 => $"{total / 1_000_000_000.0:F1} GB",
                >= 1_000_000 => $"{total / 1_000_000.0:F0} MB",
                _ => $"{total / 1_000.0:F0} KB"
            };
        }
    }

    public int SelectedPackageCount => SelectedPackages.Count(p => p.IsSelected);

    public FirstRunWizardViewModel()
        : this(null, null)
    {
    }

    public FirstRunWizardViewModel(IPackageManager? packageManager, Action? dismissWizard)
    {
        _packageManager = packageManager;
        _dismissWizard = dismissWizard;
        UpdateBundlesForProgram();
        LoadBundlePackages();
        UpdateStepState();
    }

    partial void OnCurrentStepChanged(int value)
    {
        UpdateStepState();
    }

    partial void OnSelectedProgramChanged(string value)
    {
        UpdateBundlesForProgram();
        LoadBundlePackages();
    }

    partial void OnSelectedBundleChanged(string value)
    {
        LoadBundlePackages();
    }

    private void UpdateStepState()
    {
        CanGoBack = CurrentStep > 1 && !IsInstalling;
        CanGoNext = CurrentStep < 4 && !IsInstalling;
        ShowInstallButton = CurrentStep == 4;
        CanSkipWizard = !IsInstalling && CurrentStep < 5;

        StepTitle = CurrentStep switch
        {
            1 => "Welcome",
            2 => "Bundle Selection",
            3 => "Install Location",
            4 => "Review",
            5 => "Installing",
            _ => "Welcome"
        };
    }

    private void UpdateBundlesForProgram()
    {
        Bundles.Clear();
        if (SelectedProgram == "FRC")
        {
            Bundles.Add("FRC Java Starter Kit");
            Bundles.Add("FRC C++ Starter Kit");
            Bundles.Add("CSA USB Toolkit");
        }
        else
        {
            Bundles.Add("FTC Starter Kit");
        }

        if (Bundles.Count > 0 && !Bundles.Contains(SelectedBundle))
        {
            SelectedBundle = Bundles[0];
        }
    }

    private void LoadBundlePackages()
    {
        SelectedPackages.Clear();

        if (SelectedBundle == "FRC Java Starter Kit")
        {
            AddPackage("wpilib.jdk", "JDK 17", "Eclipse Adoptium JDK 17 for Java development", "Required", 189_000_000);
            AddPackage("wpilib.vscode", "VS Code", "Visual Studio Code editor", "Required", 105_000_000);
            AddPackage("wpilib.vscode-wpilib", "WPILib Extension", "WPILib VS Code extension", "Required", 25_000_000);
            AddPackage("wpilib.gradle-wrapper", "Gradle", "Gradle build system", "Required", 140_000_000);
            AddPackage("wpilib.tools", "WPILib Tools", "Glass, Shuffleboard, SysId, and more", "Default", 250_000_000);
            AddPackage("wpilib.advantagescope", "AdvantageScope", "Robot telemetry viewer", "Default", 120_000_000);
            AddPackage("ctre.phoenix-framework", "CTRE Phoenix", "Vendor library for CTRE hardware", "Optional", 85_000_000);
            AddPackage("rev.revlib", "REVLib", "Vendor library for REV hardware", "Optional", 45_000_000);
            AddPackage("pathplanner.pathplannerlib", "PathPlannerLib", "Autonomous path planning library", "Optional", 12_000_000);
        }
        else if (SelectedBundle == "FRC C++ Starter Kit")
        {
            AddPackage("wpilib.vscode", "VS Code", "Visual Studio Code editor", "Required", 105_000_000);
            AddPackage("wpilib.vscode-wpilib", "WPILib Extension", "WPILib VS Code extension", "Required", 25_000_000);
            AddPackage("wpilib.gradle-wrapper", "Gradle", "Gradle build system", "Required", 140_000_000);
            AddPackage("wpilib.tools", "WPILib Tools", "Glass, Shuffleboard, SysId, and more", "Default", 250_000_000);
            AddPackage("wpilib.advantagescope", "AdvantageScope", "Robot telemetry viewer", "Default", 120_000_000);
            AddPackage("ctre.phoenix-framework", "CTRE Phoenix", "Vendor library for CTRE hardware", "Optional", 85_000_000);
            AddPackage("rev.revlib", "REVLib", "Vendor library for REV hardware", "Optional", 45_000_000);
            AddPackage("pathplanner.pathplannerlib", "PathPlannerLib", "Autonomous path planning library", "Optional", 12_000_000);
        }
        else if (SelectedBundle == "CSA USB Toolkit")
        {
            AddPackage("wpilib.jdk", "JDK 17", "Eclipse Adoptium JDK 17 for Java development", "Required", 189_000_000);
            AddPackage("wpilib.tools", "WPILib Tools", "Glass, OutlineViewer, and diagnostic tools", "Required", 250_000_000);
            AddPackage("wpilib.advantagescope", "AdvantageScope", "Log viewer for diagnosing robot issues", "Required", 120_000_000);
            AddPackage("ctre.phoenix-framework", "CTRE Phoenix", "Phoenix Tuner for CTRE diagnostics", "Default", 85_000_000);
            AddPackage("rev.revlib", "REVLib", "REV Hardware Client support", "Default", 45_000_000);
        }
        else if (SelectedBundle == "FTC Starter Kit")
        {
            AddPackage("wpilib.jdk", "JDK 17", "Eclipse Adoptium JDK 17 for Java development", "Required", 189_000_000);
            AddPackage("wpilib.gradle-wrapper", "Gradle", "Gradle build system", "Required", 140_000_000);
        }

        OnPropertyChanged(nameof(TotalDownloadSize));
        OnPropertyChanged(nameof(SelectedPackageCount));
    }

    private void AddPackage(string id, string name, string description, string inclusion, long size, bool requiresAdmin = false)
    {
        var item = new WizardPackageItem
        {
            Id = id,
            Name = name,
            Description = description,
            Inclusion = inclusion,
            IsRequired = inclusion == "Required",
            IsSelected = inclusion is "Required" or "Default",
            Size = size,
            RequiresAdmin = requiresAdmin,
        };
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WizardPackageItem.IsSelected))
            {
                OnPropertyChanged(nameof(TotalDownloadSize));
                OnPropertyChanged(nameof(SelectedPackageCount));
            }
        };
        SelectedPackages.Add(item);
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
        CurrentStep = 5;
        IsInstalling = true;
        InstallProgress = 0;
        InstallStatus = "Preparing downloads...";
        UpdateStepState();

        await ExecuteInstallAsync();
    }

    private async Task ExecuteInstallAsync()
    {
        if (_packageManager == null)
        {
            InstallStatus = "Package manager is not available (running in design mode).";
            return;
        }

        try
        {
            var bundleId = SelectedBundle switch
            {
                "FRC Java Starter Kit" => "frc-java-starter-2026",
                "FRC C++ Starter Kit" => "frc-cpp-starter-2026",
                "CSA USB Toolkit" => "csa-usb-toolkit-2026",
                "FTC Starter Kit" => "ftc-starter-2026",
                _ => "frc-java-starter-2026"
            };

            var selectedIds = SelectedPackages
                .Where(p => p.IsSelected && !p.IsRequired)
                .Select(p => p.Id)
                .ToList();

            InstallStatus = "Planning installation...";
            var plan = await _packageManager.PlanBundleInstallAsync(bundleId, selectedIds);

            if (plan.Steps.Count == 0)
            {
                InstallProgress = 100;
                InstallStatus = "Everything is already installed.";
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
            InstallStatus = "Installation complete!";
            IsInstalling = false;
            _dismissWizard?.Invoke();
        }
        catch (Exception ex)
        {
            InstallStatus = $"Installation failed: {ex.Message}";
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task BrowseInstallPathAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.StorageProvider is { } storage)
        {
            var result = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Install Directory",
                AllowMultiple = false
            });
            if (result.Count > 0)
            {
                InstallPath = result[0].Path.LocalPath;
            }
        }
    }

    public string ExportStateJson()
    {
        var state = new
        {
            CurrentStep,
            TotalSteps,
            SelectedProgram,
            SelectedBundle,
            InstallPath,
            StepTitle,
            CanGoBack,
            CanGoNext,
            ShowInstallButton,
            IsInstalling,
            InstallProgress,
            CanSkipWizard,
            TotalDownloadSize,
            SelectedPackageCount,
            Packages = SelectedPackages.Select(p => new
            {
                p.Id,
                p.Name,
                p.Inclusion,
                p.IsSelected,
                p.IsRequired,
                p.RequiresAdmin,
                p.SizeDisplay
            }).ToArray()
        };
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }
}
