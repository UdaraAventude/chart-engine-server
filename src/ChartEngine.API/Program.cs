using ChartEngine.Infrastructure.Extensions;
using ChartEngine.Application.Hubs;
using ChartEngine.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 2_147_483_647; // 2 GB
});

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 2_147_483_647; // 2 GB
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5110", "http://127.0.0.1", "null") // 'null' is often sent by file:// origins
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true) // Allow any origin for testing
              .AllowCredentials(); // SignalR needs credentials
    });
});

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddAnalyticsPipeline();
builder.Services.AddBackgroundPipeline();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(); // Enable CORS before routing/endpoints

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHub<DatasetHub>("/hubs/dataset");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

