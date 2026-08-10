// Developer: heaplyn
// Date: 2026-08-09
// Summary: Interactive Settings & Options GUI window overlay allowing visual configuration of API keys, themes, download paths, and system behaviors.

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
            : base("JARVIS SYSTEM SETTINGS", width: 520, height: 440)
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

                SettingsManager.Save();
                TextOverlay.Show("💾 Settings saved successfully!", 2500);
                FadeOutAndClose();
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save settings: {ex.Message}", 3000);
            }
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
    }
}
