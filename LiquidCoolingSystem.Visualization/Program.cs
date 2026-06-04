using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LiquidCoolingSystem.DataAcquisition;
using LiquidCoolingSystem.BusinessLogic;

namespace LiquidCoolingSystem.Visualization;

static class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        ConfigureServices();
        Application.Run(ServiceProvider!.GetRequiredService<MainForm>());
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddDataAcquisitionServices();
        services.AddBusinessLogicServices();

        services.AddTransient<MainForm>();

        ServiceProvider = services.BuildServiceProvider();
    }
}
