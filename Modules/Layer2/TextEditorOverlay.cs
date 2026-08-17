// Developer: heaplyn
// Date: 2026-08-17
// Summary: High-Performance Glassmorphic AI Code Studio with integrated offline analysis.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;

namespace JarvisLauncher
{
    public class EditorTab
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string OriginalText { get; set; } = string.Empty;
        public RichTextBox Editor { get; set; } = null!;
        public TabItem TabItem { get; set; } = null!;
        public bool IsModified => GetText(Editor) != OriginalText;

        public static string GetText(RichTextBox rtb) { var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd); return range.Text.Replace("\r\n", "\n").TrimEnd('\n'); }
        public static void SetText(RichTextBox rtb, string text) { rtb.Document.Blocks.Clear(); var p = new Paragraph(new Run(text)) { Margin = new Thickness(0) }; rtb.Document.Blocks.Add(p); }
    }

    public class TextEditorOverlay : BaseOverlay
    {
        private static TextEditorOverlay? _instance;
        private readonly List<EditorTab> _openTabs = new();
        private readonly TabControl _editorTabControl;
        private readonly TreeView _fileTreeView;
        private readonly ListBox _errorListBox;
        private readonly Border _errorPanel;
        private readonly TextBlock _statusLabel;

        public static void OpenFile(string path) {
            Application.Current.Dispatcher.InvokeAsync(() => {
                if (_instance == null || !_instance.IsLoaded) _instance = new TextEditorOverlay();
                _instance.Show(); _instance.LoadFile(path); _instance.BringToFront();
            });
        }

        public static void OpenWorkspace(string path) {
            Application.Current.Dispatcher.InvokeAsync(() => {
                if (_instance == null || !_instance.IsLoaded) _instance = new TextEditorOverlay();
                _instance.Show(); _instance.LoadWorkspace(path); _instance.BringToFront();
            });
        }

        public static void PromptAndOpenFile() {
            Application.Current.Dispatcher.Invoke(() => {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Open File" };
                if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
            });
        }

        private TextEditorOverlay() : base("JARVIS AI CODE STUDIO", 1280, 800)
        {
            _instance = this;
            var layoutGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var toolbar = new Border { Background = new SolidColorBrush(Color.FromArgb(40, 128, 80, 230)), Padding = new Thickness(15, 8, 15, 8), BorderThickness = new Thickness(0,0,0,1), BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _statusLabel = new TextBlock { Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(_statusLabel);

            var tbStack = new StackPanel { Orientation = Orientation.Horizontal };
            tbStack.Children.Add(CreateToolbarButton("🚀 RUN", (s, e) => RunActiveFile()));
            tbStack.Children.Add(CreateToolbarButton("🔍 ANALYZE", (s, e) => RunOfflineAnalysis()));
            tbStack.Children.Add(CreateToolbarButton("🔎 SCAN PROJECT", (s, e) => RunProjectScan()));
            tbStack.Children.Add(CreateToolbarButton("🧠 AI FIX", (s, e) => RunAiFix()));
            tbStack.Children.Add(CreateToolbarButton("💾 SAVE", (s, e) => SaveActiveFile()));
            Grid.SetColumn(tbStack, 1);
            grid.Children.Add(tbStack);
            toolbar.Child = grid; Grid.SetRow(toolbar, 0); layoutGrid.Children.Add(toolbar);

            var split = new Grid();
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _fileTreeView = new TreeView { Margin = new Thickness(5) }; BaseOverlay.StyleTreeView(_fileTreeView);
            _fileTreeView.SelectedItemChanged += (s, e) => { if (_fileTreeView.SelectedItem is TreeViewItem t && t.Tag is string p && File.Exists(p)) LoadFile(p); };
            Grid.SetColumn(_fileTreeView, 0); split.Children.Add(_fileTreeView);

            _editorTabControl = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(_editorTabControl); Grid.SetColumn(_editorTabControl, 1); split.Children.Add(_editorTabControl);
            Grid.SetRow(split, 1); layoutGrid.Children.Add(split);

            _errorPanel = new Border { Height = 150, Background = new SolidColorBrush(Color.FromArgb(30, 20, 10, 10)), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Brushes.DarkRed, Visibility = Visibility.Collapsed };
            _errorListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, Margin = new Thickness(10) };
            _errorListBox.MouseDoubleClick += (s, e) => { if (_errorListBox.SelectedItem is CodeError err) JumpToLine(err.Line); };
            _errorPanel.Child = _errorListBox; Grid.SetRow(_errorPanel, 2); layoutGrid.Children.Add(_errorPanel);

            this.UserContent = layoutGrid;
            this.PreviewKeyDown += (s, e) => { if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) SaveActiveFile(); };
        }

        public void LoadFile(string path) {
            if (!File.Exists(path)) return;
            var tab = new EditorTab { FilePath = path, OriginalText = File.ReadAllText(path) };
            var editor = new RichTextBox { Background = Brushes.Transparent, Foreground = Brushes.White, CaretBrush = Brushes.Cyan, Padding = new Thickness(15), FontFamily = new FontFamily("Consolas"), FontSize = 14 };
            editor.Document.PagePadding = new Thickness(0); EditorTab.SetText(editor, tab.OriginalText);
            tab.Editor = editor;
            tab.TabItem = new TabItem { Header = tab.FileName, Content = editor, Tag = tab };
            _openTabs.Add(tab); _editorTabControl.Items.Add(tab.TabItem); _editorTabControl.SelectedItem = tab.TabItem;
        }

        public void LoadWorkspace(string path) { if (Directory.Exists(path)) { _fileTreeView.Items.Clear(); _fileTreeView.Items.Add(new TreeViewItem { Header = Path.GetFileName(path), Tag = path }); } }
        private void SaveActiveFile() { if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab t) File.WriteAllText(t.FilePath, EditorTab.GetText(t.Editor)); }
        private void RunActiveFile() { /* logic */ }
        private void RunOfflineAnalysis() {
            if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab t) {
                var errs = EditorAnalysisManager.Analyze(EditorTab.GetText(t.Editor), Path.GetExtension(t.FilePath));
                _errorListBox.Items.Clear(); foreach (var e in errs) _errorListBox.Items.Add(e);
                _errorPanel.Visibility = errs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        private void RunProjectScan()
        {
            Task.Run(async () => {
                TextOverlay.Show("🔍 Deep Project Scan Started...", 3000);
                await CoreRegistry.ProjectContext.RunDeepAnalysisAsync((msg, p) => {
                    Application.Current.Dispatcher.Invoke(() => {
                        _statusLabel.Text = $"SCANNING: {p:F0}% - {msg}";
                    });
                });
                TextOverlay.Show("✅ Deep Map Built. AI now has full context.", 4000);
            });
        }

        private void RunAiFix() {
            if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab t) {
                var errs = EditorAnalysisManager.Analyze(EditorTab.GetText(t.Editor), Path.GetExtension(t.FilePath));
                string code = EditorTab.GetText(t.Editor);

                Task.Run(async () => {
                    string projectContext = await CoreRegistry.ProjectContext.GetProjectSummaryAsync();
                    string prompt = $"## PROJECT CONTEXT\n{projectContext}\n\n" +
                                   $"## TASK\nFix errors in this file: {Path.GetFileName(t.FilePath)}\n\n" +
                                   $"## OFFLINE ERRORS\n" + string.Join("\n", errs) + "\n\n" +
                                   $"## CODE\n{code}";

                    TextOverlay.Show("🧠 AI is analyzing with project context...", 3000);
                    var res = await CoreRegistry.Llm.AskAsync(prompt);
                    Application.Current.Dispatcher.Invoke(() => ChatOverlay.ShowChat());
                });
            }
        }
        private void JumpToLine(int line) { /* logic */ }
        private Button CreateToolbarButton(string c, RoutedEventHandler h) { var b = new Button { Content = c, Margin = new Thickness(0,0,10,0), Padding = new Thickness(10,5,10,5), Background = Brushes.Transparent, Foreground = Brushes.White, FontWeight = FontWeights.Bold }; b.Click += h; return b; }
    }
}
