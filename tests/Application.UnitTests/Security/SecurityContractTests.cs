using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Refresh;
using Application.Features.Auth.Commands.VerifyEmail;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Authorization;
using MediatR;

namespace Application.UnitTests.Security;

/// <summary>
/// Guards against the worst security mistake: a new command or query that nobody remembered to protect.
/// Every request must implement <see cref="ISecuredRequest"/> unless it is deliberately open.
/// </summary>
public class SecurityContractTests
{
    /// <summary>Requests that anonymous callers must be able to run. Adding to this list is a security decision.</summary>
    private static readonly Type[] DeliberatelyOpen = [typeof(LoginCommand), typeof(RefreshTokenCommand), typeof(VerifyEmailCommand)];

    // IBaseRequest covers both IRequest<T> (with a result) and IRequest (without a result).
    private static readonly Type[] AllRequests = typeof(LoginCommand).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseRequest).IsAssignableFrom(t))
        .ToArray();

    private static IReadOnlyCollection<string> RolesOf(Type request) =>
        ((ISecuredRequest)Activator.CreateInstance(request)!).Roles;

    [Fact]
    public void ThereAreRequestsToCheck() => AllRequests.Length.ShouldBe(32);

    [Fact]
    public void EveryRequest_IsSecured_UnlessItIsDeliberatelyOpen()
    {
        var unprotected = AllRequests
            .Where(t => !typeof(ISecuredRequest).IsAssignableFrom(t) && !DeliberatelyOpen.Contains(t))
            .Select(t => t.Name)
            .ToList();

        unprotected.ShouldBeEmpty("These requests are open to anonymous callers. Implement ISecuredRequest on them: " + string.Join(", ", unprotected));
    }

    [Fact]
    public void TheOpenRequests_AreReallyNotSecured_SoTheListStaysHonest() =>
        DeliberatelyOpen.ShouldAllBe(t => !typeof(ISecuredRequest).IsAssignableFrom(t));

    [Fact]
    public void EverySecuredRequest_OnlyNamesKnownRoles()
    {
        var known = new[] { BankRoles.Customer, BankRoles.BankStaff };

        foreach (var request in AllRequests.Where(t => typeof(ISecuredRequest).IsAssignableFrom(t)))
            RolesOf(request).ShouldAllBe(role => known.Contains(role), $"{request.Name} names an unknown role.");
    }

    [Fact]
    public void TheRoleProperty_IsNotPartOfTheRequestBody()
    {
        // It is implemented explicitly, so it never shows up in JSON, model binding or Swagger.
        foreach (var request in AllRequests.Where(t => typeof(ISecuredRequest).IsAssignableFrom(t)))
            request.GetProperty("Roles").ShouldBeNull($"{request.Name} exposes Roles publicly.");
    }

    [Theory]
    // Staff only: onboarding, decisions and setup.
    [InlineData("CreateIndividualCustomerCommand", "BankStaff")]
    [InlineData("DeleteIndividualCustomerCommand", "BankStaff")]
    [InlineData("GetListIndividualCustomerQuery", "BankStaff")]
    [InlineData("CreateCorporateCustomerCommand", "BankStaff")]
    [InlineData("DeleteCorporateCustomerCommand", "BankStaff")]
    [InlineData("GetListCorporateCustomerQuery", "BankStaff")]
    [InlineData("StartReviewLoanApplicationCommand", "BankStaff")]
    [InlineData("ApproveLoanApplicationCommand", "BankStaff")]
    [InlineData("RejectLoanApplicationCommand", "BankStaff")]
    [InlineData("CreateLoanTypeCommand", "BankStaff")]
    [InlineData("CreateLoanSubTypeCommand", "BankStaff")]
    [InlineData("CreateStaffUserCommand", "BankStaff")]
    [InlineData("BanUserCommand", "BankStaff")]
    [InlineData("UnbanUserCommand", "BankStaff")]
    [InlineData("GetListUserQuery", "BankStaff")]
    [InlineData("GetListAuditLogQuery", "BankStaff")]
    // Customers and staff: a customer's own data (ownership is checked in the handler).
    [InlineData("GetByIdIndividualCustomerQuery", "BankStaff,Customer")]
    [InlineData("UpdateIndividualCustomerCommand", "BankStaff,Customer")]
    [InlineData("GetByIdCorporateCustomerQuery", "BankStaff,Customer")]
    [InlineData("UpdateCorporateCustomerCommand", "BankStaff,Customer")]
    [InlineData("CreateLoanApplicationCommand", "BankStaff,Customer")]
    [InlineData("GetByIdLoanApplicationQuery", "BankStaff,Customer")]
    [InlineData("GetListLoanApplicationQuery", "BankStaff,Customer")]
    [InlineData("CancelLoanApplicationCommand", "BankStaff,Customer")]
    // Any logged-in user.
    [InlineData("CalculateLoanQuery", "")]
    [InlineData("GetListLoanTypeQuery", "")]
    [InlineData("MeQuery", "")]
    [InlineData("LogoutCommand", "")]
    [InlineData("SendEmailVerificationCommand", "")]
    public void EachRequest_HasTheExpectedRoles(string requestName, string expectedRoles)
    {
        var request = AllRequests.Single(t => t.Name == requestName);
        string[] expected = expectedRoles.Length == 0 ? [] : expectedRoles.Split(',');

        RolesOf(request).OrderBy(r => r).ShouldBe(expected.OrderBy(r => r));
    }

    [Fact]
    public void EveryRequest_IsCoveredByTheRoleTable()
    {
        // When a request is added, this fails until its roles are written down in the test above.
        string[] covered =
        [
            "CreateIndividualCustomerCommand", "DeleteIndividualCustomerCommand", "GetListIndividualCustomerQuery",
            "CreateCorporateCustomerCommand", "DeleteCorporateCustomerCommand", "GetListCorporateCustomerQuery",
            "StartReviewLoanApplicationCommand", "ApproveLoanApplicationCommand", "RejectLoanApplicationCommand",
            "CreateLoanTypeCommand", "CreateLoanSubTypeCommand", "CreateStaffUserCommand", "BanUserCommand", "UnbanUserCommand", "GetListUserQuery", "GetListAuditLogQuery",
            "GetByIdIndividualCustomerQuery", "UpdateIndividualCustomerCommand", "GetByIdCorporateCustomerQuery",
            "UpdateCorporateCustomerCommand", "CreateLoanApplicationCommand", "GetByIdLoanApplicationQuery",
            "GetListLoanApplicationQuery", "CancelLoanApplicationCommand", "CalculateLoanQuery", "GetListLoanTypeQuery", "MeQuery",
            "LogoutCommand", "SendEmailVerificationCommand",
            nameof(LoginCommand), nameof(RefreshTokenCommand), nameof(VerifyEmailCommand)
        ];

        AllRequests.Select(t => t.Name).Except(covered).ShouldBeEmpty();
    }
}
