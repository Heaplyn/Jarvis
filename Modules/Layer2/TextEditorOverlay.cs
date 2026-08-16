// Developer: heaplyn
// Date: 2026-08-16
// Summary: High-Performance Glassmorphic AI Code Studio.
//          Features: Multi-tab workspace, Refined Layout, Keybinds (Undo/Redo), and Debug Hub.

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

        public static string GetText(RichTextBox rtb)
        {
            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            return range.Text.Replace("\r\n", "\n").TrimEnd('\n');
        }

        public static void SetText(RichTextBox rtb, string text)
        {
            rtb.Document.Blocks.Clear();
            var p = new Paragraph(new Run(text)) { Margin = new Thickness(0) };
            rtb.Document.Blocks.Add(p);
        }
    }

    public class TextEditorOverlay : BaseOverlay
    {
        private static TextEditorOverlay? _instance;
        private string? _currentWorkspacePath;
        private readonly List<EditorTab> _openTabs = new();

        private readonly TabControl _editorTabControl;
        private readonly TextBlock _statusLabel;
        private readonly TreeView _fileTreeView;
        private readonly ListBox _outlineListBox;
        private readonly TextBlock _outlineStatusLabel;

        private readonly AsyncCSharpFileLoader _fileLoader = new();
        private CancellationTokenSource? _outlineLoadCts;
        private readonly Dictionary<RichTextBox, CancellationTokenSource> _highlightCts = new();

        private readonly Popup _autocompletePopup;
        private readonly ListBox _autocompleteListBox;

        public static void OpenFile(string filePath)
        {
            try {
                string abs = Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(Path.Combine(GetProjectRoot(), filePath));
                if (Directory.Exists(abs)) { OpenWorkspace(abs); return; }

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (_instance == null || !_instance.IsLoaded) _instance = new TextEditorOverlay();
                    _instance.Show();
                    _instance.LoadFile(abs);
                    _instance.BringToFront();
                });
            } catch { }
        }

        public static void OpenWorkspace(string folderPath)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_instance == null || !_instance.IsLoaded) _instance = new TextEditorOverlay();
                _instance.Show();
                _instance.LoadWorkspace(folderPath);
                _instance.BringToFront();
            });
        }

        public static void PromptAndOpenFile()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Open File", InitialDirectory = GetProjectRoot() };
                if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
            });
        }

        private TextEditorOverlay() : base("JARVIS AI CODE STUDIO", 1280, 800)
        {
            _instance = this;
            this.Closed += (s, e) => { _instance = null; };

            var layoutGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. TOOLBAR
            var toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 0);
            layoutGrid.Children.Add(toolbar);

            // 2. MAIN SPLIT VIEW
            var workspaceGrid = new Grid();
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Side Panel with glass tint
            var sidePanelBorder = new Border {
                Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            };

            var sideTabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(sideTabs);

            var explorerTab = new TabItem { Header = "EXPLORER" };
            _fileTreeView = new TreeView { Margin = new Thickness(5) };
            BaseOverlay.StyleTreeView(_fileTreeView);
            _fileTreeView.SelectedItemChanged += (s, e) => { if (_fileTreeView.SelectedItem is TreeViewItem t && t.Tag is string p && File.Exists(p)) LoadFile(p); };
            explorerTab.Content = _fileTreeView;
            sideTabs.Items.Add(explorerTab);

            var outlineTab = new TabItem { Header = "OUTLINE" };
            var outlineStack = new StackPanel { Margin = new Thickness(10) };
            _outlineStatusLabel = new TextBlock { Text = "IDLE", Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0,0,0,5) };
            _outlineListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Cyan };
            outlineStack.Children.Add(_outlineStatusLabel); outlineStack.Children.Add(_outlineListBox);
            outlineTab.Content = outlineStack;
            sideTabs.Items.Add(outlineTab);

            sidePanelBorder.Child = sideTabs;
            workspaceGrid.Children.Add(sidePanelBorder);

            // Editor TabControl
            _editorTabControl = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(_editorTabControl);
            Grid.SetColumn(_editorTabControl, 1);
            workspaceGrid.Children.Add(_editorTabControl);

            Grid.SetRow(workspaceGrid, 1);
            layoutGrid.Children.Add(workspaceGrid);

            // 3. AUTOCOMPLETE POPUP
            _autocompleteListBox = new ListBox { MaxHeight = 250, Width = 300, Background = new SolidColorBrush(Color.FromArgb(240, 10, 10, 20)), BorderThickness = new Thickness(1), Foreground = Brushes.White };
            _autocompletePopup = new Popup { Child = _autocompleteListBox, StaysOpen = false, AllowsTransparency = true };

            this.UserContent = layoutGrid;
            _statusLabel = (TextBlock)((Grid)toolbar.Child).Children[0];

            this.PreviewKeyDown += Window_PreviewKeyDown;
            this.Loaded += (s, e) => LoadWorkspace(GetProjectRoot());
        }

        private Border BuildToolbar()
        {
            var bdr = new Border {
                Background = new SolidColorBrush(Color.FromArgb(40, 128, 80, 230)),
                Padding = new Thickness(15, 8, 15, 8),
                BorderThickness = new Thickness(0,0,0,1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var status = new TextBlock { Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold };
            grid.Children.Add(status);

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(CreateToolbarButton("🚀 RUN", (s, e) => RunActiveFile()));
            stack.Children.Add(CreateToolbarButton("⚙️ DEBUG", (s, e) => DebugConsoleOverlay.ShowConsole()));
            stack.Children.Add(CreateToolbarButton("🧠 AUDIT", (s, e) => RunAiAction("Audit this file.")));
            stack.Children.Add(CreateToolbarButton("💾 SAVE", (s, e) => SaveActiveFile()));
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);
            bdr.Child = grid;
            return bdr;
        }

        public void LoadFile(string path)
        {
            try {
                if (!File.Exists(path) || IsBinaryFile(path)) return;
                var existing = _openTabs.FirstOrDefault(t => t.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { _editorTabControl.SelectedItem = existing.TabItem; return; }

                string content = File.ReadAllText(path);
                var tab = new EditorTab { FilePath = path, OriginalText = content };
                var editor = new RichTextBox {
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = Brushes.White, CaretBrush = Brushes.Cyan,
                    Padding = new Thickness(15), AcceptsTab = true,
                    FontFamily = new FontFamily("Consolas"), FontSize = 14
                };
                editor.Document.PagePadding = new Thickness(0);
                EditorTab.SetText(editor, content);

                editor.TextChanged += (s, e) => {
                    if (editor.Tag as string != "Highlighting") TriggerSyntaxHighlight(editor, Path.GetExtension(path));
                };
                editor.KeyUp += Editor_KeyUp;
                editor.PreviewKeyDown += Editor_PreviewKeyDown;
                tab.Editor = editor;

                var header = new StackPanel { Orientation = Orientation.Horizontal };
                header.Children.Add(new TextBlock { Text = tab.FileName, Foreground = Brushes.White, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
                var close = new Button { Content = "×", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Gray, FontSize = 16, Cursor = Cursors.Hand };
                close.Click += (s, e) => CloseTab(tab);
                header.Children.Add(close);

                tab.TabItem = new TabItem { Header = header, Content = editor, Tag = tab };
                _openTabs.Add(tab); _editorTabControl.Items.Add(tab.TabItem); _editorTabControl.SelectedItem = tab.TabItem;
                TriggerSyntaxHighlight(editor, Path.GetExtension(path));
                _ = LoadOutlineAsync(path);
            } catch { }
        }

        public void LoadWorkspace(string path)
        {
            if (!Directory.Exists(path)) return;
            _currentWorkspacePath = path;
            _fileTreeView.Items.Clear();
            _fileTreeView.Items.Add(CreateLazyTreeItem(path));
        }

        private TreeViewItem CreateLazyTreeItem(string path)
        {
            bool isFolder = Directory.Exists(path);
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = isFolder ? "📁" : GetFileIcon(path), Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            header.Children.Add(new TextBlock { Text = Path.GetFileName(path), Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var item = new TreeViewItem { Header = header, Tag = path };
            if (isFolder) { item.Items.Add("..."); item.Expanded += (s, e) => {
                if (item.Items.Count == 1 && item.Items[0] is string) {
                    item.Items.Clear();
                    foreach (var d in Directory.GetDirectories(path).OrderBy(x => x)) item.Items.Add(CreateLazyTreeItem(d));
                    foreach (var f in Directory.GetFiles(path).OrderBy(x => x)) item.Items.Add(CreateLazyTreeItem(f));
                }
            }; }
            return item;
        }

        private string GetFileIcon(string p) {
            string e = Path.GetExtension(p).ToLower();
            return e switch {
                ".cs" => "☕", ".py" => "🐍", ".js" => "📜", ".ts" => "📘",
                ".rs" => "🦀", ".go" => "🐹", ".asm" => "📟",
                ".xaml" or ".xml" => "🎨", ".json" => "📦",
                _ => "📄"
            };
        }

        private bool IsBinaryFile(string p) { try { byte[] b = new byte[1024]; using var s = File.OpenRead(p); int r = s.Read(b, 0, 1024); for (int i = 0; i < r; i++) if (b[i] == 0) return true; return false; } catch { return false; } }

        private void TriggerSyntaxHighlight(RichTextBox rtb, string ext)
        {
            if (_highlightCts.TryGetValue(rtb, out var cts)) cts.Cancel();
            var newCts = new CancellationTokenSource(); _highlightCts[rtb] = newCts;
            Task.Delay(800, newCts.Token).ContinueWith(t => {
                if (!t.IsCanceled) Application.Current.Dispatcher.Invoke(() => {
                    rtb.Tag = "Highlighting";
                    try { SyntaxHighlighter.Highlight(rtb, ext); } catch {}
                    rtb.Tag = null;
                });
            }, TaskScheduler.Default);
        }

        private void SaveActiveFile() { if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab) { File.WriteAllText(tab.FilePath, EditorTab.GetText(tab.Editor)); tab.OriginalText = EditorTab.GetText(tab.Editor); TextOverlay.Show("Saved", 1000); } }
        private void CloseTab(EditorTab t) { _openTabs.Remove(t); _editorTabControl.Items.Remove(t.TabItem); }

        private void RunActiveFile() {
            if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab) {
                string ext = Path.GetExtension(tab.FilePath).ToLower();
                string cmd = ext switch {
                    ".py" => "python", ".js" => "node", ".cs" => "dotnet run",
                    ".asm" => "nasm -f win64", ".rs" => "cargo run", ".go" => "go run",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(cmd)) {
                    Task.Run(() => {
                        var res = AgentExecutor.ExecutePowerShellDirect($"{cmd} \"{tab.FilePath}\"");
                        Application.Current.Dispatcher.Invoke(() => CliOutputOverlay.Show(tab.FileName, res));
                    });
                }
            }
        }

        private void RunAiAction(string p) {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            string code = EditorTab.GetText(tab.Editor);
            Task.Run(async () => {
                var res = await LlmRouter.AskAsync($"{p}\nFile: {tab.FilePath}\nCode:\n{code}");
                Application.Current.Dispatcher.Invoke(() => ChatOverlay.ShowChat());
            });
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                if (e.Key == Key.S) { SaveActiveFile(); e.Handled = true; }
                if (e.Key == Key.W) { if (_editorTabControl.SelectedItem is TabItem ti) CloseTab((EditorTab)ti.Tag); e.Handled = true; }
                // Undo/Redo are native but let's make sure they are handled
                if (e.Key == Key.Z) { if (_editorTabControl.SelectedItem is TabItem ti) ((RichTextBox)ti.Content).Undo(); e.Handled = true; }
                if (e.Key == Key.Y) { if (_editorTabControl.SelectedItem is TabItem ti) ((RichTextBox)ti.Content).Redo(); e.Handled = true; }
            }
            if (e.Key == Key.F5) { RunActiveFile(); e.Handled = true; }
        }

        private void Editor_KeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) _autocompletePopup.IsOpen = false; if ((e.Key >= Key.A && e.Key <= Key.Z) || e.Key == Key.OemPeriod) ShowAutocomplete(); }

        private void ShowAutocomplete() {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            var editor = tab.Editor;
            var caret = editor.CaretPosition;
            var lineStart = caret.GetLineStartPosition(0);
            if (lineStart == null) return;
            string line = new TextRange(lineStart, caret).Text;
            string word = Regex.Match(line, @"\b\w*$").Value;
            var suggestions = EditorIntelligenceManager.GetSuggestions(line, Path.GetExtension(tab.FilePath), EditorTab.GetText(editor));
            if (suggestions.Any()) {
                _autocompleteListBox.Items.Clear();
                foreach (var s in suggestions) _autocompleteListBox.Items.Add(new ListBoxItem { Content = $"{s.Icon} {s.Text}", Tag = s.Text });
                _autocompletePopup.PlacementTarget = editor;
                var rect = caret.GetCharacterRect(LogicalDirection.Forward);
                _autocompletePopup.HorizontalOffset = rect.Left;
                _autocompletePopup.VerticalOffset = rect.Bottom + 5;
                _autocompletePopup.IsOpen = true;
                _autocompleteListBox.SelectedIndex = 0;
            } else _autocompletePopup.IsOpen = false;
        }

        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (_autocompletePopup.IsOpen) {
                if (e.Key == Key.Enter || e.Key == Key.Tab) { if (_autocompleteListBox.SelectedItem is ListBoxItem i) { InsertSuggestion(i.Tag.ToString()!); e.Handled = true; } }
                if (e.Key == Key.Down) { _autocompleteListBox.SelectedIndex = (_autocompleteListBox.SelectedIndex + 1) % _autocompleteListBox.Items.Count; e.Handled = true; }
                if (e.Key == Key.Up) { _autocompleteListBox.SelectedIndex = (_autocompleteListBox.SelectedIndex - 1 + _autocompleteListBox.Items.Count) % _autocompleteListBox.Items.Count; e.Handled = true; }
            }
        }

        private void InsertSuggestion(string t) {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            var rtb = tab.Editor;
            rtb.BeginChange();
            var caret = rtb.CaretPosition;
            // Backtrack to start of word
            var wordMatch = Regex.Match(new TextRange(caret.GetLineStartPosition(0), caret).Text, @"\w*$");
            var start = caret.GetPositionAtOffset(-wordMatch.Value.Length);
            if (start != null) new TextRange(start, caret).Text = t;
            rtb.CaretPosition = rtb.CaretPosition.GetPositionAtOffset(0, LogicalDirection.Forward);
            rtb.EndChange();
            _autocompletePopup.IsOpen = false;
        }

        private async Task LoadOutlineAsync(string path) {
            if (!Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
            _outlineLoadCts?.Cancel(); _outlineLoadCts = new CancellationTokenSource();
            try {
                var o = await _fileLoader.LoadFileOutlineAsync(path, _outlineLoadCts.Token);
                Application.Current.Dispatcher.Invoke(() => {
                    _outlineListBox.Items.Clear();
                    foreach (var t in o.Types) {
                        _outlineListBox.Items.Add(new ListBoxItem { Content = "📦 " + t.Name, FontWeight = FontWeights.Bold, IsHitTestVisible = false, Foreground = Brushes.White });
                        foreach (var m in t.Methods) _outlineListBox.Items.Add(new ListBoxItem { Content = "  m: " + m.Name, Tag = m.LineNumber, Foreground = Brushes.Cyan });
                    }
                    _outlineStatusLabel.Text = "READY";
                });
            } catch { }
        }

        private Button CreateToolbarButton(string c, RoutedEventHandler h) { var b = new Button { Content = c, Margin = new Thickness(0,0,10,0), Padding = new Thickness(12,4,12,4), Background = Brushes.Transparent, Foreground = Brushes.White, Cursor = Cursors.Hand, FontWeight = FontWeights.Bold, FontSize = 11 }; b.Click += h; return b; }
        private static string GetProjectRoot() { return AppDomain.CurrentDomain.BaseDirectory; }
    }
}
