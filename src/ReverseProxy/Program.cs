using Microsoft.AspNetCore.Builder;
using Observability;
using ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

var observabilityOptions = new ObservabilityOptions();
builder.Configuration.Bind("Observability", observabilityOptions);
builder.Services.AddObservability("Rigo.ReverseProxy", typeof(Program), observabilityOptions);

var reverseProxyOptions = new ReverseProxyOptions();
builder.Configuration.Bind("ReverseProxy", reverseProxyOptions);


// builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddReverseProxy().LoadFromMemory(reverseProxyOptions.GetRoutes(), reverseProxyOptions.GetClusters());

var app = builder.Build();

app.MapReverseProxy();

app.UseHttpsRedirection();

app.Run();
