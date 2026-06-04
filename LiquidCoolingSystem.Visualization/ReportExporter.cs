using OfficeOpenXml;
using LiquidCoolingSystem.DataAcquisition.Models;
using LiquidCoolingSystem.BusinessLogic.Models;

namespace LiquidCoolingSystem.Visualization;

public static class ReportExporter
{
    public static async Task ExportToExcel(string filePath, IEnumerable<CoolingUnitData> currentData,
        IEnumerable<AlarmRecord> alarms)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();

        var dataSheet = package.Workbook.Worksheets.Add("实时数据");
        var alarmSheet = package.Workbook.Worksheets.Add("告警记录");
        var summarySheet = package.Workbook.Worksheets.Add("汇总统计");

        FillDataSheet(dataSheet, currentData);
        FillAlarmSheet(alarmSheet, alarms);
        FillSummarySheet(summarySheet, currentData, alarms);

        await package.SaveAsAsync(filePath);
    }

    private static void FillDataSheet(ExcelWorksheet sheet, IEnumerable<CoolingUnitData> data)
    {
        sheet.Cells[1, 1].Value = "机组ID";
        sheet.Cells[1, 2].Value = "采集时间";
        sheet.Cells[1, 3].Value = "供水温度(°C)";
        sheet.Cells[1, 4].Value = "回水温度(°C)";
        sheet.Cells[1, 5].Value = "温差(°C)";
        sheet.Cells[1, 6].Value = "冷却液流量(L/min)";
        sheet.Cells[1, 7].Value = "冷板压降(kPa)";
        sheet.Cells[1, 8].Value = "芯片结温(°C)";
        sheet.Cells[1, 9].Value = "功率密度(kW/rack)";
        sheet.Cells[1, 10].Value = "PUE";
        sheet.Cells[1, 11].Value = "泵速(%)";
        sheet.Cells[1, 12].Value = "风扇频率(Hz)";
        sheet.Cells[1, 13].Value = "算力负载(%)";
        sheet.Cells[1, 14].Value = "CDU状态";

        var row = 2;
        foreach (var d in data)
        {
            sheet.Cells[row, 1].Value = d.UnitId;
            sheet.Cells[row, 2].Value = d.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            sheet.Cells[row, 3].Value = Math.Round(d.SupplyTemperature, 2);
            sheet.Cells[row, 4].Value = Math.Round(d.ReturnTemperature, 2);
            sheet.Cells[row, 5].Value = Math.Round(d.ReturnTemperature - d.SupplyTemperature, 2);
            sheet.Cells[row, 6].Value = Math.Round(d.CoolantFlowRate, 2);
            sheet.Cells[row, 7].Value = Math.Round(d.ColdPlatePressureDrop, 2);
            sheet.Cells[row, 8].Value = Math.Round(d.ChipJunctionTemperature, 2);
            sheet.Cells[row, 9].Value = Math.Round(d.CabinetPowerDensity, 2);
            sheet.Cells[row, 10].Value = Math.Round(d.PUE, 3);
            sheet.Cells[row, 11].Value = Math.Round(d.PumpSpeed, 1);
            sheet.Cells[row, 12].Value = Math.Round(d.FanFrequency, 1);
            sheet.Cells[row, 13].Value = Math.Round(d.ComputingLoad, 1);
            sheet.Cells[row, 14].Value = d.CDUStatus.ToString();

            if (d.CDUStatus == CDUStatus.Alarm)
            {
                sheet.Cells[row, 1, row, 14].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                sheet.Cells[row, 1, row, 14].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 200, 200));
            }
            else if (d.CDUStatus == CDUStatus.Warning)
            {
                sheet.Cells[row, 1, row, 14].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                sheet.Cells[row, 1, row, 14].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 255, 200));
            }

            row++;
        }

        sheet.Cells[1, 1, 1, 14].Style.Font.Bold = true;
        sheet.Cells[1, 1, row - 1, 14].AutoFitColumns();
    }

    private static void FillAlarmSheet(ExcelWorksheet sheet, IEnumerable<AlarmRecord> alarms)
    {
        sheet.Cells[1, 1].Value = "告警时间";
        sheet.Cells[1, 2].Value = "机组ID";
        sheet.Cells[1, 3].Value = "告警级别";
        sheet.Cells[1, 4].Value = "告警类型";
        sheet.Cells[1, 5].Value = "告警消息";
        sheet.Cells[1, 6].Value = "当前值";
        sheet.Cells[1, 7].Value = "阈值";
        sheet.Cells[1, 8].Value = "是否确认";
        sheet.Cells[1, 9].Value = "确认人";
        sheet.Cells[1, 10].Value = "确认时间";

        var row = 2;
        foreach (var a in alarms)
        {
            sheet.Cells[row, 1].Value = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            sheet.Cells[row, 2].Value = a.UnitId;
            sheet.Cells[row, 3].Value = a.Level.ToString();
            sheet.Cells[row, 4].Value = a.Type.ToString();
            sheet.Cells[row, 5].Value = a.Message;
            sheet.Cells[row, 6].Value = Math.Round(a.CurrentValue, 2);
            sheet.Cells[row, 7].Value = Math.Round(a.Threshold, 2);
            sheet.Cells[row, 8].Value = a.Acknowledged ? "是" : "否";
            sheet.Cells[row, 9].Value = a.AcknowledgedBy ?? "";
            sheet.Cells[row, 10].Value = a.AcknowledgedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

            if (a.Level == AlarmLevel.Emergency)
            {
                sheet.Cells[row, 1, row, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                sheet.Cells[row, 1, row, 10].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 150, 150));
            }
            else if (a.Level == AlarmLevel.Critical)
            {
                sheet.Cells[row, 1, row, 10].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                sheet.Cells[row, 1, row, 10].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 220, 150));
            }

            row++;
        }

        sheet.Cells[1, 1, 1, 10].Style.Font.Bold = true;
        sheet.Cells[1, 1, row - 1, 10].AutoFitColumns();
    }

    private static void FillSummarySheet(ExcelWorksheet sheet, IEnumerable<CoolingUnitData> data,
        IEnumerable<AlarmRecord> alarms)
    {
        var dataList = data.ToList();
        var alarmList = alarms.ToList();

        sheet.Cells[1, 1].Value = "液冷基础设施监控系统报表";
        sheet.Cells[1, 1, 1, 2].Merge = true;
        sheet.Cells[1, 1].Style.Font.Size = 14;
        sheet.Cells[1, 1].Style.Font.Bold = true;

        sheet.Cells[3, 1].Value = "报表生成时间";
        sheet.Cells[3, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        sheet.Cells[5, 1].Value = "=== 系统概览 ===";
        sheet.Cells[5, 1].Style.Font.Bold = true;

        sheet.Cells[6, 1].Value = "监控机组数量";
        sheet.Cells[6, 2].Value = dataList.Count;

        sheet.Cells[7, 1].Value = "平均PUE";
        sheet.Cells[7, 2].Value = dataList.Any() ? Math.Round(dataList.Average(d => d.PUE), 3) : 0;

        sheet.Cells[8, 1].Value = "平均芯片温度";
        sheet.Cells[8, 2].Value = dataList.Any() ? Math.Round(dataList.Average(d => d.ChipJunctionTemperature), 2) + "°C" : "N/A";

        sheet.Cells[9, 1].Value = "平均功率密度";
        sheet.Cells[9, 2].Value = dataList.Any() ? Math.Round(dataList.Average(d => d.CabinetPowerDensity), 2) + " kW/rack" : "N/A";

        sheet.Cells[11, 1].Value = "=== 告警统计 ===";
        sheet.Cells[11, 1].Style.Font.Bold = true;

        sheet.Cells[12, 1].Value = "告警总数";
        sheet.Cells[12, 2].Value = alarmList.Count;

        sheet.Cells[13, 1].Value = "紧急告警";
        sheet.Cells[13, 2].Value = alarmList.Count(a => a.Level == AlarmLevel.Emergency);

        sheet.Cells[14, 1].Value = "严重告警";
        sheet.Cells[14, 2].Value = alarmList.Count(a => a.Level == AlarmLevel.Critical);

        sheet.Cells[15, 1].Value = "警告告警";
        sheet.Cells[15, 2].Value = alarmList.Count(a => a.Level == AlarmLevel.Warning);

        sheet.Cells[16, 1].Value = "未确认告警";
        sheet.Cells[16, 2].Value = alarmList.Count(a => !a.Acknowledged);

        sheet.Cells[1, 1, 20, 2].AutoFitColumns();
    }
}
