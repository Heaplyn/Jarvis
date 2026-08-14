// Developer: heaplyn
// Date: 2026-08-09
// Summary: Retro glassmorphic text editor overlay supporting multiline text edits, status tracking, saving, and dynamic theme colors.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ListBox _outlineListBox;
        private readonly TextBlock _outlineStatusLabel;
        private readonly Border _outlineBorder;
        private readonly AsyncCSharpFileLoader _fileLoader = new();
        private CancellationTokenSource? _outlineLoadCts;

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
                    _instance.Show();
                    return;
                }

                // If open for a different file, prompt to save or close it
                if (_instance != null)
                {
                    _instance.FadeOutAndClose();
                }

                _instance = new TextEditorOverlay(absolutePath);
                _instance.Show();
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
            : base("JARVIS TEXT EDITOR", width: 900, height: 520)
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

            // 2. Outline / Editor Grid (Row 1)
            var editorGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            editorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Outline panel
            _outlineBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _outlineBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            var outlineStack = new StackPanel();

            _outlineStatusLabel = new TextBlock
            {
                Text = "Loading outline...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _outlineStatusLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            outlineStack.Children.Add(_outlineStatusLabel);

            _outlineListBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ItemContainerStyle = new Style(typeof(ListBoxItem), (Style)Application.Current.FindResource("ResultItemStyle"))
            };
            _outlineListBox.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            _outlineListBox.SelectionChanged += OutlineListBox_SelectionChanged;
            outlineStack.Children.Add(_outlineListBox);

            _outlineBorder.Child = outlineStack;
            Grid.SetColumn(_outlineBorder, 0);
            editorGrid.Children.Add(_outlineBorder);

            // Editor TextBox
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
            Grid.SetColumn(_editTextBox, 1);
            editorGrid.Children.Add(_editTextBox);

            Grid.SetRow(editorGrid, 1);
            layoutGrid.Children.Add(editorGrid);
            // Default focus to TextBox on load
            this.Loaded += async (s, e) =>
            {
                _editTextBox.Focus();
                _editTextBox.CaretIndex = _editTextBox.Text.Length;
                await LoadOutlineAsync();
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

        private async Task LoadOutlineAsync()
        {
            if (!Path.GetExtension(_filePath).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                _outlineStatusLabel.Text = "Outline not available for this file type.";
                return;
            }

            _outlineLoadCts?.Cancel();
            _outlineLoadCts = new CancellationTokenSource();
            var token = _outlineLoadCts.Token;

            _outlineStatusLabel.Text = "Loading C# outline...";
            _outlineListBox.Items.Clear();

            try
            {
                var outline = await _fileLoader.LoadFileOutlineAsync(_filePath, token).ConfigureAwait(false);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _outlineStatusLabel.Text = outline.Types.Count == 0 ? "No types found in file." : "Click a method to jump to its line.";
                    foreach (var type in outline.Types)
                    {
                        _outlineListBox.Items.Add(new ListBoxItem
                        {
                            Content = $"{type.Kind} {type.Name}",
                            FontWeight = FontWeights.Bold,
                            IsHitTestVisible = false
                        });

                        foreach (var method in type.Methods)
                        {
                            _outlineListBox.Items.Add(new ListBoxItem
                            {
                                Content = $"  {method.ReturnType} {method.Name}({string.Join(", ", method.Parameters.Select(p => p.Type + " " + p.Name))})",
                                Tag = method.LineNumber,
                                Padding = new Thickness(8, 2, 4, 2)
                            });
                        }
                    }
                }).Task;
            }
            catch (OperationCanceledException)
            {
                _outlineStatusLabel.Text = "Outline loading canceled.";
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _outlineStatusLabel.Text = $"Outline failed: {ex.Message}";
                }).Task;
            }
        }

        private void OutlineListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_outlineListBox.SelectedItem is ListBoxItem item && item.Tag is int lineNumber)
            {
                int offset = _editTextBox.GetCharacterIndexFromLineIndex(lineNumber - 1);
                _editTextBox.Focus();
                _editTextBox.CaretIndex = offset;
                _editTextBox.ScrollToLine(lineNumber - 1);
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
