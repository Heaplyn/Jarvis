// Developer: heaplyn
// Date: 2026-08-19
// Summary: Advanced Hierarchical Note Manager Studio.
//          Features: Markdown Rendering, HTML Support, Multi-Mode Editor, and Autosave.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.Windows.Documents;

namespace JarvisLauncher
{
    public class NoteManagerOverlay : BaseOverlay
    {
        private static NoteManagerOverlay? _instance;
        private TreeView _treeView = null!;
        private TextBox _editor = null!;
        private RichTextBox _previewBox = null!;
        private TabControl _editorTabs = null!;
        private TextBlock _statusLabel = null!;
        private string? _activeNoteRelativePath;
        private DispatcherTimer _autosaveTimer;
        private bool _isChangingNote = false;

        public static void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() => {
                if (_instance == null || !_instance.IsLoaded) {
                    _instance = new NoteManagerOverlay();
                    _instance.Show();
                }
                _instance.Activate();
                _instance.BringToFront();
            });
        }

        public class NoteManagerState { public string? LastNotePath { get; set; } }

        public NoteManagerOverlay() : base("📓 JARVIS NOTES STUDIO", width: 950, height: 650)
        {
            var state = PersistentStateManager.LoadState<NoteManagerState>("Notes");
            _activeNoteRelativePath = state?.LastNotePath;

            this.Closed += (s, e) => {
                _instance = null;
                SaveActiveNote();
                PersistentStateManager.SaveState("Notes", new NoteManagerState { LastNotePath = _activeNoteRelativePath });
            };

            var mainGrid = new Grid { Margin = new Thickness(12) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // --- Sidebar ---
            var sidebar = new Grid();
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            toolbar.Children.Add(CreateSmallButton("📁+", "New Category", (s, e) => CreateNewCategory()));
            toolbar.Children.Add(CreateSmallButton("📄+", "New Note", (s, e) => CreateNewNote()));
            toolbar.Children.Add(CreateSmallButton("🔄", "Refresh", (s, e) => RefreshTree()));
            toolbar.Children.Add(CreateSmallButton("🗑️", "Delete Item", (s, e) => DeleteSelected()));
            sidebar.Children.Add(toolbar);

            _treeView = new TreeView { Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)), Foreground = Brushes.White, BorderThickness = new Thickness(0,0,1,0), BorderBrush = Brushes.DimGray };
            _treeView.SelectedItemChanged += (s, e) => { if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && !item.IS_FOLDER) LoadNote(item.RELATIVE_PATH); };
            Grid.SetRow(_treeView, 1); sidebar.Children.Add(_treeView);
            Grid.SetColumn(sidebar, 0); mainGrid.Children.Add(sidebar);

            // --- Editor Area ---
            var editorContainer = new Grid { Margin = new Thickness(15, 0, 0, 0) };
            editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Formatting Toolbar
            editorContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tabs
            editorContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status

            // Formatting Toolbar
            var fmtToolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
            fmtToolbar.Children.Add(CreateFormatButton("B", "Bold", "**", "**"));
            fmtToolbar.Children.Add(CreateFormatButton("I", "Italic", "*", "*"));
            fmtToolbar.Children.Add(CreateFormatButton("#", "Header", "# ", ""));
            fmtToolbar.Children.Add(CreateFormatButton("<>", "Code", "```\n", "\n```"));
            fmtToolbar.Children.Add(CreateFormatButton("🔗", "Link", "[", "](url)"));
            Grid.SetRow(fmtToolbar, 0); editorContainer.Children.Add(fmtToolbar);

            _editorTabs = new TabControl { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            BaseOverlay.StyleTabControl(_editorTabs);

            // Tab 1: Editor
            _editor = new TextBox { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = new SolidColorBrush(Color.FromArgb(40, 0,0,0)), Foreground = Brushes.White, CaretBrush = Brushes.Cyan, Padding = new Thickness(15), FontSize = 14, FontFamily = new FontFamily("Consolas"), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray };
            _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autosaveTimer.Tick += (s, e) => { _autosaveTimer.Stop(); SaveActiveNote(); };
            _editor.TextChanged += (s, e) => { if (!_isChangingNote) _autosaveTimer.Start(); };

            var editTab = new TabItem { Header = "✏️ SOURCE", Content = _editor };
            _editorTabs.Items.Add(editTab);

            // Tab 2: Preview
            _previewBox = new RichTextBox { IsReadOnly = true, Background = new SolidColorBrush(Color.FromArgb(30, 20, 20, 30)), Foreground = Brushes.White, BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Padding = new Thickness(20), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _previewBox.Document.PagePadding = new Thickness(0);
            var previewTab = new TabItem { Header = "👁️ PREVIEW", Content = _previewBox };
            _editorTabs.SelectionChanged += (s, e) => { if (_editorTabs.SelectedIndex == 1) RenderPreview(); };
            _editorTabs.Items.Add(previewTab);

            Grid.SetRow(_editorTabs, 1); editorContainer.Children.Add(_editorTabs);

            _statusLabel = new TextBlock { Text = "Standing by.", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(_statusLabel, 2); editorContainer.Children.Add(_statusLabel);

            Grid.SetColumn(editorContainer, 1); mainGrid.Children.Add(editorContainer);

            this.UserContent = mainGrid;
            RefreshTree();
            if (!string.IsNullOrEmpty(_activeNoteRelativePath)) LoadNote(_activeNoteRelativePath);
        }

        private void RenderPreview()
        {
            _previewBox.Document.Blocks.Clear();
            string text = _editor.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            // Simple Markdown Parser
            var blocks = Regex.Split(text, @"(\n{2,})");
            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block)) continue;
                var p = new Paragraph { Margin = new Thickness(0,0,0,10) };

                string line = block.Trim();
                if (line.StartsWith("# ")) {
                    p.Inlines.Add(new Run(line.Substring(2)) { FontSize = 22, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan });
                } else if (line.StartsWith("## ")) {
                    p.Inlines.Add(new Run(line.Substring(3)) { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.LightCyan });
                } else if (line.StartsWith("```") && line.EndsWith("```")) {
                    var codeBdr = new Border { Background = new SolidColorBrush(Color.FromRgb(15,15,15)), Padding = new Thickness(10), CornerRadius = new CornerRadius(4), Margin = new Thickness(0,5,0,5) };
                    codeBdr.Child = new TextBlock { Text = line.Trim('`').Trim(), FontFamily = new FontFamily("Consolas"), Foreground = Brushes.SpringGreen, FontSize = 12, TextWrapping = TextWrapping.Wrap };
                    _previewBox.Document.Blocks.Add(new BlockUIContainer(codeBdr));
                    continue;
                } else {
                    // Inline formatting
                    string pattern = @"(\*\*.*?\*\*|\*.*?\*|`.*?`)";
                    var parts = Regex.Split(line, pattern);
                    foreach (var part in parts) {
                        if (part.StartsWith("**") && part.EndsWith("**")) p.Inlines.Add(new Bold(new Run(part.Trim('*'))) { Foreground = Brushes.Cyan });
                        else if (part.StartsWith("*") && part.EndsWith("*")) p.Inlines.Add(new Italic(new Run(part.Trim('*'))));
                        else if (part.StartsWith("`") && part.EndsWith("`")) p.Inlines.Add(new Run(part.Trim('`')) { Background = Brushes.Black, FontFamily = new FontFamily("Consolas"), Foreground = Brushes.Lime });
                        else p.Inlines.Add(new Run(part));
                    }
                }
                _previewBox.Document.Blocks.Add(p);
            }
        }

        private void LoadNote(string relativePath)
        {
            _isChangingNote = true;
            _activeNoteRelativePath = relativePath;
            _editor.Text = NotesManager.LoadNote(relativePath);
            _isChangingNote = false;
            _statusLabel.Text = $"OPEN: {Path.GetFileName(relativePath)}";
            if (_editorTabs.SelectedIndex == 1) RenderPreview();
        }

        private void SaveActiveNote()
        {
            if (_activeNoteRelativePath != null) {
                NotesManager.SaveNote(_activeNoteRelativePath, _editor.Text);
                _statusLabel.Text = $"SAVED: {DateTime.Now:HH:mm:ss}";
            }
        }

        private void RefreshTree()
        {
            _treeView.Items.Clear();
            foreach (var item in NotesManager.GetHierarchy()) _treeView.Items.Add(CreateTreeViewItem(item));
        }

        private TreeViewItem CreateTreeViewItem(NoteItem item)
        {
            var tvi = new TreeViewItem { Header = (item.IS_FOLDER ? "📁 " : "📄 ") + item.NAME, Tag = item, Foreground = Brushes.White, FontSize = 13 };
            foreach (var child in item.CHILDREN) tvi.Items.Add(CreateTreeViewItem(child));
            return tvi;
        }

        private void CreateNewCategory() {
            string parent = (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && item.IS_FOLDER) ? item.RELATIVE_PATH : "";
            InputPromptOverlay.Show("Category Name:", (n) => { if (!string.IsNullOrEmpty(n)) { NotesManager.CreateCategory(parent, n); RefreshTree(); } });
        }

        private void CreateNewNote() {
            string parent = (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && item.IS_FOLDER) ? item.RELATIVE_PATH : "";
            InputPromptOverlay.Show("Note Title:", (n) => { if (!string.IsNullOrEmpty(n)) { string p = NotesManager.CreateNote(parent, n); RefreshTree(); LoadNote(Path.GetRelativePath(NotesManager.GetNotesDirectory(), p)); } });
        }

        private void DeleteSelected() {
            if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item) {
                if (MessageBox.Show($"Nuke '{item.NAME}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    if (_activeNoteRelativePath == item.RELATIVE_PATH) _activeNoteRelativePath = null;
                    NotesManager.DeleteItem(item.RELATIVE_PATH); RefreshTree(); _editor.Text = "";
                }
            }
        }

        private Button CreateSmallButton(string text, string tip, RoutedEventHandler onClick) {
            var b = new Button { Content = text, ToolTip = tip, Width = 32, Height = 32, Margin = new Thickness(0, 0, 4, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromArgb(40, 255,255,255)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            b.Click += onClick; return b;
        }

        private Button CreateFormatButton(string content, string tip, string prefix, string suffix) {
            var b = CreateSmallButton(content, tip, (s, e) => {
                var sel = _editor.SelectedText;
                _editor.SelectedText = prefix + sel + suffix;
                _editor.Focus();
            });
            return b;
        }
    }
}
