using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MySql;

namespace MoodTrackerProject.Tests.Integration;

/// <summary>
/// One real MySQL 8.4 in Docker for the whole integration collection, with the application hosted in
/// process by WebApplicationFactory and pointed at it. The real migrations run at startup, the real
/// unique index is in play, and the real MySqlConnector error 1062 is what the race test exercises.
///
/// Requires Docker. Roughly ten seconds to start; every test in the collection shares it.
/// </summary>
public sealed class MySqlApiFixture : IAsyncLifetime
{
    public const string AdminPassword = "integration-test-password";

    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.4.3")
        .WithDatabase("moodtrackerdb")
        .WithUsername("app")
        .WithPassword("password")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialised.");

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MySQLConnectionString"] = _mysql.GetConnectionString(),
                    ["TeamClock:TimeZone"] = "Australia/Melbourne",
                    ["Admin:Password"] = AdminPassword,
                    ["Admin:SessionHours"] = "1",
                    ["DataProtection:KeysPath"] = "",
                });
            });
        });

        // Force the host to build (and migrations to run) now, so a startup failure surfaces here.
        _ = _factory.Server;
    }

    /// <summary>A client with its own cookie jar — i.e. a distinct browser / participant.</summary>
    public HttpClient NewBrowser() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    /// <summary>A client that sends exactly the headers a test gives it and nothing else.</summary>
    public HttpClient RawClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _mysql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class MySqlApiCollection : ICollectionFixture<MySqlApiFixture>
{
    public const string Name = "MySQL API";
}
