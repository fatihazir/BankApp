using System.Text.Json.Nodes;
using Application.Features.Auth.Commands.Refresh;
using Application.Features.Auth.Commands.VerifyEmail;
using Application.Features.CorporateCustomers.Commands.Create;
using Application.Features.CorporateCustomers.Commands.Update;
using Application.Features.IndividualCustomers.Commands.Create;
using Application.Features.IndividualCustomers.Commands.Update;
using Application.Features.LoanApplications.Commands.Approve;
using Application.Features.LoanApplications.Commands.Create;
using Application.Features.LoanApplications.Commands.Reject;
using Application.Features.LoanApplications.Commands.Cancel;
using Application.Features.LoanSubTypes.Commands.Create;
using Application.Features.LoanTypes.Commands.Create;
using Application.Features.Users.Commands.Ban;
using Application.Features.Users.Commands.CreateStaff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WebApi.Infrastructure;

/// <summary>
/// Pre-fills request bodies in Swagger UI so "Try it out" works without typing anything.
/// </summary>
public static class OpenApiExamples
{
    public static OpenApiOptions AddRequestExamples(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "BankApp Loan API";
            document.Info.Version = "v1";

            // Adds the "Authorize" button to Swagger UI: paste the accessToken returned by POST /api/auth/login.
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the accessToken from POST /api/auth/login."
            };
            return Task.CompletedTask;
        });

        // Every endpoint that needs a login shows the lock icon in Swagger UI.
        options.AddOperationTransformer((operation, context, _) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            bool needsLogin = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();

            if (needsLogin && context.Document is { } document)
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            }

            return Task.CompletedTask;
        });

        // The example is attached to the request body itself, which Swagger UI uses to pre-fill "Try it out".
        options.AddOperationTransformer((operation, context, _) =>
        {
            Type? bodyType = context.Description.ParameterDescriptions
                .FirstOrDefault(p => p.Source == BindingSource.Body)?.Type;

            JsonNode? example = bodyType switch
            {
                _ when bodyType == typeof(CreateIndividualCustomerCommand) => IndividualCustomerExample(update: false),
                _ when bodyType == typeof(UpdateIndividualCustomerCommand) => IndividualCustomerExample(update: true),
                _ when bodyType == typeof(CreateCorporateCustomerCommand) => CorporateCustomerExample(update: false),
                _ when bodyType == typeof(UpdateCorporateCustomerCommand) => CorporateCustomerExample(update: true),
                _ when bodyType == typeof(CreateLoanApplicationCommand) => LoanApplicationExample(),
                _ when bodyType == typeof(RefreshTokenCommand) => new JsonObject { ["refreshToken"] = "paste the refreshToken returned by login or by the last refresh" },
                _ when bodyType == typeof(VerifyEmailCommand) => new JsonObject { ["email"] = "ali.veli@example.com", ["token"] = "paste the token from the verification e-mail" },
                _ when bodyType == typeof(BanUserCommand) => new JsonObject { ["reason"] = "Suspicious activity on the account." },
                _ when bodyType == typeof(CreateStaffUserCommand) => new JsonObject { ["email"] = "new.staff@bank.example.com", ["password"] = "Staff-Passw0rd-1" },
                _ when bodyType == typeof(CreateLoanTypeCommand) => new JsonObject
                {
                    ["name"] = "Individual Special Loan", ["description"] = "Loans for special campaigns.", ["segment"] = "Individual"
                },
                _ when bodyType == typeof(CreateLoanSubTypeCommand) => new JsonObject
                {
                    ["name"] = "Education Loan", ["description"] = "Loan for education expenses.",
                    ["minAmount"] = 10000.00, ["maxAmount"] = 500000.00, ["minTermInMonths"] = 6, ["maxTermInMonths"] = 48,
                    ["annualInterestRate"] = 39.50
                },
                _ when bodyType == typeof(ApproveLoanApplicationCommand) => new JsonObject { ["approvedAmount"] = 250000.00, ["reason"] = "Income and credit history are sufficient." },
                _ when bodyType == typeof(RejectLoanApplicationCommand) => new JsonObject { ["reason"] = "Debt to income ratio is too high." },
                _ when bodyType == typeof(CancelLoanApplicationCommand) => new JsonObject { ["reason"] = "Customer changed their mind." },
                _ => null
            };

            if (example is not null && operation.RequestBody?.Content is { } content)
            {
                foreach (var media in content.Values)
                    media.Example = example.DeepClone();
            }

            return Task.CompletedTask;
        });

        return options;
    }

    private static JsonObject IndividualCustomerExample(bool update)
    {
        var example = new JsonObject
        {
            ["phoneNumber"] = "5551112233",
            ["address"] = "Atatürk Cad. No:1 Kadıköy",
            ["city"] = "Istanbul",
            ["country"] = "Turkey",
            ["postalCode"] = "34710",
            ["firstName"] = "Ali",
            ["lastName"] = "Veli",
            // Checksum-valid sample national ID.
            ["nationalId"] = "10000000146",
            ["dateOfBirth"] = "1990-01-15",
            ["motherName"] = "Ayşe",
            ["employmentStatus"] = "Employed",
            ["occupation"] = "Software Engineer",
            ["employerName"] = "Acme Technology",
            ["monthlyIncome"] = 75000.00
        };

        if (update)
        {
            // The e-mail (login name) is not changed here.
            example["isActive"] = true;
        }
        else
        {
            example["email"] = "ali.veli@example.com";
            example["password"] = "Customer-Passw0rd1";
        }

        return example;
    }

    private static JsonObject CorporateCustomerExample(bool update)
    {
        var example = new JsonObject
        {
            ["phoneNumber"] = "2125551234",
            ["address"] = "Büyükdere Cad. No:100 Şişli",
            ["city"] = "Istanbul",
            ["country"] = "Turkey",
            ["postalCode"] = "34394",
            ["companyName"] = "Acme Teknoloji A.Ş.",
            ["taxNumber"] = "1234567890",
            ["taxOffice"] = "Mecidiyeköy",
            ["tradeRegistryNumber"] = "123456",
            ["foundationDate"] = "2010-05-01",
            ["sector"] = "Software",
            ["employeeCount"] = 50,
            ["annualRevenue"] = 12500000.75,
            ["authorizedPersonName"] = "Fatih Veli",
            ["authorizedPersonTitle"] = "General Manager",
            ["authorizedPersonNationalId"] = "12345678950"
        };

        if (update)
        {
            // The e-mail (login name) is not changed here.
            example["isActive"] = true;
        }
        else
        {
            example["email"] = "info@acme-teknoloji.example.com";
            example["password"] = "Company-Passw0rd1";
        }

        return example;
    }

    /// <summary>
    /// The sub type id is a seeded one (Personal Loan). The customer id is a placeholder: replace it with the id
    /// returned when a customer was created. Individual customers can use Personal, Mortgage and Auto loans,
    /// corporate customers Working Capital, Investment and Commercial loans (see GET /api/loan-types).
    /// </summary>
    private static JsonObject LoanApplicationExample() => new()
    {
        ["customerId"] = "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ["loanSubTypeId"] = "b0000000-0000-0000-0000-000000000001",
        ["requestedAmount"] = 250000.00,
        ["termInMonths"] = 36,
        ["purpose"] = "Home renovation"
    };
}
