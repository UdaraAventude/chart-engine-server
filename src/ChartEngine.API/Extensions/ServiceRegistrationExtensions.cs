namespace ChartEngine.API.Extensions;

using ChartEngine.Application.Interfaces.Infrastructure;
using ChartEngine.Application.Interfaces.Repositories;
using ChartEngine.Application.Interfaces.Services;
using ChartEngine.Infrastructure.Persistence;
using ChartEngine.Infrastructure.Persistence.Repositories;
using ChartEngine.Infrastructure.Services;
using ChartEngine.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IDatasetRepository, DatasetRepository>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IDatasetService, DatasetService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        return services;
    }
}
