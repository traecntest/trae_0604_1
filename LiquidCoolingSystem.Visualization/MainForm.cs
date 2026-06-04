using System.Windows.Forms;
using LiquidCoolingSystem.DataAcquisition.Interfaces;
using LiquidCoolingSystem.DataAcquisition.Models;
using LiquidCoolingSystem.BusinessLogic.Interfaces;
using LiquidCoolingSystem.BusinessLogic.Models;
using Microsoft.Extensions.Logging;

namespace LiquidCoolingSystem.Visualization;

public partial class MainForm : Form
{
    private readonly IDataCollector _dataCollector;
    private readonly ITimeSeriesDatabase _timeSeriesDb;
    private readonly IAlarmEngine _alarmEngine;
    private readonly ICoolingControlSystem _coolingControl;
    private readonly IEnergyEfficiencyAnalyzer _efficiencyAnalyzer;
    private readonly IOperationLogService _operationLog;
    private readonly ILogger<MainForm> _logger;

    private System.Windows.Forms.Timer? _dataTimer;
    private DigitalTwinControl? _digitalTwinControl;
    private HeatMapControl? _heatMapControl;
    private DataGridView? _alarmGrid;
    private DataGridView? _alarmHistoryGrid;
    private DataGridView? _dataGrid;
    private TextBox? _logTextBox;
    private TextBox? _reportTextBox;
    private Panel? _chartPanel;
    private ToolStripStatusLabel? _statusLabel;
    private Label? _pueLabel;
    private Label? _tempLabel;
    private Label? _powerLabel;
    private Label? _alarmCountLabel;
    private TabControl? _mainTabControl;

    private readonly Dictionary<string, CoolingUnitData> _latestData;

    public MainForm(
        IDataCollector dataCollector,
        ITimeSeriesDatabase timeSeriesDb,
        IAlarmEngine alarmEngine,
        ICoolingControlSystem coolingControl,
        IEnergyEfficiencyAnalyzer efficiencyAnalyzer,
        IOperationLogService operationLog,
        ILogger<MainForm> logger)
    {
        _dataCollector = dataCollector;
        _timeSeriesDb = timeSeriesDb;
        _alarmEngine = alarmEngine;
        _coolingControl = coolingControl;
        _efficiencyAnalyzer = efficiencyAnalyzer;
        _operationLog = operationLog;
        _logger = logger;
        _latestData = new Dictionary<string, CoolingUnitData>();

        InitializeComponent();
        InitializeDataCollection();
        _ = _operationLog.LogOperationAsync("Admin", "Login", "系统启动，用户登录");
    }

    private void InitializeComponent()
    {
        this.Text = "智算中心液冷基础设施综合监控系统";
        this.Size = new Size(1400, 900);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(240, 248, 255);

        var toolStrip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            BackColor = Color.FromArgb(30, 60, 114),
            ForeColor = Color.White,
            Height = 40
        };

        var startBtn = new ToolStripButton("开始采集") { ForeColor = Color.White };
        startBtn.Click += StartBtn_Click;
        var stopBtn = new ToolStripButton("停止采集") { ForeColor = Color.White };
        stopBtn.Click += StopBtn_Click;
        var exportBtn = new ToolStripButton("导出报表") { ForeColor = Color.White };
        exportBtn.Click += ExportBtn_Click;
        var refreshBtn = new ToolStripButton("刷新数据") { ForeColor = Color.White };
        refreshBtn.Click += RefreshBtn_Click;

        toolStrip.Items.AddRange(new ToolStripItem[] { startBtn, stopBtn, exportBtn, refreshBtn });

        _mainTabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei", 10)
        };

        var overviewPage = new TabPage("总览");
        var topologyPage = new TabPage("数字孪生");
        var heatmapPage = new TabPage("热力图");
        var alarmPage = new TabPage("告警中心");
        var reportPage = new TabPage("能效分析");
        var logPage = new TabPage("操作日志");

        SetupOverviewPage(overviewPage);
        SetupTopologyPage(topologyPage);
        SetupHeatmapPage(heatmapPage);
        SetupAlarmPage(alarmPage);
        SetupReportPage(reportPage);
        SetupLogPage(logPage);

        _mainTabControl.TabPages.AddRange(new[] { overviewPage, topologyPage, heatmapPage, alarmPage, reportPage, logPage });

        var statusStrip = new StatusStrip();
        var statusLabelItem = new ToolStripStatusLabel("系统就绪");
        _statusLabel = statusLabelItem;
        statusLabelItem.Spring = true;
        var timeLabel = new ToolStripStatusLabel(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabelItem, timeLabel });

        this.Controls.Add(_mainTabControl);
        this.Controls.Add(toolStrip);
        this.Controls.Add(statusStrip);

        var timeTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        timeTimer.Tick += (s, e) => { timeLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); };
        timeTimer.Start();
    }

    private void SetupOverviewPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);

        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 120,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(10)
        };

        var pueCard = CreateStatusCard("平均PUE", "--", Color.FromArgb(52, 152, 219), out var pueValueLabel);
        var tempCard = CreateStatusCard("芯片平均温度", "--°C", Color.FromArgb(46, 204, 113), out var tempValueLabel);
        var powerCard = CreateStatusCard("总功率密度", "-- kW", Color.FromArgb(241, 196, 15), out var powerValueLabel);
        var alarmCard = CreateStatusCard("活动告警", "0", Color.FromArgb(231, 76, 60), out var alarmValueLabel);

        _pueLabel = pueValueLabel;
        _tempLabel = tempValueLabel;
        _powerLabel = powerValueLabel;
        _alarmCountLabel = alarmValueLabel;

        topPanel.Controls.AddRange(new Control[] { pueCard, tempCard, powerCard, alarmCard });

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 650,
            BackColor = Color.Transparent
        };

        _dataGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            GridColor = Color.FromArgb(230, 230, 230)
        };

        _dataGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "UnitId", HeaderText = "机组ID", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "SupplyTemp", HeaderText = "供水温度(°C)", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "ReturnTemp", HeaderText = "回水温度(°C)", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "FlowRate", HeaderText = "流量(L/min)", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "ChipTemp", HeaderText = "芯片温度(°C)", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "PowerDensity", HeaderText = "功率密度(kW)", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "PUE", HeaderText = "PUE", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 80 }
        });

        var logPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        var logLabel = new Label
        {
            Text = "实时数据日志",
            Dock = DockStyle.Top,
            Height = 35,
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 60, 114),
            BackColor = Color.FromArgb(230, 240, 255),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0)
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(245, 245, 245),
            BorderStyle = BorderStyle.None,
            Padding = new Padding(10)
        };

        logPanel.Controls.Add(logLabel);
        logPanel.Controls.Add(_logTextBox);

        splitContainer.Panel1.Controls.Add(_dataGrid);
        splitContainer.Panel2.Controls.Add(logPanel);

        page.Controls.Add(splitContainer);
        page.Controls.Add(topPanel);
    }

    private Panel CreateStatusCard(string title, string value, Color color, out Label valueLabelRef)
    {
        var card = new Panel
        {
            Width = 180,
            Height = 100,
            BackColor = color,
            Margin = new Padding(5),
            Padding = new Padding(15)
        };

        var titleLabel = new Label
        {
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 9),
            Dock = DockStyle.Top,
            Height = 25
        };

        var valueLabel = new Label
        {
            Text = value,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 20, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        valueLabelRef = valueLabel;

        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);

        return card;
    }

    private void SetupTopologyPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);
        _digitalTwinControl = new DigitalTwinControl { Dock = DockStyle.Fill };
        page.Controls.Add(_digitalTwinControl);
    }

    private void SetupHeatmapPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);
        _heatMapControl = new HeatMapControl { Dock = DockStyle.Fill };
        page.Controls.Add(_heatMapControl);
    }

    private void SetupAlarmPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _alarmGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White
        };

        _alarmGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间", Width = 150 },
            new DataGridViewTextBoxColumn { Name = "UnitId", HeaderText = "机组", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "级别", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "类型", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "消息", Width = 250 },
            new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "当前值", Width = 100 },
            new DataGridViewCheckBoxColumn { Name = "Ack", HeaderText = "已确认", Width = 60 }
        });

        _alarmHistoryGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White
        };

        _alarmHistoryGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间", Width = 150 },
            new DataGridViewTextBoxColumn { Name = "UnitId", HeaderText = "机组", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "Level", HeaderText = "级别", Width = 80 },
            new DataGridViewTextBoxColumn { Name = "Message", HeaderText = "消息", Width = 300 },
            new DataGridViewTextBoxColumn { Name = "AckBy", HeaderText = "确认人", Width = 100 }
        });

        var ackBtn = new Button
        {
            Text = "确认选中告警",
            Height = 35,
            Dock = DockStyle.Bottom
        };
        ackBtn.Click += async (s, e) => await AcknowledgeSelectedAlarms();

        var panel1 = new Panel { Dock = DockStyle.Fill };
        panel1.Controls.Add(_alarmGrid);
        panel1.Controls.Add(ackBtn);

        panel.Controls.Add(panel1, 0, 0);
        panel.Controls.Add(_alarmHistoryGrid, 0, 1);

        page.Controls.Add(panel);
    }

    private void SetupReportPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var datePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        var startLabel = new Label { Text = "开始时间:", Location = new Point(30, 25), Width = 70, Font = new Font("Microsoft YaHei", 10) };
        var startPicker = new DateTimePicker { Location = new Point(100, 20), Width = 150, Value = DateTime.Now.AddDays(-1), Font = new Font("Microsoft YaHei", 10) };
        var endLabel = new Label { Text = "结束时间:", Location = new Point(270, 25), Width = 70, Font = new Font("Microsoft YaHei", 10) };
        var endPicker = new DateTimePicker { Location = new Point(340, 20), Width = 150, Value = DateTime.Now, Font = new Font("Microsoft YaHei", 10) };

        var generateBtn = new Button
        {
            Text = "生成报告",
            Location = new Point(510, 17),
            Width = 120,
            Height = 35,
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold)
        };
        generateBtn.Click += async (s, e) => await GenerateReport(startPicker.Value, endPicker.Value);

        datePanel.Controls.AddRange(new Control[] { startLabel, startPicker, endLabel, endPicker, generateBtn });

        _chartPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        var chartTitle = new Label
        {
            Text = "能效分析图表",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 60, 114),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0)
        };

        var chartArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(250, 250, 250)
        };
        chartArea.Paint += ChartArea_Paint;

        _chartPanel.Controls.Add(chartArea);
        _chartPanel.Controls.Add(chartTitle);

        var textPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        var textTitle = new Label
        {
            Text = "能效分析报告",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 60, 114),
            BackColor = Color.FromArgb(230, 240, 255),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(15, 0, 0, 0)
        };

        _reportTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Microsoft YaHei", 10),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(15)
        };

        textPanel.Controls.Add(_reportTextBox);
        textPanel.Controls.Add(textTitle);

        mainPanel.Controls.Add(datePanel, 0, 0);
        mainPanel.Controls.Add(_chartPanel, 0, 1);
        mainPanel.Controls.Add(textPanel, 0, 2);

        page.Controls.Add(mainPanel);
    }

    private void ChartArea_Paint(object? sender, PaintEventArgs e)
    {
        var panel = sender as Panel;
        if (panel == null || _currentChartData == null || !_currentChartData.Any()) return;

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(250, 250, 250));

        var margin = 60;
        var chartWidth = panel.Width - margin * 2;
        var chartHeight = panel.Height - margin * 2;

        using (var gridPen = new Pen(Color.FromArgb(230, 230, 230)))
        {
            for (int i = 0; i <= 5; i++)
            {
                var y = margin + (chartHeight / 5) * i;
                g.DrawLine(gridPen, margin, y, margin + chartWidth, y);
            }
        }

        var maxPUE = Math.Max(1.4, _currentChartData.Max(d => d.Value) * 1.1);
        var minPUE = Math.Min(1.0, _currentChartData.Min(d => d.Value) * 0.9);
        var pueRange = maxPUE - minPUE;

        using (var axisPen = new Pen(Color.FromArgb(100, 100, 100), 2))
        {
            g.DrawLine(axisPen, margin, margin, margin, margin + chartHeight);
            g.DrawLine(axisPen, margin, margin + chartHeight, margin + chartWidth, margin + chartHeight);
        }

        using (var labelFont = new Font("Microsoft YaHei", 9))
        using (var labelBrush = new SolidBrush(Color.FromArgb(80, 80, 80)))
        {
            for (int i = 0; i <= 5; i++)
            {
                var y = margin + (chartHeight / 5) * i;
                var value = maxPUE - (pueRange / 5) * i;
                g.DrawString(value.ToString("F2"), labelFont, labelBrush, 10, y - 8);
            }
        }

        if (_currentChartData.Count >= 2)
        {
            var points = new List<PointF>();
            var step = chartWidth / (_currentChartData.Count - 1);

            for (int i = 0; i < _currentChartData.Count; i++)
            {
                var x = margin + step * i;
                var y = margin + chartHeight - (float)((_currentChartData[i].Value - minPUE) / pueRange * chartHeight);
                points.Add(new PointF(x, y));
            }

            using (var linePen = new Pen(Color.FromArgb(52, 152, 219), 3))
            {
                g.DrawLines(linePen, points.ToArray());
            }

            using (var dotBrush = new SolidBrush(Color.FromArgb(231, 76, 60)))
            {
                foreach (var point in points)
                {
                    g.FillEllipse(dotBrush, point.X - 4, point.Y - 4, 8, 8);
                }
            }
        }

        using (var titleFont = new Font("Microsoft YaHei", 10, FontStyle.Bold))
        using (var titleBrush = new SolidBrush(Color.FromArgb(30, 60, 114)))
        {
            g.DrawString("PUE变化趋势图", titleFont, titleBrush, panel.Width / 2 - 60, 10);
        }

        if (_currentUnitEfficiency != null && _currentUnitEfficiency.Any())
        {
            var barWidth = 60;
            var barSpacing = 30;
            var startX = margin + 100;
            var barMaxHeight = chartHeight - 50;

            var maxAvgPUE = _currentUnitEfficiency.Max(u => u.Value.AveragePUE) * 1.2;

            using (var barFont = new Font("Microsoft YaHei", 8))
            {
                var idx = 0;
                foreach (var ue in _currentUnitEfficiency)
                {
                    var x = startX + (barWidth + barSpacing) * idx;
                    var height = (float)(ue.Value.AveragePUE / maxAvgPUE * barMaxHeight);
                    var y = margin + chartHeight - height;

                    var colors = new[]
                    {
                        Color.FromArgb(52, 152, 219),
                        Color.FromArgb(46, 204, 113),
                        Color.FromArgb(241, 196, 15),
                        Color.FromArgb(230, 126, 34)
                    };

                    using (var barBrush = new SolidBrush(colors[idx % colors.Length]))
                    {
                        g.FillRectangle(barBrush, x, y, barWidth, height);
                    }

                    using (var textBrush = new SolidBrush(Color.FromArgb(50, 50, 50)))
                    {
                        g.DrawString(ue.Key, barFont, textBrush, x + 10, y - 18);
                        g.DrawString(ue.Value.AveragePUE.ToString("F3"), barFont, textBrush, x + 5, y + height + 5);
                    }
                    idx++;
                }
            }

            using (var titleFont = new Font("Microsoft YaHei", 10, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(30, 60, 114)))
            {
                g.DrawString("各机组平均PUE对比", titleFont, titleBrush, panel.Width - 150, 10);
            }
        }
    }

    private List<(DateTime Time, double Value)> _currentChartData = new();
    private Dictionary<string, UnitEfficiency> _currentUnitEfficiency = new();

    private void SetupLogPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);

        var logGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White
        };

        logGrid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "时间", Width = 150 },
            new DataGridViewTextBoxColumn { Name = "User", HeaderText = "用户", Width = 100 },
            new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "类型", Width = 120 },
            new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "描述", Width = 300 },
            new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP地址", Width = 120 }
        });

        var refreshBtn = new Button
        {
            Text = "刷新日志",
            Height = 35,
            Dock = DockStyle.Bottom
        };
        refreshBtn.Click += async (s, e) => await RefreshLogs(logGrid);

        page.Controls.Add(logGrid);
        page.Controls.Add(refreshBtn);

        _ = RefreshLogs(logGrid);
    }

    private void InitializeDataCollection()
    {
        _dataTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _dataTimer.Tick += DataTimer_Tick;
        _dataTimer.Start();

        _alarmEngine.AlarmTriggered += (s, e) => this.Invoke(() => UpdateAlarmDisplay(e));
    }

    private async void DataTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var dataList = await _dataCollector.CollectAllUnitsAsync();

            foreach (var data in dataList)
            {
                _latestData[data.UnitId] = data;
                await _timeSeriesDb.WriteDataAsync(data);
                await _alarmEngine.AnalyzeDataAsync(data);
                await _coolingControl.ComputeControlActionAsync(data);
            }

            this.Invoke(() =>
            {
                UpdateDataDisplay();
                _digitalTwinControl?.UpdateData(_latestData.Values);
                _heatMapControl?.UpdateData(_latestData.Values);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据采集出错");
        }
    }

    private void UpdateDataDisplay()
    {
        if (_dataGrid == null) return;

        _dataGrid.Rows.Clear();
        foreach (var data in _latestData.Values)
        {
            var row = _dataGrid.Rows.Add(
                data.UnitId,
                data.SupplyTemperature.ToString("F1"),
                data.ReturnTemperature.ToString("F1"),
                data.CoolantFlowRate.ToString("F1"),
                data.ChipJunctionTemperature.ToString("F1"),
                data.CabinetPowerDensity.ToString("F1"),
                data.PUE.ToString("F3"),
                data.CDUStatus.ToString()
            );

            if (data.CDUStatus == CDUStatus.Alarm)
                _dataGrid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
            else if (data.CDUStatus == CDUStatus.Warning)
                _dataGrid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 200);
        }

        if (_latestData.Any())
        {
            var avgPUE = _latestData.Values.Average(d => d.PUE);
            var avgTemp = _latestData.Values.Average(d => d.ChipJunctionTemperature);
            var totalPower = _latestData.Values.Sum(d => d.CabinetPowerDensity);
            _pueLabel!.Text = avgPUE.ToString("F3");
            _tempLabel!.Text = avgTemp.ToString("F1") + "°C";
            _powerLabel!.Text = totalPower.ToString("F1") + " kW";
        }

        _statusLabel!.Text = $"数据更新中 - {_latestData.Count} 个机组在线";
    }

    private void UpdateAlarmDisplay(AlarmRecord alarm)
    {
        if (_alarmGrid == null) return;

        _alarmGrid.Rows.Insert(0,
            alarm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            alarm.UnitId,
            alarm.Level.ToString(),
            alarm.Type.ToString(),
            alarm.Message,
            alarm.CurrentValue.ToString("F1"),
            alarm.Acknowledged);
        var row = 0;

        if (alarm.Level == AlarmLevel.Emergency)
            _alarmGrid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(255, 150, 150);
        else if (alarm.Level == AlarmLevel.Critical)
            _alarmGrid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 150);

        if (_alarmCountLabel != null)
        {
            var activeCount = _alarmGrid.Rows.Cast<DataGridViewRow>()
                .Count(r => !Convert.ToBoolean(r.Cells[6].Value));
            _alarmCountLabel.Text = activeCount.ToString();
        }

        AppendLog($"[告警] {alarm.Level} - {alarm.UnitId}: {alarm.Message}");
    }

    private void AppendLog(string message)
    {
        if (_logTextBox == null) return;
        var logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logTextBox.AppendText(logEntry);
        if (_logTextBox.Lines.Length > 500)
        {
            _logTextBox.Text = string.Join(Environment.NewLine, 
                _logTextBox.Lines.Skip(_logTextBox.Lines.Length - 300));
        }
    }

    private async Task AcknowledgeSelectedAlarms()
    {
        if (_alarmGrid == null || _alarmHistoryGrid == null) return;

        var selectedAlarms = new List<AlarmRecord>();
        foreach (DataGridViewRow row in _alarmGrid.SelectedRows)
        {
            if (row.Cells[0].Value != null)
            {
                var timeStr = row.Cells[0].Value.ToString();
                var unitId = row.Cells[1].Value?.ToString();
                var alarms = await _alarmEngine.GetActiveAlarmsAsync();
                var alarm = alarms.FirstOrDefault(a =>
                    a.UnitId == unitId &&
                    a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") == timeStr);

                if (alarm != null)
                {
                    await _alarmEngine.AcknowledgeAlarmAsync(alarm.Id, "Admin");
                    selectedAlarms.Add(alarm);
                    await _operationLog.LogOperationAsync("Admin", "AlarmAck", $"确认告警: {alarm.Message}");
                    AppendLog($"[确认告警] {alarm.UnitId}: {alarm.Message}");
                }
            }
        }

        var activeAlarms = await _alarmEngine.GetActiveAlarmsAsync();
        _alarmGrid.Rows.Clear();
        foreach (var alarm in activeAlarms)
        {
            var rowIndex = _alarmGrid.Rows.Add(
                alarm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                alarm.UnitId,
                alarm.Level.ToString(),
                alarm.Type.ToString(),
                alarm.Message,
                alarm.CurrentValue.ToString("F1"),
                alarm.Acknowledged);

            if (alarm.Level == AlarmLevel.Emergency)
                _alarmGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 150, 150);
            else if (alarm.Level == AlarmLevel.Critical)
                _alarmGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 150);
        }

        var allAlarms = await _alarmEngine.GetAlarmHistoryAsync(DateTime.Now.AddDays(-7), DateTime.Now);
        _alarmHistoryGrid.Rows.Clear();
        foreach (var alarm in allAlarms.Take(100))
        {
            var rowIndex = _alarmHistoryGrid.Rows.Add(
                alarm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                alarm.UnitId,
                alarm.Level.ToString(),
                alarm.Message,
                alarm.AcknowledgedBy ?? "");

            if (alarm.Level == AlarmLevel.Emergency)
                _alarmHistoryGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
            else if (alarm.Level == AlarmLevel.Critical)
                _alarmHistoryGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 200);
        }

        if (_alarmCountLabel != null)
        {
            _alarmCountLabel.Text = activeAlarms.Count().ToString();
        }
    }

    private async Task GenerateReport(DateTime startTime, DateTime endTime)
    {
        var historicalData = new List<CoolingUnitData>();
        var unitIds = new[] { "CU-001", "CU-002", "CU-003", "CU-004" };

        foreach (var unitId in unitIds)
        {
            var unitData = await _timeSeriesDb.QueryDataAsync(unitId, startTime, endTime);
            historicalData.AddRange(unitData);
        }

        var currentData = _latestData.Values.ToList();

        EnergyEfficiencyReport report;

        if (historicalData.Any() || currentData.Any())
        {
            report = await _efficiencyAnalyzer.GenerateReportFromCurrentDataAsync(currentData, historicalData);
        }
        else
        {
            report = await _efficiencyAnalyzer.GenerateReportAsync(startTime, endTime);
        }

        _currentChartData.Clear();

        if (historicalData.Any())
        {
            var pueByTime = historicalData
                .GroupBy(d => d.Timestamp.ToString("yyyy-MM-dd HH:mm"))
                .Select(g => new
                {
                    Time = g.First().Timestamp,
                    AvgPUE = g.Average(d => d.PUE)
                })
                .OrderBy(x => x.Time)
                .ToList();

            var maxPoints = 30;
            var step = Math.Max(1, pueByTime.Count / maxPoints);

            for (int i = 0; i < pueByTime.Count; i += step)
            {
                _currentChartData.Add((pueByTime[i].Time, Math.Round(pueByTime[i].AvgPUE, 3)));
            }

            if (_currentChartData.Count > 0 && _currentChartData.Last().Time != pueByTime.Last().Time)
            {
                _currentChartData.Add((pueByTime.Last().Time, Math.Round(pueByTime.Last().AvgPUE, 3)));
            }
        }
        else if (currentData.Any())
        {
            foreach (var d in currentData)
            {
                _currentChartData.Add((d.Timestamp, Math.Round(d.PUE, 3)));
            }
        }

        _currentUnitEfficiency = new Dictionary<string, UnitEfficiency>(report.UnitEfficiencies);

        if (_chartPanel != null)
        {
            _chartPanel.Invalidate(true);
        }

        if (_reportTextBox != null)
        {
            _reportTextBox.Clear();
            _reportTextBox.AppendText($"╔{new string('═', 50)}╗\r\n");
            _reportTextBox.AppendText($"║{"能效分析报告",-48}║\r\n");
            _reportTextBox.AppendText($"╚{new string('═', 50)}╝\r\n\r\n");
            _reportTextBox.AppendText($"报告时间:     {report.ReportTime:yyyy-MM-dd HH:mm:ss}\r\n");
            _reportTextBox.AppendText($"统计周期:     {startTime:yyyy-MM-dd HH:mm} 至 {endTime:yyyy-MM-dd HH:mm}\r\n");
            _reportTextBox.AppendText($"统计时长:     {report.Duration.TotalHours:F1} 小时\r\n");
            _reportTextBox.AppendText($"数据点数:     {historicalData.Count + currentData.Count}\r\n\r\n");
            _reportTextBox.AppendText("┌" + new string('─', 50) + "┐\r\n");
            _reportTextBox.AppendText("│" + "核心指标".PadRight(48) + "│\r\n");
            _reportTextBox.AppendText("├" + new string('─', 50) + "┤\r\n");
            _reportTextBox.AppendText($"│  平均PUE:    {report.AveragePUE:F3}".PadRight(49) + "│\r\n");
            _reportTextBox.AppendText($"│  最小PUE:    {report.MinPUE:F3}".PadRight(49) + "│\r\n");
            _reportTextBox.AppendText($"│  最大PUE:    {report.MaxPUE:F3}".PadRight(49) + "│\r\n");
            _reportTextBox.AppendText($"│  冷却效率:   {report.CoolingEfficiency:F1}%".PadRight(49) + "│\r\n");
            _reportTextBox.AppendText($"│  预计节能量: {report.TotalEnergySaved:F2} kWh".PadRight(49) + "│\r\n");
            _reportTextBox.AppendText("└" + new string('─', 50) + "┘\r\n\r\n");

            if (report.UnitEfficiencies.Any())
            {
                _reportTextBox.AppendText("┌" + new string('─', 50) + "┐\r\n");
                _reportTextBox.AppendText("│" + "各机组能效".PadRight(48) + "│\r\n");
                _reportTextBox.AppendText("├" + new string('─', 50) + "┤\r\n");
                foreach (var ue in report.UnitEfficiencies)
                {
                    var status = ue.Value.AveragePUE < 1.2 ? "优" : ue.Value.AveragePUE < 1.25 ? "良" : "一般";
                    var line = $"  {ue.Key,-15} PUE: {ue.Value.AveragePUE:F3}  [{status}]";
                    _reportTextBox.AppendText("│" + line.PadRight(48) + "│\r\n");
                }
                _reportTextBox.AppendText("└" + new string('─', 50) + "┘\r\n\r\n");
            }

            if (report.Recommendations.Any())
            {
                _reportTextBox.AppendText("┌" + new string('─', 50) + "┐\r\n");
                _reportTextBox.AppendText("│" + "优化建议".PadRight(48) + "│\r\n");
                _reportTextBox.AppendText("├" + new string('─', 50) + "┤\r\n");
                foreach (var rec in report.Recommendations)
                {
                    var line = $"  • {rec}";
                    if (line.Length > 46) line = line.Substring(0, 43) + "...";
                    _reportTextBox.AppendText("│" + line.PadRight(48) + "│\r\n");
                }
                _reportTextBox.AppendText("└" + new string('─', 50) + "┘\r\n");
            }
        }

        await _operationLog.LogOperationAsync("Admin", "ReportGen", "生成能效分析报告");
        AppendLog($"[报告] 能效分析报告已生成 (数据点: {historicalData.Count + currentData.Count})");
    }

    private async Task RefreshLogs(DataGridView logGrid)
    {
        var logs = await _operationLog.GetRecentLogsAsync(100);
        logGrid.Rows.Clear();
        foreach (var log in logs)
        {
            logGrid.Rows.Add(
                log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                log.UserName,
                log.OperationType,
                log.Description,
                log.IpAddress);
        }
    }

    private void StartBtn_Click(object? sender, EventArgs e)
    {
        _dataTimer?.Start();
        _statusLabel!.Text = "数据采集已启动";
        _ = _operationLog.LogOperationAsync("Admin", "DataCollection", "启动数据采集");
    }

    private void StopBtn_Click(object? sender, EventArgs e)
    {
        _dataTimer?.Stop();
        _statusLabel!.Text = "数据采集已停止";
        _ = _operationLog.LogOperationAsync("Admin", "DataCollection", "停止数据采集");
    }

    private async void ExportBtn_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Excel文件|*.xlsx",
            FileName = $"液冷监控报表_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                await ReportExporter.ExportToExcel(dialog.FileName, _latestData.Values,
                    await _alarmEngine.GetAlarmHistoryAsync(DateTime.Now.AddDays(-7), DateTime.Now));
                MessageBox.Show("报表导出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await _operationLog.LogOperationAsync("Admin", "Export", $"导出报表: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void RefreshBtn_Click(object? sender, EventArgs e)
    {
        await Task.Run(() => DataTimer_Tick(null, EventArgs.Empty));
    }
}
