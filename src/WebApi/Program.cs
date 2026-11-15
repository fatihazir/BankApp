using System.Text.Json.Serialization;
using Application;
using BankingSystemCore.Security;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Middlewares;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Persistence.Contexts;
using WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Fail at startup (not at the first request) if a service has a wrong lifetime, e.g. a singleton
// depending on a scoped service, or a missing registration.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Layer registrations: each layer registers its own services.
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddJwtAuthentication();

builder.Services
    .AddControllers(options =>
    {
        // FluentValidation is the single source of field rules; do not add implicit [Required] checks.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureCustomApiBehavior();

// The same JSON settings are used by controllers, error responses and the OpenAPI document.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options => options.AddRequestExamples());

var app = builder.Build();

app.UseCustomExceptionMiddleware();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BankApp Loan API v1");
        options.RoutePrefix = "swagger";
    });

    // Local development convenience: open Swagger from the root URL and create/update the database schema.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<BankAppDbContext>().Database.MigrateAsync();
}
else
{
    app.UseHttpsRedirection();
}

// Authentication reads the token, then the MediatR AuthorizationBehavior decides what each request may do.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await StaffBootstrapper.EnsureAsync(app.Services, app.Configuration);

app.Run();

// Makes the entry point visible to WebApplicationFactory in integration tests.
public partial class Program;
