using NetworkService.Commands;
using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NetworkService.ViewModel
{
    public class GraphViewModel : BaseViewModel
    {
        // ── Podaci ───────────────────────────────────────────────────
        public ObservableCollection<ServerEntity> Entities =>
            MainWindowViewModel.Entities;

        private ServerEntity _selectedEntity;
        public ServerEntity SelectedEntity
        {
            get => _selectedEntity;
            set
            {
                _selectedEntity = value;
                OnPropertyChanged(nameof(SelectedEntity));
                LoadMeasurements();
                RedrawGraph();
            }
        }

        // ── Graf elementi ─────────────────────────────────────────────
        public ObservableCollection<BarData> Bars { get; } = new ObservableCollection<BarData>();
        public ObservableCollection<GridLineData> GridLines { get; } = new ObservableCollection<GridLineData>();
        public ObservableCollection<YAxisLabel> YAxisLabels { get; } = new ObservableCollection<YAxisLabel>();
        public ObservableCollection<MeasurementRow> RecentMeasurements { get; } = new ObservableCollection<MeasurementRow>();

        // ── Canvas dimenzije ──────────────────────────────────────────
        private double _canvasWidth = 300;
        private double _canvasHeight = 200;

        public double CanvasWidth
        {
            get => _canvasWidth;
            set { _canvasWidth = value; OnPropertyChanged(nameof(CanvasWidth)); }
        }

        public double CanvasHeight
        {
            get => _canvasHeight;
            set { _canvasHeight = value; OnPropertyChanged(nameof(CanvasHeight)); }
        }

        // ── Granične linije (45% i 75%) ───────────────────────────────
        private double _minBoundaryY;
        private double _maxBoundaryY;

        public double MinBoundaryY
        {
            get => _minBoundaryY;
            set { _minBoundaryY = value; OnPropertyChanged(nameof(MinBoundaryY)); }
        }

        public double MaxBoundaryY
        {
            get => _maxBoundaryY;
            set { _maxBoundaryY = value; OnPropertyChanged(nameof(MaxBoundaryY)); }
        }

        // ── Timer za real-time osvežavanje ───────────────────────────
        private readonly DispatcherTimer _refreshTimer;

        // ── Komande ──────────────────────────────────────────────────
        public ICommand UndoCommand { get; }

        public GraphViewModel()
        {
            UndoCommand = new RelayCommand(_ => { }, _ => false);

            // Real-time osvežavanje svakih 2 sekunde
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _refreshTimer.Tick += (s, e) =>
            {
                LoadMeasurements();
                RedrawGraph();
            };
            _refreshTimer.Start();
        }

        // ── Čitanje Log.txt ───────────────────────────────────────────
        private System.Collections.Generic.List<(DateTime Time, double Value)> ReadFromLog(int entityId)
        {
            var results = new System.Collections.Generic.List<(DateTime, double)>();
            string logPath = MainWindowViewModel.LogFilePath;

            if (!File.Exists(logPath)) return results;

            try
            {
                // Log format: "yyyy-MM-dd HH:mm:ss | ID: X | Name: Y | Value: Z%"
                var lines = File.ReadAllLines(logPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    string timePart = parts[0].Trim();
                    string idPart = parts[1].Trim();
                    string valuePart = parts[3].Trim();

                    if (!DateTime.TryParse(timePart, out DateTime time)) continue;

                    string idStr = idPart.Replace("ID:", "").Trim();
                    if (!int.TryParse(idStr, out int id)) continue;
                    if (id != entityId) continue;

                    string valStr = valuePart.Replace("Value:", "").Replace("%", "").Trim();
                    if (!double.TryParse(valStr,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double val)) continue;

                    results.Add((time, val));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log: {ex.Message}");
            }

            // Vraćamo poslednjih 5
            return results.GetRange(Math.Max(0, results.Count - 5), Math.Min(5, results.Count)).ToList();
        }

        // ── Punjenje merenja u legendu ────────────────────────────────
        private void LoadMeasurements()
        {
            RecentMeasurements.Clear();
            if (_selectedEntity == null) return;

            var measurements = ReadFromLog(_selectedEntity.Id);
            const double maxProgressWidth = 120.0;

            foreach (var (time, value) in measurements)
            {
                bool isValid = value >= ServerEntity.MinValue &&
                               value <= ServerEntity.MaxValue;

                RecentMeasurements.Add(new MeasurementRow
                {
                    TimeLabel = time.ToString("HH:mm"),
                    ValueText = $"{value:F1}%",
                    BarColor = isValid ? "#16A34A" : "#DC2626",
                    TextColor = isValid ? "#16A34A" : "#DC2626",
                    ProgressWidth = value / 100.0 * maxProgressWidth,
                    RawValue = value,
                    Time = time
                });
            }
        }

        // ── Crtanje G2 bar grafikona ──────────────────────────────────
        public void RedrawGraph()
        {
            Bars.Clear();
            GridLines.Clear();
            YAxisLabels.Clear();

            if (_selectedEntity == null || _canvasHeight <= 0 || _canvasWidth <= 0)
                return;

            var measurements = ReadFromLog(_selectedEntity.Id);
            if (measurements.Count == 0) return;

            const double maxValue = 100.0;
            const double paddingTop = 16.0;
            const double paddingBot = 4.0;
            const double barSpacing = 8.0;

            double graphHeight = _canvasHeight - paddingTop - paddingBot;
            double totalBars = measurements.Count;
            double barWidth = (_canvasWidth - barSpacing * (totalBars + 1)) / totalBars;

            // Granične linije (45% i 75%)
            MinBoundaryY = paddingTop + graphHeight * (1 - ServerEntity.MinValue / maxValue);
            MaxBoundaryY = paddingTop + graphHeight * (1 - ServerEntity.MaxValue / maxValue);

            // Horizontalne grid linije (0, 25, 50, 75, 100)
            double[] gridValues = { 0, 25, 50, 75, 100 };
            foreach (var gv in gridValues)
            {
                double gy = paddingTop + graphHeight * (1 - gv / maxValue);
                GridLines.Add(new GridLineData
                {
                    X1 = 0,
                    Y1 = gy,
                    X2 = _canvasWidth,
                    Y2 = gy
                });
                YAxisLabels.Add(new YAxisLabel
                {
                    Label = $"{gv:F0}",
                    YPosition = gy - 6
                });
            }

            // Crtanje barova
            for (int i = 0; i < measurements.Count; i++)
            {
                var (time, value) = measurements[i];

                bool isValid = value >= ServerEntity.MinValue &&
                               value <= ServerEntity.MaxValue;

                double barHeight = graphHeight * (value / maxValue);
                double x = barSpacing + i * (barWidth + barSpacing);
                double y = paddingTop + graphHeight - barHeight;

                Bars.Add(new BarData
                {
                    X = x,
                    Y = y,
                    Width = barWidth,
                    Height = barHeight,
                    Color = isValid
                                    ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                                    : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
                    TimeLabel = time.ToString("HH:mm"),
                    ValueLabel = $"{value:F0}%",
                    ValueLabelY = y - 14,
                    TooltipText = $"{time:HH:mm:ss} — {value:F1}%"
                });
            }
        }
    }

    // ── Pomoćne klase ─────────────────────────────────────────────────

    public class BarData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public SolidColorBrush Color { get; set; }
        public string TimeLabel { get; set; }
        public string ValueLabel { get; set; }
        public double ValueLabelY { get; set; }
        public string TooltipText { get; set; }
    }

    public class GridLineData
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
    }

    public class YAxisLabel
    {
        public string Label { get; set; }
        public double YPosition { get; set; }
    }

    public class MeasurementRow
    {
        public string TimeLabel { get; set; }
        public string ValueText { get; set; }
        public string BarColor { get; set; }
        public string TextColor { get; set; }
        public double ProgressWidth { get; set; }
        public double RawValue { get; set; }
        public DateTime Time { get; set; }
    }
}