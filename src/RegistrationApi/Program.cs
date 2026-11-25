using System.Text.Json.Serialization;
using CapacityService.Grpc;
using Microsoft.EntityFrameworkCore;
using RegistrationApi;
using RegistrationApi.Data;
using RegistrationApi.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<RegistrationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("RegistrationDb")));

builder.Services.AddGrpcClient<Capacity.CapacityClient>(o =>
    o.Address = new Uri(builder.Configuration["Services:CapacityGrpc"]!));

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GrpcExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();
