using System.Net;
using System.Net.Http.Json;
using WebApi.IntegrationTests.Infrastructure;

namespace WebApi.IntegrationTests.Controllers;

/// <summary>The current API is version 0, and the version is part of the URL.</summary>
[Collection(DatabaseCollection.Name)]
public class ApiVersioningTests(SqlServerFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task TheApi_IsVersion0_AndTheVersionIsPartOfTheUrl()
    {
        var versioned = await Client.GetAsync("/api/v0/auth/me", Ct);
        var unversioned = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new { email = StaffEmail, password = StaffPassword }, Json, Ct);

        versioned.StatusCode.ShouldBe(HttpStatusCode.OK);
        versioned.Headers.GetValues("api-supported-versions").ShouldBe(["0"]);
        unversioned.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TheOpenApiDocument_ListsOnlyVersionedPaths_OfVersion0()
    {
        var doc = await ReadJsonAsync(await AnonymousClient.GetAsync("/openapi/v0.json", Ct));

        doc.GetProperty("info").GetProperty("version").GetString().ShouldBe("v0");
        var paths = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.ShouldNotBeEmpty();
        paths.ShouldAllBe(path => path.StartsWith("/api/v0/"));
    }
}
