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
        private StackPanel _cmdListPanel;

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

            // ── TAB 1: Command Directory ──────────────────────────────────────────────
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

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _cmdListPanel = new StackPanel();
            scroll.Content = _cmdListPanel;
            Grid.SetRow(scroll, 1);
            cmdGrid.Children.Add(scroll);

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

            Grid.SetRow(tabControl, 1);
            mainGrid.Children.Add(tabControl);

            this.UserContent = mainGrid;

            RenderCommandsList("");
        }

        private void RenderCommandsList(string filter)
        {
            _cmdListPanel.Children.Clear();
            var allCmds = CommandParser.GetAllCommandDescriptions();

            var filtered = string.IsNullOrEmpty(filter)
                ? allCmds
                : allCmds.Where(c => c.CommandName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     (c.CommandDescription ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                     (c.CommandExample ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filtered.Count == 0)
            {
                _cmdListPanel.Children.Add(new TextBlock
                {
                    Text = $"No commands matching '{filter}' found.",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 16, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (var cmd in filtered)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 2, 0, 4)
                };

                var stack = new StackPanel();
                var title = new TextBlock { Text = $"⚡ {cmd.CommandName}", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.Cyan };
                var desc = new TextBlock { Text = cmd.CommandDescription, FontSize = 11, Foreground = Brushes.LightGray, Margin = new Thickness(0, 2, 0, 0) };
                stack.Children.Add(title);
                stack.Children.Add(desc);

                if (!string.IsNullOrEmpty(cmd.CommandExample))
                {
                    var ex = new TextBlock { Text = $"Example: {cmd.CommandExample}", FontSize = 10, Foreground = Brushes.Gray, Margin = new Thickness(0, 2, 0, 0) };
                    stack.Children.Add(ex);
                }

                border.Child = stack;
                _cmdListPanel.Children.Add(border);
            }
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
