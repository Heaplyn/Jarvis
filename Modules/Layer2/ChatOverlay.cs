// Developer: heaplyn
// Date: 2026-08-09
// Summary: Draggable, interactive AI chat companion panel with scrollable history and message input.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;

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

        private async Task SendUserMessage(string message)
        {
            // 1. Add User Message Bubble
            AddMessageBubble(message, isAi: false);

            // 2. Add temporary "Thinking" bubble
            var thinkingBorder = AddMessageBubble(" Jarvis is thinking...", isAi: true, isItalic: true);

            try
            {
                // 3. Ask Gemini
                string aiResponse = await AiAPI.AskGemini(message);

                // Remove thinking indicator
                _chatHistoryPanel.Children.Remove(thinkingBorder);

                // 4. Run through filesystem agent parser
                string finalResult = AgentExecutor.ProcessAIResponse(aiResponse);

                // 5. Render AI response
                AddMessageBubble(finalResult, isAi: true);
            }
            catch (Exception ex)
            {
                _chatHistoryPanel.Children.Remove(thinkingBorder);
                AddMessageBubble($"⚠️ Error generating response: {ex.Message}", isAi: true);
            }
        }

        private Border AddMessageBubble(string text, bool isAi, bool isItalic = false)
        {
            var brushConverter = new BrushConverter();
            
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
                Foreground = Brushes.White,
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

            bubbleBorder.Child = textBox;
            _chatHistoryPanel.Children.Add(bubbleBorder);

            // Auto-scroll to the bottom of the history
            _scrollViewer.UpdateLayout();
            _scrollViewer.ScrollToBottom();

            return bubbleBorder;
        }
    }
}
