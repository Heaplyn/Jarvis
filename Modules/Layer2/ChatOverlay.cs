
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
using System.Linq;

namespace JarvisLauncher
{
    public class ChatOverlay : BaseOverlay
    {
        private static ChatOverlay? _instance;
        private static readonly System.Text.StringBuilder _consoleLog = new System.Text.StringBuilder();

        private StackPanel _chatHistoryPanel = null!;
        private ScrollViewer _scrollViewer = null!;
        private TextBox _inputTextBox = null!;
        private TextBlock _placeholderTextBlock = null!;

        // Attachment controls
        private string? _attachedFilePath = null;
        private Border _attachedFileBadge = null!;
        private TextBlock _attachedFileText = null!;
        private Button _attachButton = null!;

        // History controls
        private Border _historyContainer = null!;
        private ListBox _historyListBox = null!;

        // Visual Console controls
        private Border _consoleContainer = null!;
        private TextBox _consoleTextBox = null!;
        private Button _consoleToggleBtn = null!;
        private bool _isConsoleExpanded = false;

        public new static bool IsVisible => _instance != null && _instance.Visibility == Visibility.Visible && _instance.Opacity > 0.1;

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
            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Width - this.Width - 20;
            this.Top = workArea.Top + 40;

            var contentGrid = new Grid();
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var toolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var newChatBtn = new Button { Content = "✨ New Chat", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand };
            newChatBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            newChatBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            newChatBtn.Click += (s, e) => StartNewChatSession();
            Grid.SetColumn(newChatBtn, 1);
            toolbarGrid.Children.Add(newChatBtn);

            var historyBtn = new Button { Content = "📜 History", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Cursor = Cursors.Hand };
            historyBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            historyBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            historyBtn.Click += (s, e) => ToggleHistoryDrawer();
            Grid.SetColumn(historyBtn, 2);
            toolbarGrid.Children.Add(historyBtn);

            Grid.SetRow(toolbarGrid, 0);
            contentGrid.Children.Add(toolbarGrid);

            _historyContainer = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(4), Visibility = Visibility.Collapsed };
            _historyContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            _historyContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            _historyListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 11, MaxHeight = 120 };
            _historyListBox.SetResourceReference(ListBox.ForegroundProperty, "TextPrimaryBrush");
            _historyListBox.SelectionChanged += (s, e) => { if (_historyListBox.SelectedItem is string logFile && !logFile.StartsWith("(")) LoadPastChatLog(logFile); };
            _historyContainer.Child = _historyListBox;
            Grid.SetRow(_historyContainer, 1);
            contentGrid.Children.Add(_historyContainer);

            _scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 8) };
            _chatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            _scrollViewer.Content = _chatHistoryPanel;
            Grid.SetRow(_scrollViewer, 2);
            contentGrid.Children.Add(_scrollViewer);

            AddMessageBubble("Hello! I am your Jarvis AI Companion. How can I help you today?", isAi: true);

            var divider = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) };
            divider.SetResourceReference(Border.BackgroundProperty, "WindowBorderBrush");
            Grid.SetRow(divider, 3);
            contentGrid.Children.Add(divider);

            _consoleContainer = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(6) };
            _consoleContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            _consoleContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var consoleLayoutGrid = new Grid();
            consoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            consoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var consoleHeader = new Grid();
            consoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            consoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var consoleTitle = new TextBlock { Text = "⚡ Command Execution Console", FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            consoleTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(consoleTitle, 0);
            consoleHeader.Children.Add(consoleTitle);

            _consoleToggleBtn = new Button { Content = "Show Console (»)", FontSize = 10, Padding = new Thickness(6, 2, 6, 2), Cursor = Cursors.Hand };
            _consoleToggleBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _consoleToggleBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _consoleToggleBtn.Click += (s, e) => ToggleConsole();
            Grid.SetColumn(_consoleToggleBtn, 1);
            consoleHeader.Children.Add(_consoleToggleBtn);

            Grid.SetRow(consoleHeader, 0);
            consoleLayoutGrid.Children.Add(consoleHeader);

            _consoleTextBox = new TextBox { Height = 0, Visibility = Visibility.Collapsed, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 11, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 0, 0), FocusVisualStyle = null };
            _consoleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _consoleTextBox.Text = _consoleLog.ToString();

            Grid.SetRow(_consoleTextBox, 1);
            consoleLayoutGrid.Children.Add(_consoleTextBox);

            _consoleContainer.Child = consoleLayoutGrid;
            Grid.SetRow(_consoleContainer, 4);
            contentGrid.Children.Add(_consoleContainer);

            var inputContainerStack = new StackPanel();
            _attachedFileBadge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
            _attachedFileBadge.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");
            _attachedFileBadge.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            var badgeStack = new StackPanel { Orientation = Orientation.Horizontal };
            _attachedFileText = new TextBlock { FontSize = 11, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            _attachedFileText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            badgeStack.Children.Add(_attachedFileText);

            var removeAttachBtn = new Button { Content = " ✕ ", FontSize = 10, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            removeAttachBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
            removeAttachBtn.Click += (s, e) => RemoveAttachment();
            badgeStack.Children.Add(removeAttachBtn);
            _attachedFileBadge.Child = badgeStack;
            inputContainerStack.Children.Add(_attachedFileBadge);

            var inputRowGrid = new Grid();
            inputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _attachButton = new Button { Content = "➕", ToolTip = "Attach file (or drag & drop here)", FontSize = 14, FontWeight = FontWeights.Bold, Width = 34, Height = 36, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Stretch };
            _attachButton.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            _attachButton.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _attachButton.Click += (s, e) => AttachFileInteractive();
            Grid.SetColumn(_attachButton, 0);
            inputRowGrid.Children.Add(_attachButton);

            var textboxOverlayGrid = new Grid();
            _inputTextBox = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(1), FontSize = 13, FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Padding = new Thickness(8, 6, 8, 6), MinHeight = 36, MaxHeight = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FocusVisualStyle = null };
            _inputTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _inputTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            _inputTextBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");

            _placeholderTextBlock = new TextBlock { Text = "Ask Jarvis... (Press Enter)", FontSize = 13, FontFamily = new FontFamily("Segoe UI"), IsHitTestVisible = false, Margin = new Thickness(10, 8, 10, 8), VerticalAlignment = VerticalAlignment.Center };
            _placeholderTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPlaceholderBrush");

            _inputTextBox.TextChanged += (s, e) => { _placeholderTextBlock.Visibility = string.IsNullOrEmpty(_inputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed; };
            _inputTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
                {
                    e.Handled = true;
                    string m = _inputTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(m) || _attachedFilePath != null)
                    {
                        _inputTextBox.Text = "";
                        // Use static entry point to benefit from deadlock protection logic
                        _ = SubmitTextMessage(m);
                    }
                }
            };

            textboxOverlayGrid.Children.Add(_inputTextBox);
            textboxOverlayGrid.Children.Add(_placeholderTextBlock);
            Grid.SetColumn(textboxOverlayGrid, 1);
            inputRowGrid.Children.Add(textboxOverlayGrid);
            inputContainerStack.Children.Add(inputRowGrid);

            contentGrid.AllowDrop = true;
            contentGrid.Drop += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) { string path = files[0]; if (Directory.Exists(path)) AttachFolder(path); else AttachFile(path); e.Handled = true; } };

            Grid.SetRow(inputContainerStack, 5);
            contentGrid.Children.Add(inputContainerStack);
            this.UserContent = contentGrid;
        }

        private void AttachFileInteractive()
        {
            var cm = new ContextMenu();
            var fileItem = new MenuItem { Header = "📄 Attach Single File..." };
            fileItem.Click += (s, e) => { var dlg = new Microsoft.Win32.OpenFileDialog(); if (dlg.ShowDialog() == true) AttachFile(dlg.FileName); };
            cm.Items.Add(fileItem);
            var folderItem = new MenuItem { Header = "📁 Select Folder..." };
            folderItem.Click += (s, e) => { var dlg = new Microsoft.Win32.OpenFolderDialog(); if (dlg.ShowDialog() == true) AttachFolder(dlg.FolderName); };
            cm.Items.Add(folderItem);
            cm.IsOpen = true;
        }

        private void AttachFile(string path) { if (File.Exists(path)) { _attachedFilePath = path; _isFolderContext = false; _attachedFileText.Text = "📎 " + Path.GetFileName(path); _attachedFileBadge.Visibility = Visibility.Visible; } }
        private void AttachFolder(string path) { if (Directory.Exists(path)) { _attachedFilePath = path; _isFolderContext = true; _attachedFileText.Text = "📁 " + Path.GetFileName(path); _attachedFileBadge.Visibility = Visibility.Visible; } }
        private void RemoveAttachment() { _attachedFilePath = null; _attachedFileBadge.Visibility = Visibility.Collapsed; }
        private void ToggleConsole() { _isConsoleExpanded = !_isConsoleExpanded; _consoleTextBox.Height = _isConsoleExpanded ? 120 : 0; _consoleTextBox.Visibility = _isConsoleExpanded ? Visibility.Visible : Visibility.Collapsed; _consoleToggleBtn.Content = _isConsoleExpanded ? "Hide Console («)" : "Show Console (»)"; _scrollViewer.ScrollToBottom(); }
        private void StartNewChatSession() { _chatHistoryPanel.Children.Clear(); _conversationHistory.Clear(); RemoveAttachment(); AddMessageBubble("✨ New Chat Session started!", isAi: true); }
        private void ToggleHistoryDrawer() { if (_historyContainer.Visibility == Visibility.Collapsed) { PopulateHistoryList(); _historyContainer.Visibility = Visibility.Visible; } else _historyContainer.Visibility = Visibility.Collapsed; }
        private void PopulateHistoryList() { _historyListBox.Items.Clear(); string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (Directory.Exists(dir)) { var files = Directory.GetFiles(dir, "ChatLog_*.txt"); Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a))); foreach (var f in files) _historyListBox.Items.Add(Path.GetFileName(f)); } }
        private void LoadPastChatLog(string file) { try { string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations", file); if (!File.Exists(path)) return; string text = File.ReadAllText(path); _chatHistoryPanel.Children.Clear(); _conversationHistory.Clear(); _historyContainer.Visibility = Visibility.Collapsed; string[] turns = text.Split("==========================================================================", StringSplitOptions.RemoveEmptyEntries); foreach (var turn in turns) { int uIdx = turn.IndexOf("USER: "), jIdx = turn.IndexOf("JARVIS: "); if (uIdx >= 0 && jIdx > uIdx) { string u = turn.Substring(uIdx + 6, jIdx - (uIdx + 6)).Trim(); string j = turn.Substring(jIdx + 8).Trim(); if (!string.IsNullOrEmpty(u)) AddMessageBubble(u, false); if (!string.IsNullOrEmpty(j)) AddMessageBubble(j, true); _conversationHistory.Add(new ChatTurn { Role = "user", Text = u }); _conversationHistory.Add(new ChatTurn { Role = "model", Text = j }); } } } catch { } }

        private bool _isFolderContext = false;
        private readonly List<ChatTurn> _conversationHistory = new List<ChatTurn>();

        public static async Task SubmitTextMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ShowChat();
                if (_instance != null)
                {
                    // Fire and forget SendUserMessage so the UI thread doesn't deadlock
                    // when the AI task tries to Invoke back to update status or logs.
                    _ = _instance.SendUserMessage(message);
                }
            });
        }

        public static async Task SubmitVoiceCommand(string message, bool showUi = false)
        {
            string trimmed = (message ?? "").Trim();
            if (string.IsNullOrEmpty(trimmed)) return;

            bool isChatActive = IsVisible;
            if (!isChatActive && !showUi)
            {
                string[] triggers = new[] { "open", "run", "launch", "start", "compile", "show", "restart", "reboot", "shutdown", "install", "search", "find", "get", "git", "play", "what", "how", "why", "where", "who", "when", "can", "is", "are", "please", "tell", "explain", "help" };
                if (!triggers.Any(t => trimmed.ToLower().Contains(t))) return;
            }

            if (!SettingsManager.Current.IsJarvisEnabled) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (showUi) ShowChat();

                if (_instance == null)
                {
                    _instance = new ChatOverlay();
                    _instance.Opacity = 0;
                    _instance.Visibility = Visibility.Collapsed;
                }

                _ = _instance.SendUserMessage(trimmed);
            });
        }

        private async Task SendUserMessage(string message)
        {
            string displayMessage = message;
            string apiMessage = message;

            // Handle Attachments (simplified for stability)
            if (!string.IsNullOrEmpty(_attachedFilePath))
            {
                string name = Path.GetFileName(_attachedFilePath);
                displayMessage = $"📎 Attached: {name}\n{message}";
                apiMessage = $"[ATTACHED: {_attachedFilePath}]\n{message}";
                RemoveAttachment();
            }

            AddMessageBubble(displayMessage, isAi: false);

            int turnNumber = (_conversationHistory.Count / 2) + 1;
            var (aiBorder, aiTextBlock) = AddMessageBubbleWithControl("🧠 Thinking...", isAi: true, isItalic: true);

            bool useStreaming = SettingsManager.Current.LlmBackend == "Ollama";
            var thinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            if (!useStreaming) thinkingTimer.Start();

            string finalResult = "";
            string rawResponse = "";
            try
            {
                var snapshot = new List<ChatTurn>(_conversationHistory);
                if (useStreaming)
                {
                    aiTextBlock.Text = "⏳ Loading model...";
                    var streamSb = new System.Text.StringBuilder();
                    var cts = new System.Threading.CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromSeconds(35));
                    bool firstToken = true;
                    bool streamingDone = false;

                    rawResponse = await Task.Run(async () => await LlmRouter.AskOllamaStreamAsync(apiMessage, snapshot, onToken: t => { Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (streamingDone) return; if (firstToken) { firstToken = false; streamSb.Clear(); } streamSb.Append(t); aiTextBlock.Text = streamSb.ToString(); _scrollViewer.ScrollToBottom(); })); }, ct: cts.Token));
                    streamingDone = true;
                    finalResult = await Task.Run(() => AgentExecutor.ProcessAIResponse(rawResponse));
                    aiTextBlock.FontStyle = FontStyles.Normal;
                    RenderBubbleContent((StackPanel)aiBorder.Child, aiTextBlock, !string.IsNullOrWhiteSpace(finalResult) ? finalResult : rawResponse);
                }
                else
                {
                    rawResponse = await Task.Run(async () => await LlmRouter.AskAsync(apiMessage, snapshot));
                    finalResult = AgentExecutor.ProcessAIResponse(rawResponse);
                    aiTextBlock.FontStyle = FontStyles.Normal;
                    RenderBubbleContent((StackPanel)aiBorder.Child, aiTextBlock, finalResult);
                }
            }
            catch (Exception ex) { finalResult = "⚠️ Error: " + ex.Message; aiTextBlock.Text = finalResult; }
            finally { thinkingTimer.Stop(); }

            _ = Task.Run(() => TtsManager.Speak(finalResult, isShortSpeech: true));
            if (!finalResult.StartsWith("⚠️"))
            {
                _conversationHistory.Add(new ChatTurn { Role = "user", Text = message });
                _conversationHistory.Add(new ChatTurn { Role = "model", Text = finalResult });
                LogConversationTurn(message, finalResult);
            }
        }

        private static void LogConversationTurn(string u, string j) { try { string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); File.AppendAllText(Path.Combine(dir, $"ChatLog_{DateTime.Now:yyyy-MM-dd}.txt"), $"\nUSER: {u}\nJARVIS: {j}\n" + new string('=', 60) + "\n"); } catch { } }

        private (Border Border, TextBlock TextContent) AddMessageBubbleWithControl(string text, bool isAi, bool isItalic = false)
        {
            var bubbleBorder = new Border { Background = isAi ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(64, 128, 80, 230)), CornerRadius = isAi ? new CornerRadius(12, 12, 12, 0) : new CornerRadius(12, 12, 0, 12), Margin = isAi ? new Thickness(0, 4, 48, 4) : new Thickness(48, 4, 0, 4), HorizontalAlignment = isAi ? HorizontalAlignment.Left : HorizontalAlignment.Right, Padding = new Thickness(12, 10, 12, 10), MaxWidth = 300 };
            var stack = new StackPanel(); bubbleBorder.Child = stack;
            var tb = new TextBlock { FontSize = 13, FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap, FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            stack.Children.Add(tb);
            RenderBubbleContent(stack, tb, text);
            _chatHistoryPanel.Children.Add(bubbleBorder);
            _scrollViewer.ScrollToBottom();
            return (bubbleBorder, tb);
        }

        private void RenderBubbleContent(StackPanel container, TextBlock mainText, string text)
        {
            mainText.Text = text;
            if (text.Contains("```"))
            {
                mainText.Visibility = Visibility.Collapsed;
                for (int i = container.Children.Count - 1; i >= 0; i--) if (container.Children[i] != mainText) container.Children.RemoveAt(i);
                var parts = ParseMessageParts(text);
                foreach (var part in parts)
                {
                    if (part.IsCode)
                    {
                        var codeBdr = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 4), CornerRadius = new CornerRadius(4) };
                        var codeTb = new TextBlock { Text = part.Content.Trim(), FontSize = 12, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
                        codeTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                        codeBdr.Child = codeTb;
                        container.Children.Add(codeBdr);
                    }
                    else if (!string.IsNullOrWhiteSpace(part.Content))
                    {
                        var tb = new TextBlock { Text = part.Content.Trim(), FontSize = 13, FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
                        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                        container.Children.Add(tb);
                    }
                }
            }
            else mainText.Visibility = Visibility.Visible;
        }

        private static List<MessagePart> ParseMessageParts(string text) { var parts = new List<MessagePart>(); if (string.IsNullOrEmpty(text)) return parts; int idx = 0; while (idx < text.Length) { int sIdx = text.IndexOf("```", idx); if (sIdx == -1) { parts.Add(new MessagePart { IsCode = false, Content = text.Substring(idx) }); break; } if (sIdx > idx) parts.Add(new MessagePart { IsCode = false, Content = text.Substring(idx, sIdx - idx) }); int eIdx = text.IndexOf("```", sIdx + 3); if (eIdx == -1) { parts.Add(new MessagePart { IsCode = true, Content = text.Substring(sIdx + 3) }); break; } string code = text.Substring(sIdx + 3, eIdx - (sIdx + 3)); string lang = ""; int nl = code.IndexOf('\n'); if (nl != -1) { lang = code.Substring(0, nl).Trim(); code = code.Substring(nl + 1); } parts.Add(new MessagePart { IsCode = true, Language = lang, Content = code }); idx = eIdx + 3; } return parts; }
        private Border AddMessageBubble(string t, bool isAi) { return AddMessageBubbleWithControl(t, isAi).Border; }
        private class MessagePart { public bool IsCode { get; set; } public string Language { get; set; } = ""; public string Content { get; set; } = ""; }
    }
}
