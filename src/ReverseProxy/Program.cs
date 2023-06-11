using Observability;

var builder = WebApplication.CreateBuilder(args);

var observabilityOptions = new ObservabilityOptions();
builder.Configuration.Bind("Observability", observabilityOptions);
builder.Services.AddObservability("Rigo.ReverseProxy", typeof(Program), observabilityOptions);


builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.UseHttpsRedirection();

app.Run();
