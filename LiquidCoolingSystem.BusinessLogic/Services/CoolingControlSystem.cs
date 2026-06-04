using LiquidCoolingSystem.BusinessLogic.Interfaces;
using LiquidCoolingSystem.BusinessLogic.Models;
using LiquidCoolingSystem.DataAcquisition.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.BusinessLogic.Services;

public class CoolingControlSystem : ICoolingControlSystem
{
    private readonly ILogger<CoolingControlSystem> _logger;
    private readonly List<CoolingControlCommand> _commandHistory;
    private ControlParameters _parameters;
    private readonly Dictionary<string, ControlState> _unitStates;

    public event EventHandler<CoolingControlCommand>? ControlCommandIssued;

    private class ControlState
    {
        public double IntegralError { get; set; }
        public double PreviousError { get; set; }
        public double LastPumpSpeed { get; set; } = 75;
        public double LastFanFrequency { get; set; } = 45;
    }

    public CoolingControlSystem(ILogger<CoolingControlSystem> logger)
    {
        _logger = logger;
        _commandHistory = new List<CoolingControlCommand>();
        _parameters = new ControlParameters();
        _unitStates = new Dictionary<string, ControlState>();
    }

    public async Task<CoolingControlCommand> ComputeControlActionAsync(CoolingUnitData currentData)
    {
        if (!_unitStates.TryGetValue(currentData.UnitId, out var state))
        {
            state = new ControlState();
            _unitStates[currentData.UnitId] = state;
        }

        var error = currentData.ChipJunctionTemperature - _parameters.TargetChipTemperature;
        state.IntegralError += error;
        state.IntegralError = Math.Clamp(state.IntegralError, -50, 50);

        var derivative = error - state.PreviousError;

        var pidOutput = _parameters.ProportionalGain * error +
                        _parameters.IntegralGain * state.IntegralError +
                        _parameters.DerivativeGain * derivative;

        var loadFactor = currentData.ComputingLoad / 100.0;

        var targetPumpSpeed = state.LastPumpSpeed + pidOutput * 0.5 + loadFactor * 20;
        var targetFanFrequency = state.LastFanFrequency + pidOutput * 0.3 + loadFactor * 15;

        targetPumpSpeed = Math.Clamp(targetPumpSpeed, _parameters.MinPumpSpeed, _parameters.MaxPumpSpeed);
        targetFanFrequency = Math.Clamp(targetFanFrequency, _parameters.MinFanFrequency, _parameters.MaxFanFrequency);

        var reason = GenerateControlReason(currentData, error, targetPumpSpeed, targetFanFrequency);

        var command = new CoolingControlCommand
        {
            UnitId = currentData.UnitId,
            Timestamp = DateTime.Now,
            TargetPumpSpeed = Math.Round(targetPumpSpeed, 1),
            TargetFanFrequency = Math.Round(targetFanFrequency, 1),
            TargetSupplyTemperature = CalculateTargetSupplyTemp(currentData, loadFactor),
            ControlReason = reason,
            ComputedLoad = currentData.ComputingLoad
        };

        state.PreviousError = error;
        state.LastPumpSpeed = targetPumpSpeed;
        state.LastFanFrequency = targetFanFrequency;

        _commandHistory.Add(command);
        if (_commandHistory.Count > 5000)
        {
            _commandHistory.RemoveRange(0, 500);
        }

        _logger.LogDebug("Control command for {UnitId}: Pump={Pump:F1}%, Fan={Fan:F1}Hz",
            currentData.UnitId, targetPumpSpeed, targetFanFrequency);

        ControlCommandIssued?.Invoke(this, command);

        await Task.CompletedTask;
        return command;
    }

    private string GenerateControlReason(CoolingUnitData data, double error, double pumpSpeed, double fanFreq)
    {
        var reasons = new List<string>();

        if (error > 5)
            reasons.Add("芯片温度过高");
        else if (error < -5)
            reasons.Add("芯片温度偏低");

        if (data.ComputingLoad > 80)
            reasons.Add("高算力负载");
        else if (data.ComputingLoad < 40)
            reasons.Add("低负载节能");

        if (data.OutdoorWetBulbTemp > 30)
            reasons.Add("室外高温");

        return reasons.Count > 0 ? string.Join(", ", reasons) : "常规调节";
    }

    private double CalculateTargetSupplyTemp(CoolingUnitData data, double loadFactor)
    {
        var baseTemp = 18.0;
        var loadAdjustment = loadFactor * 4;
        var outdoorAdjustment = Math.Max(0, (data.OutdoorWetBulbTemp - 25) * 0.3);

        return Math.Round(baseTemp + loadAdjustment + outdoorAdjustment, 1);
    }

    public Task<IEnumerable<CoolingControlCommand>> GetControlHistoryAsync(DateTime startTime, DateTime endTime)
    {
        var result = _commandHistory
            .Where(c => c.Timestamp >= startTime && c.Timestamp <= endTime)
            .OrderByDescending(c => c.Timestamp)
            .ToList();

        return Task.FromResult<IEnumerable<CoolingControlCommand>>(result);
    }

    public void ConfigureControlParameters(ControlParameters parameters)
    {
        _parameters = parameters;
        _logger.LogInformation("Control parameters updated");
    }

    public double GetTargetChipTemperature()
    {
        return _parameters.TargetChipTemperature;
    }
}
