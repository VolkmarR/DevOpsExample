using MediatR;
using Microsoft.EntityFrameworkCore;
using QuestionsApp.Web.Api.Commands;
using QuestionsApp.Web.Api.Queries;
using QuestionsApp.Web.DB;
using QuestionsApp.Web.Hubs;
using Observability;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// Configuration for Entity Framework
var connectionStringBuilder = new NpgsqlConnectionStringBuilder()
{
    Host = builder.Configuration.GetValue<string>("DB:Host"),
    Port = builder.Configuration.GetValue<int>("DB:Port", 5432),
    Database = builder.Configuration.GetValue<string>("DB:Database"),
    Username = builder.Configuration.GetValue<string>("DB:UserName"),
    Password = builder.Configuration.GetValue<string>("DB:Password"),
    Pooling = true,
    IntegratedSecurity = true,
};

builder.Services.AddDbContext<QuestionsContext>(x => x.UseNpgsql(connectionStringBuilder.ToString()));
// Configuration for SignalR
builder.Services.AddSignalR();


// Add Observability
var observabilityOptions = new ObservabilityOptions();
builder.Configuration.Bind("Observability", observabilityOptions);
builder.Services.AddObservability("Rigo.WebApp", typeof(Program), observabilityOptions);

var app = builder.Build();

// Make sure, that the database exists
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<QuestionsContext>().Database.EnsureCreated();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

// Queries
app.MapGet("api/queries/questions", async (IMediator mediator) 
    => await mediator.Send(new GetQuestionsRequest()));

// Commands
app.MapPost("api/commands/questions/", async (IMediator mediator, string content) 
    => await mediator.Send(new AskQuestionRequest { Content = content }));

app.MapPost("api/commands/questions/{id:int}/vote", async (IMediator mediator, int id) 
    => await mediator.Send(new VoteForQuestionRequest { QuestionID = id }));

// Activate SignalR Hub
app.MapHub<QuestionsHub>("/hub");

app.Run();
