
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
using Ellipse = System.Windows.Shapes.Ellipse;

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
        private TextBlock StatusText = null!;
        private Ellipse StatusDot = null!;
        private ComboBox ModelSelector = null!;

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

            var ToolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Status
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Spacer / Model
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // New Chat
            ToolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // History

            // 1. Status Indicator
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            StatusDot = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = Brushes.LightGreen, Margin = new Thickness(0, 0, 5, 0) };
            StatusText = new TextBlock { Text = "READY", FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            statusStack.Children.Add(StatusDot);
            statusStack.Children.Add(StatusText);
            Grid.SetColumn(statusStack, 0);
            ToolbarGrid.Children.Add(statusStack);

            // 2. Model Selector Dropdown
            ModelSelector = new ComboBox {
                Width = 120,
                FontSize = 10,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 10, 0)
            };
            PopulateModels();
            ModelSelector.SelectionChanged += (s, e) => {
                if (ModelSelector.SelectedItem is string m) {
                    SettingsManager.Current.GEMINI_MODEL = m;
                    SettingsManager.Save();
                }
            };
            Grid.SetColumn(ModelSelector, 1);
            ToolbarGrid.Children.Add(ModelSelector);

            var NewChatBtn = new Button { Content = "✨ Clear", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand };
            NewChatBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            NewChatBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            NewChatBtn.Click += (S, E) => StartNewChatSession();
            Grid.SetColumn(NewChatBtn, 2);
            ToolbarGrid.Children.Add(NewChatBtn);

            var HistoryBtn = new Button { Content = "📜 Log", FontSize = 11, Padding = new Thickness(8, 3, 8, 3), Cursor = Cursors.Hand };
            HistoryBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            HistoryBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            HistoryBtn.Click += (S, E) => ToggleHistoryDrawer();
            Grid.SetColumn(HistoryBtn, 3);
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

            var Divider = new Border { Height = 1, Margin = new Thickness(0, 0, 0, 8), IsHitTestVisible = false };
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

            // --- QUICK ACTION BAR ---
            var quickActionBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            var screenBtn = CreateQuickButton("📸 Analyze Screen", (s, e) => SubmitTextMessage("What's on my screen right now? [TAKE_SCREENSHOT]"));
            var notesBtn = CreateQuickButton("📓 Notes", (s, e) => NoteManagerOverlay.ShowOverlay());
            var specsBtn = CreateQuickButton("🖥️ Specs", (s, e) => SubmitTextMessage("Show me my system specs. [GET_ACTIVE_WINDOWS] [GET_PROCESSES]"));
            var fixBtn = CreateQuickButton("🛠️ Fix Code", (s, e) => SubmitTextMessage("Scan my active project for bugs or optimizations. [LIST_DIR: .]"));

            quickActionBar.Children.Add(screenBtn);
            quickActionBar.Children.Add(notesBtn);
            quickActionBar.Children.Add(specsBtn);
            quickActionBar.Children.Add(fixBtn);
            InputContainerStack.Children.Add(quickActionBar);

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
            InputTextBox = new TextBox {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true, // MULTI-LINE SUPPORT
                Padding = new Thickness(8, 6, 8, 6),
                MinHeight = 36,
                MaxHeight = 150, // Increased max height
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FocusVisualStyle = null
            };
            InputTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            InputTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            InputTextBox.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");

            PlaceholderTextBlock = new TextBlock { Text = "Ask Jarvis... (Enter to send, Ctrl+Enter or Shift+Enter for new line)", FontSize = 13, FontFamily = new FontFamily("Segoe UI"), IsHitTestVisible = false, Margin = new Thickness(10, 8, 10, 8), VerticalAlignment = VerticalAlignment.Top };
            PlaceholderTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextPlaceholderBrush");

            InputTextBox.TextChanged += (S, E) => { PlaceholderTextBlock.Visibility = string.IsNullOrEmpty(InputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed; };
            InputTextBox.PreviewKeyDown += (S, E) =>
            {
                // Enter sends the message (unless Shift or Ctrl is held)
                if (E.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    E.Handled = true;
                    string M = InputTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(M) || AttachedFilePath != null)
                    {
                        InputTextBox.Text = "";
                        _ = SubmitTextMessage(M);
                    }
                }
                // Support Ctrl+Enter for sending explicitly if preferred by some users
                else if (E.Key == Key.Enter && Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    // If they use Ctrl+Enter, treat it as a hard send
                    E.Handled = true;
                    string M = InputTextBox.Text.Trim();
                    if (!string.IsNullOrEmpty(M) || AttachedFilePath != null)
                    {
                        InputTextBox.Text = "";
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

        private Button CreateQuickButton(string content, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = content,
                FontSize = 10,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = Cursors.Hand,
                Height = 22
            };
            btn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            btn.Click += onClick;
            return btn;
        }

        private void PopulateModels()
        {
            ModelSelector.Items.Clear();
            string backend = SettingsManager.Current.LLM_BACKEND;

            if (backend == "Gemini")
            {
                ModelSelector.Items.Add("gemini-1.5-flash");
                ModelSelector.Items.Add("gemini-1.5-pro");
                ModelSelector.Items.Add("gemini-2.0-flash-exp");
                ModelSelector.Items.Add("gemini-2.0-pro-exp");

                string current = SettingsManager.Current.GEMINI_MODEL;
                if (ModelSelector.Items.Contains(current)) ModelSelector.SelectedItem = current;
                else ModelSelector.SelectedIndex = 0;
            }
            else if (backend == "Groq")
            {
                ModelSelector.Items.Add("llama-3.3-70b-versatile");
                ModelSelector.Items.Add("llama-3.1-8b-instant");
                ModelSelector.Items.Add("mixtral-8x7b-32768");
                ModelSelector.SelectedIndex = 0;
            }
            else if (backend == "OpenAI")
            {
                ModelSelector.Items.Add("gpt-4o");
                ModelSelector.Items.Add("gpt-4o-mini");
                ModelSelector.Items.Add("o1-preview");
                ModelSelector.SelectedIndex = 0;
            }
            else
            {
                ModelSelector.Items.Add("Default");
                ModelSelector.SelectedIndex = 0;
            }
        }

        private void SetStatus(string text, Brush color)
        {
            Dispatcher.Invoke(() => {
                StatusText.Text = text.ToUpper();
                StatusDot.Fill = color;
            });
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

        private void AttachFile(string PathString) { if (File.Exists(PathString)) { AttachedFilePath = PathString; IsFolderContext = false; AttachedFileText.Text = "📎 " + System.IO.Path.GetFileName(PathString); AttachedFileBadge.Visibility = Visibility.Visible; } }
        private void AttachFolder(string PathString) { if (Directory.Exists(PathString)) { AttachedFilePath = PathString; IsFolderContext = true; AttachedFileText.Text = "📁 " + System.IO.Path.GetFileName(PathString); AttachedFileBadge.Visibility = Visibility.Visible; } }
        private void RemoveAttachment() { AttachedFilePath = null; AttachedFileBadge.Visibility = Visibility.Collapsed; }
        private void ToggleConsole() { IsConsoleExpanded = !IsConsoleExpanded; ConsoleTextBox.Height = IsConsoleExpanded ? 120 : 0; ConsoleTextBox.Visibility = IsConsoleExpanded ? Visibility.Visible : Visibility.Collapsed; ConsoleToggleBtn.Content = IsConsoleExpanded ? "Hide Console («)" : "Show Console (»)"; ScrollViewerPrivate.ScrollToBottom(); }
        private void StartNewChatSession() { ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear(); RemoveAttachment(); DeleteActiveChatHistory(); AddMessageBubble("✨ New Chat Session started!", IsAi: true); PersistentStateManager.SaveHistory("Chat", "Started new session."); }
        private void ToggleHistoryDrawer() { if (HistoryContainer.Visibility == Visibility.Collapsed) { PopulateHistoryList(); HistoryContainer.Visibility = Visibility.Visible; } else HistoryContainer.Visibility = Visibility.Collapsed; }
        private void PopulateHistoryList() { HistoryListBox.Items.Clear(); string Dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (Directory.Exists(Dir)) { var Files = Directory.GetFiles(Dir, "ChatLog_*.txt"); Array.Sort(Files, (A, B) => File.GetLastWriteTime(B).CompareTo(File.GetLastWriteTime(A))); foreach (var F in Files) HistoryListBox.Items.Add(Path.GetFileName(F)); } }
        private void LoadPastChatLog(string FileName) { try { string PathString = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations", FileName); if (!File.Exists(PathString)) return; string Text = File.ReadAllText(PathString); ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear(); HistoryContainer.Visibility = Visibility.Collapsed; string[] Turns = Text.Split("==========================================================================", StringSplitOptions.RemoveEmptyEntries); foreach (var Turn in Turns) { int UIdx = Turn.IndexOf("USER: "), JIdx = Turn.IndexOf("JARVIS: "); if (UIdx >= 0 && JIdx > UIdx) { string U = Turn.Substring(UIdx + 6, JIdx - (UIdx + 6)).Trim(); string J = Turn.Substring(JIdx + 8).Trim(); if (!string.IsNullOrEmpty(U)) AddMessageBubble(U, false); if (!string.IsNullOrEmpty(J)) AddMessageBubble(J, true); ConversationHistory.Add(new ChatTurn { Role = "user", Text = U }); ConversationHistory.Add(new ChatTurn { Role = "model", Text = J }); } } } catch { } }

        private bool IsFolderContext = false;
        private readonly List<ChatTurn> ConversationHistory = new List<ChatTurn>();

        public static async Task SubmitTextMessage(string Message)
        {
            if (string.IsNullOrWhiteSpace(Message) || Regex.IsMatch(Message, @"^[\.\s]+$")) return;

            // --- SLASH COMMANDS (Local High-Priority) ---
            string cmd = Message.Trim().ToLower();
            if (cmd.StartsWith("/"))
            {
                if (Instance == null) return;

                if (cmd == "/clear" || cmd == "/cls")
                {
                    Instance.ConversationHistory.Clear();
                    Instance.ChatHistoryPanel.Children.Clear();
                    LogConsoleAction("Chat System", "Conversation history wiped.");
                    Instance.AddMessageBubble("Conversation history cleared. Ready for a fresh start.", true);
                    return;
                }
                if (cmd == "/restart" || cmd == "/reset")
                {
                    NativeMethods.Restart(freshBoot: false);
                    return;
                }
                if (cmd == "/rebuild")
                {
                    NativeMethods.Restart(freshBoot: true);
                    return;
                }
                if (cmd.StartsWith("/learnsound "))
                {
                    string name = Message.Substring(12).Trim();
                    _ = Task.Run(async () => await VoiceActivationManager.LearnEnvironmentalSoundAsync(name));
                    return;
                }
                if (cmd == "/resetvoice")
                {
                    VoiceDatasetManager.ResetDatabase();
                    VoiceTrainerManager.ResetProfile();
                    Instance.AddMessageBubble("Voice database and official profile have been completely reset.", true);
                    LogConsoleAction("Voice System", "Voice database wiped.");
                    return;
                }
                if (cmd == "/notes")
                {
                    Application.Current.Dispatcher.Invoke(() => NoteManagerOverlay.ShowOverlay());
                    return;
                }
                if (cmd.StartsWith("/model "))
                {
                    string model = Message.Substring(7).Trim();
                    SettingsManager.Current.GEMINI_MODEL = model;
                    SettingsManager.Save();
                    LogConsoleAction("LLM Config", $"Active model set to: {model}");
                    Instance.AddMessageBubble($"AI model switched to: {model}", true);
                    return;
                }
                if (cmd == "/help")
                {
                    Instance.AddMessageBubble(
                        "**JARVIS SLASH COMMANDS:**\n" +
                        "• `/clear` - Wipe chat history\n" +
                        "• `/restart` - Relaunch Jarvis\n" +
                        "• `/rebuild` - Clean build & restart\n" +
                        "• `/notes` - Open Notes Studio\n" +
                        "• `/learnsound <name>` - Teach Jarvis a new sound (clap, snap, etc.)\n" +
                        "• `/resetvoice` - Wipe the entire voice and sound database\n" +
                        "• `/model <name>` - Switch AI model\n" +
                        "• `/voice on|off` - Toggle Voice Mode", true);
                    return;
                }
            }

            CancelActiveTurn();

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
            if (string.IsNullOrEmpty(Trimmed) || Regex.IsMatch(Trimmed, @"^[\.\s]+$")) return;

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

        private static void CancelActiveTurn()
        {
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
            var (AiBorder, AiTextBlock, AiDebugBlock) = AddMessageBubbleWithControl("🧠 Thinking...", IsAi: true, IsItalic: true);
            SetStatus("THINKING", Brushes.Yellow);

            if (AiDebugBlock != null)
            {
                var sbDebug = new System.Text.StringBuilder();
                sbDebug.AppendLine($">>> [BRAIN TIMELINE - {DateTime.Now:HH:mm:ss.fff}]");
                sbDebug.AppendLine($"[INIT] Input: {source} | Backend: {SettingsManager.Current.LLM_BACKEND}");

                // --- CONTEXT GATHERING ---
                sbDebug.AppendLine("\n[CONTEXT GATHERING]");
                try {
                    string activeWin = MemoryManager.GetCurrentWindowTitle();
                    sbDebug.AppendLine($"  - Window: '{activeWin}'");

                    int memBytes = SemanticMemoryManager.GetMemoryContextForAi().Length;
                    sbDebug.AppendLine($"  - Memory: {memBytes} bytes injected");

                    var recent = ActionJournalManager.GetRecentActions(1);
                    if (recent.Any()) sbDebug.AppendLine($"  - Last Action: {recent[0].ActionType}");
                } catch { }

                sbDebug.AppendLine("\n[FULL PROMPT]");
                sbDebug.AppendLine(ApiMessage.Length > 800 ? ApiMessage.Substring(0, 800) + "..." : ApiMessage);

                sbDebug.AppendLine("\n[LLM] Requesting model response...");
                AiDebugBlock.Text = sbDebug.ToString();
            }

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
                        FinalResult = await InterceptAndExecuteCommandsAsync(FinalResult, AiTextBlock, (StackPanel)AiBorder.Child, ct, AiDebugBlock);
                    }

                    ct.ThrowIfCancellationRequested();
                    AiTextBlock.FontStyle = FontStyles.Normal;
                    if (AiDebugBlock != null)
                    {
                        // Extract model info if present
                        string usedModel = "Unknown";
                        var modelMatch = Regex.Match(RawResponse, @"\[METADATA_MODEL:\s*(?<m>.+?)\]");
                        if (modelMatch.Success) {
                            usedModel = modelMatch.Groups["m"].Value;
                            RawResponse = RawResponse.Replace(modelMatch.Value, "").Trim();
                        }

                        var sbFinal = new System.Text.StringBuilder();
                        sbFinal.AppendLine($"\n[LLM RESPONSE RECEIVED - {usedModel}]");
                        sbFinal.AppendLine(RawResponse);
                        sbFinal.AppendLine("\n[FINAL SYNTHESIZED OUTPUT]");
                        sbFinal.AppendLine(FinalResult);
                        sbFinal.AppendLine("\n" + AiDebugBlock.Text);
                        AiDebugBlock.Text = sbFinal.ToString();
                    }
                    RenderBubbleContent((StackPanel)AiBorder.Child, AiTextBlock, !string.IsNullOrWhiteSpace(FinalResult) ? FinalResult : RawResponse);
                }
                else
                {
                    RawResponse = await Task.Run(async () => await LlmRouter.AskAsync(ApiMessage, Snapshot, ct), ct);

                    // Extract model info
                    string usedModel = "Unknown";
                    var modelMatch = Regex.Match(RawResponse, @"\[METADATA_MODEL:\s*(?<m>.+?)\]");
                    if (modelMatch.Success) {
                        usedModel = modelMatch.Groups["m"].Value;
                        RawResponse = RawResponse.Replace(modelMatch.Value, "").Trim();
                    }

                    FinalResult = AgentExecutor.ProcessAIResponse(RawResponse);

                    if (FinalResult.Contains("[EXEC_PS:") || FinalResult.Contains("[EXEC_SHELL:") || (FinalResult.Contains("[RUN_COMMAND:") && (FinalResult.Contains("download") || FinalResult.Contains("scrape") || FinalResult.Contains("search") || FinalResult.Contains("google") || FinalResult.Contains("websearch"))))
                    {
                        FinalResult = await InterceptAndExecuteCommandsAsync(FinalResult, AiTextBlock, (StackPanel)AiBorder.Child, ct, AiDebugBlock);
                    }

                    ct.ThrowIfCancellationRequested();
                    AiTextBlock.FontStyle = FontStyles.Normal;
                    if (AiDebugBlock != null)
                    {
                        var sbFinal = new System.Text.StringBuilder();
                        sbFinal.AppendLine($"\n[LLM RESPONSE RECEIVED - {usedModel}]");
                        sbFinal.AppendLine(RawResponse);
                        sbFinal.AppendLine("\n[FINAL SYNTHESIZED OUTPUT]");
                        sbFinal.AppendLine(FinalResult);
                        sbFinal.AppendLine("\n" + AiDebugBlock.Text);
                        AiDebugBlock.Text = sbFinal.ToString();
                    }
                    RenderBubbleContent((StackPanel)AiBorder.Child, AiTextBlock, FinalResult);
                }
            }
            catch (OperationCanceledException) { SetStatus("READY", Brushes.LightGreen); return; }
            catch (Exception Ex) { FinalResult = "⚠️ Error: " + Ex.Message; AiTextBlock.Text = FinalResult; SetStatus("ERROR", Brushes.Red); }
            finally { ThinkingTimer.Stop(); }

            SetStatus("READY", Brushes.LightGreen);

            if (ct.IsCancellationRequested) return;

            // --- FINAL CLEANUP & ECHO PROTECTION ---
            // 1. Extract Spoken Shorthand (@say{...} or @say text)
            string extractedSpeech = "";
            // Regex to catch @say{...} or @say Text until end of line
            var sayMatches = Regex.Matches(RawResponse, @"@say(?:\{(?<t>.*?)\}|\s+(?<t>.*?)(?:\n|@|$))", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match m in sayMatches)
            {
                string t = m.Groups["t"].Value.Trim().Trim('{', '}');
                if (!string.IsNullOrEmpty(t)) extractedSpeech += t + " ";
            }
            extractedSpeech = extractedSpeech.Trim();

            // 2. Sanitize the primary response
            string sanitizedResult = FinalResult; // FinalResult is already sanitized by AiAPI
            bool skipSpeech = false;

            // 3. Smart Fallback for empty/action-only responses
            bool isNoise = string.IsNullOrWhiteSpace(sanitizedResult) || Regex.IsMatch(sanitizedResult, @"^[\.\s\?\!]+$");

            // --- SMART FALLBACK & ANTI-SPAM ---
            if (isNoise)
            {
                if (!string.IsNullOrEmpty(extractedSpeech))
                {
                    sanitizedResult = extractedSpeech;
                    skipSpeech = true;
                }
                else
                {
                    bool hadActions = !string.IsNullOrEmpty(RawResponse) && (RawResponse.Contains("@") || (RawResponse.Contains("[") && !RawResponse.Contains("[METADATA")));

                    if (hadActions)
                    {
                        if (RawResponse.Contains("@snap") || RawResponse.Contains("[TAKE_SCREENSHOT]")) sanitizedResult = "Captured your screen.";
                        else if (RawResponse.Contains("@clip") || RawResponse.Contains("[SET_CLIPBOARD")) sanitizedResult = "Updated your clipboard.";
                        else sanitizedResult = "System action performed.";

                        skipSpeech = true;
                    }
                    else if (source == "TEXT")
                    {
                        string[] fallbacks = { "Acknowledged.", "Done.", "I've handled that.", "Ready." };
                        sanitizedResult = fallbacks[new Random().Next(fallbacks.Length)];
                    }
                }
            }

            // Always clear the Thinking state even on errors
            SetStatus("READY", Brushes.LightGreen);
            AiTextBlock.FontStyle = FontStyles.Normal;
            if (AiDebugBlock != null) AiDebugBlock.Text += "\n[SYSTEM] Interaction Complete.";

            // 4. Reject echoes ONLY for Voice input
            double echoSimilarity = SearchUtil.GetSimilarity(sanitizedResult.ToLower(), Message.ToLower());
            bool isEcho = sanitizedResult.Equals(Message, StringComparison.OrdinalIgnoreCase) || (echoSimilarity > 0.9);

            if (source == "VOICE" && (isEcho || string.IsNullOrEmpty(sanitizedResult)))
            {
                Application.Current.Dispatcher.Invoke(() => ChatHistoryPanel.Children.Remove(AiBorder));
                return;
            }

            // 5. Final Visibility Check
            if (string.IsNullOrEmpty(sanitizedResult))
            {
                Application.Current.Dispatcher.Invoke(() => ChatHistoryPanel.Children.Remove(AiBorder));
                return;
            }

            // 6. Show the result in the bubble
            RenderBubbleContent((StackPanel)AiBorder.Child, AiTextBlock, sanitizedResult);

            // 7. Speak the result if not already spoken by tags
            if (!skipSpeech)
            {
                _ = Task.Run(() => TtsManager.Speak(sanitizedResult, isShortSpeech: true));
            }

            if (!sanitizedResult.StartsWith("⚠️"))
            {
                ConversationHistory.Add(new ChatTurn { Role = "user", Text = Message });
                ConversationHistory.Add(new ChatTurn { Role = "model", Text = sanitizedResult });
                LogConversationTurn(Message, sanitizedResult);
                SaveActiveChatHistory();

                // Background Task: Emotional Analysis & Fact Extraction
                _ = Task.Run(async () =>
                {
                    await EmotionalContextManager.AnalyzeSentimentAsync(Message);
                    await SemanticMemoryManager.ExtractFactsFromChatAsync(Message, sanitizedResult);
                });
            }
        }

        private async Task<string> InterceptAndExecuteCommandsAsync(string RawResponseString, TextBlock AiTextBlock, StackPanel BubblePanel, CancellationToken ct, TextBox? AiDebugBlock = null)
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
                    if (AiDebugBlock != null) AiDebugBlock.Text += $"\n>>> [EXECUTING POWERSHELL]\n{ParameterString}";
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
                    if (AiDebugBlock != null) AiDebugBlock.Text += $"\n>>> [EXECUTING SHELL]\n{ParameterString}";
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
                    if (AiDebugBlock != null) AiDebugBlock.Text += $"\n>>> [INTERCEPTED WEB ACTION: {CmdTypeString}]\n{ParameterString}";
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

            if (AiDebugBlock != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AiDebugBlock.Text += $"\n>>> [TOOL OUTPUT RECEIVED]\n{ExecResult.Substring(0, Math.Min(ExecResult.Length, 500))}";
                });
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

        private static void LogConversationTurn(string U, string J) { try { string Dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Conversations"); if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir); File.AppendAllText(Path.Combine(Dir, $"ChatLog_{DateTime.Now:yyyy-MM-dd}.txt"), $"\nUSER: {U}\nJARVIS: {J}\n" + new string('=', 60) + "\n"); } catch { } }

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

        private (Border Border, TextBlock TextContent, TextBox DebugContent) AddMessageBubbleWithControl(string Text, bool IsAi, bool IsItalic = false, string? RawResponse = null)
        {
            var BubbleBorder = new Border { Background = IsAi ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(64, 128, 80, 230)), CornerRadius = IsAi ? new CornerRadius(12, 12, 12, 0) : new CornerRadius(12, 12, 0, 12), Margin = IsAi ? new Thickness(0, 4, 48, 4) : new Thickness(48, 4, 0, 4), HorizontalAlignment = IsAi ? HorizontalAlignment.Left : HorizontalAlignment.Right, Padding = new Thickness(12, 10, 12, 10), MaxWidth = 300 };

            var outerStack = new StackPanel();
            BubbleBorder.Child = outerStack;

            var mainStack = new StackPanel();
            outerStack.Children.Add(mainStack);

            var Tb = new TextBlock
            { 
                FontSize = 13, 
                FontFamily = new FontFamily("Segoe UI"), 
                TextWrapping = TextWrapping.Wrap, 
                FontStyle = IsItalic ? FontStyles.Italic : FontStyles.Normal
            };
            Tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            mainStack.Children.Add(Tb);

            TextBox debugText = null!;

            // --- BUBBLE ACTION BUTTONS (AI ONLY) ---
            if (IsAi)
            {
                var actionStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 0), Opacity = 0.6 };

                var copyBtn = new Button { Content = "📋 Copy", FontSize = 9, Padding = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
                copyBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                copyBtn.Click += (s, e) => { try { Clipboard.SetText(Tb.Text); TextOverlay.Show("Copied to clipboard", 1500); } catch { } };

                var speakBtn = new Button { Content = "🔊 Repeat", FontSize = 9, Padding = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(5, 0, 0, 0) };
                speakBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                speakBtn.Click += (s, e) => TtsManager.Speak(Tb.Text, isShortSpeech: false);

                // --- DEBUG DETAILS TOGGLE ---
                var debugPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 8, 0, 0),
                    CornerRadius = new CornerRadius(4),
                    Visibility = Visibility.Collapsed,
                    BorderThickness = new Thickness(0, 1, 0, 0)
                };
                debugPanel.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

                var debugContent = new StackPanel();
                var debugTitle = new TextBlock { Text = "DEBUG TRACE", FontSize = 9, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4), Opacity = 0.7 };
                debugTitle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                debugContent.Children.Add(debugTitle);

                debugText = new TextBox
                {
                    Text = RawResponse ?? Text,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    IsReadOnly = true,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8
                };
                debugText.SetResourceReference(TextBox.ForegroundProperty, "TextSecondaryBrush");
                debugContent.Children.Add(debugText);
                debugPanel.Child = debugContent;

                var detailsBtn = new Button { Content = "⌄ Details", FontSize = 9, Padding = new Thickness(4, 1, 4, 1), Cursor = Cursors.Hand, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(5, 0, 0, 0) };
                detailsBtn.SetResourceReference(Button.ForegroundProperty, "TextSecondaryBrush");
                detailsBtn.Click += (s, e) =>
                {
                    bool isVisible = debugPanel.Visibility == Visibility.Visible;
                    debugPanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                    detailsBtn.Content = isVisible ? "⌄ Details" : "⌃ Details";
                    if (!isVisible) ScrollViewerPrivate.ScrollToBottom();
                };

                actionStack.Children.Add(copyBtn);
                actionStack.Children.Add(speakBtn);
                actionStack.Children.Add(detailsBtn);
                outerStack.Children.Add(actionStack);
                outerStack.Children.Add(debugPanel);
            }

            RenderBubbleContent(mainStack, Tb, Text);
            ChatHistoryPanel.Children.Add(BubbleBorder);
            ScrollViewerPrivate.ScrollToBottom();
            return (BubbleBorder, Tb, debugText);
        }

        private void RenderBubbleContent(StackPanel Container, TextBlock MainText, string Text)
        {
            string cleanText = Text;
            string usageInfo = "";

            // Extract usage metadata if present
            var usageMatch = Regex.Match(Text, @"\[METADATA_USAGE:\s*(\d+),(\d+),(\d+)\]");
            if (usageMatch.Success)
            {
                usageInfo = $"Tokens: {usageMatch.Groups[1].Value} prompt, {usageMatch.Groups[2].Value} response ({usageMatch.Groups[3].Value} total)";
                cleanText = Text.Replace(usageMatch.Value, "").Trim();
            }

            // --- NOISE FILTER ---
            // Only collapse the bubble if it's truly empty AND NOT a manual user interaction
            if (string.IsNullOrWhiteSpace(cleanText) || Regex.IsMatch(cleanText, @"^[\.\s\?\!]+$"))
            {
                // If it's a direct AI response bubble (already visible with "Thinking"), we should show a placeholder instead of hiding
                if (MainText.Text == "🧠 Thinking..." || MainText.Text == "⏳ Loading model...")
                {
                    cleanText = "Done.";
                }
                else
                {
                    if (Container.Parent is Border border) border.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            MainText.Text = cleanText;

            if (cleanText.Contains("```"))
            {
                MainText.Visibility = Visibility.Collapsed;
                // Preserve the usage label if we re-render
                for (int I = Container.Children.Count - 1; I >= 0; I--)
                    if (Container.Children[I] != MainText && !(Container.Children[I] is TextBlock tb && tb.Tag?.ToString() == "usage"))
                        Container.Children.RemoveAt(I);

                var Parts = ParseMessageParts(cleanText);
                foreach (var Part in Parts)
                {
                    if (Part.IsCode)
                    {
                        var CodeBdr = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 4), CornerRadius = new CornerRadius(4) };
                        var CodeTb = new TextBlock
                        { 
                            Text = Part.Content.Trim(),
                            FontSize = 12, 
                            FontFamily = new FontFamily("Consolas"), 
                            TextWrapping = TextWrapping.Wrap
                        };
                        CodeTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                        CodeBdr.Child = CodeTb;
                        Container.Children.Add(CodeBdr);
                    }
                    else if (!string.IsNullOrWhiteSpace(Part.Content))
                    {
                        var TbPart = new TextBlock
                        { 
                            Text = Part.Content.Trim(),
                            FontSize = 13, 
                            FontFamily = new FontFamily("Segoe UI"), 
                            TextWrapping = TextWrapping.Wrap, 
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        TbPart.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                        Container.Children.Add(TbPart);
                    }
                }
            }
            else MainText.Visibility = Visibility.Visible;

            // Append Usage Info Label (Subtle)
            if (!string.IsNullOrEmpty(usageInfo))
            {
                var usageLabel = new TextBlock
                {
                    Text = usageInfo,
                    FontSize = 8,
                    Opacity = 0.4,
                    Margin = new Thickness(0, 5, 0, 0),
                    Tag = "usage",
                    FontStyle = FontStyles.Italic
                };
                usageLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                Container.Children.Add(usageLabel);
            }
        }

        private static List<MessagePart> ParseMessageParts(string Text) { var Parts = new List<MessagePart>(); if (string.IsNullOrEmpty(Text)) return Parts; int Idx = 0; while (Idx < Text.Length) { int SIdx = Text.IndexOf("```", Idx); if (SIdx == -1) { Parts.Add(new MessagePart { IsCode = false, Content = Text.Substring(Idx) }); break; } if (SIdx > Idx) Parts.Add(new MessagePart { IsCode = false, Content = Text.Substring(Idx, SIdx - Idx) }); int EIdx = Text.IndexOf("```", SIdx + 3); if (EIdx == -1) { Parts.Add(new MessagePart { IsCode = true, Content = Text.Substring(SIdx + 3) }); break; } string Code = Text.Substring(SIdx + 3, EIdx - (SIdx + 3)); string Lang = ""; int Nl = Code.IndexOf('\n'); if (Nl != -1) { Lang = Code.Substring(0, Nl).Trim(); Code = Code.Substring(Nl + 1); } Parts.Add(new MessagePart { IsCode = true, Language = Lang, Content = Code }); Idx = EIdx + 3; } return Parts; }
        private Border AddMessageBubble(string T, bool IsAi) { return AddMessageBubbleWithControl(T, IsAi).Border; }
        private class MessagePart { public bool IsCode { get; set; } public string Language { get; set; } = ""; public string Content { get; set; } = ""; }
    }
}
