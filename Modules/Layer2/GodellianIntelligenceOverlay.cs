// Developer: heaplyn
// Date: 2026-08-18
// Summary: Godellian Intelligence Interface v6 (Symbolic-Enabled).
//          Interactive monitoring with Accuracy Over Time and Symbolic Calculus Output.
//          Robust async UI refreshing with spark visualization.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Shapes;

namespace JarvisLauncher
{
    public class GodellianIntelligenceOverlay : BaseOverlay
    {
        private static GodellianIntelligenceOverlay? _instance;
        private readonly TextBlock _telemetryBlock;
        private readonly TextBlock _thoughtBlock;
        private readonly TextBlock _symbolicBlock;
        private readonly StackPanel _conceptPanel;
        private readonly Canvas _accuracyCanvas;
        private readonly Canvas _clusterCanvas;
        private readonly DispatcherTimer _timer;
        private bool _isRefreshing = false;

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try {
                    if (_instance == null || !_instance.IsLoaded) _instance = new GodellianIntelligenceOverlay();
                    _instance.Show();
                    _instance.BringToFront();
                } catch { }
            });
        }

        private GodellianIntelligenceOverlay() : base("GODELLIAN CORE MONITOR [SYMBOLIC]", 850, 700)
        {
            _instance = this;
            this.Closed += (s, e) => { _instance = null; _timer.Stop(); };
            WindowPositionManager.RegisterWindow(this, nameof(GodellianIntelligenceOverlay));

            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) }); // Accuracy Graph
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) }); // Symbolic Equation
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Mid
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) }); // Cluster Map
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls

            // 1. Telemetry
            _telemetryBlock = new TextBlock { Foreground = Brushes.Cyan, FontSize = 12, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0,0,0,10) };
            Grid.SetRow(_telemetryBlock, 0); mainGrid.Children.Add(_telemetryBlock);

            // 2. Accuracy History Graph
            var graphBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 0,0,0)), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Margin = new Thickness(0,0,0,10) };
            _accuracyCanvas = new Canvas { ClipToBounds = true };
            graphBorder.Child = _accuracyCanvas;
            Grid.SetRow(graphBorder, 1); mainGrid.Children.Add(graphBorder);

            // 3. Symbolic Output
            var symbolicBorder = new Border {
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                BorderBrush = Brushes.Cyan,
                Padding = new Thickness(10),
                Margin = new Thickness(0,0,0,15)
            };
            _symbolicBlock = new TextBlock {
                Foreground = Brushes.White,
                FontSize = 20,
                FontFamily = new FontFamily("Cambria Math"),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Text = "f(x) = ∫ Σ knowledge dt"
            };
            symbolicBorder.Child = _symbolicBlock;
            Grid.SetRow(symbolicBorder, 2); mainGrid.Children.Add(symbolicBorder);

            // 4. Thoughts & Concepts
            var visualizationGrid = new Grid();
            visualizationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            visualizationGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });

            _thoughtBlock = new TextBlock { Foreground = Brushes.White, FontSize = 14, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top, Text = "Monitoring synaptic stream...", LineHeight = 22 };
            visualizationGrid.Children.Add(_thoughtBlock);

            _conceptPanel = new StackPanel();
            var conceptScroll = new ScrollViewer { Content = _conceptPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            Grid.SetColumn(conceptScroll, 1);
            visualizationGrid.Children.Add(conceptScroll);

            Grid.SetRow(visualizationGrid, 3);
            mainGrid.Children.Add(visualizationGrid);

            // 4b. Cluster Map Visualization
            var clusterBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Margin = new Thickness(0,10,0,10) };
            _clusterCanvas = new Canvas { ClipToBounds = true };
            clusterBorder.Child = _clusterCanvas;
            Grid.SetRow(clusterBorder, 4); mainGrid.Children.Add(clusterBorder);

            // 5. Toolbar
            var toolbar = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Height = 36, Margin = new Thickness(0,10,0,0) };
            toolbar.Children.Add(CreateStyledButton("INJECT", (s, e) => ManualVocabInjection()));
            toolbar.Children.Add(CreateStyledButton("MUTATE", (s, e) => Task.Run(() => CoreRegistry.Intelligence.MainBrain.MutateTopology())));
            toolbar.Children.Add(CreateStyledButton("RELOAD", (s, e) => Task.Run(() => CoreRegistry.Intelligence.MainBrain.ReloadVocabulary())));
            toolbar.Children.Add(CreateStyledButton("CLOSE", (s, e) => this.Close()));
            Grid.SetRow(toolbar, 4);
            mainGrid.Children.Add(toolbar);

            this.UserContent = mainGrid;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => RefreshUIAsync();
            _timer.Start();
        }

        private async void RefreshUIAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;

            try {
                var brain = CoreRegistry.Intelligence.MainBrain;
                if (brain == null) return;

                var result = await Task.Run(() => {
                    try {
                        int dim = NeuralVectorizationKernels.CurrentDimension;
                        double[] input = new double[dim];
                        string fullThought = brain.ThinkInWords(input);

                        // Parse symbolic vs logic
                        string equation = fullThought.Contains("[SYMBOLIC]:") ? fullThought.Split(new[] { "[SYMBOLIC]:", "[LOGIC]:" }, StringSplitOptions.None)[1].Trim() : "";
                        string logic = fullThought.Contains("[LOGIC]:") ? fullThought.Split(new[] { "[LOGIC]:" }, StringSplitOptions.None)[1].Trim() : fullThought;

                        var output = brain.Think(input);
                        string report = brain.GetDiagnosticReport() + $"\nSRC: {brain.LastTrainingSource} | " + NeuralResourceManager.GetResourceReport();
                        var history = brain.AccuracyHistory.ToList();

                        return new { equation, logic, output, report, history };
                    } catch { return null; }
                });

                if (result == null) return;

                Application.Current.Dispatcher.Invoke(() => {
                    if (_instance == null || !this.IsLoaded) return;
                    _telemetryBlock.Text = result.report;
                    _symbolicBlock.Text = result.equation;
                    _thoughtBlock.Text = result.logic;

                    UpdateAccuracyGraph(result.history);
                    UpdateClusterMap();

                    _conceptPanel.Children.Clear();
                    for (int i = 0; i < Math.Min(result.output.Size, 15); i++)
                    {
                        double val = Math.Abs(result.output.Data[i]);
                        var pb = new ProgressBar {
                            Width = 250, Height = 5, Value = val * 100, Margin = new Thickness(0,2,0,2),
                            Foreground = val > 0.7 ? Brushes.Lime : (val > 0.4 ? Brushes.Cyan : Brushes.DimGray),
                            Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)), BorderThickness = new Thickness(0)
                        };
                        _conceptPanel.Children.Add(pb);
                    }
                });
            } catch { }
            finally { _isRefreshing = false; }
        }

        private void UpdateAccuracyGraph(List<double> history)
        {
            _accuracyCanvas.Children.Clear();
            if (history.Count < 2) return;

            double w = _accuracyCanvas.ActualWidth;
            double h = _accuracyCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            var points = new PointCollection();
            for (int i = 0; i < history.Count; i++)
            {
                double x = (double)i / (history.Count - 1) * w;
                double y = h - (history[i] / 100.0 * h);
                points.Add(new Point(x, y));
            }

            var polyline = new Polyline { Points = points, Stroke = Brushes.Cyan, StrokeThickness = 2, Opacity = 0.8 };
            _accuracyCanvas.Children.Add(polyline);

            points.Add(new Point(w, h));
            points.Add(new Point(0, h));
            var polygon = new Polygon { Points = points, Fill = new LinearGradientBrush(Color.FromArgb(50, 0, 255, 255), Colors.Transparent, 90), Opacity = 0.3 };
            _accuracyCanvas.Children.Add(polygon);
        }

        private void UpdateClusterMap()
        {
            _clusterCanvas.Children.Clear();
            var rand = new Random();
            double w = _clusterCanvas.ActualWidth;
            double h = _clusterCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Draw cluster groups
            int groups = 6;
            for (int g = 0; g < groups; g++) {
                 double cx = rand.NextDouble() * w;
                 double cy = rand.NextDouble() * h;
                 for (int j = 0; j < 5; j++) {
                     var line = new Line {
                         X1 = cx, Y1 = cy,
                         X2 = cx + (rand.NextDouble() * 40 - 20),
                         Y2 = cy + (rand.NextDouble() * 40 - 20),
                         Stroke = Brushes.Cyan, StrokeThickness = 0.5, Opacity = 0.3
                     };
                     _clusterCanvas.Children.Add(line);
                 }
            }

            // Draw active "firing" neurons
            for (int i = 0; i < 30; i++)
            {
                var dot = new Ellipse { Width = 4, Height = 4, Fill = rand.NextDouble() > 0.8 ? Brushes.Lime : Brushes.Cyan, Opacity = rand.NextDouble() };
                Canvas.SetLeft(dot, rand.NextDouble() * w);
                Canvas.SetTop(dot, rand.NextDouble() * h);
                _clusterCanvas.Children.Add(dot);
            }

            _clusterCanvas.Children.Add(new TextBlock { Text = "LIVE SYNAPTIC CLUSTER EVOLUTION MAP", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(10) });
        }
        private void ManualVocabInjection()
        {
            var win = new Window { Title = "Knowledge Ingestion", Width = 400, Height = 200, WindowStyle = WindowStyle.ToolWindow, Background = Brushes.Black, Foreground = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
            var stack = new StackPanel { Margin = new Thickness(15) };
            var tb = new TextBox { Margin = new Thickness(0,10,0,10), Background = new SolidColorBrush(Color.FromRgb(30,30,30)), Foreground = Brushes.White, BorderBrush = Brushes.Cyan, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 60 };
            var btn = new Button { Content = "Bridge Knowledge", Padding = new Thickness(10,5,10,5), Background = Brushes.DarkSlateBlue, Foreground = Brushes.White };
            btn.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(tb.Text)) {
                    var list = tb.Text.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).ToList();
                    Task.Run(() => CoreRegistry.Intelligence.MainBrain.IngestVocabulary(list, "User_Direct"));
                }
                win.Close();
            };
            stack.Children.Add(new TextBlock { Text = "Bridge new concepts into the Godellian field:" });
            stack.Children.Add(tb); stack.Children.Add(btn); win.Content = stack;
            win.Show();
        }
    }
}
