namespace ChartEngine.Infrastructure.Extensions;

using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Infrastructure.BackgroundJobs;
using ChartEngine.Infrastructure.Persistence;
using ChartEngine.Infrastructure.Persistence.Repositories;
using ChartEngine.Infrastructure.Services;
using ChartEngine.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IDatasetRepository, DatasetRepository>();
        services.AddScoped<ITreeRepository, TreeRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IDatasetService, DatasetService>();
        services.AddScoped<IDatasetPipelineService, DatasetPipelineService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }

    public static IServiceCollection AddBackgroundPipeline(
        this IServiceCollection services)
    {
        // Singleton: one shared channel instance for the whole app
        services.AddSingleton<DatasetProcessingChannel>();

        // AddHostedService registers the worker so .NET starts it automatically
        // when the app starts and stops it cleanly when the app shuts down
        services.AddHostedService<DatasetProcessingWorker>();

        return services;
    }

    public static IServiceCollection AddAnalyticsPipeline(
        this IServiceCollection services)
    {
        services.AddSingleton<ChartEngine.Application.Interfaces.Analytics.ISchemaDetector, ChartEngine.Infrastructure.Analytics.SchemaDetector>();
        services.AddSingleton<ChartEngine.Application.Interfaces.Analytics.ITreeBuilder, ChartEngine.Infrastructure.Analytics.TreeBuilder>();
        return services;
    }
}
