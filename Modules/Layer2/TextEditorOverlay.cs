// Developer: heaplyn
// Date: 2026-08-09
// Summary: Retro glassmorphic text editor overlay supporting multiline text edits, status tracking, saving, and dynamic theme colors.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class TextEditorOverlay : BaseOverlay
    {
        private static TextEditorOverlay? _instance;

        private readonly string _filePath;
        private string _originalText;
        private readonly TextBox _editTextBox;
        private readonly TextBlock _statusLabel;

        public static void OpenFile(string filePath)
        {
            string projectRoot = GetProjectRoot();
            string absolutePath = Path.IsPathRooted(filePath) 
                ? filePath 
                : Path.GetFullPath(Path.Combine(projectRoot, filePath));

            Application.Current.Dispatcher.Invoke(() =>
            {
                // If editor is already open for this exact file, focus it
                if (_instance != null && _instance._filePath.Equals(absolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (_instance.WindowState == WindowState.Minimized)
                        _instance.WindowState = WindowState.Normal;

                    _instance.Activate();
                    _instance.Focus();
                    return;
                }

                // If open for a different file, prompt to save or close it
                if (_instance != null)
                {
                    _instance.FadeOutAndClose();
                }

                _instance = new TextEditorOverlay(absolutePath);
                _instance.Show();
                _instance.Activate();
                _instance.Focus();
            });
        }

        public static void PromptAndOpenFile()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Open File for Editing in Jarvis",
                    Filter = "Source Code (*.cs;*.xaml;*.js;*.ts;*.json;*.md;*.txt)|*.cs;*.xaml;*.js;*.ts;*.json;*.md;*.txt|All Files (*.*)|*.*",
                    InitialDirectory = GetProjectRoot()
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    OpenFile(openFileDialog.FileName);
                }
            });
        }

        private TextEditorOverlay(string filePath)
            : base("JARVIS TEXT EDITOR", width: 700, height: 480)
        {
            _filePath = filePath;
            this.Closed += (s, e) => { _instance = null; };

            // Load file content
            string fileContent = string.Empty;
            if (File.Exists(_filePath))
            {
                try
                {
                    fileContent = File.ReadAllText(_filePath);
                }
                catch (Exception ex)
                {
                    fileContent = $"[Error loading file: {ex.Message}]";
                }
            }
            _originalText = fileContent;

            // Main layout grid
            var layoutGrid = new Grid();
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layoutGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. Toolbar Wrapper Border (Row 0)
            var toolbarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(4)
            };

            var toolbarGrid = new Grid();
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // File path status label
            string displayPath = Path.GetFileName(_filePath);
            _statusLabel = new TextBlock
            {
                Text = $"Editing: {displayPath}",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            Grid.SetColumn(_statusLabel, 0);
            toolbarGrid.Children.Add(_statusLabel);

            // Save and Close buttons stack
            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(buttonStack, 1);

            var saveBtn = new Button
            {
                Content = "Save (Ctrl+S)",
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 2, 10, 2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11
            };
            saveBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            saveBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            saveBtn.Click += (s, e) => SaveFile();
            buttonStack.Children.Add(saveBtn);

            var closeBtn = new Button
            {
                Content = "Close",
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 2, 10, 2),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11
            };
            closeBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            closeBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            closeBtn.Click += (s, e) => HandleCloseCheck();
            buttonStack.Children.Add(closeBtn);

            toolbarGrid.Children.Add(buttonStack);
            
            toolbarBorder.Child = toolbarGrid;
            Grid.SetRow(toolbarBorder, 0);
            layoutGrid.Children.Add(toolbarBorder);

            // 2. Editor TextBox (Row 1)
            _editTextBox = new TextBox
            {
                Text = fileContent,
                AcceptsReturn = true,
                AcceptsTab = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(6)
            };
            _editTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _editTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            _editTextBox.TextChanged += (s, e) => EvaluateModifiedState();
            _editTextBox.PreviewKeyDown += TextBox_PreviewKeyDown;

            Grid.SetRow(_editTextBox, 1);
            layoutGrid.Children.Add(_editTextBox);

            this.UserContent = layoutGrid;

            // Hook Ctrl+S and Escape shortcuts at the window level
            this.PreviewKeyDown += Window_PreviewKeyDown;

            // Default focus to TextBox on load
            this.Loaded += (s, e) =>
            {
                _editTextBox.Focus();
                _editTextBox.CaretIndex = _editTextBox.Text.Length;
            };
        }

        private void EvaluateModifiedState()
        {
            string displayPath = Path.GetFileName(_filePath);
            bool isModified = _editTextBox.Text != _originalText;
            _statusLabel.Text = $"Editing: {displayPath}" + (isModified ? " *" : "");
        }

        private void SaveFile()
        {
            try
            {
                string textToSave = _editTextBox.Text;
                File.WriteAllText(_filePath, textToSave);
                _originalText = textToSave;
                EvaluateModifiedState();

                TextOverlay.Show("💾 File Saved Successfully!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Save failed: {ex.Message}", 3000);
            }
        }

        private void HandleCloseCheck()
        {
            if (_editTextBox.Text != _originalText)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveFile();
                    FadeOutAndClose();
                }
                else if (result == MessageBoxResult.No)
                {
                    FadeOutAndClose();
                }
            }
            else
            {
                FadeOutAndClose();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SaveFile();
                e.Handled = true;
            }
            else if (e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                HandleCloseCheck();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Escape only closes if the text box doesn't have focus or handles it differently
                // Actually, let's keep standard escape for closing the HUD but let the editor close with Ctrl+W to avoid accidental closures.
            }
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Intercept tab to insert spaces instead of shifting focus
            if (e.Key == Key.Tab)
            {
                int caret = _editTextBox.CaretIndex;
                _editTextBox.Text = _editTextBox.Text.Insert(caret, "    ");
                _editTextBox.CaretIndex = caret + 4;
                e.Handled = true;
            }
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, "Modules")))
            {
                return devPath;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
