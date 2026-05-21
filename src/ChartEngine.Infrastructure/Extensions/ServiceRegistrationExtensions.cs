namespace ChartEngine.Infrastructure.Extensions;

using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Application.Interfaces.Visualization;
using ChartEngine.Infrastructure.Persistence;
using ChartEngine.Infrastructure.Persistence.Repositories;
using ChartEngine.Infrastructure.Storage;
using ChartEngine.Infrastructure.Services;
using ChartEngine.Infrastructure.Services.Visualization;
using ChartEngine.Infrastructure.Services.Visualization.Formatters;
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
        services.AddScoped<IExportService, ExportService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }

    public static IServiceCollection AddBackgroundPipeline(
        this IServiceCollection services)
    {
        services.AddSingleton<ChartEngine.Infrastructure.BackgroundJobs.ExportProcessingChannel>();
        services.AddHostedService<ChartEngine.Infrastructure.BackgroundJobs.ExportProcessingWorker>();
        return services;
    }

    public static IServiceCollection AddAnalyticsPipeline(
        this IServiceCollection services)
    {
        services.AddSingleton<ChartEngine.Application.Interfaces.Analytics.ISchemaDetector, ChartEngine.Infrastructure.Analytics.SchemaDetector>();
        services.AddSingleton<ChartEngine.Application.Interfaces.Analytics.ITreeBuilder, ChartEngine.Infrastructure.Analytics.TreeBuilder>();
        return services;
    }

    public static IServiceCollection AddVisualizationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUploadProcessingService, UploadProcessingService>();
        services.AddScoped<IVisualizationService, VisualizationService>();

        // Register Formatters
        services.AddSingleton<IChartFormatter, BarChartFormatter>();
        services.AddSingleton<IChartFormatter, PieChartFormatter>();
        services.AddSingleton<IChartFormatter, LineChartFormatter>();
        services.AddSingleton<IChartFormatter, ScatterFormatter>();
        services.AddSingleton<IChartFormatter, BubbleFormatter>();
        services.AddSingleton<IChartFormatter, HeatmapFormatter>();
        services.AddSingleton<IChartFormatter, HistogramFormatter>();
        services.AddSingleton<IChartFormatter, SunburstFormatter>();
        services.AddSingleton<IChartFormatter, MultilineFormatter>();
        services.AddSingleton<IChartFormatter, CorrelationFormatter>();

        services.AddSingleton<ChartFormatterRegistry>();

        return services;
    }
}

