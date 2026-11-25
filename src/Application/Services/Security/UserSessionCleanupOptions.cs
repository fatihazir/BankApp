namespace Application.Services.Security;

/// <summary>Bound from the "Security:SessionCleanup" configuration section.</summary>
public class UserSessionCleanupOptions
{
    public const string SectionName = "Security:SessionCleanup";

    /// <summary>Turn the background cleanup off, e.g. when another process does it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the cleanup runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long an ended session (revoked, or its refresh token expired) is kept before it is deleted.
    /// The rows are the record of who was logged in and why a session ended.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Rows deleted per statement, so one cleanup never locks the table for long.</summary>
    public int BatchSize { get; set; } = 500;
}
