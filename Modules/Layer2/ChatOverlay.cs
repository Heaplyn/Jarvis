
// Developer: heaplyn
// Date: 2026-08-09
// Summary: Draggable, interactive AI chat companion panel with scrollable history and message input.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class ChatOverlay : BaseOverlay
    {
        private static ChatOverlay? _instance;
        private static readonly System.Text.StringBuilder _consoleLog = new System.Text.StringBuilder();

        private StackPanel _chatHistoryPanel;
        private ScrollViewer _scrollViewer;
        private TextBox _inputTextBox;
        private TextBlock _placeholderTextBlock;

        // Attachment controls
        private string? _attachedFilePath = null;
        private Border _attachedFileBadge;
        private TextBlock _attachedFileText;
        private Button _attachButton;

        // History controls
        private Border _historyContainer;
        private ListBox _historyListBox;

        // Visual Console controls
        private Border _consoleContainer;
        private TextBox _consoleTextBox;
        private Button _consoleToggleBtn;
        private bool _isConsoleExpanded = false;

        public static void ShowOverlay() => ShowChat();

        public static void ShowChat()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new ChatOverlay();
                    _instance.Closed += (s, e) => _instance = null;
                }

                // Use the robust base Show method which handles animations and visibility
                _instance.Show();
            });
        }

        public static void LogConsoleAction(string action, string details)
        {
            string logLine = $"[{DateTime.Now:HH:mm:ss}] {action.ToUpper()}\n{details}\n----------------------------------\n";
            lock (_consoleLog)
            {
                _consoleLog.AppendLine(logLine);
            }

            // Update UI thread-safely
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_instance != null && _instance._consoleTextBox != null)
                {
                    _instance._consoleTextBox.Text = _consoleLog.ToString();
                    _instance._consoleTextBox.ScrollToEnd();
                }
            }));
        }

        private ChatOverlay()
            : base("JARVIS AI COMPANION", width: 380, height: 500)
        {
            // Position on the top right area of the primary screen
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Width - this.Width - 20;
            this.Top = workArea.Top + 40;

            // 1. Root Grid for the inner Content
            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Row 0: Header Toolbar
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Row 1: History Drawer
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Row 2: Chat History
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Row 3: Divider
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Row 4: Console Drawer
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Row 5: Input Box Area

            // --- Header Toolbar (Row 0) ---
            var toolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var newChatBtn = new Button
            {
                Content = "✨ New Chat",
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            newChatBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            newChatBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            newChatBtn.Click += (s, e) => StartNewChatSession();
            Grid.SetColumn(newChatBtn, 1);
            toolbarGrid.Children.Add(newChatBtn);

            var historyBtn = new Button
            {
                Content = "📜 History",
                FontSize = 11,
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            historyBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            historyBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            historyBtn.Click += (s, e) => ToggleHistoryDrawer();
            Grid.SetColumn(historyBtn, 2);
            toolbarGrid.Children.Add(historyBtn);

            Grid.SetRow(toolbarGrid, 0);
            contentGrid.Children.Add(toolbarGrid);

            // --- Collapsible History Drawer (Row 1) ---
            _historyContainer = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(4),
                Visibility = Visibility.Collapsed
            };
            _historyContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            _historyContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            _historyListBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                MaxHeight = 120,
                ItemContainerStyle = (Style)Application.Current.FindResource("ResultItemStyle")
            };
            _historyListBox.SetResourceReference(ListBox.ForegroundProperty, "TextPrimaryBrush");
            _historyListBox.SelectionChanged += (s, e) =>
            {
                if (_historyListBox.SelectedItem is string logFile && !logFile.StartsWith("("))
                {
                    LoadPastChatLog(logFile);
                }
            };
            _historyContainer.Child = _historyListBox;
            Grid.SetRow(_historyContainer, 1);
            contentGrid.Children.Add(_historyContainer);

            // 2. Scrollable Chat History (Row 2)
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _chatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            _scrollViewer.Content = _chatHistoryPanel;
            Grid.SetRow(_scrollViewer, 2);
            contentGrid.Children.Add(_scrollViewer);

            // Add starting welcome message
            AddMessageBubble("Hello! I am your Jarvis AI Companion. How can I help you customize your system or files today?", isAi: true);

            // 3. Thin Divider (Row 3)
            var divider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 0, 0, 8)
            };
            divider.SetResourceReference(Border.BackgroundProperty, "WindowBorderBrush");
            Grid.SetRow(divider, 3);
            contentGrid.Children.Add(divider);

            // --- Collapsible Console Drawer (Row 4) ---
            _consoleContainer = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(6)
            };
            _consoleContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            _consoleContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var consoleLayoutGrid = new Grid();
            consoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            consoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Log content

            var consoleHeader = new Grid();
            consoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            consoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var consoleTitle = new TextBlock
            {
                Text = "⚡ Command Execution Console",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            consoleTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(consoleTitle, 0);
            consoleHeader.Children.Add(consoleTitle);

            _consoleToggleBtn = new Button
            {
                Content = "Show Console (»)",
                FontSize = 10,
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand
            };
            _consoleToggleBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _consoleToggleBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _consoleToggleBtn.Click += (s, e) => ToggleConsole();
            Grid.SetColumn(_consoleToggleBtn, 1);
            consoleHeader.Children.Add(_consoleToggleBtn);

            Grid.SetRow(consoleHeader, 0);
            consoleLayoutGrid.Children.Add(consoleHeader);

            _consoleTextBox = new TextBox
            {
                Height = 0, // Collapsed initially
                Visibility = Visibility.Collapsed,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 0),
                FocusVisualStyle = null
            };
            _consoleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _consoleTextBox.Text = _consoleLog.ToString();

            Grid.SetRow(_consoleTextBox, 1);
            consoleLayoutGrid.Children.Add(_consoleTextBox);

            _consoleContainer.Child = consoleLayoutGrid;
            Grid.SetRow(_consoleContainer, 4);
            contentGrid.Children.Add(_consoleContainer);

            // 4. Input Area Grid (Row 5)
            var inputContainerStack = new StackPanel();

            // Attachment Pill Badge
            _attachedFileBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };
            _attachedFileBadge.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");
            _attachedFileBadge.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            var badgeStack = new StackPanel { Orientation = Orientation.Horizontal };
            _attachedFileText = new TextBlock { FontSize = 11, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            _attachedFileText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            badgeStack.Children.Add(_attachedFileText);

            var removeAttachBtn = new Button
            {
                Content = " ✕ ",
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            removeAttachBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
            removeAttachBtn.Click += (s, e) => RemoveAttachment();
            badgeStack.Children.Add(removeAttachBtn);
            _attachedFileBadge.Child = badgeStack;
            inputContainerStack.Children.Add(_attachedFileBadge);

            // Input Row with Plus (+) Button and Textbox
            var inputRowGrid = new Grid();
            inputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _attachButton = new Button
            {
                Content = "➕",
                ToolTip = "Attach file to AI Chat (or drag & drop file here)",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Width = 34,
                Height = 36,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _attachButton.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _attachButton.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _attachButton.Click += (s, e) => AttachFileInteractive();
            Grid.SetColumn(_attachButton, 0);
            inputRowGrid.Children.Add(_attachButton);

            var textboxOverlayGrid = new Grid();
            _inputTextBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false, // Enter to send
                Padding = new Thickness(8, 6, 8, 6),
                MinHeight = 36,
                MaxHeight = 80,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FocusVisualStyle = null
            };
            _inputTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _inputTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            _inputTextBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");

            _placeholderTextBlock = new TextBlock
            {
                Text = "Ask Jarvis or drag file... (Press Enter)",
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                IsHitTestVisible = false,
                Margin = new Thickness(10, 8, 10, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            _placeholderTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPlaceholderBrush");

            _inputTextBox.TextChanged += (s, e) =>
            {
                _placeholderTextBlock.Visibility = string.IsNullOrEmpty(_inputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            };

            _inputTextBox.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    e.Handled = true;
                    string message = _inputTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(message) || _attachedFilePath != null)
                    {
                        _inputTextBox.Text = string.Empty;
                        await SendUserMessage(message);
                    }
                }
            };

            textboxOverlayGrid.Children.Add(_inputTextBox);
            textboxOverlayGrid.Children.Add(_placeholderTextBlock);
            Grid.SetColumn(textboxOverlayGrid, 1);
            inputRowGrid.Children.Add(textboxOverlayGrid);

            inputContainerStack.Children.Add(inputRowGrid);

            // Drag and Drop File & Folder Support
            contentGrid.AllowDrop = true;
            contentGrid.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
            };
            contentGrid.Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string path = files[0];
                    if (Directory.Exists(path))
                    {
                        AttachFolder(path);
                    }
                    else
                    {
                        AttachFile(path);
                    }
                    e.Handled = true;
                }
            };

            Grid.SetRow(inputContainerStack, 5);
            contentGrid.Children.Add(inputContainerStack);

            this.UserContent = contentGrid;
        }

        private void AttachFileInteractive()
        {
            var cm = new ContextMenu();

            var fileItem = new MenuItem { Header = "📄 Attach Single File..." };
            fileItem.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select File Attachment for Jarvis AI",
                    Filter = "All Files (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    AttachFile(dlg.FileName);
                }
            };
            cm.Items.Add(fileItem);

            var folderItem = new MenuItem { Header = "📁 Select Folder for Context..." };
            folderItem.Click += (s, e) =>
            {
                try
                {
                    var dlg = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Select Folder for AI Context"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        AttachFolder(dlg.FolderName);
                    }
                }
                catch
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select Any File Inside Desired Folder",
                        Filter = "All Files (*.*)|*.*"
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        string dir = Path.GetDirectoryName(dlg.FileName) ?? "";
                        if (!string.IsNullOrEmpty(dir)) AttachFolder(dir);
                    }
                }
            };
            cm.Items.Add(folderItem);

            cm.IsOpen = true;
        }

        private void AttachFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                _attachedFilePath = filePath;
                _isFolderContext = false;
                _attachedFileText.Text = $"📎 Attached: {Path.GetFileName(filePath)}";
                _attachedFileBadge.Visibility = Visibility.Visible;
            }
        }

        private void AttachFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                _attachedFilePath = folderPath;
                _isFolderContext = true;
                _attachedFileText.Text = $"📁 Folder Context: {Path.GetFileName(folderPath)}";
                _attachedFileBadge.Visibility = Visibility.Visible;
            }
        }

        private void RemoveAttachment()
        {
            _attachedFilePath = null;
            _isFolderContext = false;
            _attachedFileBadge.Visibility = Visibility.Collapsed;
        }

        private void ToggleConsole()
        {
            if (!_isConsoleExpanded)
            {
                _consoleTextBox.Height = 120;
                _consoleTextBox.Visibility = Visibility.Visible;
                _consoleToggleBtn.Content = "Hide Console («)";
                _isConsoleExpanded = true;
                _consoleTextBox.ScrollToEnd();
            }
            else
            {
                _consoleTextBox.Height = 0;
                _consoleTextBox.Visibility = Visibility.Collapsed;
                _consoleToggleBtn.Content = "Show Console (»)";
                _isConsoleExpanded = false;
            }
            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();
        }

        private void StartNewChatSession()
        {
            _chatHistoryPanel.Children.Clear();
            _conversationHistory.Clear();
            RemoveAttachment();
            if (_historyContainer != null) _historyContainer.Visibility = Visibility.Collapsed;
            AddMessageBubble("✨ New Chat Session started! How can I help you today?", isAi: true);
            TextOverlay.Show("✨ New Chat Session", 1500);
        }

        private void ToggleHistoryDrawer()
        {
            if (_historyContainer.Visibility == Visibility.Collapsed)
            {
                PopulateHistoryList();
                _historyContainer.Visibility = Visibility.Visible;
            }
            else
            {
                _historyContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void PopulateHistoryList()
        {
            _historyListBox.Items.Clear();
            string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations");
            if (Directory.Exists(dataDir))
            {
                var files = Directory.GetFiles(dataDir, "ChatLog_*.txt");
                Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

                foreach (var f in files)
                {
                    _historyListBox.Items.Add(Path.GetFileName(f));
                }
            }

            if (_historyListBox.Items.Count == 0)
            {
                _historyListBox.Items.Add("(No past conversation logs found)");
            }
        }

        private void LoadPastChatLog(string fileName)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations");
                string filePath = Path.Combine(dataDir, fileName);
                if (!File.Exists(filePath)) return;

                string text = File.ReadAllText(filePath);
                _chatHistoryPanel.Children.Clear();
                _conversationHistory.Clear();
                _historyContainer.Visibility = Visibility.Collapsed;

                AddMessageBubble($"📜 Loaded session log: {fileName}", isAi: true, isItalic: true);

                // Parse turns separated by "=========================================================================="
                string[] turns = text.Split("==========================================================================", StringSplitOptions.RemoveEmptyEntries);
                foreach (var turn in turns)
                {
                    string trimmed = turn.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    int userIdx = trimmed.IndexOf("USER: ");
                    int jarvisIdx = trimmed.IndexOf("JARVIS: ");

                    if (userIdx >= 0 && jarvisIdx > userIdx)
                    {
                        string userMsg = trimmed.Substring(userIdx + 6, jarvisIdx - (userIdx + 6)).Trim().TrimEnd('-', '\r', '\n');
                        string jarvisMsg = trimmed.Substring(jarvisIdx + 8).Trim();

                        if (!string.IsNullOrWhiteSpace(userMsg)) AddMessageBubble(userMsg, isAi: false);
                        if (!string.IsNullOrWhiteSpace(jarvisMsg)) AddMessageBubble(jarvisMsg, isAi: true);

                        _conversationHistory.Add(new ChatTurn { Role = "user", Text = userMsg });
                        _conversationHistory.Add(new ChatTurn { Role = "model", Text = jarvisMsg });
                    }
                }

                _scrollViewer.UpdateLayout();
                _scrollViewer.ScrollToBottom();
                TextOverlay.Show($"📜 Loaded history: {fileName}", 2000);
            }
            catch (Exception ex)
            {
                AddMessageBubble($"⚠️ Error loading history log: {ex.Message}", isAi: true);
            }
        }

        private bool _isFolderContext = false;
        private readonly List<ChatTurn> _conversationHistory = new List<ChatTurn>();

        public static async Task SubmitVoiceCommand(string message, bool showUi = false)
        {
            if (!SettingsManager.Current.IsJarvisEnabled || !SettingsManager.Current.IsVoiceModeActive)
            {
                string trimmed = message.Trim();
                var suggestions = CommandParser.GetSuggestions(trimmed);
                if (suggestions.Count > 0 && suggestions[0].Similarity >= 3.0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        CommandParser.ExecuteFirstSuggestion(trimmed);
                    });
                    return;
                }

                TextOverlay.Show("⚡ Voice AI is paused. Enable in Settings (⚙️).", 2000);
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (showUi) ShowChat();

                if (_instance == null)
                {
                    _instance = new ChatOverlay();
                    _instance.Opacity = 0;
                    _instance.Visibility = Visibility.Collapsed;
                }

                await _instance.SendUserMessage(message);
            });
        }

        private async Task SendUserMessage(string message)
        {
            string displayMessage = message;
            string apiMessage = message;

            if (_isFolderContext && !string.IsNullOrEmpty(_attachedFilePath) && Directory.Exists(_attachedFilePath))
            {
                string folderPath = _attachedFilePath;
                string folderName = Path.GetFileName(folderPath);

                displayMessage = string.IsNullOrEmpty(message)
                    ? $"📁 Folder Context: {folderName}"
                    : $"📁 Folder Context: {folderName}\n{message}";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[FOLDER_CONTEXT: {folderPath}]");
                sb.AppendLine($"Root Folder Name: {folderName}\n");

                try
                {
                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                    sb.AppendLine($"Structure ({allFiles.Length} total files):");
                    int count = 0;
                    foreach (var file in allFiles)
                    {
                        if (count++ > 50) { sb.AppendLine("... (truncated list)"); break; }
                        string relative = Path.GetRelativePath(folderPath, file);
                        sb.AppendLine($"- {relative}");
                    }
                    sb.AppendLine("\n--- FILE CONTENTS ---");

                    int readCount = 0;
                    foreach (var file in allFiles)
                    {
                        if (readCount >= 20) break;
                        string ext = Path.GetExtension(file).ToLower();
                        bool isCodeOrText = ext == ".txt" || ext == ".cs" || ext == ".lua" || ext == ".luau" ||
                                           ext == ".json" || ext == ".xml" || ext == ".md" || ext == ".py" ||
                                           ext == ".js" || ext == ".ts" || ext == ".html" || ext == ".css" || ext == ".bat" || ext == ".ps1";

                        if (isCodeOrText)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                if (fileInfo.Length < 100000)
                                {
                                    string relative = Path.GetRelativePath(folderPath, file);
                                    string text = File.ReadAllText(file);
                                    sb.AppendLine($"\nFILE: {relative}");
                                    sb.AppendLine("```");
                                    sb.AppendLine(text);
                                    sb.AppendLine("```");
                                    readCount++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error scanning folder: {ex.Message}");
                }

                apiMessage = $"{sb.ToString()}\n\n[USER INSTRUCTION]: {message}";
                RemoveAttachment();
            }
            else if (!string.IsNullOrEmpty(_attachedFilePath) && File.Exists(_attachedFilePath))
            {
                string filePath = _attachedFilePath;
                string fileName = Path.GetFileName(filePath);
                string fileExt = Path.GetExtension(filePath).ToLower();

                displayMessage = string.IsNullOrEmpty(message)
                    ? $"📎 Attached: {fileName}"
                    : $"📎 Attached: {fileName}\n{message}";

                bool isText = fileExt == ".txt" || fileExt == ".cs" || fileExt == ".lua" || fileExt == ".luau" ||
                             fileExt == ".json" || fileExt == ".xml" || fileExt == ".md" || fileExt == ".py" ||
                             fileExt == ".js" || fileExt == ".ts" || fileExt == ".html" || fileExt == ".css" || fileExt == ".bat" || fileExt == ".ps1";

                if (isText)
                {
                    try
                    {
                        string content = File.ReadAllText(filePath);
                        apiMessage = $"[ATTACHED_FILE: {filePath}]\n```\n{content}\n```\n\n{message}";
                    }
                    catch
                    {
                        apiMessage = $"[ATTACHED_FILE: {filePath}]\n\n{message}";
                    }
                }
                else
                {
                    apiMessage = $"[ATTACHED_FILE: {filePath}]\n\n{message}";
                }

                RemoveAttachment();
            }

            // 1. Add user bubble
            AddMessageBubble(displayMessage, isAi: false);

            // 2. Create AI response bubble with initial thinking text
            int turnNumber = (_conversationHistory.Count / 2) + 1;
            var (aiBorder, aiTextBox) = AddMessageBubbleWithControl("🧠 Thinking...", isAi: true, isItalic: true);

            // 3. Thinking animation via DispatcherTimer — stays entirely on the UI thread, no cross-thread issues
            string[] thinkingPhases = turnNumber > 1
                ? new[] {
                    $"🧠 [Turn {turnNumber}] Analyzing context...",
                    "🔍 Searching codebase & memory...",
                    "📡 Querying AI model...",
                    "📝 Synthesizing response...",
                    "✨ Finalizing output..."
                }
                : new[] {
                    "🧠 Analyzing your request...",
                    "🔍 Searching codebase & memory...",
                    "📡 Querying AI model...",
                    "📝 Synthesizing response...",
                    "✨ Finalizing output..."
                };
            int phaseIndex = 0;
            var thinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            thinkingTimer.Tick += (s, e) =>
            {
                aiTextBox.Text = thinkingPhases[phaseIndex % thinkingPhases.Length];
                phaseIndex++;
                _scrollViewer.ScrollToBottom();
            };
            thinkingTimer.Start();

            // 4. Run AI on background thread
            string finalResult = "";
            try
            {
                DebugConsoleOverlay.Log("AI", $"Sending prompt: {(message.Length > 50 ? message.Substring(0, 50) + "..." : message)}");
                var snapshot = new List<ChatTurn>(_conversationHistory);
                string aiResponse = await Task.Run(async () => await AiAPI.AskGemini(apiMessage, snapshot));
                finalResult = AgentExecutor.ProcessAIResponse(aiResponse);
                DebugConsoleOverlay.Log("AI", $"Received response ({finalResult.Length} chars)");
            }
            catch (Exception ex)
            {
                finalResult = $"⚠️ Error: {ex.Message}";
                LogConversationTurn(message, $"ERROR: {ex.Message}");
            }
            finally
            {
                thinkingTimer.Stop();
            }

            // 5. Write final result
            if (string.IsNullOrWhiteSpace(finalResult))
            {
                finalResult = "⚠️ No response text returned from AI.";
            }
            aiTextBox.Text = finalResult;
            aiTextBox.FontStyle = FontStyles.Normal;
            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();

            // Speak response as a concise, fast spoken summary
            TtsManager.Speak(finalResult, isShortSpeech: true);

            if (!finalResult.StartsWith("⚠️"))
            {
                // Chain conversation turns
                _conversationHistory.Add(new ChatTurn { Role = "user", Text = message });
                _conversationHistory.Add(new ChatTurn { Role = "model", Text = finalResult });

                if (_conversationHistory.Count > 200)
                {
                    _conversationHistory.RemoveRange(0, 2);
                }

                LogConversationTurn(message, finalResult);
            }
        }

        private static List<string> SplitIntoMultipleMessages(string rawText)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(rawText)) return list;

            // Split into up to 100 individual message bubbles based on double newlines, line breaks, or section headers
            string[] parts = rawText.Split(new[] { "\n\n", "\r\n\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var currentBlock = new System.Text.StringBuilder();

            foreach (var p in parts)
            {
                string trimmed = p.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Create a new separate message bubble for every distinct thought/line/header (up to 100 bubbles limit)
                if (list.Count < 100)
                {
                    if (currentBlock.Length > 0 && (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("#") || trimmed.StartsWith("```") || trimmed.StartsWith("[") || currentBlock.Length > 150))
                    {
                        list.Add(currentBlock.ToString().Trim());
                        currentBlock.Clear();
                    }

                    if (currentBlock.Length > 0) currentBlock.AppendLine();
                    currentBlock.Append(trimmed);
                }
                else
                {
                    currentBlock.AppendLine(trimmed);
                }
            }

            if (currentBlock.Length > 0)
            {
                list.Add(currentBlock.ToString().Trim());
            }

            return list.Count > 0 ? list : new List<string> { rawText };
        }

        private static void LogConversationTurn(string userMessage, string aiResponse)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations");
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                // Create a daily conversation log file (e.g. ChatLog_2026-08-09.txt)
                string fileName = $"ChatLog_{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(dataDir, fileName);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"==========================================================================");
                sb.AppendLine($"TIMESTAMP: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"USER: {userMessage}");
                sb.AppendLine($"--------------------------------------------------------------------------");
                sb.AppendLine($"JARVIS: {aiResponse}");
                sb.AppendLine($"==========================================================================");
                sb.AppendLine();

                File.AppendAllText(filePath, sb.ToString());
            }
            catch { }
        }

        private async Task UpdateThinkingStatus(TextBox targetTextBox, string statusText, int delayMs)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                targetTextBox.Text = statusText;
                _scrollViewer.UpdateLayout();
                _scrollViewer.ScrollToBottom();
            });
            await Task.Delay(delayMs);
        }

        private (Border Border, TextBox TextBox) AddMessageBubbleWithControl(string text, bool isAi, bool isItalic = false)
        {
            var bubbleBg = isAi 
                ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) 
                : new SolidColorBrush(Color.FromArgb(64, 128, 80, 230));

            var bubbleBorder = new Border
            {
                Background = bubbleBg,
                CornerRadius = isAi ? new CornerRadius(12, 12, 12, 0) : new CornerRadius(12, 12, 0, 12),
                Margin = isAi ? new Thickness(0, 4, 48, 4) : new Thickness(48, 4, 0, 4),
                HorizontalAlignment = isAi ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                Padding = new Thickness(10, 8, 10, 8),
                MaxWidth = 280
            };

            var containerStack = new StackPanel { Orientation = Orientation.Vertical };
            bubbleBorder.Child = containerStack;

            var textBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
                Padding = new Thickness(0),
                Cursor = Cursors.Arrow,
                Margin = new Thickness(0),
                FocusVisualStyle = null
            };
            textBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            containerStack.Children.Add(textBox);

            textBox.TextChanged += (s, e) =>
            {
                string currentText = textBox.Text;
                if (currentText.Contains("```"))
                {
                    textBox.Visibility = Visibility.Collapsed;

                    for (int i = containerStack.Children.Count - 1; i >= 0; i--)
                    {
                        if (containerStack.Children[i] != textBox)
                        {
                            containerStack.Children.RemoveAt(i);
                        }
                    }

                    var parts = ParseMessageParts(currentText);
                    foreach (var part in parts)
                    {
                        if (part.IsCode)
                        {
                            int lineCount = part.Content.Split('\n').Length;
                            if (lineCount < 4 && part.Content.Length < 100)
                            {
                                var codeTextBox = new TextBox
                                {
                                    Text = part.Content.Trim(),
                                    Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                                    BorderThickness = new Thickness(0),
                                    IsReadOnly = true,
                                    FontSize = 12,
                                    FontFamily = new FontFamily("Consolas"),
                                    TextWrapping = TextWrapping.Wrap,
                                    Padding = new Thickness(6),
                                    Margin = new Thickness(0, 4, 0, 4),
                                    Cursor = Cursors.Arrow,
                                    FocusVisualStyle = null
                                };
                                codeTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
                                containerStack.Children.Add(codeTextBox);
                            }
                            else
                            {
                                string langLabel = string.IsNullOrEmpty(part.Language) ? "Code Document" : $"{char.ToUpper(part.Language[0])}{part.Language.Substring(1)} Source";
                                string filename = ExtractFilename(part.Content, part.Language);
                                
                                var cardBorder = new Border
                                {
                                    Background = new SolidColorBrush(Color.FromArgb(30, 59, 130, 246)),
                                    BorderThickness = new Thickness(1),
                                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)),
                                    CornerRadius = new CornerRadius(6),
                                    Padding = new Thickness(8),
                                    Margin = new Thickness(0, 6, 0, 6),
                                    Cursor = Cursors.Hand
                                };

                                var cardGrid = new Grid();
                                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                                var iconBlock = new TextBlock
                                {
                                    Text = "📄",
                                    FontSize = 24,
                                    Margin = new Thickness(0, 0, 8, 0),
                                    VerticalAlignment = VerticalAlignment.Center
                                };
                                Grid.SetColumn(iconBlock, 0);
                                cardGrid.Children.Add(iconBlock);

                                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                                var titleBlock = new TextBlock
                                {
                                    Text = filename,
                                    FontSize = 12,
                                    FontWeight = FontWeights.Bold,
                                    TextTrimming = TextTrimming.CharacterEllipsis
                                };
                                titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                                textStack.Children.Add(titleBlock);

                                var detailsBlock = new TextBlock
                                {
                                    Text = $"{langLabel} ({lineCount} lines) • Click to Edit",
                                    FontSize = 10,
                                    Opacity = 0.8
                                };
                                detailsBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                                textStack.Children.Add(detailsBlock);

                                Grid.SetColumn(textStack, 1);
                                cardGrid.Children.Add(textStack);

                                cardBorder.Child = cardGrid;

                                cardBorder.PreviewMouseLeftButtonDown += (sender, args) =>
                                {
                                    try
                                    {
                                        string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Temp");
                                        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                                        string tempPath = Path.Combine(tempDir, filename);
                                        File.WriteAllText(tempPath, part.Content);

                                        TextEditorOverlay.OpenFile(tempPath);
                                        TextOverlay.Show($"Opened in Editor: {filename}", 2000);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show($"Failed to open code block: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                };

                                containerStack.Children.Add(cardBorder);
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(part.Content))
                            {
                                var normalBlock = new TextBlock
                                {
                                    Text = part.Content.Trim(),
                                    FontSize = 13,
                                    FontFamily = new FontFamily("Segoe UI"),
                                    TextWrapping = TextWrapping.Wrap,
                                    Margin = new Thickness(0, 2, 0, 2)
                                };
                                normalBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                                containerStack.Children.Add(normalBlock);
                            }
                        }
                    }
                }
                else
                {
                    textBox.Visibility = Visibility.Visible;
                    for (int i = containerStack.Children.Count - 1; i >= 0; i--)
                    {
                        if (containerStack.Children[i] != textBox)
                        {
                            containerStack.Children.RemoveAt(i);
                        }
                    }
                }
            };

            textBox.Text = text;
            _chatHistoryPanel.Children.Add(bubbleBorder);

            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();

            return (bubbleBorder, textBox);
        }

        private static List<MessagePart> ParseMessageParts(string text)
        {
            var parts = new List<MessagePart>();
            if (string.IsNullOrEmpty(text)) return parts;

            int idx = 0;
            while (idx < text.Length)
            {
                int startCodeIdx = text.IndexOf("```", idx);
                if (startCodeIdx == -1)
                {
                    parts.Add(new MessagePart { IsCode = false, Content = text.Substring(idx) });
                    break;
                }

                if (startCodeIdx > idx)
                {
                    parts.Add(new MessagePart { IsCode = false, Content = text.Substring(idx, startCodeIdx - idx) });
                }

                int endCodeIdx = text.IndexOf("```", startCodeIdx + 3);
                if (endCodeIdx == -1)
                {
                    parts.Add(new MessagePart { IsCode = true, Content = text.Substring(startCodeIdx + 3) });
                    break;
                }

                string codeBlock = text.Substring(startCodeIdx + 3, endCodeIdx - (startCodeIdx + 3));
                string lang = "";
                int firstNewLine = codeBlock.IndexOf('\n');
                if (firstNewLine != -1)
                {
                    lang = codeBlock.Substring(0, firstNewLine).Trim();
                    codeBlock = codeBlock.Substring(firstNewLine + 1);
                }

                parts.Add(new MessagePart { IsCode = true, Language = lang, Content = codeBlock });
                idx = endCodeIdx + 3;
            }

            return parts;
        }

        private static string ExtractFilename(string code, string lang)
        {
            using (var reader = new StringReader(code))
            {
                for (int i = 0; i < 3; i++)
                {
                    string? line = reader.ReadLine()?.Trim();
                    if (line == null) break;

                    string cleaned = line.TrimStart('/', '*', '#', '-', ' ', '<', '!');
                    cleaned = cleaned.Replace("filename:", "", StringComparison.OrdinalIgnoreCase);
                    cleaned = cleaned.Replace("file:", "", StringComparison.OrdinalIgnoreCase);
                    cleaned = cleaned.Trim();

                    if (cleaned.Contains('.') && cleaned.Length > 2 && cleaned.Length < 40 && !cleaned.Contains(' '))
                    {
                        return cleaned;
                    }
                }
            }

            string ext = "txt";
            if (lang.Equals("csharp", StringComparison.OrdinalIgnoreCase) || lang.Equals("cs", StringComparison.OrdinalIgnoreCase)) ext = "cs";
            else if (lang.Equals("lua", StringComparison.OrdinalIgnoreCase) || lang.Equals("luau", StringComparison.OrdinalIgnoreCase)) ext = "lua";
            else if (lang.Equals("json", StringComparison.OrdinalIgnoreCase)) ext = "json";
            else if (lang.Equals("xml", StringComparison.OrdinalIgnoreCase)) ext = "xml";
            else if (lang.Equals("md", StringComparison.OrdinalIgnoreCase) || lang.Equals("markdown", StringComparison.OrdinalIgnoreCase)) ext = "md";
            else if (lang.Equals("html", StringComparison.OrdinalIgnoreCase)) ext = "html";
            else if (lang.Equals("css", StringComparison.OrdinalIgnoreCase)) ext = "css";
            else if (lang.Equals("javascript", StringComparison.OrdinalIgnoreCase) || lang.Equals("js", StringComparison.OrdinalIgnoreCase)) ext = "js";
            else if (lang.Equals("typescript", StringComparison.OrdinalIgnoreCase) || lang.Equals("ts", StringComparison.OrdinalIgnoreCase)) ext = "ts";
            else if (lang.Equals("python", StringComparison.OrdinalIgnoreCase) || lang.Equals("py", StringComparison.OrdinalIgnoreCase)) ext = "py";
            else if (lang.Equals("powershell", StringComparison.OrdinalIgnoreCase) || lang.Equals("ps1", StringComparison.OrdinalIgnoreCase)) ext = "ps1";
            else if (lang.Equals("bash", StringComparison.OrdinalIgnoreCase) || lang.Equals("sh", StringComparison.OrdinalIgnoreCase)) ext = "sh";
            else if (lang.Equals("bat", StringComparison.OrdinalIgnoreCase) || lang.Equals("cmd", StringComparison.OrdinalIgnoreCase)) ext = "bat";

            return $"artifact_{Guid.NewGuid().ToString().Substring(0, 8)}.{ext}";
        }

        private Border AddMessageBubble(string text, bool isAi, bool isItalic = false)
        {
            var tuple = AddMessageBubbleWithControl(text, isAi, isItalic);
            return tuple.Border;
        }

        private class MessagePart
        {
            public bool IsCode { get; set; }
            public string Language { get; set; } = "";
            public string Content { get; set; } = "";
        }
    }
}
