using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthenticationApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AuthenticationApi.Tests;

public class AuthApiTests
{
    [Fact]
    public async Task RegisterLoginProfileRefreshAndLogoutWork()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        factory.CreateDatabase();

        var register = await client.PostAsJsonAsync("/api/Auth/register",
            new { email = "demo@example.com", password = "ExamplePassword12345!" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "demo@example.com", password = "ExamplePassword12345!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginData = await login.Content.ReadFromJsonAsync<TokenReply>();
        Assert.False(string.IsNullOrWhiteSpace(loginData?.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginData?.RefreshToken));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginData!.AccessToken);
        var me = await client.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var rotated = await client.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = loginData.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var newTokens = await rotated.Content.ReadFromJsonAsync<TokenReply>();

        var reuse = await client.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = loginData.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var logout = await client.PostAsJsonAsync("/api/Auth/logout",
            new { refreshToken = newTokens!.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var afterLogout = await client.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = newTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task WeakPasswordAndDuplicateEmailAreRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        factory.CreateDatabase();

        var weak = await client.PostAsJsonAsync("/api/Auth/register",
            new { email = "demo@example.com", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var valid = new { email = "demo@example.com", password = "ExamplePassword12345!" };
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/Auth/register", valid)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/Auth/register", valid)).StatusCode);
    }

    [Fact]
    public async Task ProtectedProfileRequiresJwt()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        factory.CreateDatabase();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/Auth/me")).StatusCode);
    }

    public sealed record TokenReply(string AccessToken, string RefreshToken);

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            connection.Open();

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "very-long-dev-only-key-for-running-tests-123456789",
                    ["Jwt:Issuer"] = "AuthenticationApi",
                    ["Jwt:Audience"] = "AuthenticationApiClient"
                }));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AuthDbContext>>();
                services.RemoveAll<AuthDbContext>();
                services.AddDbContext<AuthDbContext>(options => options.UseSqlite(connection));
            });
        }

        public void CreateDatabase()
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureCreated();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            connection.Dispose();
        }
    }
}
