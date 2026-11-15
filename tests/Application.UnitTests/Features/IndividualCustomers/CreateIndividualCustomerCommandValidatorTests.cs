using Application.Features.IndividualCustomers.Commands.Create;
using Domain.Entities;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Features.IndividualCustomers;

public class CreateIndividualCustomerCommandValidatorTests
{
    private readonly CreateIndividualCustomerCommandValidator _validator = new();

    private static CreateIndividualCustomerCommand ValidCommand() => new()
    {
        Password = "Correct-Horse-9",
        Email = "ali@example.com",
        PhoneNumber = "5551112233",
        Address = "Street 1",
        City = "Istanbul",
        Country = "TR",
        FirstName = "Ali",
        LastName = "Veli",
        NationalId = "10000000146",
        DateOfBirth = new DateOnly(1990, 1, 1),
        EmploymentStatus = EmploymentStatus.Employed,
        MonthlyIncome = 50000m
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
        var result = _validator.TestValidate(new CreateIndividualCustomerCommand());

        result.ShouldHaveValidationErrorFor(c => c.Email);
        result.ShouldHaveValidationErrorFor(c => c.Password);
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
        result.ShouldHaveValidationErrorFor(c => c.LastName);
        result.ShouldHaveValidationErrorFor(c => c.NationalId);
        result.ShouldHaveValidationErrorFor(c => c.DateOfBirth);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@")]
    public void InvalidEmail_HasError(string email)
    {
        var command = ValidCommand();
        command.Email = email;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Email);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    public void NationalIdWithWrongLength_HasError(string nationalId)
    {
        var command = ValidCommand();
        command.NationalId = nationalId;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.NationalId);
    }

    [Fact]
    public void NegativeIncome_HasError()
    {
        var command = ValidCommand();
        command.MonthlyIncome = -1;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.MonthlyIncome);
    }

    [Fact]
    public void UndefinedEmploymentStatus_HasError()
    {
        var command = ValidCommand();
        command.EmploymentStatus = (EmploymentStatus)999;
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.EmploymentStatus);
    }
}
