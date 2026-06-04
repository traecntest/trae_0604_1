using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LiquidCoolingSystem.DataAcquisition;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddDataAcquisitionServices(this IServiceCollection services)
    {
        services.AddSingleton<IDataCollector, SimulatedDataCollector>();
        services.AddSingleton<ITimeSeriesDatabase, InfluxDbWriter>();
        services.AddSingleton<IOperationLogService, OperationLogService>();
        
        return services;
    }
}
