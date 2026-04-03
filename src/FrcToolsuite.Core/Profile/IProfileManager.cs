using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Core.Profile;

public interface IProfileManager
{
    Task<TeamProfile> CreateProfileAsync(
        string profileName,
        int teamNumber,
        CompetitionProgram competition,
        int season,
        CancellationToken ct = default);

    Task<TeamProfile> GetProfileAsync(
        string profileName,
        CancellationToken ct = default);

    Task<IReadOnlyList<TeamProfile>> ListProfilesAsync(
        CancellationToken ct = default);

    Task UpdateProfileAsync(
        TeamProfile profile,
        CancellationToken ct = default);

    Task DeleteProfileAsync(
        string profileName,
        CancellationToken ct = default);

    Task ExportProfileAsync(
        string profileName,
        string targetPath,
        CancellationToken ct = default);

    Task<TeamProfile> ImportProfileAsync(
        string sourcePath,
        CancellationToken ct = default);
}
