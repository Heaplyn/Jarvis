// Developer: heaplyn
// Date: 2026-08-16
// Summary: High-Performance Glassmorphic AI Code Studio.
//          Features: Multi-tab workspace, Styled Project Explorer, Async Syntax Highlighting, and AI Analysis.

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
                    try {
                        if (_instance != null && _instance.IsLoaded)
                        {
                            _instance.WindowState = WindowState.Normal;
                            _instance.LoadFile(abs);
                            _instance.BringToFront();
                            return;
                        }

                        if (_instance != null) { try { _instance.Close(); } catch { } }
                        _instance = new TextEditorOverlay();
                        _instance.Show();
                        _instance.LoadFile(abs);
                        _instance.BringToFront();
                    } catch (Exception ex) {
                        DebugConsoleOverlay.Log("Editor-Crash", "Failed to open file: " + ex.Message);
                    }
                });
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Editor-Crash", "Pre-Open error: " + ex.Message);
            }
        }

        public static void OpenWorkspace(string folderPath)
        {
            try {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try {
                        if (_instance != null && _instance.IsLoaded)
                        {
                            _instance.WindowState = WindowState.Normal;
                            _instance.LoadWorkspace(folderPath);
                            _instance.BringToFront();
                            return;
                        }

                        if (_instance != null) { try { _instance.Close(); } catch { } }
                        _instance = new TextEditorOverlay();
                        _instance.Show();
                        _instance.LoadWorkspace(folderPath);
                        _instance.BringToFront();
                    } catch (Exception ex) {
                        DebugConsoleOverlay.Log("Editor-Crash", "Failed to open workspace: " + ex.Message);
                    }
                });
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Editor-Crash", "Pre-Workspace error: " + ex.Message);
            }
        }

        public static void PromptAndOpenFile()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Open File or Project", InitialDirectory = GetProjectRoot() };
                if (dlg.ShowDialog() == true) OpenFile(dlg.FileName);
            });
        }

        private TextEditorOverlay() : base("JARVIS AI CODE STUDIO", 1280, 800)
        {
            _instance = this;
            this.Closed += (s, e) => { _instance = null; };

            this.Topmost = false;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var layoutGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. TOOLBAR / APP OVERHEAD
            var toolbar = BuildToolbar();
            Grid.SetRow(toolbar, 0);
            layoutGrid.Children.Add(toolbar);

            // 2. MAIN WORKSPACE GRID
            var workspaceGrid = new Grid();
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            workspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Side Panel with slight glass tint
            var sidePanelBorder = new Border {
                Background = new SolidColorBrush(Color.FromArgb(25, 10, 10, 15)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            };

            var sideTabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(sideTabs);

            var explorerTab = new TabItem { Header = "📁 EXPLORER" };
            _fileTreeView = new TreeView { Margin = new Thickness(4) };
            BaseOverlay.StyleTreeView(_fileTreeView);
            _fileTreeView.SelectedItemChanged += (s, e) => { if (_fileTreeView.SelectedItem is TreeViewItem t && t.Tag is string p && File.Exists(p)) LoadFile(p); };
            explorerTab.Content = new ScrollViewer { Content = _fileTreeView, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            sideTabs.Items.Add(explorerTab);

            var outlineTab = new TabItem { Header = "🧭 OUTLINE" };
            var outlineStack = new StackPanel { Margin = new Thickness(10) };
            _outlineStatusLabel = new TextBlock { Text = "SELECT A C# FILE", FontSize = 10, Opacity = 0.8, Margin = new Thickness(0,0,0,10) };
            _outlineStatusLabel.Foreground = Brushes.White;
            outlineStack.Children.Add(_outlineStatusLabel);
            _outlineListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Cyan };
            _outlineListBox.SelectionChanged += (s, e) => JumpToOutlineItem();
            outlineStack.Children.Add(_outlineListBox);
            outlineTab.Content = new ScrollViewer { Content = outlineStack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            sideTabs.Items.Add(outlineTab);

            sidePanelBorder.Child = sideTabs;
            Grid.SetColumn(sidePanelBorder, 0);
            workspaceGrid.Children.Add(sidePanelBorder);

            // Editor TabControl
            _editorTabControl = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(_editorTabControl);
            _editorTabControl.SelectionChanged += (s, e) => UpdateStatusWithCursor();

            Grid.SetColumn(_editorTabControl, 2);
            workspaceGrid.Children.Add(_editorTabControl);

            Grid.SetRow(workspaceGrid, 1);
            layoutGrid.Children.Add(workspaceGrid);

            // 3. AUTOCOMPLETE POPUP
            _autocompleteListBox = new ListBox { MaxHeight = 250, Width = 300, Background = new SolidColorBrush(Color.FromArgb(240, 10, 10, 20)), BorderThickness = new Thickness(1) };
            _autocompleteListBox.SetResourceReference(ListBox.BorderBrushProperty, "AccentCaretBrush");
            _autocompletePopup = new Popup { Child = _autocompleteListBox, StaysOpen = false, AllowsTransparency = true };

            this.UserContent = layoutGrid;
            _statusLabel = (TextBlock)((Grid)((Border)toolbar).Child).Children[0];

            this.PreviewKeyDown += Window_PreviewKeyDown;

            // Optimization: Load workspace after UI is ready
            this.Loaded += (s, e) => { Task.Run(() => Application.Current.Dispatcher.Invoke(() => LoadWorkspace(GetProjectRoot()))); };
        }

        private Border BuildToolbar()
        {
            var bdr = new Border {
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), // Brighter toolbar
                Padding = new Thickness(15, 8, 15, 8),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold, Opacity = 1.0 };
            status.Foreground = Brushes.White;
            Grid.SetColumn(status, 0);
            grid.Children.Add(status);

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(CreateToolbarButton("🚀 RUN (F5)", (s, e) => RunActiveFile()));
            stack.Children.Add(CreateToolbarButton("🛠️ BUILD HUB", (s, e) => BuildStudioOverlay.ShowOverlay()));
            stack.Children.Add(CreateToolbarButton("🧠 AI AUDIT", (s, e) => RunAiAction("Analyze active workspace.")));
            stack.Children.Add(CreateToolbarButton("💾 SAVE (Ctrl+S)", (s, e) => SaveActiveFile()));

            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);
            bdr.Child = grid;
            return bdr;
        }

        public void LoadFile(string path)
        {
            try {
                if (!File.Exists(path)) return;

                // Safety: Binary file check
                if (IsBinaryFile(path)) {
                    TextOverlay.Show("⚠️ Cannot edit binary files.", 2000);
                    return;
                }

                // Safety: Large file check
                var info = new FileInfo(path);
                /*if (info.Length > 2 * 1024 * 1024) { // 2MB limit for now
                    TextOverlay.Show("⚠️ File too large for AI Studio (>2MB).", 2000);
                    return;
                }*/

                var existing = _openTabs.FirstOrDefault(t => t.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) { _editorTabControl.SelectedItem = existing.TabItem; return; }

                string content = File.ReadAllText(path);
                var tab = new EditorTab { FilePath = path, OriginalText = content };

                var editor = new RichTextBox
                {
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White,
                    CaretBrush = Brushes.Cyan, FontFamily = new FontFamily("Consolas"), FontSize = 14,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Padding = new Thickness(20), AcceptsTab = true
                };
                editor.Resources.Add(SystemParameters.VerticalScrollBarWidthKey, 8.0);

                // Manual Scroll Redirect to bypass potential template blocking
                editor.PreviewMouseWheel += (s, e) =>
                {
                    if (s is RichTextBox rtb)
                    {
                        var sv = FindVisualChild<ScrollViewer>(rtb);
                        if (sv != null) {
                            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 2.0);
                            e.Handled = true;
                        }
                        else {
                            rtb.ScrollToVerticalOffset(rtb.VerticalOffset - e.Delta / 3.0);
                            e.Handled = true;
                        }
                    }
                };
                editor.Document.PagePadding = new Thickness(0);

                // Load text BEFORE hooking TextChanged to avoid redundant first-pass highlight during setup
                EditorTab.SetText(editor, content);

                editor.TextChanged += (s, e) => {
                    if (editor.Tag as string == "Highlighting") return;
                    UpdateTabHeader(tab);
                    TriggerSyntaxHighlight(editor, Path.GetExtension(path));
                };
                editor.SelectionChanged += (s, e) => UpdateStatusWithCursor();
                editor.KeyUp += Editor_KeyUp;
                editor.PreviewKeyDown += Editor_PreviewKeyDown;
                tab.Editor = editor;

                var header = new StackPanel { Orientation = Orientation.Horizontal };
                var title = new TextBlock { Text = tab.FileName, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
                var close = new Button { Content = "×", Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.LightGray, FontSize = 14, Cursor = Cursors.Hand };
                close.Click += (s, e) => CloseTab(tab);
                header.Children.Add(title); header.Children.Add(close);

                tab.TabItem = new TabItem { Header = header, Content = editor, Tag = tab };
                _openTabs.Add(tab); _editorTabControl.Items.Add(tab.TabItem); _editorTabControl.SelectedItem = tab.TabItem;

                _ = LoadOutlineAsync(path);

                // Manually trigger the first highlight pass since TextChanged was hooked late
                TriggerSyntaxHighlight(editor, Path.GetExtension(path));
            } catch (Exception ex) {
                DebugConsoleOverlay.Log("Editor-Error", $"Failed to load {Path.GetFileName(path)}: {ex.Message}");
                TextOverlay.Show("❌ Load Failed: " + ex.Message, 3000);
            }
        }

        private bool IsBinaryFile(string path)
        {
            try {
                byte[] buffer = new byte[1024];
                using var stream = File.OpenRead(path);
                int read = stream.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < read; i++) {
                    if (buffer[i] == 0) return true;
                }
                return false;
            } catch { return false; }
        }

        public void LoadWorkspace(string path)
        {
            if (!Directory.Exists(path)) return;
            _currentWorkspacePath = path;
            _fileTreeView.Items.Clear();

            var rootItem = CreateLazyTreeItem(path);
            rootItem.IsExpanded = true;
            _fileTreeView.Items.Add(rootItem);
        }

        private TreeViewItem CreateLazyTreeItem(string path)
        {
            string fileName = Path.GetFileName(path);
            bool isFolder = Directory.Exists(path);
            string icon = isFolder ? "📁" : GetFileIcon(path);

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock { Text = icon, Margin = new Thickness(0, 0, 6, 0), FontSize = 12 });
            header.Children.Add(new TextBlock { Text = fileName, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White });

            var item = new TreeViewItem
            {
                Header = header,
                Tag = path,
                IsExpanded = false
            };

            if (isFolder)
            {
                item.Items.Add(new TreeViewItem { Header = "Loading..." });
                item.Expanded += Folder_Expanded;
            }

            return item;
        }

        private string GetFileIcon(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext switch
            {
                ".cs" => "☕",
                ".py" => "🐍",
                ".js" => "📜",
                ".ts" => "📘",
                ".rs" => "🦀",
                ".go" => "🐹",
                ".cpp" or ".c" or ".h" => "⚙️",
                ".asm" => "📟",
                ".xaml" or ".xml" => "🎨",
                ".json" => "📦",
                ".md" => "📝",
                ".exe" or ".dll" => "⚙️",
                ".png" or ".jpg" or ".jpeg" or ".gif" => "🖼️",
                _ => "📄"
            };
        }

        private void Folder_Expanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem placeholder && placeholder.Header.ToString() == "Loading...")
            {
                item.Items.Clear();
                string path = (string)item.Tag;

                try
                {
                    var dirs = Directory.GetDirectories(path)
                        .Where(d => !d.Contains(".git") && !d.Contains("bin") && !d.Contains("obj"))
                        .OrderBy(d => d);

                    foreach (var d in dirs)
                    {
                        item.Items.Add(CreateLazyTreeItem(d));
                    }

                    var files = Directory.GetFiles(path).OrderBy(f => f);
                    foreach (var f in files)
                    {
                        item.Items.Add(CreateLazyTreeItem(f));
                    }
                }
                catch (Exception ex)
                {
                    item.Items.Add(new TreeViewItem { Header = "Error: " + ex.Message, Foreground = Brushes.Red });
                }
            }
            e.Handled = true;
        }

        private void TriggerSyntaxHighlight(RichTextBox rtb, string ext)
        {
            if (_highlightCts.TryGetValue(rtb, out var cts)) cts.Cancel();
            var newCts = new CancellationTokenSource();
            _highlightCts[rtb] = newCts;

            // Debounce for stability and UI responsiveness
            Task.Delay(500, newCts.Token).ContinueWith(t => {
                if (!t.IsCanceled) {
                    Application.Current.Dispatcher.Invoke(() => {
                        if (rtb.Tag as string == "Highlighting") return;

                        rtb.Tag = "Highlighting";
                        rtb.BeginChange();
                        try {
                            SyntaxHighlighter.Highlight(rtb, ext);
                        } catch (Exception ex) {
                            DebugConsoleOverlay.Log("Highlight-Error", ex.Message);
                        } finally {
                            rtb.EndChange();
                            rtb.Tag = null;
                        }
                    });
                }
            }, TaskScheduler.Default);
        }

        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void SaveActiveFile()
        {
            if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)
            {
                try {
                    string text = EditorTab.GetText(tab.Editor);
                    File.WriteAllText(tab.FilePath, text);
                    tab.OriginalText = text;
                    UpdateTabHeader(tab);
                    TextOverlay.Show($"💾 Saved: {tab.FileName}", 1500);
                } catch (Exception ex) { TextOverlay.Show("❌ Error: " + ex.Message, 3000); }
            }
        }

        private void CloseTab(EditorTab tab)
        {
            if (tab.IsModified) { if (MessageBox.Show($"Save {tab.FileName}?", "Unsaved", MessageBoxButton.YesNo) == MessageBoxResult.Yes) SaveActiveFile(); }
            _openTabs.Remove(tab); _editorTabControl.Items.Remove(tab.TabItem);
        }

        private void RunActiveFile()
        {
            if (_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)
            {
                string ext = Path.GetExtension(tab.FilePath).ToLower();
                string cmd = ext switch {
                    ".py" => "python",
                    ".js" => "node",
                    ".cs" => "dotnet run",
                    ".asm" => "nasm",
                    ".rs" => "cargo run",
                    ".go" => "go run",
                    ".cpp" => "g++",
                    _ => ""
                };

                if (string.IsNullOrEmpty(cmd)) { TextOverlay.Show($"No run profile for {ext}", 2000); return; }

                string code = string.Empty;
                Application.Current.Dispatcher.Invoke(() => code = EditorTab.GetText(tab.Editor));

                Task.Run(() => {
                    var res = AgentExecutor.ExecutePowerShellDirect($"{cmd} \"{tab.FilePath}\"");
                    Application.Current.Dispatcher.Invoke(() => CliOutputOverlay.Show(tab.FileName, res));
                });
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { SaveActiveFile(); e.Handled = true; }
            if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control) { if (_editorTabControl.SelectedItem is TabItem ti) CloseTab((EditorTab)ti.Tag); e.Handled = true; }
            if (e.Key == Key.F5) { RunActiveFile(); e.Handled = true; }
        }

        private void Editor_KeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) _autocompletePopup.IsOpen = false; if ((e.Key >= Key.A && e.Key <= Key.Z) || e.Key == Key.OemPeriod) ShowAutocomplete(); }

        private void ShowAutocomplete()
        {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            var editor = tab.Editor;
            TextPointer caret = editor.CaretPosition;
            string line = caret.GetLineStartPosition(0)?.GetTextInRun(LogicalDirection.Forward) ?? "";
            string word = Regex.Match(line, @"\b\w*$").Value;
            var suggs = EditorIntelligenceManager.GetSuggestions(word, Path.GetExtension(tab.FilePath), EditorTab.GetText(editor));
            if (suggs.Any())
            {
                _autocompleteListBox.Items.Clear();
                foreach (var s in suggs) _autocompleteListBox.Items.Add(new ListBoxItem { Content = $"{s.Icon} {s.Text}", Tag = s.Text });
                _autocompletePopup.PlacementTarget = editor; _autocompletePopup.HorizontalOffset = caret.GetCharacterRect(LogicalDirection.Forward).Left;
                _autocompletePopup.VerticalOffset = caret.GetCharacterRect(LogicalDirection.Forward).Bottom + 5;
                _autocompletePopup.IsOpen = true; _autocompleteListBox.SelectedIndex = 0;
            } else _autocompletePopup.IsOpen = false;
        }

        private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_autocompletePopup.IsOpen) {
                if (e.Key == Key.Enter || e.Key == Key.Tab) { if (_autocompleteListBox.SelectedItem is ListBoxItem i) { InsertSuggestion(i.Tag.ToString()!); e.Handled = true; return; } }
                if (e.Key == Key.Down) { _autocompleteListBox.SelectedIndex = (_autocompleteListBox.SelectedIndex + 1) % _autocompleteListBox.Items.Count; e.Handled = true; return; }
                if (e.Key == Key.Up) { _autocompleteListBox.SelectedIndex = (_autocompleteListBox.SelectedIndex - 1 + _autocompleteListBox.Items.Count) % _autocompleteListBox.Items.Count; e.Handled = true; return; }
            }
        }

        private void InsertSuggestion(string text)
        {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            tab.Editor.BeginChange(); tab.Editor.CaretPosition.InsertTextInRun(text); tab.Editor.EndChange();
            _autocompletePopup.IsOpen = false;
        }

        private void UpdateStatusWithCursor()
        {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) { _statusLabel.Text = "IDLE"; return; }
            _statusLabel.Text = $"EDITING: {tab.FileName.ToUpper()}{(tab.IsModified ? " *" : "")}";
        }

        private void JumpToOutlineItem() { if (_outlineListBox.SelectedItem is ListBoxItem i && i.Tag is int l && _editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab) { var editor = tab.Editor; /* Jump logic */ } }

        private async Task LoadOutlineAsync(string path)
        {
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
                    _outlineStatusLabel.Text = "OUTLINE READY";
                });
            } catch { }
        }

        private void RunAiAction(string p)
        {
            if (!(_editorTabControl.SelectedItem is TabItem ti && ti.Tag is EditorTab tab)) return;
            string code = string.Empty;
            Application.Current.Dispatcher.Invoke(() => {
                code = EditorTab.GetText(tab.Editor);
                TextOverlay.Show("🧠 AI Analyzing Workspace...", 3000);
            });

            Task.Run(async () => {
                var res = await LlmRouter.AskAsync($"{p}\n[WORKSPACE: {_currentWorkspacePath}]\n[FILE: {tab.FilePath}]\n[CODE]:\n{code}");
                Application.Current.Dispatcher.Invoke(() => CliOutputOverlay.Show("AI Workspace Audit", res));
            });
        }

        private void HandleCloseCheck() { if (_openTabs.Any(t => t.IsModified)) { if (MessageBox.Show("Unsaved work! Exit?", "Warning", MessageBoxButton.YesNo) == MessageBoxResult.No) return; } FadeOutAndClose(); }
        private void UpdateTabHeader(EditorTab tab) { var tb = (TextBlock)((StackPanel)tab.TabItem.Header).Children[0]; tb.Text = tab.FileName + (tab.IsModified ? " *" : ""); tb.Foreground = Brushes.White; UpdateStatusWithCursor(); }
        private Button CreateToolbarButton(string c, RoutedEventHandler h) { var b = new Button { Content = c, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 4, 12, 4), Cursor = Cursors.Hand, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White }; b.SetResourceReference(Button.BorderBrushProperty, "WindowBorderBrush"); b.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush"); b.Click += h; return b; }
        private static string GetProjectRoot() { string d = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..")); return Directory.Exists(Path.Combine(d, "Modules")) ? d : AppDomain.CurrentDomain.BaseDirectory; }
    }
}
