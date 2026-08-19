
// Developer: heaplyn
// Date: 2026-08-18
// Summary: High-Performance Glassmorphic AI Chat Companion.
//          Enabled text selection/highlighting in bubbles using Read-Only TextBox.
//          Restored sliding debug console with deep development logs.

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
            lock (ConsoleLog) ConsoleLog.AppendLine(LogLine);
            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                if (Instance?.ConsoleTextBox != null) {
                    Instance.ConsoleTextBox.Text = ConsoleLog.ToString();
                    Instance.ConsoleTextBox.ScrollToEnd();
                }
            }));
        }

        private ChatOverlay() : base("JARVIS AI COMPANION", 440, 680) {
            var WorkArea = SystemParameters.WorkArea; this.Left = WorkArea.Width - this.Width - 20; this.Top = WorkArea.Top + 40;
            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Console
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input

            // --- Toolbar ---
            var tb = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tb.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var st = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            StatusDot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.LightGreen, Margin = new Thickness(0,0,6,0) };
            StatusText = new TextBlock { Text = "READY", FontSize = 10, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White };
            st.Children.Add(StatusDot); st.Children.Add(StatusText); tb.Children.Add(st);

            var toolStack = new StackPanel { Orientation = Orientation.Horizontal };
            var historyBtn = new Button { Content = "📜 Log", FontSize = 10, Padding = new Thickness(10,3,10,3), Margin = new Thickness(0,0,6,0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromArgb(30, 255,255,255)), Foreground = Brushes.White };
            historyBtn.Click += (s, e) => { RefreshHistoryList(); HistoryContainer.Visibility = HistoryContainer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; };
            toolStack.Children.Add(historyBtn);

            var clrBtn = new Button { Content = "✨ Clear", FontSize = 10, Padding = new Thickness(10,3,10,3), Cursor = Cursors.Hand, Background = new SolidColorBrush(Color.FromArgb(30, 255,255,255)), Foreground = Brushes.White };
            clrBtn.Click += (s, e) => { StartNewSession(); };
            toolStack.Children.Add(clrBtn);

            Grid.SetColumn(toolStack, 2); tb.Children.Add(toolStack);
            Grid.SetRow(tb, 0); root.Children.Add(tb);

            // --- Chat Display ---
            var chatGrid = new Grid();
            ScrollViewerPrivate = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            ChatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            ScrollViewerPrivate.Content = ChatHistoryPanel; chatGrid.Children.Add(ScrollViewerPrivate);

            HistoryContainer = new Border { Visibility = Visibility.Collapsed, Background = new SolidColorBrush(Color.FromArgb(245, 15, 15, 25)), Padding = new Thickness(12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray, Margin = new Thickness(15), VerticalAlignment = VerticalAlignment.Top };
            var historyStack = new StackPanel();
            historyStack.Children.Add(new TextBlock { Text = "PAST SESSIONS", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan, Margin = new Thickness(0,0,0,12) });
            HistoryListBox = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, MaxHeight = 350 };
            HistoryListBox.SelectionChanged += (s, e) => { if (HistoryListBox.SelectedItem is string log) LoadSession(log); };
            historyStack.Children.Add(HistoryListBox); HistoryContainer.Child = historyStack;
            chatGrid.Children.Add(HistoryContainer);

            Grid.SetRow(chatGrid, 1); root.Children.Add(chatGrid);

            // --- Sliding Debug Console ---
            ConsoleBorder = new Border { CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromArgb(40, 0,0,0)), BorderThickness = new Thickness(0,1,0,0), BorderBrush = Brushes.DimGray, Margin = new Thickness(0,10,0,0) };
            var consoleGrid = new Grid();
            consoleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            consoleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            ConsoleToggleBtn = new Button { Content = "Show Debug Console (»)", FontSize = 9, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Gray, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Left };
            ConsoleToggleBtn.Click += (s, e) => ToggleConsole();
            Grid.SetRow(ConsoleToggleBtn, 0); consoleGrid.Children.Add(ConsoleToggleBtn);

            ConsoleTextBox = new TextBox { Height = 0, Visibility = Visibility.Collapsed, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 10, Background = Brushes.Transparent, Foreground = Brushes.Lime, BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap, Opacity = 0.8, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            ConsoleTextBox.Text = ConsoleLog.ToString();
            Grid.SetRow(ConsoleTextBox, 1); consoleGrid.Children.Add(ConsoleTextBox);
            ConsoleBorder.Child = consoleGrid;
            Grid.SetRow(ConsoleBorder, 2); root.Children.Add(ConsoleBorder);

            // --- Input Area ---
            var inpStack = new StackPanel { Margin = new Thickness(0,10,0,0) };
            AttachedFileBadge = new Border { Visibility = Visibility.Collapsed, Padding = new Thickness(8,4,8,4), Background = new SolidColorBrush(Color.FromArgb(80, 0,0,0)), Margin = new Thickness(0,0,0,8), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Brushes.DimGray };
            AttachedFileText = new TextBlock { FontSize = 10, Foreground = Brushes.Cyan }; AttachedFileBadge.Child = AttachedFileText;
            inpStack.Children.Add(AttachedFileBadge);

            var inpGrid = new Grid();
            inpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inpGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var addBtn = new Button { Content = "➕", Width = 34, Height = 34, Margin = new Thickness(0,0,8,0), Cursor = Cursors.Hand, FontSize = 16 };
            addBtn.Click += (s, e) => { var dlg = new Microsoft.Win32.OpenFileDialog(); if(dlg.ShowDialog()==true) { AttachedFilePath = dlg.FileName; AttachedFileText.Text = "📎 " + Path.GetFileName(dlg.FileName); AttachedFileBadge.Visibility = Visibility.Visible; } };
            Grid.SetColumn(addBtn, 0); inpGrid.Children.Add(addBtn);

            var boxGrid = new Grid();
            InputTextBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 34, MaxHeight = 120, Padding = new Thickness(8,6,8,6), FontSize = 13, Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Foreground = Brushes.White, BorderBrush = Brushes.DimGray, CaretBrush = Brushes.Cyan };
            var placeholder = new TextBlock { Text = "Ask Jarvis... (Enter to send)", Foreground = Brushes.Gray, IsHitTestVisible = false, Margin = new Thickness(12,8,0,0), FontSize = 13 };
            InputTextBox.TextChanged += (s, e) => placeholder.Visibility = string.IsNullOrEmpty(InputTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            InputTextBox.PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; string m = InputTextBox.Text.Trim(); if(!string.IsNullOrEmpty(m) || AttachedFilePath != null) _ = SendUserMessage(m); InputTextBox.Text = ""; } };
            boxGrid.Children.Add(InputTextBox); boxGrid.Children.Add(placeholder);
            Grid.SetColumn(boxGrid, 1); inpGrid.Children.Add(boxGrid);

            inpStack.Children.Add(inpGrid);
            Grid.SetRow(inpStack, 3); root.Children.Add(inpStack);

            this.UserContent = root;
            LoadLastSession();
        }

        private void ToggleConsole() {
            IsConsoleExpanded = !IsConsoleExpanded;
            ConsoleTextBox.Height = IsConsoleExpanded ? 150 : 0;
            ConsoleTextBox.Visibility = IsConsoleExpanded ? Visibility.Visible : Visibility.Collapsed;
            ConsoleToggleBtn.Content = IsConsoleExpanded ? "Hide Debug Console («)" : "Show Debug Console (»)";
            ScrollViewerPrivate.ScrollToBottom();
        }

        private void StartNewSession() {
            if (ConversationHistory.Count > 0) SaveCurrentSession();
            ChatHistoryPanel.Children.Clear();
            ConversationHistory.Clear();
            AddMessageBubble("New session started. How can I assist, Boss?", true);
        }

        private void RefreshHistoryList() {
            HistoryListBox.Items.Clear();
            string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTime)) {
                HistoryListBox.Items.Add(Path.GetFileName(f));
            }
        }

        private void LoadSession(string fileName) {
            string path = Path.Combine(PathHandler.GetDataDirectory(), "Conversations", fileName);
            if (!File.Exists(path)) return;
            ChatHistoryPanel.Children.Clear();
            ConversationHistory.Clear();
            try {
                var turns = JsonSerializer.Deserialize<List<ChatTurn>>(File.ReadAllText(path));
                if (turns != null) {
                    foreach (var turn in turns) {
                        ConversationHistory.Add(turn);
                        AddMessageBubble(turn.Text, turn.Role == "model");
                    }
                }
            } catch { }
            HistoryContainer.Visibility = Visibility.Collapsed;
        }

        private void SaveCurrentSession() {
            try {
                string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations");
                Directory.CreateDirectory(dir);
                string name = $"Chat_{DateTime.Now:yyyyMMdd_HHmm}.json";
                File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(ConversationHistory));
            } catch { }
        }

        private void LoadLastSession() {
            string dir = Path.Combine(PathHandler.GetDataDirectory(), "Conversations");
            if (!Directory.Exists(dir)) { AddMessageBubble("Jarvis Systems Online. Ready.", true); return; }
            var last = Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            if (last != null) LoadSession(Path.GetFileName(last));
            else AddMessageBubble("Jarvis Systems Online. Ready.", true);
        }

        public static async Task SubmitTextMessage(string msg) { ShowChat(); if (Instance != null) await Instance.SendUserMessage(msg); }
        public static async Task SubmitVoiceCommand(string msg, bool showUi = true) { if (showUi) ShowChat(); if (Instance != null) await Instance.SendUserMessage(msg); }

        private async Task SendUserMessage(string msg) {
            DebugConsoleOverlay.Log("Chat-UI", $">>> SendUserMessage triggered. Msg length: {msg?.Length ?? 0}");
            string apiMsg = msg;
            string displayMsg = msg;
            if (AttachedFilePath != null) {
                apiMsg = $"[FILE: {AttachedFilePath}]\n{msg}";
                displayMsg = $"📎 Attached: {Path.GetFileName(AttachedFilePath)}\n{msg}";
                AttachedFilePath = null; AttachedFileBadge.Visibility = Visibility.Collapsed;
            }

            AddMessageBubble(displayMsg, false);
            ChronoLogManager.LogEvent("Chat", $"User: {msg}");

            var (bdr, tb, dbg) = AddMessageBubbleWithControls("🧠 Thinking...", true);
            StatusText.Text = "THINKING"; StatusDot.Fill = Brushes.Yellow;

            try {
                var cts = new CancellationTokenSource();
                _activeCts = cts;
                string response = "";
                if (CoreRegistry.Data.Settings.Current.LLM_BACKEND == "Ollama") {
                    response = await CoreRegistry.Intelligence.Llm.AskOllamaStreamAsync(apiMsg, ConversationHistory, t => Application.Current.Dispatcher.Invoke(() => tb.Text = (tb.Text == "🧠 Thinking..." ? "" : tb.Text) + t), cts.Token);
                } else {
                    response = await AiAPI.AskAgentAsync(apiMsg, ConversationHistory, cts.Token);
                }

                tb.Text = AiAPI.SanitizeText(response);
                ChronoLogManager.LogEvent("Chat", $"Jarvis: {tb.Text}");

                // Extract debug log if present
                if (response.Contains("### DEBUG LOG")) {
                    dbg.Text = response.Split("### DEBUG LOG")[1].Trim();
                } else {
                    dbg.Text = "Raw Response:\n" + response;
                }

                ConversationHistory.Add(new ChatTurn { Role = "user", Text = msg });
                ConversationHistory.Add(new ChatTurn { Role = "model", Text = tb.Text });
                SaveCurrentSession();
            } catch (Exception ex) {
                // Log the full technical detail to the debug console.
                LogConsoleAction("AI Fault", ex.ToString());

                // Show a clean, readable message in the chat bubble.
                string friendly;
                string errMsg = ex.Message;
                if (errMsg.Contains("invalid") || errMsg.Contains("API key") || errMsg.Contains("INVALID_ARGUMENT") || errMsg.Contains("restricted"))
                    friendly = "⚠️ API key issue — your Gemini key may be invalid or missing. Go to Settings → API Keys to update it.";
                else if (errMsg.Contains("not found") || errMsg.Contains("NOT_FOUND") || errMsg.Contains("deprecated") || errMsg.Contains("exhausted"))
                    friendly = "⚠️ The configured AI model is unavailable. Jarvis tried all fallbacks. Check Settings → LLM to pick a working model.";
                else if (errMsg.Contains("timed out") || errMsg.Contains("TaskCanceled") || errMsg.Contains("OperationCanceled"))
                    friendly = "⏱️ Request timed out. Check your internet connection and try again.";
                else if (errMsg.Contains("No providers") || errMsg.Contains("FATAL") || errMsg.Contains("collapsed"))
                    friendly = "🔴 All AI providers failed. Check that at least one API key is valid in Settings → API Keys.";
                else if (errMsg.Contains("Unauthorized") || errMsg.Contains("Forbidden"))
                    friendly = "🔐 Access denied — the API key may be expired or blocked. Please refresh it in Settings → API Keys.";
                else
                    friendly = "❌ Something went wrong. Tap ⌄ below for technical details.";

                tb.Text = friendly;
            } finally {
                StatusText.Text = "READY"; StatusDot.Fill = Brushes.LightGreen;
                _activeCts = null;
            }

            ScrollViewerPrivate.ScrollToBottom();
        }

        private void AddMessageBubble(string t, bool ai) => AddMessageBubbleWithControls(t, ai);

        private (Border, TextBox, TextBox) AddMessageBubbleWithControls(string t, bool ai) {
            var b = new Border { Background = ai ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(70, 138, 43, 226)), CornerRadius = new CornerRadius(12, 12, ai ? 12 : 0, ai ? 0 : 12), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(ai ? 0 : 40, 5, ai ? 40 : 0, 5), HorizontalAlignment = ai ? HorizontalAlignment.Left : HorizontalAlignment.Right, MaxWidth = 340, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
            var stack = new StackPanel();

            // Using ReadOnly TextBox for selection/highlighting
            var tb = new TextBox { Text = t, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("Segoe UI"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), IsReadOnly = true, FocusVisualStyle = null };
            stack.Children.Add(tb);

            TextBox dbg = new TextBox { Visibility = Visibility.Collapsed, Margin = new Thickness(0,10,0,0), Background = new SolidColorBrush(Color.FromArgb(100, 0,0,0)), Foreground = Brushes.Gray, FontSize = 10, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 250, BorderThickness = new Thickness(0), FontFamily = new FontFamily("Consolas") };

            if (ai) {
                var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Opacity = 0.5, Margin = new Thickness(0,4,0,0) };
                var copyBtn = new Button { Content = "📋", FontSize = 9, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, Cursor = Cursors.Hand };
                copyBtn.Click += (s, e) => { try { Clipboard.SetText(tb.Text); TextOverlay.Show("Copied", 1000); } catch { } };
                var detailsBtn = new Button { Content = "⌄", FontSize = 9, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, Cursor = Cursors.Hand, Margin = new Thickness(5,0,0,0) };
                detailsBtn.Click += (s, e) => { dbg.Visibility = dbg.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; detailsBtn.Content = dbg.Visibility == Visibility.Visible ? "⌃" : "⌄"; if(dbg.Visibility==Visibility.Visible) ScrollViewerPrivate.ScrollToBottom(); };
                btnStack.Children.Add(copyBtn); btnStack.Children.Add(detailsBtn); stack.Children.Add(btnStack); stack.Children.Add(dbg);
            }

            b.Child = stack; ChatHistoryPanel.Children.Add(b); ScrollViewerPrivate.ScrollToBottom();
            return (b, tb, dbg);
        }
    }
}
