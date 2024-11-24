using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Observability
{
    public static class OpenTelemetryExtensions
    {
        public static void AddObservability(this IServiceCollection services, string name, Type? programType, ObservabilityOptions? options = null)
        {
            // Register the ActivitySource to the DI as Singleton
            var activitySource = new ActivitySource(name);
            services.AddSingleton(activitySource);

            var assemblyVersion = programType?.Assembly.GetName().Version?.ToString() ?? "0.0.0";

            // Register OpenTelemetry
            services.AddOpenTelemetry()
                // Add Tracing
                .WithTracing(builder =>
                {
                    builder
                        .AddSource(activitySource.Name).ConfigureResource(resource =>
                            resource.AddService(name, serviceVersion: assemblyVersion, serviceInstanceId: Environment.MachineName))
                        .AddAspNetCoreInstrumentation();
                    builder.AddConsoleExporter();
                    if (!string.IsNullOrEmpty(options?.JaegerUrl))
                        builder.AddJaegerExporter(config => config.Endpoint = new Uri(options.JaegerUrl));
                    if (options?.HttpClient == true)
                        builder.AddHttpClientInstrumentation();
                    if (options?.Postgres == true)
                        builder.AddNpgsql();
                })
                .WithMetrics(builder =>
                {
                    builder
                        .ConfigureResource(resource => resource.AddService(name))
                        .AddAspNetCoreInstrumentation()
                        .AddConsoleExporter();
                })
                ;
        }

    }
}
