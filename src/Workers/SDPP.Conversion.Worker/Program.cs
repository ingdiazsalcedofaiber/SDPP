using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using SDPP.Classification.Infrastructure;
using SDPP.Conversion.Worker;
using SDPP.Documents.Infrastructure;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Everything ProtectionEngine needs (which levels get watermark/footer/header/signature, template
// text, watermark styling) is configurable here without a rebuild — reloadOnChange means an
// administrator can edit this file in place and have it picked up on the next conversion.
builder.Configuration.AddJsonFile("protection-policies.json", optional: false, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "SDPP.Conversion.Worker")
    .WriteTo.Console()
    .WriteTo.Seq("http://seq:80")
    .CreateLogger();
builder.Services.AddSerilog();

// Runs inside the sandboxed sdpp-workers namespace with no network egress (see
// docs/07-operations/kubernetes-architecture.md §2 and §4) — it only talks to sdpp-data
// (RabbitMQ, SQL Server, MinIO) plus the local conversion binaries, never the Internet.
builder.Services.AddDocumentsInfrastructure(builder.Configuration, bus =>
{
    bus.AddConsumer<ConversionRequestedConsumer>();
});

// Watermark/protection stack, moved here from Documents.Infrastructure — see the "Clasificación
// de Activos de Información" extraction. Slim registration: no ClassificationDbContext, no extra
// MassTransit bus (AddDocumentsInfrastructure above already owns the one bus this process gets).
builder.Services.AddClassificationProtection(builder.Configuration);

var host = builder.Build();

// Prometheus metrics exposed on a small standalone Kestrel listener — the Worker has no ASP.NET
// pipeline of its own (docs/07-operations/kubernetes-architecture.md §7).
var metricServer = new KestrelMetricServer(port: 9090);
metricServer.Start();

host.Run();
