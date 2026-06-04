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
    private DataGridView? _dataGrid;
    private ToolStripStatusLabel? _statusLabel;
    private Label? _pueLabel;
    private Label? _tempLabel;
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

        var pueCard = CreateStatusCard("平均PUE", "--", Color.FromArgb(52, 152, 219));
        var tempCard = CreateStatusCard("芯片平均温度", "--°C", Color.FromArgb(46, 204, 113));
        var powerCard = CreateStatusCard("总功率密度", "-- kW", Color.FromArgb(241, 196, 15));
        var alarmCard = CreateStatusCard("活动告警", "0", Color.FromArgb(231, 76, 60));

        _pueLabel = pueCard.Controls[1] as Label;
        _tempLabel = tempCard.Controls[1] as Label;

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
            Height = 30,
            Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10)
        };

        var logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(245, 245, 245)
        };

        logPanel.Controls.Add(logTextBox);
        logPanel.Controls.Add(logLabel);

        splitContainer.Panel1.Controls.Add(_dataGrid);
        splitContainer.Panel2.Controls.Add(logPanel);

        page.Controls.Add(splitContainer);
        page.Controls.Add(topPanel);
    }

    private Panel CreateStatusCard(string title, string value, Color color)
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

        var historyGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White
        };

        historyGrid.Columns.AddRange(new DataGridViewColumn[]
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
        panel.Controls.Add(historyGrid, 0, 1);

        page.Controls.Add(panel);
    }

    private void SetupReportPage(TabPage page)
    {
        page.BackColor = Color.FromArgb(240, 248, 255);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoScroll = true,
            Padding = new Padding(20)
        };

        var datePanel = new Panel
        {
            Width = 1000,
            Height = 60,
            Padding = new Padding(10)
        };

        var startLabel = new Label { Text = "开始时间:", Location = new Point(10, 20), Width = 70 };
        var startPicker = new DateTimePicker { Location = new Point(80, 15), Width = 150, Value = DateTime.Now.AddDays(-1) };
        var endLabel = new Label { Text = "结束时间:", Location = new Point(250, 20), Width = 70 };
        var endPicker = new DateTimePicker { Location = new Point(320, 15), Width = 150, Value = DateTime.Now };

        var generateBtn = new Button
        {
            Text = "生成报告",
            Location = new Point(490, 12),
            Width = 100,
            Height = 30,
            BackColor = Color.FromArgb(52, 152, 219),
            ForeColor = Color.White
        };
        generateBtn.Click += async (s, e) => await GenerateReport(startPicker.Value, endPicker.Value);

        datePanel.Controls.AddRange(new Control[] { startLabel, startPicker, endLabel, endPicker, generateBtn });

        var resultPanel = new Panel
        {
            Width = 1000,
            Height = 400,
            BackColor = Color.White
        };

        var resultTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Microsoft YaHei", 10)
        };
        resultPanel.Controls.Add(resultTextBox);

        panel.Controls.Add(datePanel);
        panel.Controls.Add(resultPanel);

        page.Controls.Add(panel);
    }

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
            _pueLabel!.Text = _latestData.Values.Average(d => d.PUE).ToString("F3");
            _tempLabel!.Text = _latestData.Values.Average(d => d.ChipJunctionTemperature).ToString("F1") + "°C";
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
    }

    private async Task AcknowledgeSelectedAlarms()
    {
        if (_alarmGrid == null) return;

        foreach (DataGridViewRow row in _alarmGrid.SelectedRows)
        {
            if (row.Cells[0].Value != null)
            {
                var timeStr = row.Cells[0].Value.ToString();
                var unitId = row.Cells[1].Value.ToString();
                var alarms = await _alarmEngine.GetActiveAlarmsAsync();
                var alarm = alarms.FirstOrDefault(a =>
                    a.UnitId == unitId &&
                    a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") == timeStr);

                if (alarm != null)
                {
                    await _alarmEngine.AcknowledgeAlarmAsync(alarm.Id, "Admin");
                    await _operationLog.LogOperationAsync("Admin", "AlarmAck", $"确认告警: {alarm.Message}");
                }
            }
        }

        var activeAlarms = await _alarmEngine.GetActiveAlarmsAsync();
        _alarmGrid.Rows.Clear();
        foreach (var alarm in activeAlarms)
        {
            _alarmGrid.Rows.Add(
                alarm.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                alarm.UnitId,
                alarm.Level.ToString(),
                alarm.Type.ToString(),
                alarm.Message,
                alarm.CurrentValue.ToString("F1"),
                alarm.Acknowledged);
        }
    }

    private async Task GenerateReport(DateTime startTime, DateTime endTime)
    {
        var report = await _efficiencyAnalyzer.GenerateReportAsync(startTime, endTime);

        var resultPanel = this.Controls.Find("resultTextBox", true).FirstOrDefault() as TextBox;
        if (resultPanel != null)
        {
            resultPanel.Text = $"=== 能效分析报告 ===\n";
            resultPanel.Text += $"报告时间: {report.ReportTime:yyyy-MM-dd HH:mm:ss}\n";
            resultPanel.Text += $"统计周期: {startTime:yyyy-MM-dd HH:mm} 至 {endTime:yyyy-MM-dd HH:mm}\n";
            resultPanel.Text += $"时长: {report.Duration.TotalHours:F1} 小时\n\n";
            resultPanel.Text += $"平均PUE: {report.AveragePUE:F3}\n";
            resultPanel.Text += $"最小PUE: {report.MinPUE:F3}\n";
            resultPanel.Text += $"最大PUE: {report.MaxPUE:F3}\n";
            resultPanel.Text += $"冷却效率: {report.CoolingEfficiency:F1}%\n";
            resultPanel.Text += $"预计节能量: {report.TotalEnergySaved:F2} kWh\n\n";
            resultPanel.Text += $"--- 各机组能效 ---\n";
            foreach (var ue in report.UnitEfficiencies)
            {
                resultPanel.Text += $"  {ue.Key}: PUE={ue.Value.AveragePUE:F3}\n";
            }
            resultPanel.Text += $"\n--- 优化建议 ---\n";
            foreach (var rec in report.Recommendations)
            {
                resultPanel.Text += $"  • {rec}\n";
            }
        }

        await _operationLog.LogOperationAsync("Admin", "ReportGen", "生成能效分析报告");
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
