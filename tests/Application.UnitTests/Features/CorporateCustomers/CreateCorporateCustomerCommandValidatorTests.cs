using Application.Features.CorporateCustomers.Commands.Create;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.CorporateCustomers;

public class CreateCorporateCustomerCommandValidatorTests
{
    private readonly CreateCorporateCustomerCommandValidator _validator = new();

    private static CreateCorporateCustomerCommand ValidCommand() => new()
    {
        Password = "Correct-Horse-9",
        Email = "info@acme.com",
        PhoneNumber = "2121112233",
        Address = "Plaza 1",
        City = "Istanbul",
        Country = "TR",
        CompanyName = "Acme",
        TaxNumber = "1234567890",
        TaxOffice = "Kadikoy",
        FoundationDate = new DateOnly(2010, 5, 1),
        EmployeeCount = 50,
        AnnualRevenue = 1_000_000m,
        AuthorizedPersonName = "John Doe"
    };

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        _validator.TestValidate(ValidCommand()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1A")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoDigitsAtAllHere")]
    public void AWeakPassword_HasAnError(string password)
    {
        var command = ValidCommand();
        command.Password = password;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Password);
    }

    [Fact]
    public void EmptyCommand_HasErrorsOnRequiredFields()
    {
        var result = _validator.TestValidate(new CreateCorporateCustomerCommand());

        result.ShouldHaveValidationErrorFor(c => c.CompanyName);
        result.ShouldHaveValidationErrorFor(c => c.TaxNumber);
        result.ShouldHaveValidationErrorFor(c => c.TaxOffice);
        result.ShouldHaveValidationErrorFor(c => c.AuthorizedPersonName);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345678901")]
    public void TaxNumberWithWrongLength_HasError(string taxNumber)
    {
        var command = ValidCommand();
        command.TaxNumber = taxNumber;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.TaxNumber);
    }

    [Fact]
    public void NegativeRevenueOrEmployeeCount_HasError()
    {
        var command = ValidCommand();
        command.AnnualRevenue = -1;
        command.EmployeeCount = -1;

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(c => c.AnnualRevenue);
        result.ShouldHaveValidationErrorFor(c => c.EmployeeCount);
    }

    [Fact]
    public void AuthorizedPersonNationalId_IsOptional_ButMustBeElevenCharsWhenGiven()
    {
        var command = ValidCommand();
        command.AuthorizedPersonNationalId = null;
        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(c => c.AuthorizedPersonNationalId);

        command.AuthorizedPersonNationalId = "123";
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.AuthorizedPersonNationalId);
    }
}
