// Developer: heaplyn
// Date: 2026-08-19
// Summary: Advanced Glassmorphic AI Chat Companion.
//          Added: Model Selector, Markdown-ish Rich Text Support, Session Management.

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
using System.Windows.Documents;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace JarvisLauncher
{
    public class ChatOverlay : BaseOverlay
    {
        private static ChatOverlay? Instance;
        private static readonly System.Text.StringBuilder ConsoleLog = new System.Text.StringBuilder();
        private static CancellationTokenSource? _activeCts;

        private StackPanel ChatHistoryPanel = null!;
        private ScrollViewer ScrollViewerPrivate = null!;
        private TextBox InputTextBox = null!;
        private TextBlock StatusText = null!;
        private Ellipse StatusDot = null!;
        private Border AttachedFileBadge = null!;
        private TextBlock AttachedFileText = null!;
        private string? AttachedFilePath = null;
        private Border HistoryContainer = null!;
        private ListBox HistoryListBox = null!;
        private TextBox ConsoleTextBox = null!;
        private Button ConsoleToggleBtn = null!;
        private Border ConsoleBorder = null!;
        private ComboBox ModelSelector = null!;
        private Button StopBtn = null!;
        private bool IsConsoleExpanded = false;
        private readonly List<ChatTurn> ConversationHistory = new List<ChatTurn>();

        public new static bool IsVisible => Instance != null && Instance.Visibility == Visibility.Visible && Instance.Opacity > 0.1;

        public static void ShowChat() {
            Application.Current.Dispatcher.Invoke(() => {
                if (Instance == null || !Instance.IsLoaded) { Instance = new ChatOverlay(); Instance.Closed += (S, E) => Instance = null; }
                Instance.Show(); Instance.BringToFront();
            });
        }

        public static void ShowOverlay() => ShowChat();

        public static void LogConsoleAction(string ActionName, string Details) {
            string LogLine = $"[{DateTime.Now:HH:mm:ss}] {ActionName.ToUpper()}\n{Details}\n----------------------------------\n";
            lock (ConsoleLog) {
                if (ConsoleLog.Length > 100000) ConsoleLog.Remove(0, 50000); // Prevent memory leak in logs
                ConsoleLog.AppendLine(LogLine);
            }
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                if (Instance?.ConsoleTextBox != null) {
                    Instance.ConsoleTextBox.Text = ConsoleLog.ToString();
                    Instance.ConsoleTextBox.ScrollToEnd();
                }
            }));
        }

        private ChatOverlay() : base("JARVIS AI COMPANION", 480, 720) {
            var WorkArea = SystemParameters.WorkArea; this.Left = WorkArea.Width - this.Width - 20; this.Top = WorkArea.Top + 40;
            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Console
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input

            // --- Enhanced Toolbar ---
            var tb = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var st = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            StatusDot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.LightGreen, Margin = new Thickness(0,0,6,0) };
            StatusText = new TextBlock { Text = "READY", FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
            st.Children.Add(StatusDot); st.Children.Add(StatusText); tb.Children.Add(st);

            var toolStack = new StackPanel { Orientation = Orientation.Horizontal };

            ModelSelector = new ComboBox { Width = 110, Height = 22, FontSize = 10, Margin = new Thickness(0,0,8,0), Background = new SolidColorBrush(Color.FromArgb(40, 0,0,0)), Foreground = Brushes.Cyan, BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray };
            ModelSelector.Items.Add("Gemini"); ModelSelector.Items.Add("OpenAI"); ModelSelector.Items.Add("Anthropic"); ModelSelector.Items.Add("Groq"); ModelSelector.Items.Add("Ollama");
            ModelSelector.SelectedItem = CoreRegistry.Data.Settings.Current.LLM_BACKEND;
            ModelSelector.SelectionChanged += (s, e) => {
                if (ModelSelector.SelectedItem is string sel) {
                    SettingsManager.Current.LLM_BACKEND = sel;
                    SettingsManager.Save();
                    DebugConsoleOverlay.Log("AI-Switch", $"Backend changed to: {sel}");
                }
            };
            toolStack.Children.Add(ModelSelector);

            var historyBtn = CreateToolbarButton("📜 LOGS", (s, e) => { RefreshHistoryList(); HistoryContainer.Visibility = HistoryContainer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; });
            toolStack.Children.Add(historyBtn);

            var newChatBtn = CreateToolbarButton("🆕 NEW CHAT", (s, e) => { StartNewSession(); });
            toolStack.Children.Add(newChatBtn);

            var systemBtn = CreateToolbarButton("⚙️ SYSTEM", (s, e) => {
                string prompt = AiAPI.GetCompactSystemPrompt();
                MessageBox.Show(prompt, "JARVIS SYSTEM PROMPT", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            toolStack.Children.Add(systemBtn);

            Grid.SetColumn(toolStack, 2); tb.Children.Add(toolStack);
            Grid.SetRow(tb, 0); root.Children.Add(tb);

            // --- Chat Display ---
            var chatGrid = new Grid();
            ScrollViewerPrivate = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            ChatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            ScrollViewerPrivate.Content = ChatHistoryPanel;

            HistoryContainer = new Border { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Color.FromArgb(250, 10, 10, 20)), Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.Cyan, Margin = new Thickness(15), VerticalAlignment = VerticalAlignment.Top };
            var historyStack = new StackPanel();
            historyStack.Children.Add(new TextBlock { Text = "ARCHIVED MISSIONS", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, Margin = new Thickness(0,0,0,12) });
            HistoryListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, MaxHeight = 400 };
            HistoryListBox.SelectionChanged += (s, e) => { if (HistoryListBox.SelectedItem is string log) LoadSession(log); };
            historyStack.Children.Add(HistoryListBox); HistoryContainer.Child = historyStack;

            // --- Quick Actions Panel ---
            var quickActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
            quickActions.Children.Add(CreateQuickActionChip("🚀 OPTIMIZE", "Optimize the current codebase for performance."));
            quickActions.Children.Add(CreateQuickActionChip("🔍 AUDIT", "Run a deep security and logic audit."));
            quickActions.Children.Add(CreateQuickActionChip("📝 DOCS", "Generate technical documentation for the current module."));

            var chatStack = new Grid();
            chatStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            chatStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(quickActions, 0); chatStack.Children.Add(quickActions);
            Grid.SetRow(ScrollViewerPrivate, 1); chatStack.Children.Add(ScrollViewerPrivate);

            chatGrid.Children.Add(chatStack);
            chatGrid.Children.Add(HistoryContainer);

            Grid.SetRow(chatGrid, 1); root.Children.Add(chatGrid);

            // --- Sliding Debug Console ---
            ConsoleBorder = new Border { CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb(50, 0,0,0)), BorderThickness = new Thickness(0,1,0,0), BorderBrush = Brushes.DimGray, Margin = new Thickness(0,10,0,0) };
            var consoleGrid = new Grid();
            consoleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            consoleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            ConsoleToggleBtn = new Button { Content = "SYSTEM LOGS (»)", FontSize = 9, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Gray, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            ConsoleToggleBtn.Click += (s, e) => ToggleConsole();
            Grid.SetRow(ConsoleToggleBtn, 0); consoleGrid.Children.Add(ConsoleToggleBtn);

            ConsoleTextBox = new TextBox { Height = 0, Visibility = Visibility.Collapsed, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 10, Background = Brushes.Transparent, Foreground = Brushes.Lime, BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap, Opacity = 0.8, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            ConsoleTextBox.Text = ConsoleLog.ToString();
            Grid.SetRow(ConsoleTextBox, 1); consoleGrid.Children.Add(ConsoleTextBox);
            ConsoleBorder.Child = consoleGrid;
            Grid.SetRow(ConsoleBorder, 2); root.Children.Add(ConsoleBorder);

            // --- Input Area ---
            var inpStack = new StackPanel { Margin = new Thickness(0,10,0,0) };
            AttachedFileBadge = new Border { Visibility = Visibility.Collapsed, Padding = new Thickness(8,4,8,4), Background = new SolidColorBrush(Color.FromArgb(100, 0,0,0)), Margin = new Thickness(0,0,0,8), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Brushes.Cyan };
            AttachedFileText = new TextBlock { FontSize = 10, Foreground = Brushes.Cyan }; AttachedFileBadge.Child = AttachedFileText;
            inpStack.Children.Add(AttachedFileBadge);

            var inpGrid = new Grid();
            inpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var addBtn = new Button { Content = "📎", Width = 38, Height = 38, Margin = new Thickness(0,0,8,0), Cursor = Cursors.Hand, FontSize = 18, Background = new SolidColorBrush(Color.FromArgb(30, 255,255,255)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            addBtn.Click += (s, e) => { var dlg = new Microsoft.Win32.OpenFileDialog(); if(dlg.ShowDialog()==true) { AttachedFilePath = dlg.FileName; AttachedFileText.Text = "ATTACHED: " + Path.GetFileName(dlg.FileName).ToUpper(); AttachedFileBadge.Visibility = Visibility.Visible; } };
            Grid.SetColumn(addBtn, 0); inpGrid.Children.Add(addBtn);

            var boxGrid = new Grid();
            InputTextBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 38, MaxHeight = 150, Padding = new Thickness(12,8,12,8), FontSize = 14, Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Foreground = Brushes.White, BorderBrush = Brushes.DimGray, CaretBrush = Brushes.Cyan, BorderThickness = new Thickness(1) };
            var placeholder = new TextBlock { Text = "Command Jarvis...", Foreground = Brushes.Gray, IsHitTestVisible = false, Margin = new Thickness(15,10,0,0), FontSize = 14 };
            InputTextBox.TextChanged += (s, e) => placeholder.Visibility = string.IsNullOrEmpty(InputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            InputTextBox.PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; SendCurrentInput(); } };
            boxGrid.Children.Add(InputTextBox); boxGrid.Children.Add(placeholder);
            Grid.SetColumn(boxGrid, 1); inpGrid.Children.Add(boxGrid);

            StopBtn = new Button { Content = "⏹", Width = 38, Height = 38, Margin = new Thickness(8,0,0,0), Cursor = Cursors.Hand, FontSize = 18, Background = Brushes.DarkRed, Foreground = Brushes.White, BorderThickness = new Thickness(0), Visibility = Visibility.Collapsed };
            StopBtn.Click += (s, e) => { _activeCts?.Cancel(); };
            Grid.SetColumn(StopBtn, 2); inpGrid.Children.Add(StopBtn);

            inpStack.Children.Add(inpGrid);
            Grid.SetRow(inpStack, 3); root.Children.Add(inpStack);

            this.UserContent = root;
            LoadLastSession();
        }

        private void SendCurrentInput() {
            string m = InputTextBox.Text.Trim();
            if(!string.IsNullOrEmpty(m) || AttachedFilePath != null) {
                _ = SendUserMessage(m);
                InputTextBox.Text = "";
            }
        }

        private Button CreateQuickActionChip(string text, string prompt) {
            var b = new Button { Content = text, FontSize = 9, Padding = new Thickness(10,4,10,4), Margin = new Thickness(0,0,8,0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromArgb(50, 0, 255, 255)), Foreground = Brushes.White, BorderThickness = new Thickness(1), BorderBrush = Brushes.Cyan };
            b.Click += (s, e) => { _ = SendUserMessage(prompt); };
            return b;
        }

        private Button CreateToolbarButton(string text, RoutedEventHandler onClick) {
            var b = new Button { Content = text, FontSize = 9, Padding = new Thickness(8,3,8,3), Margin = new Thickness(0,0,6,0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromArgb(40, 255,255,255)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            b.Click += onClick; return b;
        }

        private void ToggleConsole() {
            IsConsoleExpanded = !IsConsoleExpanded;
            ConsoleTextBox.Height = IsConsoleExpanded ? 200 : 0;
            ConsoleTextBox.Visibility = IsConsoleExpanded ? Visibility.Visible : Visibility.Collapsed;
            ConsoleToggleBtn.Content = IsConsoleExpanded ? "HIDE SYSTEM LOGS («)" : "SHOW SYSTEM LOGS (»)";
            ScrollViewerPrivate.ScrollToBottom();
        }

        private void StartNewSession() {
            if (ConversationHistory.Count > 0) SaveCurrentSession();
            ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear();
            AddMessageBubble("Synchronized. Standing by for new instructions, Sir.", true);
        }

        private void RefreshHistoryList() {
            HistoryListBox.Items.Clear();
            string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTime)) HistoryListBox.Items.Add(Path.GetFileName(f));
        }

        private void LoadSession(string fileName) {
            string path = Path.Combine(PathHandler.GetDataDirectory(), "Conversations", fileName);
            if (!File.Exists(path)) return;
            ChatHistoryPanel.Children.Clear(); ConversationHistory.Clear();
            try {
                var turns = JsonSerializer.Deserialize<List<ChatTurn>>(File.ReadAllText(path));
                if (turns != null) foreach (var turn in turns) { ConversationHistory.Add(turn); AddMessageBubble(turn.Text, turn.Role == "model"); }
            } catch { }
            HistoryContainer.Visibility = Visibility.Collapsed;
        }

        private void SaveCurrentSession() {
            try {
                string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations"); Directory.CreateDirectory(dir);
                string name = $"Session_{DateTime.Now:yyyyMMdd_HHmm}.json";
                File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(ConversationHistory));
            } catch { }
        }

        private void LoadLastSession() {
            string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations");
            if (!Directory.Exists(dir)) { AddMessageBubble("Jarvis Online. Operational.", true); return; }
            var last = Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            if (last != null) LoadSession(Path.GetFileName(last)); else AddMessageBubble("Jarvis Online. Operational.", true);
        }

        public static async Task SubmitTextMessage(string msg) { ShowChat(); if (Instance != null) await Instance.SendUserMessage(msg); }
        public static async Task SubmitVoiceCommand(string msg, bool showUi = true) { if (showUi) ShowChat(); if (Instance != null) await Instance.SendUserMessage(msg); }

        private async Task SendUserMessage(string msg) {
            string apiMsg = msg; string displayMsg = msg;
            if (AttachedFilePath != null) {
                apiMsg = $"[FILE: {AttachedFilePath}]\n{msg}";
                displayMsg = $"📎 {Path.GetFileName(AttachedFilePath).ToUpper()}\n{msg}";
                AttachedFilePath = null; AttachedFileBadge.Visibility = Visibility.Collapsed;
            }

            AddMessageBubble(displayMsg, false);
            ChronoLogManager.LogEvent("Chat", $"User: {msg}");
            ScrollViewerPrivate.ScrollToBottom();

            var (bdr, rt, dbg) = AddMessageBubbleWithControls("Thinking...", true);
            StatusText.Text = "THINKING"; StatusDot.Fill = Brushes.Yellow; StopBtn.Visibility = Visibility.Visible;
            ScrollViewerPrivate.ScrollToBottom();

            try {
                var cts = new CancellationTokenSource(); _activeCts = cts;
                string aiRaw = "";

                if (LlmRouter.IsStreamingBackend(CoreRegistry.Data.Settings.Current.LLM_BACKEND)) {
                    // Stream tokens live into the bubble for Ollama + all OpenAI-compatible providers.
                    Application.Current.Dispatcher.Invoke(() => { try { rt.Document.Blocks.Clear(); } catch { } });
                    aiRaw = await LlmRouter.AskStreamAsync(apiMsg, ConversationHistory, t => Application.Current.Dispatcher.Invoke(() => AppendToRichText(rt, t)), cts.Token);
                } else {
                    // Inject MCP Context for tool discovery
                    string mcpContext = "[MCP ENABLED]\nServers: " + string.Join(", ", McpManager.Servers.Select(s => s.Name)) + "\n";
                    mcpContext += "To use a tool: [CALL_MCP_TOOL: Server | Tool | {args}]\n\n";

                    aiRaw = await AiAPI.AskAgentAsync(mcpContext + apiMsg, ConversationHistory, cts.Token);

                    // Check for Tool Call
                    var match = Regex.Match(aiRaw, @"\[CALL_MCP_TOOL:\s*(?<server>.*?)\s*\|\s*(?<tool>.*?)\s*\|\s*(?<args>.*?)\]");
                    if (match.Success)
                    {
                        string server = match.Groups["server"].Value.Trim();
                        string tool = match.Groups["tool"].Value.Trim();
                        string argsJson = match.Groups["args"].Value.Trim();

                        Application.Current.Dispatcher.Invoke(() => {
                            AppendToRichText(rt, $"\n\n⚡ [MCP PROTOCOL] Calling {tool} on {server}...");
                        });

                        try {
                            var args = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson) ?? new();
                            string toolResult = await McpManager.CallToolAsync(server, tool, args);

                            // Re-ask AI with result
                            aiRaw = await AiAPI.AskAgentAsync($"[MCP TOOL RESULT]\nResult: {toolResult}\n\nContinue mission.", ConversationHistory, cts.Token);
                        } catch { }
                    }

                    FormatRichText(rt, AiAPI.SanitizeText(aiRaw));
                }

                StatusText.Text = "EXECUTING TOOLS"; StatusDot.Fill = Brushes.Orange;
                string processedText = await AgentExecutor.ProcessAIResponseAsync(aiRaw);
                if (!string.IsNullOrEmpty(processedText)) FormatRichText(rt, processedText);

                dbg.Text = "INTERNAL TRACE:\n" + aiRaw;
                ChronoLogManager.LogEvent("Chat", $"Jarvis: {aiRaw}");

                ConversationHistory.Add(new ChatTurn { Role = "user", Text = msg });
                ConversationHistory.Add(new ChatTurn { Role = "model", Text = aiRaw });
                SaveCurrentSession();
            } catch (Exception ex) {
                LogConsoleAction("AI Fault", ex.ToString());
                FormatRichText(rt, "⚠️ THE MISSION HAS ENCOUNTERED AN ERROR. CHECK SYSTEM LOGS.");
            } finally {
                StatusText.Text = "READY"; StatusDot.Fill = Brushes.LightGreen;
                StopBtn.Visibility = Visibility.Collapsed; _activeCts = null; ScrollViewerPrivate.ScrollToBottom();
            }
        }

        private void AppendToRichText(RichTextBox rt, string text) {
            if (new TextRange(rt.Document.ContentStart, rt.Document.ContentEnd).Text.Trim() == "Thinking...") rt.Document.Blocks.Clear();

            var p = rt.Document.Blocks.FirstBlock as Paragraph ?? new Paragraph();
            if (rt.Document.Blocks.Count == 0) rt.Document.Blocks.Add(p);
            p.Inlines.Add(new Run(text));
            ScrollViewerPrivate.ScrollToBottom();
        }

        private void FormatRichText(RichTextBox rt, string text) {
            rt.Document.Blocks.Clear();
            if (string.IsNullOrEmpty(text)) return;

            // Split by code blocks first (triple backticks)
            var blocks = Regex.Split(text, @"(```[\s\S]*?```)");

            foreach (var block in blocks) {
                if (block.StartsWith("```") && block.EndsWith("```")) {
                    string code = block.Trim('`').Trim();
                    var bdr = new Border { Background = new SolidColorBrush(Color.FromArgb(60, 0,0,0)), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Padding = new Thickness(10), Margin = new Thickness(0,5,0,10), CornerRadius = new CornerRadius(4) };
                    var tb = new TextBox { Text = code, FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Brushes.Lime, Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
                    bdr.Child = tb;
                    rt.Document.Blocks.Add(new BlockUIContainer(bdr));
                } else {
                    var p = new Paragraph { Margin = new Thickness(0,0,0,8) };
                    string[] subParts = Regex.Split(block, @"(\*\*.*?\*\*|`.*?`)");
                    foreach (var part in subParts) {
                        if (part.StartsWith("**") && part.EndsWith("**")) {
                            p.Inlines.Add(new Bold(new Run(part.Trim('*'))) { Foreground = Brushes.Cyan });
                        } else if (part.StartsWith("`") && part.EndsWith("`")) {
                            p.Inlines.Add(new Run(part.Trim('`')) { Background = new SolidColorBrush(Color.FromArgb(60, 0,0,0)), FontFamily = new FontFamily("Consolas"), Foreground = Brushes.Lime });
                        } else {
                            p.Inlines.Add(new Run(part));
                        }
                    }
                    rt.Document.Blocks.Add(p);
                }
            }
        }

        private void AddMessageBubble(string t, bool ai) => AddMessageBubbleWithControls(t, ai);

        private (Border, RichTextBox, TextBox) AddMessageBubbleWithControls(string t, bool ai) {
            var b = new Border { Background = ai ? new SolidColorBrush(Color.FromArgb(35, 25, 25, 35)) : new SolidColorBrush(Color.FromArgb(90, 0, 120, 215)), CornerRadius = new CornerRadius(14, 14, ai ? 14 : 2, ai ? 2 : 14), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(ai ? 0 : 50, 8, ai ? 50 : 0, 8), HorizontalAlignment = ai ? HorizontalAlignment.Left : HorizontalAlignment.Right, MaxWidth = 420, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
            var stack = new StackPanel();

            var rt = new RichTextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsReadOnly = true, Foreground = Brushes.White, FontSize = 13.5, FontFamily = new FontFamily("Segoe UI"), VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsHitTestVisible = true };
            rt.Document.PagePadding = new Thickness(0);
            FormatRichText(rt, t);
            stack.Children.Add(rt);

            TextBox dbg = new TextBox { Visibility = Visibility.Collapsed, Margin = new Thickness(0,12,0,0), Background = new SolidColorBrush(Color.FromArgb(140, 0,0,0)), Foreground = Brushes.Gray, FontSize = 10.5, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 350, BorderThickness = new Thickness(0), FontFamily = new FontFamily("Consolas"), Padding = new Thickness(8) };

            if (ai) {
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Opacity = 0.6, Margin = new Thickness(0,6,0,0) };
                var copyBtn = CreateBubbleButton("📋", (s, e) => { try { Clipboard.SetText(new TextRange(rt.Document.ContentStart, rt.Document.ContentEnd).Text); TextOverlay.Show("COPIED", 1000); } catch { } });
                var detailsBtn = CreateBubbleButton("⌄", (s, e) => { dbg.Visibility = dbg.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; (s as Button)!.Content = dbg.Visibility == Visibility.Visible ? "⌃" : "⌄"; if(dbg.Visibility==Visibility.Visible) ScrollViewerPrivate.ScrollToBottom(); });
                btnStack.Children.Add(copyBtn); btnStack.Children.Add(detailsBtn); stack.Children.Add(btnStack); stack.Children.Add(dbg);
            }

            b.Child = stack; ChatHistoryPanel.Children.Add(b); ScrollViewerPrivate.ScrollToBottom();
            return (b, rt, dbg);
        }

        private Button CreateBubbleButton(string icon, RoutedEventHandler click) {
            var b = new Button { Content = icon, FontSize = 10, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, Cursor = Cursors.Hand, Margin = new Thickness(8,0,0,0) };
            b.Click += click; return b;
        }
    }
}
