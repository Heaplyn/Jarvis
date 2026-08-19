// Developer: heaplyn
// Date: 2026-08-19
// Summary: Godellian Intelligence Interface v7 (Tabbed & Training-Aware).

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
        private readonly ListBox _logList;
        private readonly TextBlock _feedbackBlock;
        private readonly DispatcherTimer _timer;
        private bool _isRefreshing = false;

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try {
                    if (_instance == null || !_instance.IsLoaded) _instance = new GodellianIntelligenceOverlay();
                    _instance.Show(); _instance.BringToFront();
                } catch { }
            });
        }

        private GodellianIntelligenceOverlay() : base("GODELLIAN CORE MONITOR [SYMBOLIC]", 920, 780)
        {
            _instance = this;
            this.Closed += (s, e) => { _instance = null; _timer.Stop(); };
            WindowPositionManager.RegisterWindow(this, nameof(GodellianIntelligenceOverlay));

            var mainGrid = new Grid { Margin = new Thickness(15) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _telemetryBlock = new TextBlock { Foreground = Brushes.Cyan, FontSize = 12, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0,0,0,10) };
            Grid.SetRow(_telemetryBlock, 0); mainGrid.Children.Add(_telemetryBlock);

            var graphBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 0,0,0)), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Margin = new Thickness(0,0,0,10) };
            _accuracyCanvas = new Canvas { ClipToBounds = true };
            graphBorder.Child = _accuracyCanvas;
            Grid.SetRow(graphBorder, 1); mainGrid.Children.Add(graphBorder);

            var symbolicBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)), BorderThickness = new Thickness(0, 1, 0, 1), BorderBrush = Brushes.Cyan, Padding = new Thickness(10), Margin = new Thickness(0,0,0,15) };
            _symbolicBlock = new TextBlock { Foreground = Brushes.White, FontSize = 20, FontFamily = new FontFamily("Cambria Math"), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Text = "f(x) = ∫ Σ knowledge dt" };
            symbolicBorder.Child = _symbolicBlock;
            Grid.SetRow(symbolicBorder, 2); mainGrid.Children.Add(symbolicBorder);

            var tc = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(tc);

            var feedTab = new TabItem { Header = "📡 LIVE FEED" };
            var feedGrid = new Grid();
            feedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            feedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            var feedLeft = new StackPanel { Margin = new Thickness(0,0,10,0) };
            _thoughtBlock = new TextBlock { Foreground = Brushes.White, FontSize = 14, FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap, Text = "Monitoring synaptic stream...", LineHeight = 22 };
            feedLeft.Children.Add(_thoughtBlock);
            _logList = new ListBox { Background = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)), Foreground = Brushes.Cyan, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), FontSize = 10, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0,10,0,0), Height = 240 };
            feedLeft.Children.Add(new TextBlock { Text = "TRAINING LOG:", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(0,10,0,2) });
            feedLeft.Children.Add(_logList);
            Grid.SetColumn(feedLeft, 0); feedGrid.Children.Add(feedLeft);
            _conceptPanel = new StackPanel();
            var conceptScroll = new ScrollViewer { Content = _conceptPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            Grid.SetColumn(conceptScroll, 1); feedGrid.Children.Add(conceptScroll);
            feedTab.Content = feedGrid; tc.Items.Add(feedTab);

            var clusterTab = new TabItem { Header = "🧬 CLUSTERS" };
            var clusterBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Margin = new Thickness(5) };
            _clusterCanvas = new Canvas { ClipToBounds = true };
            clusterBorder.Child = _clusterCanvas;
            clusterTab.Content = clusterBorder; tc.Items.Add(clusterTab);

            var exchangeTab = new TabItem { Header = "🤝 LLM EXCHANGE" };
            var exStack = new StackPanel { Margin = new Thickness(15) };
            exStack.Children.Add(CreateHeaderBlock("Autonomic Logic Consensus Bridge"));
            _feedbackBlock = new TextBlock { Foreground = Brushes.White, FontSize = 12, TextWrapping = TextWrapping.Wrap, Text = "Press 'EXCHANGE' to sync logic with distributed LLMs...", Opacity = 0.7 };
            var scrollFeedback = new ScrollViewer { Content = _feedbackBlock, Height = 300, Margin = new Thickness(0,10,0,15) };
            exStack.Children.Add(scrollFeedback);
            var exchangeBtn = CreateStyledButton("🚀 INITIATE CROSS-MODEL EXCHANGE", async (s, e) => {
                _feedbackBlock.Text = "Synchronizing manifold state with cloud failover nodes..."; _feedbackBlock.Opacity = 1.0;
                if (CoreRegistry.Intelligence.MainBrain != null) { string res = await CoreRegistry.Intelligence.MainBrain.ExchangeLogicWithLlmAsync(); _feedbackBlock.Text = res; }
            }, isPrimary: true);
            exStack.Children.Add(exchangeBtn);
            exchangeTab.Content = exStack; tc.Items.Add(exchangeTab);

            Grid.SetRow(tc, 3); mainGrid.Children.Add(tc);

            var toolbar = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Height = 42, Margin = new Thickness(0,15,0,0) };
            toolbar.Children.Add(CreateStyledButton("INJECT", (s, e) => ManualVocabInjection()));
            toolbar.Children.Add(CreateStyledButton("MUTATE", (s, e) => Task.Run(() => CoreRegistry.Intelligence.MainBrain?.MutateTopology())));
            toolbar.Children.Add(CreateStyledButton("EXCHANGE", async (s, e) => { if (CoreRegistry.Intelligence.MainBrain != null) await CoreRegistry.Intelligence.MainBrain.ExchangeLogicWithLlmAsync(); }));
            toolbar.Children.Add(CreateStyledButton("RELOAD", (s, e) => Task.Run(() => CoreRegistry.Intelligence.MainBrain?.ReloadVocabulary())));
            toolbar.Children.Add(CreateStyledButton("CLOSE", (s, e) => this.Close()));
            Grid.SetRow(toolbar, 4); mainGrid.Children.Add(toolbar);

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
                        string equation = fullThought.Contains("[SYMBOLIC]:") ? fullThought.Split(new[] { "[SYMBOLIC]:", "[LOGIC]:" }, StringSplitOptions.None)[1].Trim() : "";
                        string logic = fullThought.Contains("[LOGIC]:") ? fullThought.Split(new[] { "[LOGIC]:" }, StringSplitOptions.None)[1].Trim() : fullThought;
                        var output = brain.Think(input);
                        string report = brain.GetDiagnosticReport() + $"\nSRC: {brain.LastTrainingSource} | " + NeuralResourceManager.GetResourceReport();
                        var history = brain.AccuracyHistory.ToList();
                        var log = brain.TrainingLog.ToList();
                        return new { equation, logic, output, report, history, log };
                    } catch { return null; }
                });
                if (result == null) return;
                Application.Current.Dispatcher.Invoke(() => {
                    if (_instance == null || !this.IsLoaded) return;
                    _telemetryBlock.Text = result.report; _symbolicBlock.Text = result.equation; _thoughtBlock.Text = result.logic;
                    UpdateAccuracyGraph(result.history); UpdateClusterMap();
                    _logList.ItemsSource = result.log; _conceptPanel.Children.Clear();
                    for (int i = 0; i < Math.Min(result.output.Size, 15); i++) {
                        double val = Math.Abs(result.output.Data[i]);
                        var pb = new ProgressBar { Width = 280, Height = 6, Value = val * 100, Margin = new Thickness(0,2,0,2), Foreground = val > 0.7 ? Brushes.Lime : (val > 0.4 ? Brushes.Cyan : Brushes.DimGray), Background = new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)), BorderThickness = new Thickness(0) };
                        _conceptPanel.Children.Add(pb);
                    }
                });
            } catch { }
            finally { _isRefreshing = false; }
        }

        private void UpdateAccuracyGraph(List<double> history) {
            _accuracyCanvas.Children.Clear(); if (history.Count < 2) return;
            double w = _accuracyCanvas.ActualWidth; double h = _accuracyCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var points = new PointCollection();
            for (int i = 0; i < history.Count; i++) { double x = (double)i / (history.Count - 1) * w; double y = h - (history[i] / 100.0 * h); points.Add(new Point(x, y)); }
            var polyline = new Polyline { Points = points, Stroke = Brushes.Cyan, StrokeThickness = 2, Opacity = 0.8 };
            _accuracyCanvas.Children.Add(polyline);
            points.Add(new Point(w, h)); points.Add(new Point(0, h));
            var polygon = new Polygon { Points = points, Fill = new LinearGradientBrush(Color.FromArgb(50, 0, 255, 255), Colors.Transparent, 90), Opacity = 0.3 };
            _accuracyCanvas.Children.Add(polygon);
        }

        private void UpdateClusterMap() {
            _clusterCanvas.Children.Clear(); var rand = new Random();
            double w = _clusterCanvas.ActualWidth; double h = _clusterCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            int groups = 8;
            for (int g = 0; g < groups; g++) {
                 double cx = rand.NextDouble() * w; double cy = rand.NextDouble() * h;
                 var color = g % 3 == 0 ? Brushes.Cyan : (g % 3 == 1 ? Brushes.Lime : Brushes.Fuchsia);
                 for (int j = 0; j < 6; j++) {
                     var line = new Line { X1 = cx, Y1 = cy, X2 = cx + (rand.NextDouble() * 50 - 25), Y2 = cy + (rand.NextDouble() * 50 - 25), Stroke = color, StrokeThickness = 0.6, Opacity = 0.4 };
                     _clusterCanvas.Children.Add(line);
                 }
            }
            for (int i = 0; i < 40; i++) {
                var dot = new Ellipse { Width = 5, Height = 5, Fill = rand.NextDouble() > 0.7 ? Brushes.White : Brushes.Cyan, Opacity = rand.NextDouble() };
                Canvas.SetLeft(dot, rand.NextDouble() * w); Canvas.SetTop(dot, rand.NextDouble() * h);
                _clusterCanvas.Children.Add(dot);
            }
        }

        private static TextBlock CreateHeaderBlock(string text) => new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, Margin = new Thickness(0, 0, 0, 10) };

        private void ManualVocabInjection()
        {
            var win = new Window { Title = "Knowledge Ingestion", Width = 400, Height = 200, WindowStyle = WindowStyle.ToolWindow, Background = Brushes.Black, Foreground = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true };
            var stack = new StackPanel { Margin = new Thickness(15) };
            var tb = new TextBox { Margin = new Thickness(0,10,0,10), Background = new SolidColorBrush(Color.FromRgb(30,30,30)), Foreground = Brushes.White, BorderBrush = Brushes.Cyan, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 60 };
            var btn = new Button { Content = "Bridge Knowledge", Padding = new Thickness(10,5,10,5), Background = Brushes.DarkSlateBlue, Foreground = Brushes.White };
            btn.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(tb.Text)) {
                    var list = tb.Text.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).ToList();
                    Task.Run(() => CoreRegistry.Intelligence.MainBrain?.IngestVocabulary(list, "User_Direct"));
                }
                win.Close();
            };
            stack.Children.Add(new TextBlock { Text = "Bridge new concepts into the Godellian field:" });
            stack.Children.Add(tb); stack.Children.Add(btn); win.Content = stack;
            win.Show();
        }
    }
}
