using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SDPP.BuildingBlocks.Domain;
using SDPP.BuildingBlocks.Infrastructure.Outbox;
using SDPP.BuildingBlocks.Infrastructure.Security;
using SDPP.Classification.Api.Endpoints;
using SDPP.Classification.Application;
using SDPP.Classification.Infrastructure;
using SDPP.Classification.Infrastructure.Persistence;
using SDPP.Classification.Infrastructure.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Classification.Api"));

// See SDPP.Documents.Api/Program.cs — same shared cookie-JWT auth every service uses now.
builder.Services.AddSdppCookieJwtBearer(builder.Configuration);
builder.Services.AddSdppTokenRevocation(builder.Configuration);
builder.Services.AddSdppCurrentActor();

builder.Services.AddClassificationApplication();
builder.Services.AddClassificationInfrastructure(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Forces every DateTime/DateTime? in every JSON response to serialize with a "Z" (UTC) suffix —
    // see UtcDateTimeJsonConverter's doc comment for the exact bug this fixes.
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcDateTimeJsonConverter());
    options.SerializerOptions.Converters.Add(new SDPP.BuildingBlocks.Infrastructure.Serialization.UtcNullableDateTimeJsonConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("ClassificationDb"), new SqlServerStorageOptions
    {
        SchemaName = "classification_hangfire",
    }));
builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.UseSdppCsrfProtection();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var (status, title) = feature?.Error switch
    {
        DomainException domainEx => (StatusCodes.Status422UnprocessableEntity, domainEx.Message),
        FluentValidation.ValidationException validationEx =>
            (StatusCodes.Status422UnprocessableEntity, string.Join("; ", validationEx.Errors.Select(e => e.ErrorMessage))),
        _ => (StatusCodes.Status500InternalServerError, "Ha ocurrido un error interno."),
    };

    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { title, traceId = context.TraceIdentifier });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapMetrics();
app.MapClassificationEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ClassificationDbContext>();
    await dbContext.Database.MigrateAsync();
    await DefaultDataSeeder.SeedAsync(dbContext);
}

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxProcessor<ClassificationDbContext>>(
    "classification-outbox-publisher",
    processor => processor.ProcessPendingMessagesAsync(CancellationToken.None),
    Cron.Minutely);

app.Run();
