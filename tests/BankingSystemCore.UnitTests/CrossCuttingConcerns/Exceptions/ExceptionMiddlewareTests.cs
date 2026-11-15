using System.Text.Json;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Middlewares;
using BankingSystemCore.CrossCuttingConcerns.Exceptions.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace BankingSystemCore.UnitTests.CrossCuttingConcerns.Exceptions;

public class ExceptionMiddlewareTests
{
    private sealed record Result(int Status, string? ContentType, JsonElement Body);

    private static async Task<Result> Run(RequestDelegate next, string path = "/api/customers")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await new ExceptionMiddleware(next, NullLogger<ExceptionMiddleware>.Instance).InvokeAsync(context);

        context.Response.Body.Position = 0;
        string text = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        JsonElement body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement;

        return new Result(context.Response.StatusCode, context.Response.ContentType, body);
    }

    [Fact]
    public async Task NoException_PassesThrough_AndWritesNothing()
    {
        var result = await Run(_ => Task.CompletedTask);

        result.Status.ShouldBe(StatusCodes.Status200OK);
        result.Body.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task BusinessException_Returns400_WithMessageAsDetail()
    {
        var result = await Run(_ => throw new BusinessException("Rule broken."));

        result.Status.ShouldBe(400);
        result.ContentType.ShouldBe("application/problem+json");
        result.Body.GetProperty("title").GetString().ShouldBe("Business rule violation");
        result.Body.GetProperty("detail").GetString().ShouldBe("Rule broken.");
        result.Body.GetProperty("status").GetInt32().ShouldBe(400);
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:business");
        result.Body.GetProperty("instance").GetString().ShouldBe("/api/customers");
        result.Body.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ValidationException_Returns400_WithErrorsGroupedByProperty()
    {
        var errors = new[]
        {
            new ValidationExceptionModel { Property = "Email", Errors = ["Invalid e-mail.", "Too long."] },
            new ValidationExceptionModel { Property = "FirstName", Errors = ["Required."] }
        };

        var result = await Run(_ => throw new ValidationException(errors));

        result.Status.ShouldBe(400);
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:validation");
        JsonElement items = result.Body.GetProperty("errors");
        items.GetArrayLength().ShouldBe(2);
        items[0].GetProperty("property").GetString().ShouldBe("Email");
        items[0].GetProperty("errors").GetArrayLength().ShouldBe(2);
        items[1].GetProperty("property").GetString().ShouldBe("FirstName");
    }

    [Fact]
    public async Task NotFoundException_Returns404()
    {
        var result = await Run(_ => throw new NotFoundException("Missing."));

        result.Status.ShouldBe(404);
        result.Body.GetProperty("detail").GetString().ShouldBe("Missing.");
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:not-found");
    }

    [Fact]
    public async Task AuthenticationException_Returns401()
    {
        var result = await Run(_ => throw new AuthenticationException("Not logged in."));

        result.Status.ShouldBe(401);
        result.ContentType.ShouldBe("application/problem+json");
        result.Body.GetProperty("detail").GetString().ShouldBe("Not logged in.");
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:authentication");
    }

    [Fact]
    public async Task ConflictException_Returns409()
    {
        var result = await Run(_ => throw new ConflictException("Changed meanwhile."));

        result.Status.ShouldBe(409);
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:conflict");
        result.Body.GetProperty("detail").GetString().ShouldBe("Changed meanwhile.");
    }

    [Fact]
    public async Task ADatabaseConcurrencyConflict_Returns409_WithoutLeakingDatabaseDetails()
    {
        var result = await Run(_ => throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException(
            "The database operation was expected to affect 1 row(s), but actually affected 0 row(s) in table [Users]."));

        result.Status.ShouldBe(409);
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:conflict");
        result.Body.GetRawText().ShouldNotContain("Users");
        result.Body.GetRawText().ShouldNotContain("row(s)");
    }

    [Fact]
    public async Task AuthorizationException_Returns403()
    {
        var result = await Run(_ => throw new AuthorizationException("Not allowed."));

        result.Status.ShouldBe(403);
        result.Body.GetProperty("detail").GetString().ShouldBe("Not allowed.");
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:authorization");
    }

    [Fact]
    public async Task UnexpectedException_Returns500_WithoutLeakingTheRealMessage()
    {
        var result = await Run(_ => throw new InvalidOperationException("secret connection string"));

        result.Status.ShouldBe(500);
        result.Body.GetProperty("type").GetString().ShouldBe("urn:problem:internal-server-error");
        result.Body.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        result.Body.GetRawText().ShouldNotContain("secret");
    }

    [Fact]
    public async Task WhenResponseAlreadyStarted_ExceptionIsRethrown()
    {
        var context = new DefaultHttpContext();
        var feature = new StartedResponseFeature();
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(feature);
        var middleware = new ExceptionMiddleware(_ => throw new BusinessException("late"), NullLogger<ExceptionMiddleware>.Instance);

        await Should.ThrowAsync<BusinessException>(() => middleware.InvokeAsync(context));
    }

    private sealed class StartedResponseFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
