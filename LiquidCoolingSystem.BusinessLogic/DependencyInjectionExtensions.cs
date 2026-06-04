using LiquidCoolingSystem.BusinessLogic.Interfaces;
using LiquidCoolingSystem.BusinessLogic.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LiquidCoolingSystem.BusinessLogic;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddBusinessLogicServices(this IServiceCollection services)
    {
        services.AddSingleton<IAlarmEngine, AlarmEngine>();
        services.AddSingleton<ICoolingControlSystem, CoolingControlSystem>();
        services.AddSingleton<IEnergyEfficiencyAnalyzer, EnergyEfficiencyAnalyzer>();
        
        return services;
    }
}
