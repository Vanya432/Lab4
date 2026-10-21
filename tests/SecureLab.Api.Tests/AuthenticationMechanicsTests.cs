using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task RealCookie_ReachesSessionCheck()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/auth/session-check");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task A1_AnonCreate()
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var request = new
        {
            title = "Anon incident",
            description = "Authorization test",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow
        };

        using var response = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A2_LoginAndGetMe()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password") ?? throw new InvalidOperationException(
            "Set local SeedUsers__Password outside Git.");
        using var client = await factory.LoginAsync("alice", password);
        using var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("alice", document.RootElement.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task A5_OwnerCannotBeCopied()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password") ?? throw new InvalidOperationException(
            "Set local SeedUsers__Password outside Git.");
        using var client = await factory.LoginAsync("bob", password);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var alice = await db.Users.SingleAsync(u => u.UserName == "alice");
        var bob = await db.Users.SingleAsync(u => u.UserName == "bob");
        var title = $"Owner test {Guid.NewGuid()}";
        var request = new
        {
            title,
            description = "Checking ownership",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow,
            ownerUserId = alice.Id
        };

        using var response = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var incident = await db.Incidents
            .AsNoTracking()
            .SingleAsync(i => i.Title == title);

        Assert.Equal(bob.Id, incident.OwnerUserId);
        Assert.NotEqual(alice.Id, incident.OwnerUserId);
    }
}
