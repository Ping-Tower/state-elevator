using Application.Configuration;
using Infrastructure;
using StateElevatorWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
builder.Services.AddHostedService<StateElevatorRabbitMqWorker>();

var host = builder.Build();
host.Run();
