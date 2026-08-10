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
        private StackPanel _chatHistoryPanel;
        private ScrollViewer _scrollViewer;
        private TextBox _inputTextBox;
        private TextBlock _placeholderTextBlock;

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

            // 4. Input Area Grid
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

            Grid.SetRow(inputGrid, 2);
            contentGrid.Children.Add(inputGrid);

            this.UserContent = contentGrid;
        }

        private readonly List<ChatTurn> _conversationHistory = new List<ChatTurn>();

        private async Task SendUserMessage(string message)
        {
            // 1. Add User Message Bubble for this turn
            AddMessageBubble(message, isAi: false);

            // 2. Determine turn number & create a brand new dedicated AI response message bubble for this chained turn
            int turnNumber = (_conversationHistory.Count / 2) + 1;
            string initialStatus = turnNumber > 1 
                ? $"🧠 [Turn {turnNumber}] Analyzing chained context & formulating response..." 
                : "🧠 Jarvis is initializing deep reasoning...";

            var (aiBorder, aiTextBox) = AddMessageBubbleWithControl(initialStatus, isAi: true, isItalic: true);

            using var cts = new System.Threading.CancellationTokenSource();

            // Start background status updates directly inside this turn's new AI response bubble
            var thinkingTask = Task.Run(async () =>
            {
                string[] thinkingPhases = turnNumber > 1
                    ? new[]
                    {
                        $"🧠 [Turn {turnNumber}] Analyzing chained conversation history...",
                        "🔍 Searching local codebase & workspace memory...",
                        "⚡ Evaluating previous turn context & instructions...",
                        "📡 Querying Gemini AI model endpoints...",
                        "⚙️ Executing follow-up agent file operations...",
                        "📝 Synthesizing chained response...",
                        "✨ Finalizing formatting and output verification..."
                    }
                    : new[]
                    {
                        "🧠 Analyzing query structure & intentions...",
                        "🔍 Searching local codebase & workspace memory...",
                        "⚡ Formulating deep reasoning context...",
                        "📡 Querying Gemini AI model endpoints...",
                        "⚙️ Evaluating file operations and shell commands...",
                        "📝 Synthesizing comprehensive response...",
                        "✨ Finalizing formatting and output verification..."
                    };

                int index = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    string currentThought = thinkingPhases[index % thinkingPhases.Length];
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        aiTextBox.Text = currentThought;
                        _scrollViewer.UpdateLayout();
                        _scrollViewer.ScrollToBottom();
                    });

                    index++;
                    try { await Task.Delay(3500, cts.Token); } catch { break; }
                }
            });

            try
            {
                // 3. Ask Gemini with full multi-turn chained history context
                string aiResponse = await AiAPI.AskGemini(message, _conversationHistory);

                // 4. Run through filesystem agent parser
                string finalResult = AgentExecutor.ProcessAIResponse(aiResponse);

                // Stop thinking animation loop
                cts.Cancel();

                // 5. Multi-message rendering: Remove thinking bubble and spawn separate response bubbles for each block
                var messageBlocks = SplitIntoMultipleMessages(finalResult);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Remove temporary thinking indicator bubble
                    _chatHistoryPanel.Children.Remove(aiBorder);

                    // Render every message block as a distinct, dedicated AI chat bubble for this turn!
                    foreach (var block in messageBlocks)
                    {
                        AddMessageBubble(block, isAi: true);
                    }

                    _scrollViewer.UpdateLayout();
                    _scrollViewer.ScrollToBottom();
                });

                // 6. Chain conversation turns together for future messages (retains up to 100 turns / 200 items)
                _conversationHistory.Add(new ChatTurn { Role = "user", Text = message });
                _conversationHistory.Add(new ChatTurn { Role = "model", Text = finalResult });

                if (_conversationHistory.Count > 200)
                {
                    _conversationHistory.RemoveRange(0, 2); // Maintain rolling 100-turn window
                }

                // 7. Log conversation turn to .txt file
                LogConversationTurn(message, finalResult);
            }
            catch (Exception ex)
            {
                cts.Cancel();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    aiTextBox.Text = $"⚠️ Error generating response: {ex.Message}";
                    aiTextBox.FontStyle = FontStyles.Normal;
                });
                LogConversationTurn(message, $"ERROR: {ex.Message}");
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
