using System.Reflection;
using Application.Features.Auth.Rules;
using Application.Features.CorporateCustomers.Rules;
using Application.Features.IndividualCustomers.Rules;
using Application.Features.LoanApplications.Rules;
using Application.Features.LoanSubTypes.Rules;
using Application.Features.LoanTypes.Rules;
using Application.Pipelines.Validation;
using Application.Services.Auditing;
using Application.Services.Security;
using BankingSystemCore.Application.Pipelines.Auditing;
using BankingSystemCore.Application.Pipelines.Authorization;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // Validators are stateless; scoped keeps them consistent with the handlers that use them.
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        services.AddMediatR(configuration =>
        {
            configuration.Lifetime = ServiceLifetime.Scoped;
            configuration.RegisterServicesFromAssembly(assembly);
            // Order matters: who you are is checked before anything else, and before the request is validated.
            configuration.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            // Last: it records what the handler did, so it only sees requests that were allowed and valid.
            configuration.AddOpenBehavior(typeof(AuditBehavior<,>));
        });

        // Business rules depend on scoped repositories, so they must be scoped as well.
        services.AddScoped<IndividualCustomerBusinessRules>();
        services.AddScoped<CorporateCustomerBusinessRules>();
        services.AddScoped<LoanApplicationBusinessRules>();
        services.AddScoped<LoanTypeBusinessRules>();
        services.AddScoped<AuthBusinessRules>();
        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<UserSessionService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<SecurityAudit>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<UserSessionCleanupService>();

        services.AddOptions<EmailVerificationOptions>().BindConfiguration(EmailVerificationOptions.SectionName);
        services.AddOptions<UserSessionCleanupOptions>().BindConfiguration(UserSessionCleanupOptions.SectionName);
        services.AddScoped<CustomerAccessPolicy>();
        services.AddScoped<LoanSubTypeBusinessRules>();

        return services;
    }
}
