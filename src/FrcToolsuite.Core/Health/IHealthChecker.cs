namespace FrcToolsuite.Core.Health;

public interface IHealthChecker
{
    Task<HealthReport> RunFullCheckAsync(CancellationToken ct = default);

    Task<HealthReport> RunCheckAsync(string checkName, CancellationToken ct = default);

    Task<bool> RepairAsync(HealthIssue issue, CancellationToken ct = default);
}

public class HealthReport
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool IsHealthy => Issues.Count == 0;
    public List<HealthIssue> Issues { get; set; } = [];
}

public class HealthIssue
{
    public HealthSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool CanAutoFix { get; set; }
    public string? PackageId { get; set; }
}

public enum HealthSeverity
{
    Info,
    Warning,
    Error,
    Critical
}
