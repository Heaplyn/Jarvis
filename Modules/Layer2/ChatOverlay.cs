
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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace JarvisLauncher
{
    public class ChatOverlay : BaseOverlay
    {
        private static ChatOverlay? Instance;
        private static readonly System.Text.StringBuilder ConsoleLog = new System.Text.StringBuilder();

        private static CancellationTokenSource? _activeMessageCts;
        private static readonly object _ctsLock = new object();

        private StackPanel ChatHistoryPanel = null!;
        private ScrollViewer ScrollViewerPrivate = null!;
        private TextBox InputTextBox = null!;
        private TextBlock PlaceholderTextBlock = null!;

        // Attachment controls
        private string? AttachedFilePath = null;
        private Border AttachedFileBadge = null!;
        private TextBlock AttachedFileText = null!;
        private Button AttachButton = null!;

        // History controls
        private Border HistoryContainer = null!;
        private ListBox HistoryListBox = null!;

        // Visual Console controls
        private Border ConsoleContainer = null!;
        private TextBox ConsoleTextBox = null!;
        private Button ConsoleToggleBtn = null!;
        private bool IsConsoleExpanded = false;

        public new static bool IsVisible => Instance != null && Instance.Visibility == Visibility.Visible && Instance.Opacity > 0.1;

        public static void ShowOverlay() => ShowChat();

        public static void ShowChat()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Instance == null)
                {
                    Instance = new ChatOverlay();
                    Instance.Closed += (S, E) => Instance = null;
                }

                Instance.Show();
            });
        }

        public static void LogConsoleAction(string ActionName, string Details)
        {
            string LogLine = $"[{DateTime.Now:HH:mm:ss}] {ActionName.ToUpper()}\n{Details}\n----------------------------------\n";
            lock (ConsoleLog)
            {
                ConsoleLog.AppendLine(LogLine);
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (Instance != null && Instance.ConsoleTextBox != null)
                {
                    Instance.ConsoleTextBox.Text = ConsoleLog.ToString();
                    Instance.ConsoleTextBox.ScrollToEnd();
                }
            }));
        }

        private ChatOverlay()
            : base("JARVIS AI COMPANION", width: 380, height: 500)
        {
            var WorkArea = SystemParameters.WorkArea;
            this.Left = WorkArea.Width - this.Width - 20;
            this.Top = WorkArea.Top + 40;

            var ContentGrid = new Grid();
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var ToolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var NewChatBtn = new Button { Content = "✨ New Chat", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand };
            NewChatBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            NewChatBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            NewChatBtn.Click += (S, E) => StartNewChatSession();
            Grid.SetColumn(NewChatBtn, 1);
            ToolbarGrid.Children.Add(NewChatBtn);

            var HistoryBtn = new Button { Content = "📜 History", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Cursor = Cursors.Hand };
            HistoryBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            HistoryBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            HistoryBtn.Click += (S, E) => ToggleHistoryDrawer();
            Grid.SetColumn(HistoryBtn, 2);
            ToolbarGrid.Children.Add(HistoryBtn);

            Grid.SetRow(ToolbarGrid, 0);
            ContentGrid.Children.Add(ToolbarGrid);

            HistoryContainer = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(4), Visibility = Visibility.Collapsed };
            HistoryContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            HistoryContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            HistoryListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 11, MaxHeight = 120 };
            HistoryListBox.SetResourceReference(ListBox.ForegroundProperty, "TextPrimaryBrush");
            HistoryListBox.SelectionChanged += (S, E) => { if (HistoryListBox.SelectedItem is string logFile && !logFile.StartsWith("(")) LoadPastChatLog(logFile); };
            HistoryContainer.Child = HistoryListBox;
            Grid.SetRow(HistoryContainer, 1);
            ContentGrid.Children.Add(HistoryContainer);

            ScrollViewerPrivate = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 8) };
            ChatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            ScrollViewerPrivate.Content = ChatHistoryPanel;
            Grid.SetRow(ScrollViewerPrivate, 2);
            ContentGrid.Children.Add(ScrollViewerPrivate);

            LoadOrWelcomeActiveChatHistory();

            var Divider = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8) };
            Divider.SetResourceReference(Border.BackgroundProperty, "WindowBorderBrush");
            Grid.SetRow(Divider, 3);
            ContentGrid.Children.Add(Divider);

            ConsoleContainer = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(6) };
            ConsoleContainer.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            ConsoleContainer.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

            var ConsoleLayoutGrid = new Grid();
            ConsoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ConsoleLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var ConsoleHeader = new Grid();
            ConsoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ConsoleHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ConsoleTitle = new TextBlock { Text = "⚡ Command Execution Console", FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            ConsoleTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(ConsoleTitle, 0);
            ConsoleHeader.Children.Add(ConsoleTitle);

            ConsoleToggleBtn = new Button { Content = "Show Console (»)", FontSize = 10, Padding = new Thickness(6, 2, 6, 2), Cursor = Cursors.Hand };
            ConsoleToggleBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            ConsoleToggleBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            ConsoleToggleBtn.Click += (S, E) => ToggleConsole();
            Grid.SetColumn(ConsoleToggleBtn, 1);
            ConsoleHeader.Children.Add(ConsoleToggleBtn);

            Grid.SetRow(ConsoleHeader, 0);
            ConsoleLayoutGrid.Children.Add(ConsoleHeader);

            ConsoleTextBox = new TextBox { Height = 0, Visibility = Visibility.Collapsed, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 11, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 0, 0), FocusVisualStyle = null };
            ConsoleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            ConsoleTextBox.Text = ConsoleLog.ToString();

            Grid.SetRow(ConsoleTextBox, 1);
            ConsoleLayoutGrid.Children.Add(ConsoleTextBox);

            ConsoleContainer.Child = ConsoleLayoutGrid;
            Grid.SetRow(ConsoleContainer, 4);
            ContentGrid.Children.Add(ConsoleContainer);

            var InputContainerStack = new StackPanel();
            AttachedFileBadge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
            AttachedFileBadge.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");
            AttachedFileBadge.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            var BadgeStack = new StackPanel { Orientation = Orientation.Horizontal };
            AttachedFileText = new TextBlock { FontSize = 11, FontWeight = FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center };
            AttachedFileText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            BadgeStack.Children.Add(AttachedFileText);

            var RemoveAttachBtn = new Button { Content = " ✕ ", FontSize = 10, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(2, 0, 2, 0), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            RemoveAttachBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
            RemoveAttachBtn.Click += (S, E) => RemoveAttachment();
            BadgeStack.Children.Add(RemoveAttachBtn);
            AttachedFileBadge.Child = BadgeStack;
            InputContainerStack.Children.Add(AttachedFileBadge);

            var InputRowGrid = new Grid();
            InputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            InputRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AttachButton = new Button { Content = "➕", ToolTip = "Attach file (or drag & drop here)", FontSize = 14, FontWeight = FontWeights.Bold, Width = 34, Height = 36, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Stretch };
            AttachButton.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            AttachButton.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            AttachButton.Click += (S, E) => AttachFileInteractive();
            Grid.SetColumn(AttachButton, 0);
            InputRowGrid.Children.Add(AttachButton);

            var TextboxOverlayGrid = new Grid();
            InputTextBox = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(1), FontSize = 13, FontFamily = new FontFamily("Segoe UI"), TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Padding = new Thickness(8, 6, 8, 6), MinHeight = 36, MaxHeight = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FocusVisualStyle = null };
            InputTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            InputTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            InputTextBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");

            PlaceholderTextBlock = new TextBlock { Text = "Ask Jarvis... (Press Enter)", FontSize = 13, FontFamily = new FontFamily("Segoe UI"), IsHitTestVisible = false, Margin = new Thickness(10, 8, 10, 8), VerticalAlignment = VerticalAlignment.Center };
            PlaceholderTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPlaceholderBrush");

            InputTextBox.TextChanged += (S, E) => { PlaceholderTextBlock.Visibility = string.IsNullOrEmpty(InputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed; };
            InputTextBox.KeyDown += (S, E) =>
            {
                if (E.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift))
                {
                    E.Handled = true;
                    string M = InputTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(M) || AttachedFilePath != null)
                    {
                        InputTextBox.Text = "";
                        // Use static entry point to benefit from deadlock protection logic
                        _ = SubmitTextMessage(M);
                    }
                }
            };

            TextboxOverlayGrid.Children.Add(InputTextBox);
            TextboxOverlayGrid.Children.Add(PlaceholderTextBlock);
            Grid.SetColumn(TextboxOverlayGrid, 1);
            InputRowGrid.Children.Add(TextboxOverlayGrid);
            InputContainerStack.Children.Add(InputRowGrid);

            ContentGrid.AllowDrop = true;
            ContentGrid.Drop += (S, E) => { if (E.Data.GetDataPresent(DataFormats.FileDrop) && E.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0) { string path = files[0]; if (Directory.Exists(path)) AttachFolder(path); else AttachFile(path); E.Handled = true; } };

            Grid.SetRow(InputContainerStack, 5);
            ContentGrid.Children.Add(InputContainerStack);
            this.UserContent = ContentGrid;
        }

        private void AttachFileInteractive()
        {
            var Cm = new ContextMenu();
            var FileItem = new MenuItem { Header = "📄 Attach Single File..." };
            FileItem.Click += (S, E) => { var Dlg = new Microsoft.Win32.OpenFileDialog(); if (Dlg.ShowDialog() == true) AttachFile(Dlg.FileName); };
            Cm.Items.Add(FileItem);
            var FolderItem = new MenuItem { Header = "📁 Select Folder..." };
            FolderItem.Click += (S, E) => { var Dlg = new Microsoft.Win32.OpenFolderDialog(); if (Dlg.ShowDialog() == true) AttachFolder(Dlg.FolderName); };
            Cm.Items.Add(FolderItem);
            Cm.IsOpen = true;
        }

        private void AttachFile(string PathString) { if (File.Exists(PathString)) { AttachedFilePath = PathString; IsFolderContext = false; AttachedFileText.Text = "📎 " + Path.GetFileName(PathString); AttachedFileBadge.Visibility = Visibility.Visible; } }
        private void AttachFolder(string PathString) { if (Directory.Exists(PathString)) { AttachedFilePath = PathString; IsFolderContext = true; AttachedFileText.Text = "📁 " + Path.GetFileName(PathString); AttachedFileBadge.Visibility = Visibility.Visible; } }
        private void RemoveAttachment() { AttachedFilePath = null; AttachedFileBadge.Visibility = Visibility.Collapsed; }
        private void ToggleConsole() { IsConsoleExpanded = !IsConsoleExpanded; ConsoleTextBox.Height = IsConsoleExpanded ? 120 : 0; ConsoleTextBox.Visibility = IsConsoleExpanded ? Visibility.Visible : Visibility.Collapsed; ConsoleToggleBtn.Content = IsConsoleExpanded ? "Hide Console («)" : "Show Console (»)"; ScrollViewerPrivate.ScrollToBottom(); }
        private void StartNewChatSession() { ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear(); RemoveAttachment(); DeleteActiveChatHistory(); AddMessageBubble("✨ New Chat Session started!", IsAi: true); }
        private void ToggleHistoryDrawer() { if (HistoryContainer.Visibility == Visibility.Collapsed) { PopulateHistoryList(); HistoryContainer.Visibility = Visibility.Visible; } else HistoryContainer.Visibility = Visibility.Collapsed; }
        private void PopulateHistoryList() { HistoryListBox.Items.Clear(); string Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (Directory.Exists(Dir)) { var Files = Directory.GetFiles(Dir, "ChatLog_*.txt"); Array.Sort(Files, (A, B) => File.GetLastWriteTime(B).CompareTo(File.GetLastWriteTime(A))); foreach (var F in Files) HistoryListBox.Items.Add(Path.GetFileName(F)); } }
        private void LoadPastChatLog(string FileName) { try { string PathString = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations", FileName); if (!File.Exists(PathString)) return; string Text = File.ReadAllText(PathString); ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear(); HistoryContainer.Visibility = Visibility.Collapsed; string[] Turns = Text.Split("==========================================================================", StringSplitOptions.RemoveEmptyEntries); foreach (var Turn in Turns) { int UIdx = Turn.IndexOf("USER: "), JIdx = Turn.IndexOf("JARVIS: "); if (UIdx >= 0 && JIdx > UIdx) { string U = Turn.Substring(UIdx + 6, JIdx - (UIdx + 6)).Trim(); string J = Turn.Substring(JIdx + 8).Trim(); if (!string.IsNullOrEmpty(U)) AddMessageBubble(U, false); if (!string.IsNullOrEmpty(J)) AddMessageBubble(J, true); ConversationHistory.Add(new ChatTurn { Role = "user", Text = U }); ConversationHistory.Add(new ChatTurn { Role = "model", Text = J }); } } } catch { } }

        private bool IsFolderContext = false;
        private readonly List<ChatTurn> ConversationHistory = new List<ChatTurn>();

        public static async Task SubmitTextMessage(string Message)
        {
            if (string.IsNullOrWhiteSpace(Message)) return;

            lock (_ctsLock)
            {
                if (_activeMessageCts != null)
                {
                    try { _activeMessageCts.Cancel(); } catch { }
                    _activeMessageCts.Dispose();
                    _activeMessageCts = null;
                }
            }
            TtsManager.Stop();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ShowChat();
                if (Instance != null)
                {
                    var cts = new CancellationTokenSource();
                    lock (_ctsLock) _activeMessageCts = cts;
                    _ = Instance.SendUserMessage(Message, "TEXT", cts.Token);
                }
            });
        }

        public static async Task SubmitVoiceCommand(string Message, bool showUi = false)
        {
            string Trimmed = (Message ?? "").Trim();
            if (string.IsNullOrEmpty(Trimmed)) return;

            string Lower = Trimmed.ToLower();

            if (Lower.Contains("turn on voice mode") || Lower.Contains("turn off voice mode") ||
                Lower.Contains("enable voice mode") || Lower.Contains("disable voice mode"))
            {
            }
            else
            {
                if (!SettingsManager.Current.IS_VOICE_MODE_ACTIVE) return;
            }

            if (!SettingsManager.Current.IS_JARVIS_ENABLED) return;

            lock (_ctsLock)
            {
                if (_activeMessageCts != null)
                {
                    try { _activeMessageCts.Cancel(); } catch { }
                    _activeMessageCts.Dispose();
                    _activeMessageCts = null;
                }
            }
            TtsManager.Stop();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ShowChat();

                if (Instance == null)
                {
                    Instance = new ChatOverlay();
                    Instance.Opacity = 0;
                    Instance.Visibility = Visibility.Collapsed;
                }

                var cts = new CancellationTokenSource();
                lock (_ctsLock) _activeMessageCts = cts;
                _ = Instance.SendUserMessage(Trimmed, "VOICE", cts.Token);
            });
        }

        private async Task SendUserMessage(string Message, string source, CancellationToken ct)
        {
            string DisplayMessage = Message;
            string ApiMessage = $"[INPUT_SOURCE: {source}]\n{Message}";

            if (!string.IsNullOrEmpty(AttachedFilePath))
            {
                string Name = Path.GetFileName(AttachedFilePath);
                DisplayMessage = $"📎 Attached: {Name}\n{Message}";
                ApiMessage = $"[ATTACHED: {AttachedFilePath}]\n[INPUT_SOURCE: {source}]\n{Message}";
                RemoveAttachment();
            }

            AddMessageBubble(DisplayMessage, IsAi: false);

            int TurnNumber = (ConversationHistory.Count / 2) + 1;
            var (AiBorder, AiTextBlock) = AddMessageBubbleWithControl("🧠 Thinking...", IsAi: true, IsItalic: true);

            bool UseStreaming = SettingsManager.Current.LLM_BACKEND == "Ollama";
            var ThinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            if (!UseStreaming) ThinkingTimer.Start();

            string FinalResult = "";
            string RawResponse = "";
            try
            {
                var Snapshot = new List<ChatTurn>(ConversationHistory);
                if (UseStreaming)
                {
                    AiTextBlock.Text = "⏳ Loading model...";
                    var StreamSb = new System.Text.StringBuilder();

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    linkedCts.CancelAfter(TimeSpan.FromSeconds(25));

                    bool FirstToken = true;
                    bool StreamingDone = false;
                    bool IsTimedOut = false;

                    try
                    {
                        RawResponse = await Task.Run(async () => await LlmRouter.AskOllamaStreamAsync(ApiMessage, Snapshot, onToken: t => { Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (StreamingDone) return; if (FirstToken) { FirstToken = false; StreamSb.Clear(); } StreamSb.Append(t); AiTextBlock.Text = StreamSb.ToString(); ScrollViewerPrivate.ScrollToBottom(); })); }, ct: linkedCts.Token), linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (ct.IsCancellationRequested) return;
                        IsTimedOut = true;
                        RawResponse = "⚠️ The AI backend timed out. Please check if your local LLM (Ollama) is running and responsive.";
                    }

                    StreamingDone = true;
                    if (!IsTimedOut)
                    {
                        FinalResult = await Task.Run(() => AgentExecutor.ProcessAIResponse(RawResponse), ct);
                    }
                    else
                    {
                        FinalResult = RawResponse;
                    }

                    if (FinalResult.Contains("[EXEC_PS:") || FinalResult.Contains("[EXEC_SHELL:") || (FinalResult.Contains("[RUN_COMMAND:") && (FinalResult.Contains("download") || FinalResult.Contains("scrape") || FinalResult.Contains("search") || FinalResult.Contains("google") || FinalResult.Contains("websearch"))))
                    {
                        FinalResult = await InterceptAndExecuteCommandsAsync(FinalResult, AiTextBlock, (StackPanel)AiBorder.Child, ct);
                    }

                    ct.ThrowIfCancellationRequested();
                    AiTextBlock.FontStyle = FontStyles.Normal;
                    RenderBubbleContent((StackPanel)AiBorder.Child, AiTextBlock, !string.IsNullOrWhiteSpace(FinalResult) ? FinalResult : RawResponse);
                }
                else
                {
                    RawResponse = await Task.Run(async () => await LlmRouter.AskAsync(ApiMessage, Snapshot, ct), ct);
                    FinalResult = AgentExecutor.ProcessAIResponse(RawResponse);

                    if (FinalResult.Contains("[EXEC_PS:") || FinalResult.Contains("[EXEC_SHELL:") || (FinalResult.Contains("[RUN_COMMAND:") && (FinalResult.Contains("download") || FinalResult.Contains("scrape") || FinalResult.Contains("search") || FinalResult.Contains("google") || FinalResult.Contains("websearch"))))
                    {
                        FinalResult = await InterceptAndExecuteCommandsAsync(FinalResult, AiTextBlock, (StackPanel)AiBorder.Child, ct);
                    }

                    ct.ThrowIfCancellationRequested();
                    AiTextBlock.FontStyle = FontStyles.Normal;
                    RenderBubbleContent((StackPanel)AiBorder.Child, AiTextBlock, FinalResult);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception Ex) { FinalResult = "⚠️ Error: " + Ex.Message; AiTextBlock.Text = FinalResult; }
            finally { ThinkingTimer.Stop(); }

            if (ct.IsCancellationRequested) return;
            _ = Task.Run(() => TtsManager.Speak(FinalResult, isShortSpeech: true));
            if (!FinalResult.StartsWith("⚠️"))
            {
                ConversationHistory.Add(new ChatTurn { Role = "user", Text = Message });
                ConversationHistory.Add(new ChatTurn { Role = "model", Text = FinalResult });
                LogConversationTurn(Message, FinalResult);
                SaveActiveChatHistory();
            }
        }

        private async Task<string> InterceptAndExecuteCommandsAsync(string RawResponseString, TextBox AiTextBlock, StackPanel BubblePanel, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(RawResponseString)) return RawResponseString;
            ct.ThrowIfCancellationRequested();

            var PsMatch = Regex.Match(RawResponseString, @"\[EXEC_PS:\s*(?<cmd>[\s\S]+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var ShellMatch = Regex.Match(RawResponseString, @"\[EXEC_SHELL:\s*(?<cmd>[\s\S]+?)\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var WebMatch = Regex.Match(RawResponseString, @"\[RUN_COMMAND:\s*(?<cmd>download|download-list|scrape|search|google|websearch)\s+(?<param>[^\]]+)\]", RegexOptions.IgnoreCase);

            string ExecResult = "";
            string CmdTypeString = "";
            string ParameterString = "";

            if (PsMatch.Success)
            {
                CmdTypeString = "powershell";
                ParameterString = PsMatch.Groups["cmd"].Value.Trim();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AiTextBlock.Text = "⚡ Executing PowerShell script in background...";
                });
                ExecResult = await Task.Run(() => AgentExecutor.ExecutePowerShellDirect(ParameterString));
            }
            else if (ShellMatch.Success)
            {
                CmdTypeString = "shell";
                ParameterString = ShellMatch.Groups["cmd"].Value.Trim();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AiTextBlock.Text = "💻 Executing Command shell script in background...";
                });
                ExecResult = await Task.Run(() => AgentExecutor.ExecuteShellDirect(ParameterString));
            }
            else if (WebMatch.Success)
            {
                CmdTypeString = WebMatch.Groups["cmd"].Value.ToLower().Trim();
                ParameterString = WebMatch.Groups["param"].Value.Trim();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AiTextBlock.Text = $"🌐 Intercepted task: {CmdTypeString} {ParameterString}...\nExecuting...";
                });

                if (CmdTypeString == "download-list")
                {
                    ExecResult = await WebOperationManager.DownloadListAsync(ParameterString);
                }
                else if (CmdTypeString == "download")
                {
                    ExecResult = await WebOperationManager.DownloadFileAsync(ParameterString);
                }
                else if (CmdTypeString == "scrape")
                {
                    ExecResult = await WebOperationManager.ScrapeWebpageAsync(ParameterString);
                }
                else if (CmdTypeString == "search" || CmdTypeString == "google" || CmdTypeString == "websearch")
                {
                    ExecResult = await WebOperationManager.SearchWebAsync(ParameterString);
                }
            }
            else
            {
                return RawResponseString;
            }

            string Prompt = $"The user asked a query that required executing a background action. Here is the output/result from that execution:\n\n" +
                            $"{ExecResult}\n\n" +
                            $"Based on this output, provide your final response to the user's original query. Do NOT include any command execution block tags like [EXEC_PS:] or [EXEC_SHELL:] in your final response. Keep it clean, direct, and conversational.";

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AiTextBlock.Text = "🧠 Synthesizing final results...";
            });

            string FinalAns = await LlmRouter.AskAsync(Prompt, ConversationHistory);
            return FinalAns;
        }

        private static void LogConversationTurn(string U, string J) { try { string Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir); File.AppendAllText(Path.Combine(Dir, $"ChatLog_{DateTime.Now:yyyy-MM-dd}.txt"), $"\nUSER: {U}\nJARVIS: {J}\n" + new string('=', 60) + "\n"); } catch { } }

        private static string GetChatHistoryFilePath()
        {
            string DataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            return Path.Combine(DataDir, "ChatHistory.json");
        }

        private void LoadOrWelcomeActiveChatHistory()
        {
            try
            {
                string PathString = GetChatHistoryFilePath();
                if (File.Exists(PathString))
                {
                    string Json = File.ReadAllText(PathString);
                    var Turns = JsonSerializer.Deserialize<List<ChatTurn>>(Json);
                    if (Turns != null && Turns.Count > 0)
                    {
                        ChatHistoryPanel.Children.Clear();
                        ConversationHistory.Clear();
                        foreach (var Turn in Turns)
                        {
                            ConversationHistory.Add(Turn);
                            AddMessageBubble(Turn.Text, Turn.Role == "model");
                        }
                        return;
                    }
                }
            }
            catch (Exception Ex)
            {
                DebugConsoleOverlay.Log("ChatHistory", $"Error loading persistent history: {Ex.Message}");
            }

            AddMessageBubble("Hello! I am your Jarvis AI Companion. How can I help you today?", IsAi: true);
        }

        private void SaveActiveChatHistory()
        {
            try
            {
                string PathString = GetChatHistoryFilePath();
                var TurnsToSave = ConversationHistory.Count > 200
                    ? ConversationHistory.Skip(ConversationHistory.Count - 200).ToList()
                    : ConversationHistory;
                string Json = JsonSerializer.Serialize(TurnsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PathString, Json);
            }
            catch (Exception Ex)
            {
                DebugConsoleOverlay.Log("ChatHistory", $"Error saving persistent history: {Ex.Message}");
            }
        }

        private void DeleteActiveChatHistory()
        {
            try
            {
                string PathString = GetChatHistoryFilePath();
                if (File.Exists(PathString)) File.Delete(PathString);
            }
            catch { }
        }

        private (Border Border, TextBox TextContent) AddMessageBubbleWithControl(string Text, bool IsAi, bool IsItalic = false)
        {
            var BubbleBorder = new Border { Background = IsAi ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(64, 128, 80, 230)), CornerRadius = IsAi ? new CornerRadius(12, 12, 12, 0) : new CornerRadius(12, 12, 0, 12), Margin = IsAi ? new Thickness(0, 4, 48, 4) : new Thickness(48, 4, 0, 4), HorizontalAlignment = IsAi ? HorizontalAlignment.Left : HorizontalAlignment.Right, Padding = new Thickness(12, 10, 12, 10), MaxWidth = 300 };
            var Stack = new StackPanel(); BubbleBorder.Child = Stack;
            var Tb = new TextBox
            { 
                FontSize = 13, 
                FontFamily = new FontFamily("Segoe UI"), 
                TextWrapping = TextWrapping.Wrap, 
                FontStyle = IsItalic ? FontStyles.Italic : FontStyles.Normal,
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FocusVisualStyle = null,
                IsReadOnlyCaretVisible = false
            };
            Tb.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            Stack.Children.Add(Tb);
            RenderBubbleContent(Stack, Tb, Text);
            ChatHistoryPanel.Children.Add(BubbleBorder);
            ScrollViewerPrivate.ScrollToBottom();
            return (BubbleBorder, Tb);
        }

        private void RenderBubbleContent(StackPanel Container, TextBox MainText, string Text)
        {
            MainText.Text = Text;
            if (Text.Contains("```"))
            {
                MainText.Visibility = Visibility.Collapsed;
                for (int I = Container.Children.Count - 1; I >= 0; I--) if (Container.Children[I] != MainText) Container.Children.RemoveAt(I);
                var Parts = ParseMessageParts(Text);
                foreach (var Part in Parts)
                {
                    if (Part.IsCode)
                    {
                        var CodeBdr = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 4), CornerRadius = new CornerRadius(4) };
                        var CodeTb = new TextBox
                        { 
                            Text = Part.Content.Trim(),
                            FontSize = 12, 
                            FontFamily = new FontFamily("Consolas"), 
                            TextWrapping = TextWrapping.Wrap,
                            IsReadOnly = true,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            FocusVisualStyle = null,
                            IsReadOnlyCaretVisible = false
                        };
                        CodeTb.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
                        CodeBdr.Child = CodeTb;
                        Container.Children.Add(CodeBdr);
                    }
                    else if (!string.IsNullOrWhiteSpace(Part.Content))
                    {
                        var Tb = new TextBox
                        { 
                            Text = Part.Content.Trim(),
                            FontSize = 13, 
                            FontFamily = new FontFamily("Segoe UI"), 
                            TextWrapping = TextWrapping.Wrap, 
                            Margin = new Thickness(0, 2, 0, 2),
                            IsReadOnly = true,
                            Background = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            FocusVisualStyle = null,
                            IsReadOnlyCaretVisible = false
                        };
                        Tb.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
                        Container.Children.Add(Tb);
                    }
                }
            }
            else MainText.Visibility = Visibility.Visible;
        }

        private static List<MessagePart> ParseMessageParts(string Text) { var Parts = new List<MessagePart>(); if (string.IsNullOrEmpty(Text)) return Parts; int Idx = 0; while (Idx < Text.Length) { int SIdx = Text.IndexOf("```", Idx); if (SIdx == -1) { Parts.Add(new MessagePart { IsCode = false, Content = Text.Substring(Idx) }); break; } if (SIdx > Idx) Parts.Add(new MessagePart { IsCode = false, Content = Text.Substring(Idx, SIdx - Idx) }); int EIdx = Text.IndexOf("```", SIdx + 3); if (EIdx == -1) { Parts.Add(new MessagePart { IsCode = true, Content = Text.Substring(SIdx + 3) }); break; } string Code = Text.Substring(SIdx + 3, EIdx - (SIdx + 3)); string Lang = ""; int Nl = Code.IndexOf('\n'); if (Nl != -1) { Lang = Code.Substring(0, Nl).Trim(); Code = Code.Substring(Nl + 1); } Parts.Add(new MessagePart { IsCode = true, Language = Lang, Content = Code }); Idx = EIdx + 3; } return Parts; }
        private Border AddMessageBubble(string T, bool IsAi) { return AddMessageBubbleWithControl(T, IsAi).Border; }
        private class MessagePart { public bool IsCode { get; set; } public string Language { get; set; } = ""; public string Content { get; set; } = ""; }
    }
}
