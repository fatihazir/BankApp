using BankingSystemCore.Mailing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BankingSystemCore.UnitTests.Mailing;

public class LoggingEmailSenderTests
{
    private sealed class Capture : ILogger<LoggingEmailSender>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static readonly EmailMessage Message = new("ali@bank.test", "Verify", "Secret link: https://bank/verify?token=SECRET-TOKEN");

    private static (LoggingEmailSender Sender, Capture Log) Create(bool logBody)
    {
        var log = new Capture();
        return (new LoggingEmailSender(log, Options.Create(new MailingOptions { LogBody = logBody })), log);
    }

    [Fact]
    public async Task ItDoesNotSendAnything_ButSaysSoLoudly()
    {
        var (sender, log) = Create(logBody: false);

        await sender.SendAsync(Message, TestContext.Current.CancellationToken);

        var entry = log.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("NOT sent");
        entry.Message.ShouldContain("ali@bank.test");
        entry.Message.ShouldContain("Verify");
    }

    [Fact]
    public async Task ByDefault_TheBodyWithItsSecretToken_IsNeverWrittenToTheLog()
    {
        var (sender, log) = Create(logBody: false);

        await sender.SendAsync(Message, TestContext.Current.CancellationToken);

        log.Entries.ShouldNotContain(e => e.Message.Contains("SECRET-TOKEN"));
    }

    [Fact]
    public async Task InDevelopment_TheBodyCanBeLogged_SoTheFlowCanBeTriedWithoutAMailServer()
    {
        var (sender, log) = Create(logBody: true);

        await sender.SendAsync(Message, TestContext.Current.CancellationToken);

        log.Entries.ShouldContain(e => e.Level == LogLevel.Information && e.Message.Contains("SECRET-TOKEN"));
    }

    [Fact]
    public void LoggingTheBody_IsOffUnlessSwitchedOn() => new MailingOptions().LogBody.ShouldBeFalse();
}
