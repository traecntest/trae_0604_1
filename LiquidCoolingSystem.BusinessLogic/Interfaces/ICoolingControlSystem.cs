using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.BusinessLogic.Interfaces;

public interface ICoolingControlSystem
{
    event EventHandler<CoolingControlCommand>? ControlCommandIssued;

    Task<CoolingControlCommand> ComputeControlActionAsync(CoolingUnitData currentData);
    Task<IEnumerable<CoolingControlCommand>> GetControlHistoryAsync(DateTime startTime, DateTime endTime);
    void ConfigureControlParameters(ControlParameters parameters);
    double GetTargetChipTemperature();
}

public class ControlParameters
{
    public double TargetChipTemperature { get; set; } = 75;
    public double MinChipTemperature { get; set; } = 60;
    public double MaxChipTemperature { get; set; } = 85;
    public double MinPumpSpeed { get; set; } = 40;
    public double MaxPumpSpeed { get; set; } = 100;
    public double MinFanFrequency { get; set; } = 20;
    public double MaxFanFrequency { get; set; } = 75;
    public double ProportionalGain { get; set; } = 0.8;
    public double IntegralGain { get; set; } = 0.1;
    public double DerivativeGain { get; set; } = 0.05;
}
