using FrcToolsuite.Cli.Output;
using FrcToolsuite.Core.Registry;

namespace FrcToolsuite.Cli.Commands;

public static class SearchCommand
{
    public static async Task<int> ExecuteAsync(
        IRegistryClient registryClient,
        string query)
    {
        try
        {
            ConsoleHelper.WriteInfo($"Searching for '{query}'...");
            ConsoleHelper.WriteInfo("");

            var results = await registryClient.SearchAsync(query);

            if (results.Count == 0)
            {
                ConsoleHelper.WriteInfo("No packages found matching your query.");
                return 0;
            }

            var headers = new[] { "Package", "Name", "Version", "Category", "Description" };
            var rows = results.Select(p => new[]
            {
                p.Id,
                p.Name,
                p.Version,
                p.Category,
                Truncate(p.Description, 50)
            }).ToList();

            ConsoleHelper.WriteTable(headers, rows);
            ConsoleHelper.WriteInfo("");
            ConsoleHelper.WriteInfo($"{results.Count} package(s) found.");

            if (registryClient.IsOffline)
            {
                ConsoleHelper.WriteWarning("Note: Showing cached data (offline mode).");
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteError($"Search failed: {ex.Message}");
            return 1;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
