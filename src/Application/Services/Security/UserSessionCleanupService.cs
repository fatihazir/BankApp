using Application.Services.Repositories;
using Microsoft.Extensions.Options;

namespace Application.Services.Security;

/// <summary>Deletes sessions that ended long ago, so the table does not grow forever.</summary>
public class UserSessionCleanupService
{
    private readonly IUserSessionRepository _sessions;
    private readonly TimeProvider _time;
    private readonly UserSessionCleanupOptions _options;

    public UserSessionCleanupService(IUserSessionRepository sessions, TimeProvider time, IOptions<UserSessionCleanupOptions> options)
    {
        _sessions = sessions;
        _time = time;
        _options = options.Value;
    }

    /// <summary>Returns how many sessions were deleted.</summary>
    public async Task<int> CleanAsync(CancellationToken cancellationToken = default)
    {
        DateTime before = _time.GetUtcNow().UtcDateTime.AddDays(-_options.RetentionDays);
        int total = 0;
        int deleted;

        do
        {
            deleted = await _sessions.DeleteEndedBeforeAsync(before, _options.BatchSize, cancellationToken);
            total += deleted;
        }
        while (deleted == _options.BatchSize);

        return total;
    }
}
