using System.Text.Json.Serialization;
using ClaimsModule.Api.Infrastructure;
using ClaimsModule.Application;
using ClaimsModule.Application.Claims.Jobs;
using ClaimsModule.Infrastructure;
using ClaimsModule.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddControllers()
    // Ticket 04: CreateClaimCommand takes enum-valued fields (party role/type, risk-object
    // asset type) straight in the JSON body — string values ("Claimant", "Vehicle", ...) match
    // every other enum-shaped field this API already returns (DTOs call .ToString() on them),
    // instead of forcing clients to know C#'s underlying ordinal values.
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

const string AngularDevClientPolicy = "AngularDevClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevClientPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Claims Module API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by POST /api/auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            []
        }
    });
});

var hangfireConnectionString = builder.Configuration.GetConnectionString(ClaimsModule.Persistence.DependencyInjection.ConnectionStringName)
    ?? throw new InvalidOperationException($"Missing required connection string \"{ClaimsModule.Persistence.DependencyInjection.ConnectionStringName}\".");

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));
builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Claims Module API v1"));
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(AngularDevClientPolicy);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHangfireDashboard("/hangfire");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    await ApplicationDbContextSeeder.SeedAsync(context);
    await ReferenceDataSeeder.SeedAsync(context);
}

// Ticket 09: the first real Hangfire job (Ticket 01 wired Hangfire with none defined yet).
// SlaBreachScanJob itself has no Hangfire dependency — only this registration does — so it
// stays directly callable/testable independent of Hangfire's own scheduler. Registered after
// the migration block above (not before) so it's the second thing to touch SQL Server, not the
// first — Hangfire's own AcquireDistributedLock needs a connection, and issuing it before the
// app's own first proven-good connection made this call the flakiest thing at startup under the
// test suite's parallel Testcontainers instances.
RecurringJob.AddOrUpdate<SlaBreachScanJob>(
    "sla-breach-scan",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/15 * * * *"); // every 15 minutes (Cron.MinuteInterval is obsolete in this Hangfire version)

app.Run();

public partial class Program;
