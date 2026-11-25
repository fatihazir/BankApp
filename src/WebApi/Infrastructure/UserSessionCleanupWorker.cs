using Application.Services.Security;
using Microsoft.Extensions.Options;

namespace WebApi.Infrastructure;

/// <summary>
/// Runs <see cref="UserSessionCleanupService"/> on a timer. Every instance of the API may run it:
/// deleting rows that are already gone does no harm.
/// </summary>
public sealed class UserSessionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly UserSessionCleanupOptions _options;
    private readonly ILogger<UserSessionCleanupWorker> _logger;

    public UserSessionCleanupWorker(
        IServiceScopeFactory scopes, IOptions<UserSessionCleanupOptions> options, ILogger<UserSessionCleanupWorker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(_options.Interval);
        do
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                int deleted = await scope.ServiceProvider.GetRequiredService<UserSessionCleanupService>().CleanAsync(stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Deleted {Count} ended sessions.", deleted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run must not stop the worker; the next run tries again.
                _logger.LogError(exception, "The session cleanup failed.");
            }
        }
        while (await WaitForNextRunAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextRunAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }
}
