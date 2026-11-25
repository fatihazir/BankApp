namespace Application.UnitTests.Support;

/// <summary>
/// A clock the test controls. It starts at a fixed instant, so a result never depends on the day the test runs.
/// </summary>
public sealed class TestClock(DateTimeOffset? start = null) : TimeProvider
{
    public static readonly DateTimeOffset DefaultStart = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; set; } = start ?? DefaultStart;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
