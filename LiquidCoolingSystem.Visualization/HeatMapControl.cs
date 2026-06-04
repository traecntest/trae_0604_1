using System.Drawing.Drawing2D;
using LiquidCoolingSystem.DataAcquisition.Models;

namespace LiquidCoolingSystem.Visualization;

public class HeatMapControl : Control
{
    private readonly Dictionary<string, CoolingUnitData> _unitData;
    private readonly double[,] _heatData;

    public HeatMapControl()
    {
        _unitData = new Dictionary<string, CoolingUnitData>();
        _heatData = new double[8, 8];
        DoubleBuffered = true;
        InitializeHeatData();
    }

    private void InitializeHeatData()
    {
        var rand = new Random();
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                _heatData[i, j] = 60 + rand.NextDouble() * 25;
            }
        }
    }

    public void UpdateData(IEnumerable<CoolingUnitData> data)
    {
        foreach (var d in data)
        {
            _unitData[d.UnitId] = d;
        }

        var unitIndex = 0;
        foreach (var unitData in _unitData.Values)
        {
            var row = unitIndex / 2;
            var col = unitIndex % 2;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    var baseTemp = unitData.ChipJunctionTemperature;
                    var variation = (i + j - 3) * 1.5;
                    _heatData[row * 4 + i, col * 4 + j] = Math.Clamp(baseTemp + variation, 55, 95);
                }
            }
            unitIndex++;
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
        DrawHeatMap(g);
        DrawColorBar(g);
        DrawStats(g);
    }

    private void DrawTitle(Graphics g)
    {
        using var font = new Font("Microsoft YaHei", 14, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(30, 60, 114));
        g.DrawString("机房热点热力图", font, brush, new PointF(Width / 2 - 80, 10));
    }

    private void DrawHeatMap(Graphics g)
    {
        var startX = 60;
        var startY = 60;
        var cellSize = 50;

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                var temp = _heatData[i, j];
                var color = GetHeatColor(temp);

                using var brush = new SolidBrush(color);
                var rect = new Rectangle(startX + j * cellSize, startY + i * cellSize, cellSize, cellSize);

                g.FillRectangle(brush, rect);

                using var pen = new Pen(Color.FromArgb(200, 200, 200), 1);
                g.DrawRectangle(pen, rect);

                using var font = new Font("Microsoft YaHei", 8);
                using var textBrush = new SolidBrush(temp > 75 ? Color.White : Color.Black);
                var textSize = g.MeasureString($"{temp:F0}°", font);
                g.DrawString($"{temp:F0}°", font, textBrush,
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2);
            }
        }

        using var labelFont = new Font("Microsoft YaHei", 9);
        using var labelBrush = new SolidBrush(Color.FromArgb(44, 62, 80));

        for (int i = 0; i < 8; i++)
        {
            g.DrawString($"R{i + 1}", labelFont, labelBrush, startX - 35, startY + i * cellSize + 15);
        }

        for (int j = 0; j < 8; j++)
        {
            g.DrawString($"C{j + 1}", labelFont, labelBrush, startX + j * cellSize + 15, startY - 25);
        }
    }

    private Color GetHeatColor(double temp)
    {
        var normalized = (temp - 55) / 40;
        normalized = Math.Clamp(normalized, 0, 1);

        if (normalized < 0.25)
        {
            var t = normalized / 0.25;
            return Color.FromArgb(
                (int)(46 + t * 6),
                (int)(204 - t * 52),
                (int)(113 - t * 42));
        }
        else if (normalized < 0.5)
        {
            var t = (normalized - 0.25) / 0.25;
            return Color.FromArgb(
                (int)(52 + t * (241 - 52)),
                (int)(152 + t * (196 - 152)),
                (int)(219 - t * (219 - 15)));
        }
        else if (normalized < 0.75)
        {
            var t = (normalized - 0.5) / 0.25;
            return Color.FromArgb(
                (int)(241 + t * (231 - 241)),
                (int)(196 - t * (196 - 76)),
                (int)(15 + t * 60));
        }
        else
        {
            var t = (normalized - 0.75) / 0.25;
            return Color.FromArgb(
                (int)(231 + t * (150 - 231)),
                (int)(76 - t * 76),
                (int)(60 + t * 60));
        }
    }

    private void DrawColorBar(Graphics g)
    {
        var startX = 520;
        var startY = 60;
        var width = 30;
        var height = 400;

        for (int i = 0; i < height; i++)
        {
            var temp = 95 - (i / (double)height) * 40;
            using var brush = new SolidBrush(GetHeatColor(temp));
            g.FillRectangle(brush, startX, startY + i, width, 1);
        }

        using var pen = new Pen(Color.FromArgb(100, 100, 100), 2);
        g.DrawRectangle(pen, startX, startY, width, height);

        using var font = new Font("Microsoft YaHei", 9);
        using var brushText = new SolidBrush(Color.FromArgb(44, 62, 80));

        var temps = new[] { 95, 85, 75, 65, 55 };
        foreach (var temp in temps)
        {
            var y = startY + (float)((95 - temp) / 40.0) * height;
            g.DrawString($"{temp}°C", font, brushText, startX + width + 10, y - 7f);
        }
    }

    private void DrawStats(Graphics g)
    {
        var startX = 580;
        var startY = 60;

        using var font = new Font("Microsoft YaHei", 10);
        using var boldFont = new Font("Microsoft YaHei", 10, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(44, 62, 80));

        g.DrawString("统计信息", boldFont, brush, startX, startY);

        var maxTemp = _heatData.Cast<double>().DefaultIfEmpty(0).Max();
        var minTemp = _heatData.Cast<double>().DefaultIfEmpty(0).Min();
        var avgTemp = _heatData.Cast<double>().DefaultIfEmpty(0).Average();

        g.DrawString($"最高温度: {maxTemp:F1}°C", font, brush, startX, startY + 30);
        g.DrawString($"最低温度: {minTemp:F1}°C", font, brush, startX, startY + 55);
        g.DrawString($"平均温度: {avgTemp:F1}°C", font, brush, startX, startY + 80);

        var hotSpots = 0;
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (_heatData[i, j] > 80) hotSpots++;
            }
        }

        g.DrawString($"热点数量: {hotSpots}", font, brush, startX, startY + 115);

        if (_unitData.Any())
        {
            g.DrawString("各机柜温度:", boldFont, brush, startX, startY + 150);
            var idx = 0;
            foreach (var kv in _unitData)
            {
                g.DrawString($"{kv.Key}: {kv.Value.ChipJunctionTemperature:F1}°C", font, brush,
                    startX, startY + 175 + idx * 25);
                idx++;
            }
        }
    }
}
