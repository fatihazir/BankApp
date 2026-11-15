namespace Persistence.IntegrationTests.Infrastructure;

/// <summary>
/// The clock of the application under test. It starts at the real time and then stands still, until a test moves it.
/// That makes ordering and expiry tests exact, without sleeping.
/// </summary>
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
