using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MoodTrackerProject.Auth;
using MoodTrackerProject.Data;
using MoodTrackerProject.Identity;
using MoodTrackerProject.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration that must be valid before the app is allowed to start ----------------------

builder.Services.AddOptions<TeamClockOptions>()
    .BindConfiguration(TeamClockOptions.SectionName)
    .ValidateDataAnnotations()
    .Validate(o => TeamClockOptions.CanResolve(o.TimeZone),
        "TeamClock:TimeZone must be a time-zone id this machine can resolve (e.g. Australia/Melbourne). " +
        "A mood tracker that silently falls back to UTC is worse than one that refuses to start.")
    .ValidateOnStart();

// ---- services ---------------------------------------------------------------------------------

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITeamClock, TeamClock>();
builder.Services.AddSingleton<IDuplicateKeyDetector, MySqlDuplicateKeyDetector>();
builder.Services.AddScoped<ParticipantContext>();
builder.Services.AddScoped<IMoodService, MoodService>();

var connectionString = builder.Configuration.GetConnectionString("MySQLConnectionString")
    ?? throw new InvalidOperationException("ConnectionStrings:MySQLConnectionString is not configured.");

builder.Services.AddDbContext<MoodDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 4, 3)),
        mysql => mysql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// Admin ticket cookies are protected with Data Protection. In the container the keys persist on a
// named volume so a restart doesn't sign every admin out; locally the default user-profile store is used.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("MoodTrackerProject");
if (!string.IsNullOrWhiteSpace(keysPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services.AddAdminAuth();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Development only: lets `ng serve` reach the API directly if someone bypasses proxy.conf.json.
// In the container, nginx makes everything same-origin and this policy is never registered.
const string DevCors = "DevClient";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));
}

var app = builder.Build();

// ---- schema -----------------------------------------------------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoodDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DatabaseInitializer.MigrateWithRetryAsync(db, logger, cancellationToken: app.Lifetime.ApplicationStopping);

    var clock = scope.ServiceProvider.GetRequiredService<ITeamClock>();
    logger.LogInformation("Team clock: {Zone}; today is {Today}.", clock.Zone.Id, clock.Today);
}

// ---- pipeline ---------------------------------------------------------------------------------

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
    app.UseCors(DevCors);
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<ParticipantCookieMiddleware>();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health");

app.MapControllers();

await app.RunAsync();

/// <summary>Exposed so WebApplicationFactory-based integration tests can host the app.</summary>
public partial class Program
{
}
