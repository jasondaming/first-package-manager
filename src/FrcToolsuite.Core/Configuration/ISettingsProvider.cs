namespace FrcToolsuite.Core.Configuration;

public interface ISettingsProvider
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
