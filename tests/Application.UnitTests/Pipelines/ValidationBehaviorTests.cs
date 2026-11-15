using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Queries.GetById;
using Application.Pipelines.Validation;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;

namespace Application.UnitTests.Pipelines;

public class ValidationBehaviorTests
{
    private static ValidationBehavior<GetByIdIndividualCustomerQuery, string> CreateBehavior() =>
        new([new GetByIdIndividualCustomerQueryValidator()]);

    [Fact]
    public async Task Handle_Throws_AndSkipsHandler_WhenRequestIsInvalid()
    {
        bool nextCalled = false;

        var exception = await Should.ThrowAsync<ValidationException>(() => CreateBehavior().Handle(
            new GetByIdIndividualCustomerQuery { Id = Guid.Empty },
            _ => { nextCalled = true; return Task.FromResult("ok"); },
            TestContext.Current.CancellationToken));

        nextCalled.ShouldBeFalse();
        exception.Errors.ShouldContain(e => e.Property == nameof(GetByIdIndividualCustomerQuery.Id));
    }

    [Fact]
    public async Task Handle_GroupsMessagesByProperty()
    {
        var behavior = new ValidationBehavior<CreateIndividualCustomerCommand, string>([new CreateIndividualCustomerCommandValidator()]);

        var exception = await Should.ThrowAsync<ValidationException>(() => behavior.Handle(
            new CreateIndividualCustomerCommand { Email = "x" }, _ => Task.FromResult("ok"), TestContext.Current.CancellationToken));

        // One entry per property, with every message for that property.
        exception.Errors.Select(e => e.Property).ShouldBeUnique();
        exception.Errors.Single(e => e.Property == nameof(CreateIndividualCustomerCommand.Email)).Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Handle_CallsNext_WhenRequestIsValid()
    {
        string result = await CreateBehavior().Handle(
            new GetByIdIndividualCustomerQuery { Id = Guid.NewGuid() },
            _ => Task.FromResult("ok"),
            TestContext.Current.CancellationToken);

        result.ShouldBe("ok");
    }

    [Fact]
    public async Task Handle_CallsNext_WhenNoValidatorsAreRegistered()
    {
        var behavior = new ValidationBehavior<GetByIdIndividualCustomerQuery, string>([]);

        string result = await behavior.Handle(
            new GetByIdIndividualCustomerQuery(), _ => Task.FromResult("ok"), TestContext.Current.CancellationToken);

        result.ShouldBe("ok");
    }
}
