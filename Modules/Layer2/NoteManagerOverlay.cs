// Developer: heaplyn
// Date: 2026-08-14
// Summary: Advanced Hierarchical Note Manager Studio. Features category/subcategory TreeView, Markdown-style editor, and autosave.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class NoteManagerOverlay : BaseOverlay
    {
        private static NoteManagerOverlay? _instance;
        private TreeView _treeView = null!;
        private TextBox _editor = null!;
        private TextBlock _statusLabel = null!;
        private string? _activeNoteRelativePath;
        private DispatcherTimer _autosaveTimer;
        private bool _isChangingNote = false;

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new NoteManagerOverlay();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        public NoteManagerOverlay()
            : base("📓 JARVIS NOTES STUDIO", width: 850, height: 600)
        {
            this.Closed += (s, e) => { _instance = null; SaveActiveNote(); };

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // --- Sidebar (Col 0) ---
            var sidebarGrid = new Grid();
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Tree

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            var addCatBtn = CreateSmallButton("📁+", "New Category", (s, e) => CreateNewCategory());
            var addNoteBtn = CreateSmallButton("📄+", "New Note", (s, e) => CreateNewNote());
            var refreshBtn = CreateSmallButton("🔄", "Refresh", (s, e) => RefreshTree());
            var deleteBtn = CreateSmallButton("🗑️", "Delete Item", (s, e) => DeleteSelected());

            toolbar.Children.Add(addCatBtn);
            toolbar.Children.Add(addNoteBtn);
            toolbar.Children.Add(refreshBtn);
            toolbar.Children.Add(deleteBtn);
            sidebarGrid.Children.Add(toolbar);

            _treeView = new TreeView
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                Margin = new Thickness(0)
            };
            // Style the TreeViewItems to be dark/glassy
            _treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
            Grid.SetRow(_treeView, 1);
            sidebarGrid.Children.Add(_treeView);

            Grid.SetColumn(sidebarGrid, 0);
            mainGrid.Children.Add(sidebarGrid);

            // --- Editor Area (Col 1) ---
            var editorGrid = new Grid { Margin = new Thickness(15, 0, 0, 0) };
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            editorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status bar

            _editor = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10),
                FontSize = 14,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = Brushes.White,
                CaretBrush = Brushes.Cyan
            };
            _editor.TextChanged += (s, e) => { if (!_isChangingNote) _autosaveTimer.Start(); };
            Grid.SetRow(_editor, 0);
            editorGrid.Children.Add(_editor);

            _statusLabel = new TextBlock
            {
                Text = "Ready.",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 5, 0, 0)
            };
            Grid.SetRow(_statusLabel, 1);
            editorGrid.Children.Add(_statusLabel);

            Grid.SetColumn(editorGrid, 1);
            mainGrid.Children.Add(editorGrid);

            this.UserContent = mainGrid;

            _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autosaveTimer.Tick += (s, e) => { _autosaveTimer.Stop(); SaveActiveNote(); };

            RefreshTree();
        }

        private void RefreshTree()
        {
            _treeView.Items.Clear();
            var hierarchy = NotesManager.GetHierarchy();
            foreach (var item in hierarchy)
            {
                _treeView.Items.Add(CreateTreeViewItem(item));
            }
        }

        private TreeViewItem CreateTreeViewItem(NoteItem item)
        {
            var tvi = new TreeViewItem
            {
                Header = (item.IsFolder ? "📁 " : "📄 ") + item.Name,
                Tag = item,
                IsExpanded = false,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 2, 0, 2)
            };

            foreach (var child in item.Children)
            {
                tvi.Items.Add(CreateTreeViewItem(child));
            }

            return tvi;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && !item.IsFolder)
            {
                LoadNote(item.RelativePath);
            }
        }

        private void LoadNote(string relativePath)
        {
            SaveActiveNote();
            _isChangingNote = true;
            _activeNoteRelativePath = relativePath;
            _editor.Text = NotesManager.LoadNote(relativePath);
            _isChangingNote = false;
            _statusLabel.Text = $"Editing: {Path.GetFileName(relativePath)}";
        }

        private void SaveActiveNote()
        {
            if (_activeNoteRelativePath != null)
            {
                NotesManager.SaveNote(_activeNoteRelativePath, _editor.Text);
                _statusLabel.Text = $"Last saved: {DateTime.Now:HH:mm:ss}";
            }
        }

        private void CreateNewCategory()
        {
            string parent = "";
            if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && item.IsFolder)
            {
                parent = item.RelativePath;
            }

            InputPromptOverlay.Show("New Category Name:", (name) =>
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                NotesManager.CreateCategory(parent, name);
                RefreshTree();
            });
        }

        private void CreateNewNote()
        {
            string parent = "";
            if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item && item.IsFolder)
            {
                parent = item.RelativePath;
            }

            InputPromptOverlay.Show("New Note Title:", (name) =>
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                string path = NotesManager.CreateNote(parent, name);
                RefreshTree();
                LoadNote(Path.GetRelativePath(NotesManager.GetNotesDirectory(), path));
            });
        }

        private void DeleteSelected()
        {
            if (_treeView.SelectedItem is TreeViewItem tvi && tvi.Tag is NoteItem item)
            {
                var res = MessageBox.Show($"Delete '{item.Name}' permanently?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    if (_activeNoteRelativePath == item.RelativePath) _activeNoteRelativePath = null;
                    NotesManager.DeleteItem(item.RelativePath);
                    RefreshTree();
                    _editor.Text = "";
                    _statusLabel.Text = "Item deleted.";
                }
            }
        }

        private Button CreateSmallButton(string text, string toolTip, RoutedEventHandler onClick)
        {
            var b = new Button
            {
                Content = text,
                ToolTip = toolTip,
                Width = 40,
                Height = 30,
                Margin = new Thickness(0, 0, 5, 0),
                FontSize = 14,
                Cursor = Cursors.Hand
            };
            b.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            b.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            b.Click += onClick;
            return b;
        }
    }
}
