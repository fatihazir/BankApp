using BankingSystemCore.Application.Pipelines.Auditing;
using BankingSystemCore.Security.CurrentUser;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankingSystemCore.UnitTests.Auditing;

public class AuditBehaviorTests
{
    private sealed record Result(Guid Id);

    [Audit("Thing.Change", "Thing")]
    private sealed class ChangeThing : IRequest<Result>, IAuditDetail
    {
        public Guid Id { get; set; }
        public string? Note { get; set; }
        string? IAuditDetail.AuditDetail => Note;
    }

    [Audit("Thing.Create", "Thing")]
    private sealed class CreateThing : IRequest<Result>
    {
        public string Name { get; set; } = "x";
    }

    [Audit("Thing.Make", "Thing")]
    private sealed class MakeThing : IRequest<Made>;

    private sealed class Made : IAuditTarget
    {
        public Guid ThingId { get; init; }
        Guid? IAuditTarget.AuditTargetId => ThingId;
    }

    private sealed class NotAudited : IRequest<Result>;

    private sealed class RecordingWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];
        public bool Fails { get; set; }

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            if (Fails) throw new InvalidOperationException("the audit store is down");
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Person : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? Id { get; } = Guid.NewGuid();
        public string? Email => "staff@bank.test";
        public Guid? SessionId => null;
        public IReadOnlyCollection<string> Roles => ["BankStaff"];
        public string? IpAddress => "203.0.113.9";
        public bool IsInRole(string role) => Roles.Contains(role);
        public string? FindClaim(string claimType) => null;
    }

    private readonly RecordingWriter _writer = new();
    private readonly Person _person = new();

    private AuditBehavior<TRequest, Result> Behavior<TRequest>() where TRequest : notnull =>
        new(_writer, _person, NullLogger<AuditBehavior<TRequest, Result>>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ASuccessfulAuditedRequest_WritesWhoDidWhatToWhat()
    {
        Guid id = Guid.NewGuid();
        var request = new ChangeThing { Id = id, Note = "because" };

        await Behavior<ChangeThing>().Handle(request, _ => Task.FromResult(new Result(id)), Ct);

        var entry = _writer.Entries.ShouldHaveSingleItem();
        (entry.Action, entry.TargetType, entry.TargetId, entry.Detail, entry.Succeeded).ShouldBe(("Thing.Change", "Thing", id, "because", true));
        (entry.ActorUserId, entry.ActorEmail, entry.ActorRole, entry.IpAddress).ShouldBe((_person.Id, "staff@bank.test", "BankStaff", "203.0.113.9"));
    }

    [Fact]
    public async Task ACreateHasNoIdYet_SoTheIdOfTheResponseIsTheTarget()
    {
        Guid created = Guid.NewGuid();

        await Behavior<CreateThing>().Handle(new CreateThing(), _ => Task.FromResult(new Result(created)), Ct);

        _writer.Entries.ShouldHaveSingleItem().TargetId.ShouldBe(created);
    }

    [Fact]
    public async Task AResponseThatNamesItsTarget_IsAsGoodAsAnId()
    {
        Guid thing = Guid.NewGuid();
        var behavior = new AuditBehavior<MakeThing, Made>(_writer, _person, NullLogger<AuditBehavior<MakeThing, Made>>.Instance);

        await behavior.Handle(new MakeThing(), _ => Task.FromResult(new Made { ThingId = thing }), Ct);

        _writer.Entries.ShouldHaveSingleItem().TargetId.ShouldBe(thing);
    }

    [Fact]
    public async Task AnEmptyIdOfTheRequest_FallsBackToTheResponse()
    {
        Guid fromResponse = Guid.NewGuid();

        await Behavior<ChangeThing>().Handle(new ChangeThing { Id = Guid.Empty }, _ => Task.FromResult(new Result(fromResponse)), Ct);

        _writer.Entries.ShouldHaveSingleItem().TargetId.ShouldBe(fromResponse);
    }

    [Fact]
    public async Task AFailedRequest_WritesNothing_AndTheErrorGoesOn()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Behavior<ChangeThing>().Handle(new ChangeThing { Id = Guid.NewGuid() }, _ => throw new InvalidOperationException("rule broken"), Ct));

        _writer.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestWithoutTheAttribute_IsNotAudited()
    {
        await Behavior<NotAudited>().Handle(new NotAudited(), _ => Task.FromResult(new Result(Guid.NewGuid())), Ct);

        _writer.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task AFailingAuditStore_DoesNotTurnADoneActionIntoAnError()
    {
        _writer.Fails = true;
        var response = new Result(Guid.NewGuid());

        var result = await Behavior<ChangeThing>().Handle(new ChangeThing { Id = response.Id }, _ => Task.FromResult(response), Ct);

        result.ShouldBe(response);
    }

    [Fact]
    public async Task ALongNote_IsCutAt500Characters()
    {
        var request = new ChangeThing { Id = Guid.NewGuid(), Note = new string('n', 2_000) };

        await Behavior<ChangeThing>().Handle(request, _ => Task.FromResult(new Result(request.Id)), Ct);

        _writer.Entries.Single().Detail!.Length.ShouldBe(500);
    }
}
