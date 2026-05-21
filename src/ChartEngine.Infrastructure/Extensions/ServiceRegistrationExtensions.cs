namespace ChartEngine.Infrastructure.Extensions;

using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Infrastructure.Persistence;
using ChartEngine.Infrastructure.Persistence.Repositories;
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
        services.AddScoped<IExportRepository, ExportRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IFileStorage, LocalFileStorage>();
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

