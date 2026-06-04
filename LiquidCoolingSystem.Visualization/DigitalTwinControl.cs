using System.Drawing.Drawing2D;
using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.Visualization;

public class DigitalTwinControl : Control
{
    private readonly Dictionary<string, CoolingUnitData> _unitData;
    private readonly Dictionary<string, Rectangle> _componentPositions;

    public DigitalTwinControl()
    {
        _unitData = new Dictionary<string, CoolingUnitData>();
        _componentPositions = new Dictionary<string, Rectangle>();
        DoubleBuffered = true;
        InitializePositions();
    }

    private void InitializePositions()
    {
        _componentPositions["ColdSource"] = new Rectangle(50, 50, 120, 80);
        _componentPositions["MainPipe"] = new Rectangle(200, 80, 150, 20);
        _componentPositions["CDU1"] = new Rectangle(380, 40, 100, 50);
        _componentPositions["CDU2"] = new Rectangle(380, 110, 100, 50);
        _componentPositions["CDU3"] = new Rectangle(380, 180, 100, 50);
        _componentPositions["CDU4"] = new Rectangle(380, 250, 100, 50);
        _componentPositions["Rack1"] = new Rectangle(520, 40, 120, 50);
        _componentPositions["Rack2"] = new Rectangle(520, 110, 120, 50);
        _componentPositions["Rack3"] = new Rectangle(520, 180, 120, 50);
        _componentPositions["Rack4"] = new Rectangle(520, 250, 120, 50);
    }

    public void UpdateData(IEnumerable<CoolingUnitData> data)
    {
        foreach (var d in data)
        {
            _unitData[d.UnitId] = d;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(240, 248, 255));

        DrawTitle(g);
        DrawConnections(g);
        DrawComponents(g);
        DrawLegend(g);
    }

    private void DrawTitle(Graphics g)
    {
        using var font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(30, 60, 114));
        g.DrawString("液冷系统数字孪生拓扑图", font, brush, new PointF(Width / 2 - 120, 10));
    }

    private void DrawConnections(Graphics g)
    {
        using var pen = new Pen(Color.FromArgb(52, 152, 219), 3);

        for (int i = 0; i < 4; i++)
        {
            var cduRect = _componentPositions[$"CDU{i + 1}"];
            var rackRect = _componentPositions[$"Rack{i + 1}"];

            g.DrawLine(pen,
                cduRect.X + cduRect.Width, cduRect.Y + cduRect.Height / 2,
                rackRect.X, rackRect.Y + rackRect.Height / 2);
        }

        using var mainPen = new Pen(Color.FromArgb(46, 204, 113), 5);
        var mainPipe = _componentPositions["MainPipe"];

        for (int i = 0; i < 4; i++)
        {
            var cduRect = _componentPositions[$"CDU{i + 1}"];
            g.DrawLine(mainPen,
                mainPipe.X + mainPipe.Width, mainPipe.Y + mainPipe.Height / 2,
                cduRect.X, cduRect.Y + cduRect.Height / 2);
        }
    }

    private void DrawComponents(Graphics g)
    {
        DrawColdSource(g);
        DrawCDUs(g);
        DrawRacks(g);
    }

    private void DrawColdSource(Graphics g)
    {
        var rect = _componentPositions["ColdSource"];
        using var brush = new LinearGradientBrush(rect, Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185), LinearGradientMode.Vertical);
        using var pen = new Pen(Color.FromArgb(30, 116, 166), 2);

        g.FillRoundedRectangle(brush, rect, 10);
        g.DrawRoundedRectangle(pen, rect, 10);

        using var font = new Font("Microsoft YaHei", 10, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var textSize = g.MeasureString("冷源", font);
        g.DrawString("冷源", font, textBrush, rect.X + (rect.Width - textSize.Width) / 2, rect.Y + (rect.Height - textSize.Height) / 2);

        using var iconFont = new Font("Segoe UI Symbol", 20);
        g.DrawString("❄", iconFont, textBrush, rect.X + 10, rect.Y + 25);
    }

    private void DrawCDUs(Graphics g)
    {
        for (int i = 0; i < 4; i++)
        {
            var unitId = $"CU-00{i + 1}";
            var rect = _componentPositions[$"CDU{i + 1}"];
            var hasData = _unitData.TryGetValue(unitId, out var data);
            var status = hasData ? data!.CDUStatus : CDUStatus.Offline;

            Color color1, color2, borderColor;

            switch (status)
            {
                case CDUStatus.Normal:
                    color1 = Color.FromArgb(46, 204, 113);
                    color2 = Color.FromArgb(39, 174, 96);
                    borderColor = Color.FromArgb(30, 130, 76);
                    break;
                case CDUStatus.Warning:
                    color1 = Color.FromArgb(241, 196, 15);
                    color2 = Color.FromArgb(243, 156, 18);
                    borderColor = Color.FromArgb(212, 172, 13);
                    break;
                case CDUStatus.Alarm:
                    color1 = Color.FromArgb(231, 76, 60);
                    color2 = Color.FromArgb(192, 57, 43);
                    borderColor = Color.FromArgb(169, 50, 38);
                    break;
                default:
                    color1 = Color.FromArgb(149, 165, 166);
                    color2 = Color.FromArgb(127, 140, 141);
                    borderColor = Color.FromArgb(108, 122, 137);
                    break;
            }

            using var brush = new LinearGradientBrush(rect, color1, color2, LinearGradientMode.Vertical);
            using var pen = new Pen(borderColor, 2);

            g.FillRoundedRectangle(brush, rect, 8);
            g.DrawRoundedRectangle(pen, rect, 8);

            using var font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString($"CDU-{i + 1}", font, textBrush, rect.X + 10, rect.Y + 15);

            if (hasData)
            {
                using var smallFont = new Font("Microsoft YaHei", 7);
                g.DrawString($"{data!.SupplyTemperature:F1}→{data.ReturnTemperature:F1}°C", smallFont, textBrush, rect.X + 10, rect.Y + 30);
            }
        }
    }

    private void DrawRacks(Graphics g)
    {
        for (int i = 0; i < 4; i++)
        {
            var unitId = $"CU-00{i + 1}";
            var rect = _componentPositions[$"Rack{i + 1}"];
            var hasData = _unitData.TryGetValue(unitId, out var data);

            var rackColor = hasData ? GetTemperatureColor(data!.ChipJunctionTemperature) : Color.Gray;

            using var brush = new SolidBrush(rackColor);
            using var pen = new Pen(Color.FromArgb(44, 62, 80), 2);

            g.FillRectangle(brush, rect);
            g.DrawRectangle(pen, rect);

            using var font = new Font("Microsoft YaHei", 9, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            g.DrawString($"机柜{i + 1}", font, textBrush, rect.X + 10, rect.Y + 5);

            if (hasData)
            {
                using var smallFont = new Font("Microsoft YaHei", 8);
                g.DrawString($"芯片: {data!.ChipJunctionTemperature:F1}°C", smallFont, textBrush, rect.X + 10, rect.Y + 20);
                g.DrawString($"负载: {data.ComputingLoad:F0}%", smallFont, textBrush, rect.X + 10, rect.Y + 32);
            }
        }
    }

    private Color GetTemperatureColor(double temp)
    {
        if (temp < 65) return Color.FromArgb(46, 204, 113);
        if (temp < 75) return Color.FromArgb(52, 152, 219);
        if (temp < 80) return Color.FromArgb(241, 196, 15);
        if (temp < 85) return Color.FromArgb(230, 126, 34);
        return Color.FromArgb(231, 76, 60);
    }

    private void DrawLegend(Graphics g)
    {
        var legendY = Height - 80;
        var items = new[]
        {
            new { Text = "正常", Color = Color.FromArgb(46, 204, 113) },
            new { Text = "警告", Color = Color.FromArgb(241, 196, 15) },
            new { Text = "告警", Color = Color.FromArgb(231, 76, 60) },
            new { Text = "离线", Color = Color.FromArgb(149, 165, 166) }
        };

        using var font = new Font("Microsoft YaHei", 9);
        using var textBrush = new SolidBrush(Color.FromArgb(44, 62, 80));

        g.DrawString("状态图例:", font, textBrush, 50, legendY);

        for (int i = 0; i < items.Length; i++)
        {
            var x = 150 + i * 100;
            using var brush = new SolidBrush(items[i].Color);
            g.FillRectangle(brush, x, legendY, 20, 15);
            g.DrawString(items[i].Text, font, textBrush, x + 25, legendY - 2);
        }
    }
}

public static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseAllFigures();
        g.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics g, Pen pen, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseAllFigures();
        g.DrawPath(pen, path);
    }
}
