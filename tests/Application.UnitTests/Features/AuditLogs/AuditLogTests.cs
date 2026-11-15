using System.Linq.Expressions;
using Application.Features.AuditLogs.Queries.GetList;
using Application.Services.Auditing;
using Application.Services.Repositories;
using BankingSystemCore.Application.Pipelines.Auditing;
using BankingSystemCore.Repositories;
using Domain.Entities;
using FluentValidation.TestHelper;
using NSubstitute;

namespace Application.UnitTests.Features.AuditLogs;

public class AuditLogTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheWriter_StoresEveryFieldOfTheEntry()
    {
        var logs = Substitute.For<IAuditLogRepository>();
        Guid actor = Guid.NewGuid(), target = Guid.NewGuid();

        await new AuditWriter(logs).WriteAsync(new AuditEntry
        {
            ActorUserId = actor, ActorEmail = "a@b.com", ActorRole = "BankStaff", Action = "User.Ban", TargetType = "User",
            TargetId = target, Detail = "fraud", Succeeded = true, IpAddress = "203.0.113.1", TraceId = "trace"
        }, Ct);

        await logs.Received(1).AddAsync(
            Arg.Is<AuditLog>(l => l.ActorUserId == actor && l.ActorEmail == "a@b.com" && l.ActorRole == "BankStaff"
                                  && l.Action == "User.Ban" && l.TargetType == "User" && l.TargetId == target && l.Detail == "fraud"
                                  && l.Succeeded && l.IpAddress == "203.0.113.1" && l.TraceId == "trace"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheList_IsNewestFirst_AndMapsEveryField()
    {
        var logs = Substitute.For<IAuditLogRepository>();
        var entity = new AuditLog { Id = Guid.NewGuid(), Action = "User.Ban", ActorEmail = "a@b.com", Detail = "x", Succeeded = false, CreateDate = new DateTime(2026, 1, 1) };
        logs.GetListAsync(default, default, default, default, default, default, default, Ct).ReturnsForAnyArgs(
            new Paginate<AuditLog> { Index = 0, Size = 10, Count = 1, Pages = 1, Items = [entity] });

        var page = await new GetListAuditLogQueryHandler(logs).Handle(new GetListAuditLogQuery(), Ct);

        var item = page.Items.ShouldHaveSingleItem();
        (item.Id, item.Action, item.ActorEmail, item.Detail, item.Succeeded, item.CreateDate)
            .ShouldBe((entity.Id, "User.Ban", "a@b.com", "x", false, entity.CreateDate));
        await logs.Received(1).GetListAsync(
            Arg.Any<Expression<Func<AuditLog, bool>>?>(), Arg.Is<Func<IQueryable<AuditLog>, IOrderedQueryable<AuditLog>>?>(o => o != null),
            Arg.Any<Func<IQueryable<AuditLog>, Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<AuditLog, object?>>?>(),
            0, 10, false, false, Ct);
    }

    [Fact]
    public void TheValidator_ChecksPagingActionAndTheRangeOfDates()
    {
        var validator = new GetListAuditLogQueryValidator();

        validator.TestValidate(new GetListAuditLogQuery()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetListAuditLogQuery { Index = -1 }).ShouldHaveValidationErrorFor(q => q.Index);
        validator.TestValidate(new GetListAuditLogQuery { Size = 0 }).ShouldHaveValidationErrorFor(q => q.Size);
        validator.TestValidate(new GetListAuditLogQuery { Size = 101 }).ShouldHaveValidationErrorFor(q => q.Size);
        validator.TestValidate(new GetListAuditLogQuery { Action = new string('a', 101) }).ShouldHaveValidationErrorFor(q => q.Action);
        validator.TestValidate(new GetListAuditLogQuery { From = new DateTime(2026, 2, 1), To = new DateTime(2026, 1, 1) })
            .ShouldHaveValidationErrorFor(q => q.To);
    }
}
