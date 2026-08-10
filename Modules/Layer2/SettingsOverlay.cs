// Developer: heaplyn
// Date: 2026-08-09
// Summary: Interactive Settings & Options GUI window overlay allowing visual configuration of API keys, themes, startup behavior, sounds, search engine, and transparency.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class SettingsOverlay : BaseOverlay
    {
        private static SettingsOverlay? _instance;

        private readonly TextBox _geminiKeyBox;
        private readonly TextBox _githubTokenBox;
        private readonly TextBox _downloadDirBox;
        private readonly ComboBox _themeComboBox;
        private readonly ComboBox _searchEngineComboBox;
        private readonly CheckBox _startWithWinCheckBox;
        private readonly CheckBox _playSoundsCheckBox;
        private readonly CheckBox _autoHideCheckBox;
        private readonly CheckBox _alwaysOnTopCheckBox;
        private readonly Slider _opacitySlider;
        private readonly TextBlock _opacityValueLabel;

        public static void OpenSettings()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new SettingsOverlay();
                }

                _instance.LoadCurrentValues();
                _instance.Show();

                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }

                _instance.Activate();
                _instance.Focus();
            });
        }

        private SettingsOverlay()
            : base("JARVIS SYSTEM SETTINGS", width: 560, height: 480)
        {
            this.Closed += (s, e) => { _instance = null; };

            var mainGrid = new Grid { Margin = new Thickness(8) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Form area
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                     // Buttons bar

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 12)
            };

            var formPanel = new StackPanel();

            // 1. Google Gemini API Key
            formPanel.Children.Add(CreateLabel("🔑 Google Gemini API Key:"));
            _geminiKeyBox = CreateTextBox();
            formPanel.Children.Add(_geminiKeyBox);
            formPanel.Children.Add(CreateHint("Used for AI Chat Companion and File Agent functions."));

            // 2. GitHub Token
            formPanel.Children.Add(CreateLabel("🐙 GitHub Personal Access Token:"));
            _githubTokenBox = CreateTextBox();
            formPanel.Children.Add(_githubTokenBox);
            formPanel.Children.Add(CreateHint("Required for self-pushing to private GitHub repositories."));

            // 3. Media Download Directory
            formPanel.Children.Add(CreateLabel("📁 Media Download Destination Folder:"));
            var dirGrid = new Grid();
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _downloadDirBox = CreateTextBox();
            Grid.SetColumn(_downloadDirBox, 0);
            dirGrid.Children.Add(_downloadDirBox);

            var browseBtn = new Button
            {
                Content = "Browse...",
                Margin = new Thickness(8, 0, 0, 8),
                Padding = new Thickness(12, 4, 12, 4),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI")
            };
            browseBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            browseBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            browseBtn.Click += (s, e) => BrowseDownloadDir();
            Grid.SetColumn(browseBtn, 1);
            dirGrid.Children.Add(browseBtn);

            formPanel.Children.Add(dirGrid);
            formPanel.Children.Add(CreateHint("Directory where downloaded audio and videos will be saved."));

            // 4. Color Theme Selection
            formPanel.Children.Add(CreateLabel("🎨 Active Visual Color Theme:"));
            _themeComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI")
            };
            
            string[] themes = new string[] { 
                "purple", "dark", "blue", "green", "cyberpunk", "glass",
                "dracula", "sunset", "crimson", "gold", "nordic" 
            };
            foreach (var t in themes)
            {
                _themeComboBox.Items.Add(t);
            }
            _themeComboBox.SelectionChanged += (s, e) =>
            {
                if (_themeComboBox.SelectedItem is string selectedTheme)
                {
                    ThemeManager.ApplyTheme(selectedTheme);
                }
            };
            formPanel.Children.Add(_themeComboBox);

            // 5. Default Web Search Engine
            formPanel.Children.Add(CreateLabel("🌐 Default Web Search Engine:"));
            _searchEngineComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI")
            };
            _searchEngineComboBox.Items.Add("Google");
            _searchEngineComboBox.Items.Add("DuckDuckGo");
            _searchEngineComboBox.Items.Add("Bing");
            formPanel.Children.Add(_searchEngineComboBox);

            // 6. Window Opacity / Transparency Slider
            var opacityStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4) };
            var opacityLabel = CreateLabel("👁️ HUD Window Opacity: ");
            opacityLabel.Margin = new Thickness(0);
            opacityStack.Children.Add(opacityLabel);

            _opacityValueLabel = new TextBlock
            {
                Text = "100%",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center
            };
            _opacityValueLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            opacityStack.Children.Add(_opacityValueLabel);
            formPanel.Children.Add(opacityStack);

            _opacitySlider = new Slider
            {
                Minimum = 0.3,
                Maximum = 1.0,
                Value = 1.0,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _opacitySlider.ValueChanged += (s, e) =>
            {
                int pct = (int)(_opacitySlider.Value * 100);
                _opacityValueLabel.Text = $"{pct}%";
            };
            formPanel.Children.Add(_opacitySlider);

            // 7. Checkbox Toggles
            formPanel.Children.Add(CreateLabel("⚙️ System Behaviors & Preferences:"));

            _startWithWinCheckBox = CreateCheckBox("🚀 Start Jarvis automatically with Windows");
            formPanel.Children.Add(_startWithWinCheckBox);

            _playSoundsCheckBox = CreateCheckBox("🔊 Play sound alerts on notification popups");
            formPanel.Children.Add(_playSoundsCheckBox);

            _autoHideCheckBox = CreateCheckBox("🙈 Auto-hide HUD search bar after executing commands");
            formPanel.Children.Add(_autoHideCheckBox);

            _alwaysOnTopCheckBox = CreateCheckBox("📌 Keep HUD launcher window always on top");
            formPanel.Children.Add(_alwaysOnTopCheckBox);

            // 8. Global Hotkeys Reference Card
            formPanel.Children.Add(CreateLabel("⌨️ Registered Global System Keybinds:"));
            var keybindBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 10)
            };
            keybindBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            keybindBorder.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");

            var kbStack = new StackPanel();
            kbStack.Children.Add(new TextBlock { Text = "• ~ (Tilde) / Backtick : Toggle Jarvis Launcher HUD", FontSize = 11, Margin = new Thickness(0, 2, 0, 2) });
            kbStack.Children.Add(new TextBlock { Text = "• Ctrl + Alt + M : Toggle Mobile Companion Hub Overlay", FontSize = 11, Margin = new Thickness(0, 2, 0, 2) });
            kbStack.Children.Add(new TextBlock { Text = "• Ctrl + Shift + A : Toggle AI Companion Chat Overlay", FontSize = 11, Margin = new Thickness(0, 2, 0, 2) });
            kbStack.Children.Add(new TextBlock { Text = "• Ctrl + Shift + R : Restart Jarvis System", FontSize = 11, Margin = new Thickness(0, 2, 0, 2) });
            kbStack.Children.Add(new TextBlock { Text = "• Ctrl + Shift + C : Terminate / Exit Jarvis", FontSize = 11, Margin = new Thickness(0, 2, 0, 2) });

            keybindBorder.Child = kbStack;
            formPanel.Children.Add(keybindBorder);

            scrollViewer.Content = formPanel;
            Grid.SetRow(scrollViewer, 0);
            mainGrid.Children.Add(scrollViewer);

            // Action Buttons Bar (Save / Cancel)
            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var saveBtn = new Button
            {
                Content = "💾 Save Settings",
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI")
            };
            saveBtn.SetResourceReference(Button.BackgroundProperty, "SelectedBackgroundBrush");
            saveBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            saveBtn.SetResourceReference(Button.BorderBrushProperty, "SelectedBorderBrush");
            saveBtn.Click += (s, e) => SaveSettings();
            Grid.SetColumn(saveBtn, 0);
            buttonGrid.Children.Add(saveBtn);

            var closeBtn = new Button
            {
                Content = "Close",
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI")
            };
            closeBtn.SetResourceReference(Button.BackgroundProperty, "HoverBackgroundBrush");
            closeBtn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            closeBtn.Click += (s, e) => FadeOutAndClose();
            Grid.SetColumn(closeBtn, 1);
            buttonGrid.Children.Add(closeBtn);

            Grid.SetRow(buttonGrid, 1);
            mainGrid.Children.Add(buttonGrid);

            this.UserContent = mainGrid;
        }

        private void LoadCurrentValues()
        {
            var settings = SettingsManager.Current;
            _geminiKeyBox.Text = settings.GoogleAIKey;
            _githubTokenBox.Text = settings.GithubToken;
            _downloadDirBox.Text = settings.DownloadDirectory;
            _themeComboBox.SelectedItem = settings.Theme;

            _searchEngineComboBox.SelectedItem = string.IsNullOrEmpty(settings.DefaultSearchEngine) ? "Google" : settings.DefaultSearchEngine;
            _opacitySlider.Value = settings.WindowOpacity > 0.2 ? settings.WindowOpacity : 1.0;
            _opacityValueLabel.Text = $"{(int)(_opacitySlider.Value * 100)}%";

            _startWithWinCheckBox.IsChecked = settings.StartWithWindows;
            _playSoundsCheckBox.IsChecked = settings.PlaySounds;
            _autoHideCheckBox.IsChecked = settings.AutoHideOnExecute;
            _alwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        }

        private void SaveSettings()
        {
            try
            {
                var settings = SettingsManager.Current;
                settings.GoogleAIKey = _geminiKeyBox.Text.Trim();
                settings.GithubToken = _githubTokenBox.Text.Trim();
                settings.DownloadDirectory = _downloadDirBox.Text.Trim();

                if (_themeComboBox.SelectedItem is string selectedTheme)
                {
                    settings.Theme = selectedTheme;
                    ThemeManager.ApplyTheme(selectedTheme);
                }

                if (_searchEngineComboBox.SelectedItem is string selectedEngine)
                {
                    settings.DefaultSearchEngine = selectedEngine;
                }

                settings.WindowOpacity = _opacitySlider.Value;
                settings.StartWithWindows = _startWithWinCheckBox.IsChecked == true;
                settings.PlaySounds = _playSoundsCheckBox.IsChecked == true;
                settings.AutoHideOnExecute = _autoHideCheckBox.IsChecked == true;
                settings.AlwaysOnTop = _alwaysOnTopCheckBox.IsChecked == true;

                // Handle Windows Startup Registry key toggle
                ConfigureWindowsStartup(settings.StartWithWindows);

                SettingsManager.Save();

                // Dynamic update of all open overlay windows in real-time
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is BaseOverlay baseOverlay)
                        {
                            baseOverlay.Topmost = settings.AlwaysOnTop;
                        }
                    }
                });

                TextOverlay.Show("💾 Settings saved successfully!", 2500);
                FadeOutAndClose();
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save settings: {ex.Message}", 3000);
            }
        }

        private void ConfigureWindowsStartup(bool enable)
        {
            try
            {
                string keyName = "JarvisHUDLauncher";
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (enable)
                    {
                        key.SetValue(keyName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(keyName, false);
                    }
                }
            }
            catch { }
        }

        private void BrowseDownloadDir()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Media Downloads Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                _downloadDirBox.Text = dialog.FolderName;
            }
        }

        private TextBlock CreateLabel(string text)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 8, 0, 4)
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            return label;
        }

        private TextBlock CreateHint(string text)
        {
            var hint = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 10)
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            return hint;
        }

        private TextBox CreateTextBox()
        {
            var box = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 5, 6, 5),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            box.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            box.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            box.SetResourceReference(TextBox.BorderBrushProperty, "SelectedBorderBrush");
            return box;
        }

        private CheckBox CreateCheckBox(string labelText)
        {
            var box = new CheckBox
            {
                Content = labelText,
                Margin = new Thickness(0, 4, 0, 6),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                Cursor = Cursors.Hand
            };
            box.SetResourceReference(CheckBox.ForegroundProperty, "TextPrimaryBrush");
            return box;
        }
    }
}
