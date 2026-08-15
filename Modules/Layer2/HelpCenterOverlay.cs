// Developer: heaplyn
// Date: 2026-08-13
// Summary: Interactive WPF Help Center, Command Reference, Keyboard Shortcut Cheat Sheet, and Documentation GUI Overlay.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class HelpCenterOverlay : BaseOverlay
    {
        private static HelpCenterOverlay? _instance;

        public static void ShowOverlay()
        {
            if (_instance == null || !_instance.IsLoaded)
            {
                _instance = new HelpCenterOverlay();
            }
            _instance.Show();
            _instance.BringToFront();
            _instance.Focus();
        }

        private TextBox _searchBox;
        private TabControl _cmdSubTabControl;

        public HelpCenterOverlay() : base("📖 JARVIS HELP & DOCUMENTATION CENTER", 780, 560)
        {
            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            BaseOverlay.StyleTabControl(tabControl);

            // ── TAB 1: Command Directory with Category Sub-Tabs ──────────────────────
            var cmdTab = new TabItem { Header = "⚡ Command Directory" };
            var cmdGrid = new Grid { Margin = new Thickness(10) };
            cmdGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cmdGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Search Filter
            _searchBox = new TextBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _searchBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            _searchBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _searchBox.TextChanged += (s, e) => RenderCommandsList(_searchBox.Text.Trim());
            Grid.SetRow(_searchBox, 0);
            cmdGrid.Children.Add(_searchBox);

            // Sub-Tabs for Command Categories
            _cmdSubTabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 4, 0, 0)
            };
            BaseOverlay.StyleTabControl(_cmdSubTabControl);

            Grid.SetRow(_cmdSubTabControl, 1);
            cmdGrid.Children.Add(_cmdSubTabControl);

            cmdTab.Content = cmdGrid;
            tabControl.Items.Add(cmdTab);

            // ── TAB 2: Keyboard Shortcuts ──────────────────────────────────────────
            var keysTab = new TabItem { Header = "⌨ Global Shortcuts" };
            var keysScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
            var keysStack = new StackPanel();

            keysStack.Children.Add(CreateHeaderBlock("Global Hotkeys & System Accelerators"));

            keysStack.Children.Add(CreateShortcutRow("` (Backtick / Tilde)", "Toggle Jarvis Main HUD Command Bar"));
            keysStack.Children.Add(CreateShortcutRow("Ctrl + Shift + A", "Open AI Chat Assistant Overlay"));
            keysStack.Children.Add(CreateShortcutRow("Ctrl + Alt + M", "Open Mobile Companion Hub Overlay"));
            keysStack.Children.Add(CreateShortcutRow("Ctrl + Shift + R", "Restart Jarvis Launcher Application"));
            keysStack.Children.Add(CreateShortcutRow("Ctrl + Shift + C", "Emergency Exit Application"));
            keysStack.Children.Add(CreateShortcutRow("Tab", "Autocomplete Top Ghost Suggestion in Search Bar"));
            keysStack.Children.Add(CreateShortcutRow("Esc", "Hide HUD or Overlay Window"));
            keysStack.Children.Add(CreateShortcutRow("Enter", "Execute Currently Highlighted Command"));
            keysStack.Children.Add(CreateShortcutRow("Up / Down Arrows", "Navigate Search Results List"));

            keysScroll.Content = keysStack;
            keysTab.Content = keysScroll;
            tabControl.Items.Add(keysTab);

            // ── TAB 3: Chaining & Advanced Features ─────────────────────────────
            var advTab = new TabItem { Header = "🚀 Advanced Pipelines & Tips" };
            var advScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
            var advStack = new StackPanel();

            advStack.Children.Add(CreateHeaderBlock("Command Chaining & Inline Scripting"));
            advStack.Children.Add(CreateInfoRow("Pipeline Operator '|'", "Chain multiple commands together (e.g. 'sysinfo | screenshot')"));
            advStack.Children.Add(CreateInfoRow("Sequence Operator '&&'", "Run sequential commands step-by-step (e.g. 'lock && timer 10')"));
            advStack.Children.Add(CreateInfoRow("Math Evaluator", "Type math directly (e.g. '54 * 12 + sqrt(144)') for instant output"));
            advStack.Children.Add(CreateInfoRow("Machine Learning Prior", "Jarvis learns your frequently used commands and ranks them higher"));
            advStack.Children.Add(CreateInfoRow("Voice AI Studio", "Type 'voice' or 'voicestudio' to calibrate voice commands & sample recorder"));

            advScroll.Content = advStack;
            advTab.Content = advScroll;
            tabControl.Items.Add(advTab);

            // ── TAB 4: Master User Guide & Feature Manual ──────────────────────
            var guideTab = new TabItem { Header = "📚 Master User Guide" };
            var guideScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) };
            var guideStack = new StackPanel();

            guideStack.Children.Add(CreateHeaderBlock("🤖 JARVIS MASTER USER GUIDE & FEATURE MANUAL"));

            string guideText = "⚠️ Guide file user_guide.md not loaded.";
            try
            {
                string guidePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_guide.md");
                if (System.IO.File.Exists(guidePath))
                {
                    guideText = System.IO.File.ReadAllText(guidePath);
                }
            }
            catch { }

            var guideBox = new TextBox
            {
                Text = guideText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5,
                Padding = new Thickness(10),
                AcceptsReturn = true,
                BorderThickness = new Thickness(0)
            };
            guideBox.SetResourceReference(TextBox.BackgroundProperty, "WindowBackgroundBrush");
            guideBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            guideStack.Children.Add(guideBox);

            guideScroll.Content = guideStack;
            guideTab.Content = guideScroll;
            tabControl.Items.Add(guideTab);

            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            this.UserContent = mainGrid;

            RenderCommandsList("");
        }

        private void RenderCommandsList(string filter)
        {
            _cmdSubTabControl.Items.Clear();

            var allCmds = CommandParser.GetAllCommandDescriptions();
            var filtered = string.IsNullOrEmpty(filter)
                ? allCmds
                : allCmds.Where(c => c.COMMAND_NAME.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     (c.COMMAND_DESCRIPTION ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     (c.COMMAND_EXAMPLE ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var categories = new Dictionary<string, List<CommandDesc>>
            {
                ["🤖 AI, LLM & MCP"] = new List<CommandDesc>(),
                ["💻 Developer Tools"] = new List<CommandDesc>(),
                ["🎙️ Voice Studio"] = new List<CommandDesc>(),
                ["🎬 Media & Files"] = new List<CommandDesc>(),
                ["💡 ADHD & Productivity"] = new List<CommandDesc>(),
                ["🎛️ System & Power"] = new List<CommandDesc>()
            };

            foreach (var cmd in filtered)
            {
                string cat = GetCommandCategory(cmd);
                categories[cat].Add(cmd);
            }

            foreach (var pair in categories)
            {
                var tabItem = new TabItem { Header = pair.Key };
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(6) };
                var stack = new StackPanel();
                scroll.Content = stack;
                tabItem.Content = scroll;

                if (pair.Value.Count == 0)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrEmpty(filter) ? "No commands in this category." : "No matching commands in this category.",
                        FontSize = 11,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(0, 16, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                }
                else
                {
                    foreach (var cmd in pair.Value)
                    {
                        var border = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 2, 0, 4)
                        };

                        var itemStack = new StackPanel();
                        var title = new TextBlock { Text = $"⚡ {cmd.COMMAND_NAME}", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan };
                        var desc = new TextBlock { Text = cmd.COMMAND_DESCRIPTION, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0, 2, 0, 0) };
                        itemStack.Children.Add(title);
                        itemStack.Children.Add(desc);

                        if (!string.IsNullOrEmpty(cmd.COMMAND_EXAMPLE))
                        {
                            var ex = new TextBlock { Text = $"Example: {cmd.COMMAND_EXAMPLE}", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) };
                            itemStack.Children.Add(ex);
                        }

                        border.Child = itemStack;
                        stack.Children.Add(border);
                    }
                }

                _cmdSubTabControl.Items.Add(tabItem);
            }
        }

        private string GetCommandCategory(CommandDesc cmd)
        {
            string name = cmd.COMMAND_NAME.ToLower();
            string desc = (cmd.COMMAND_DESCRIPTION ?? "").ToLower();

            if (name.Contains("ai") || name.Contains("gemini") || name.Contains("chat") || name.Contains("copilot") || name.Contains("mcp") || name.Contains("oauth") || name.Contains("login") || name.Contains("auth") || name.Contains("llm"))
            {
                return "🤖 AI, LLM & MCP";
            }
            if (name.Contains("git") || name.Contains("powershell") || name.Contains("roblox") || name.Contains("blender") || name.Contains("vector") || name.Contains("r1") || name.Contains("coder") || name.Contains("tile") || name.Contains("tiling") || name.Contains("ipa") || name.Contains("ios"))
            {
                return "💻 Developer Tools";
            }
            if (name.Contains("voice") || name.Contains("dataset") || name.Contains("mic") || name.Contains("silence") || name.Contains("noise") || name.Contains("confidence") || name.Contains("gate"))
            {
                return "🎙️ Voice Studio";
            }
            if (name.Contains("convert") || name.Contains("webp") || name.Contains("gif") || name.Contains("png") || name.Contains("mp4") || name.Contains("mp3") || name.Contains("wav") || name.Contains("file") || name.Contains("organize") || name.Contains("edit") || name.Contains("open"))
            {
                return "🎬 Media & Files";
            }
            if (name.Contains("todo") || name.Contains("calendar") || name.Contains("reminder") || name.Contains("adhd") || name.Contains("pomodoro") || name.Contains("timer") || name.Contains("habits") || name.Contains("clock") || name.Contains("time"))
            {
                return "💡 ADHD & Productivity";
            }
            return "🎛️ System & Power";
        }

        private TextBlock CreateHeaderBlock(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Cyan,
                Margin = new Thickness(0, 0, 0, 10)
            };
        }

        private Border CreateShortcutRow(string key, string desc)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 4)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBlock = new TextBlock { Text = key, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.Yellow };
            var descBlock = new TextBlock { Text = desc, FontSize = 11, Foreground = Brushes.White };

            Grid.SetColumn(keyBlock, 0);
            Grid.SetColumn(descBlock, 1);

            grid.Children.Add(keyBlock);
            grid.Children.Add(descBlock);

            border.Child = grid;
            return border;
        }

        private Border CreateInfoRow(string title, string details)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 2, 0, 4)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.LightGreen });
            stack.Children.Add(new TextBlock { Text = details, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0, 2, 0, 0) });

            border.Child = stack;
            return border;
        }
    }
}
