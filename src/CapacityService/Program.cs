using CapacityService.Data;
using CapacityService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddDbContext<CapacityDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("CapacityDb")));

var app = builder.Build();

app.MapGrpcService<CapacityGrpcService>();
app.MapGrpcReflectionService();

app.Run();
