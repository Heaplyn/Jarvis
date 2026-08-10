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

        // Visual Console controls
        private Border _consoleContainer;
        private TextBox _consoleTextBox;
        private Button _consoleToggleBtn;
        private bool _isConsoleExpanded = false;

        public static void ShowChat()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new ChatOverlay();
                    _instance.Closed += (s, e) => _instance = null;
                    _instance.Show();
                }
                else
                {
                    _instance.Activate();
                    if (_instance.WindowState == WindowState.Minimized)
                    {
                        _instance.WindowState = WindowState.Normal;
                    }
                }
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
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Chat History
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Divider
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Console Drawer
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Input Box Area

            // 2. Scrollable Chat History
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _chatHistoryPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
            _scrollViewer.Content = _chatHistoryPanel;
            Grid.SetRow(_scrollViewer, 0);
            contentGrid.Children.Add(_scrollViewer);

            // Add starting welcome message
            AddMessageBubble("Hello! I am your Jarvis AI Companion. How can I help you customize your system or files today?", isAi: true);

            // 3. Thin Divider
            var divider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 0, 0, 8)
            };
            divider.SetResourceReference(Border.BackgroundProperty, "WindowBorderBrush");
            Grid.SetRow(divider, 1);
            contentGrid.Children.Add(divider);

            // --- Collapsible Console Drawer (Row 2) ---
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
                Margin = new Thickness(0, 4, 0, 0)
            };
            _consoleTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _consoleTextBox.Text = _consoleLog.ToString();

            Grid.SetRow(_consoleTextBox, 1);
            consoleLayoutGrid.Children.Add(_consoleTextBox);

            _consoleContainer.Child = consoleLayoutGrid;
            Grid.SetRow(_consoleContainer, 2);
            contentGrid.Children.Add(_consoleContainer);

            // 4. Input Area Grid (Row 3)
            var inputGrid = new Grid();
            
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
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            _inputTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _inputTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            _inputTextBox.SetResourceReference(TextBox.BorderBrushProperty, "SelectedBorderBrush");

            _placeholderTextBlock = new TextBlock
            {
                Text = "Ask Jarvis... (Press Enter to send)",
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
                    if (!string.IsNullOrEmpty(message))
                    {
                        _inputTextBox.Text = string.Empty;
                        await SendUserMessage(message);
                    }
                }
            };

            inputGrid.Children.Add(_inputTextBox);
            inputGrid.Children.Add(_placeholderTextBlock);

            Grid.SetRow(inputGrid, 3);
            contentGrid.Children.Add(inputGrid);

            this.UserContent = contentGrid;
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

        private readonly List<ChatTurn> _conversationHistory = new List<ChatTurn>();

        private async Task SendUserMessage(string message)
        {
            // 1. Add user bubble
            AddMessageBubble(message, isAi: false);

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
            var thinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            thinkingTimer.Tick += (s, e) =>
            {
                aiTextBox.Text = thinkingPhases[phaseIndex % thinkingPhases.Length];
                phaseIndex++;
                _scrollViewer.ScrollToBottom();
            };
            thinkingTimer.Start();

            // 4. Run AI on background thread — awaiting without ConfigureAwait(false) so we resume on UI thread
            string finalResult = "";
            try
            {
                var snapshot = new List<ChatTurn>(_conversationHistory);
                string aiResponse = await Task.Run(async () => await AiAPI.AskGemini(message, snapshot));
                finalResult = AgentExecutor.ProcessAIResponse(aiResponse);
            }
            catch (Exception ex)
            {
                finalResult = $"⚠️ Error: {ex.Message}";
                LogConversationTurn(message, $"ERROR: {ex.Message}");
            }
            finally
            {
                thinkingTimer.Stop(); // back on UI thread — safe
            }

            // 5. Write final result — we are on the UI thread, direct access is safe
            aiTextBox.Text = finalResult;
            aiTextBox.FontStyle = FontStyles.Normal;
            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();

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

            var textBox = new TextBox
            {
                Text = text,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
                Padding = new Thickness(0),
                Cursor = Cursors.Arrow,
                Margin = new Thickness(0)
            };
            textBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");

            bubbleBorder.Child = textBox;
            _chatHistoryPanel.Children.Add(bubbleBorder);

            // Auto-scroll to the bottom of the history
            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();

            return (bubbleBorder, textBox);
        }

        private Border AddMessageBubble(string text, bool isAi, bool isItalic = false)
        {
            var tuple = AddMessageBubbleWithControl(text, isAi, isItalic);
            return tuple.Border;
        }
    }
}
