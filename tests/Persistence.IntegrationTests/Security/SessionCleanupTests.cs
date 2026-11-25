using Application.Services.Security;
using BankingSystemCore.Security.Tokens;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.IntegrationTests.Infrastructure;

namespace Persistence.IntegrationTests.Security;

/// <summary>Ended sessions are deleted after a week; live ones and recent ones stay.</summary>
[Collection(DatabaseCollection.Name)]
public class SessionCleanupTests(SqlServerFixture database) : IntegrationTestBase(database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> AddSessionAsync(Guid userId, DateTime expiresAt, DateTime? revokedAt = null)
    {
        return await UsingDbContext(async db =>
        {
            var session = new UserSession
            {
                UserId = userId, RefreshTokenHash = SecureToken.Hash(Guid.NewGuid().ToString()),
                RefreshTokenExpiresAt = expiresAt, RevokedDate = revokedAt, CreateDate = Clock.Now.UtcDateTime
            };
            db.UserSessions.Add(session);
            await db.SaveChangesAsync(Ct);
            return session.Id;
        });
    }

    private async Task<int> CleanAsync()
    {
        using var scope = NewScope();
        return await Resolve<UserSessionCleanupService>(scope).CleanAsync(Ct);
    }

    [Fact]
    public async Task OnlySessionsThatEndedMoreThanAWeekAgo_AreDeleted()
    {
        Guid userId = (await Send(TestData.IndividualCreate())).Id;
        DateTime now = Clock.Now.UtcDateTime;

        Guid live = await AddSessionAsync(userId, now.AddMinutes(10));
        Guid expiredLongAgo = await AddSessionAsync(userId, now.AddDays(-10));
        Guid revokedLongAgo = await AddSessionAsync(userId, now.AddMinutes(10), revokedAt: now.AddDays(-10));
        Guid revokedAnHourAgo = await AddSessionAsync(userId, now.AddMinutes(10), revokedAt: now.AddHours(-1));
        Guid expiredYesterday = await AddSessionAsync(userId, now.AddDays(-1));

        int deleted = await CleanAsync();

        deleted.ShouldBe(2);
        var left = await UsingDbContext(db => db.UserSessions.Select(s => s.Id).ToListAsync(Ct));
        left.ShouldBe([live, revokedAnHourAgo, expiredYesterday], ignoreOrder: true);
        left.ShouldNotContain(expiredLongAgo);
        left.ShouldNotContain(revokedLongAgo);
    }

    [Fact]
    public async Task TimePassing_MakesMoreSessionsOldEnough()
    {
        Guid userId = (await Send(TestData.IndividualCreate())).Id;
        await AddSessionAsync(userId, Clock.Now.UtcDateTime.AddDays(-1));

        (await CleanAsync()).ShouldBe(0);

        Clock.Advance(TimeSpan.FromDays(7));

        (await CleanAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task NothingToClean_IsNotAnError() => (await CleanAsync()).ShouldBe(0);
}

/// <summary>A small batch size: the cleanup repeats until nothing old is left.</summary>
[Collection(DatabaseCollection.Name)]
public class SessionCleanupInBatchesTests(SqlServerFixture database)
    : IntegrationTestBase(database, new() { ["Security:SessionCleanup:BatchSize"] = "2" })
{
    [Fact]
    public async Task ManyOldSessions_AreAllDeleted_TwoAtATime()
    {
        Guid userId = (await Send(TestData.IndividualCreate())).Id;
        await UsingDbContext(async db =>
        {
            for (int i = 0; i < 5; i++)
                db.UserSessions.Add(new UserSession
                {
                    UserId = userId, RefreshTokenHash = SecureToken.Hash(Guid.NewGuid().ToString()),
                    RefreshTokenExpiresAt = Clock.Now.UtcDateTime.AddDays(-30), CreateDate = Clock.Now.UtcDateTime
                });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        using var scope = NewScope();
        int deleted = await Resolve<UserSessionCleanupService>(scope).CleanAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(5);
        (await UsingDbContext(db => db.UserSessions.CountAsync(TestContext.Current.CancellationToken))).ShouldBe(0);
    }
}
