using ChartEngine.Infrastructure.Extensions;
using ChartEngine.Application.Hubs;
using ChartEngine.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddAnalyticsPipeline();
builder.Services.AddBackgroundPipeline();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHub<DatasetHub>("/hubs/dataset");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

