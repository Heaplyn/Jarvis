// Developer: heaplyn
// Date: 2026-08-08
// Summary: Code-behind controlling the MainWindow visual state, animations, focus triggers, hotkey bindings, and input text changes.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Threading;

namespace JarvisLauncher
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private const int CTRL_SHIFT_C_ID = 9001;
        private const int CTRL_SHIFT_R_ID = 9002;
        private const int CTRL_ALT_M_ID = 9003;
        private const int CTRL_SHIFT_A_ID = 9004;
        private const uint VK_OEM_3 = 0xC0; // Backtick (`) / Tilde (~) key on US keyboard
        private const uint VK_C = 0x43;      // 'C' key
        private const uint VK_R = 0x52;      // 'R' key
        private const uint VK_M = 0x4D;      // 'M' key
        private const uint VK_A = 0x41;      // 'A' key

        private HwndSource? _hwndSource;
        private IntPtr _previousForegroundWindow = IntPtr.Zero;
        private bool _isHiding = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyMarginSettings();

            // Automatically register Jarvis in Windows Search Bar & Start Menu
            StartMenuRegistrar.EnsureStartMenuShortcut();

            // Initialize 100% offline local wake-word detector ("Jarvis")
            try
            {
                LocalWakeWordDetector.Initialize();
                LocalWakeWordDetector.OnWakeWordDetected += (phrase) =>
                {
                    TextOverlay.Show($"🎙️ Recognized: \"{phrase}\"", 1800);
                };
            }
            catch { }

            // Initialize Clipboard History Manager
            ClipboardHistoryManager.Initialize();

            // Start Mobile Bridge HTTP & REST Server (for phone AI chat & PC remote control deck)
            try
            {
                MobileBridgeServer.Start(SettingsManager.Current.MobilePort);
            }
            catch (Exception ex)
            {
                try { System.IO.File.WriteAllText("server_fatal.txt", ex.ToString()); } catch { }
            }

            // Apply persistent theme from settings
            try
            {
                ThemeManager.ApplyTheme(SettingsManager.Current.Theme);
            }
            catch { }

            // Subscribe to the text opacity event from Layer 3
            CommandParser.OnTextOpacityChanged += (opacityValue) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    SearchInput.Opacity = opacityValue;
                    PlaceholderText.Opacity = opacityValue;
                    ResultsList.Opacity = opacityValue;
                });
            };

            // Run self-healing downloader dependency setup on a background thread on startup
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await DownloadMediaRunner.EnsureDependenciesAsync();
                }
                catch { }
            });
        }

        public void PositionWindowAtTopCenter()
        {
            
            // Resolve the actual monitor (under the cursor) in physical pixels, then convert to this
            // window's DIP space, since SystemParameters.WorkArea only reflects the primary monitor
            // and ignores per-monitor DPI, which was pushing the bar off-center on scaled/secondary displays.
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget is null)
            {
                var fallback = SystemParameters.WorkArea;
                this.Left = (fallback.Width - 689) / 2 + fallback.Left;
                this.Top = fallback.Top + 10;
                return;
            }

            var workAreaPx = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            var transform = source.CompositionTarget.TransformFromDevice;
            var topLeft = transform.Transform(new Point(workAreaPx.Left, workAreaPx.Top));
            var bottomRight = transform.Transform(new Point(workAreaPx.Right, workAreaPx.Bottom));
            double workWidth = bottomRight.X - topLeft.X;

            this.Left = topLeft.X + (workWidth - 689) / 2;
            this.Top = topLeft.Y + 10; // 10px offset from the very top
            CliOutputOverlay.Show("MainWindow", $"this.Left = {this.Left}");
            CliOutputOverlay.Show("MainWindow", $"this.Top = {this.Top}");
            CliOutputOverlay.Show("MainWindow",$"workWidth = {workWidth}");
            CliOutputOverlay.Show("MainWindow", $"this.ActualWidth = {this.ActualWidth}");
            
        }

        private void ApplyMarginSettings()
        {
            //RootGrid.Margin = new Thickness(SettingsManager.Current.WindowMargin);
            PositionWindowAtTopCenter();
        }

        public void ApplyMargin(int margin)
        {
            this.Dispatcher.Invoke(() =>
            {
               // RootGrid.Margin = new Thickness(margin);
                PositionWindowAtTopCenter();
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(HwndHook);

            // Register Hotkey: Backtick (`) with no modifier, preventing repeats
            bool success = NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID, 0, VK_OEM_3);
            if (!success)
            {
                MessageBox.Show("Could not register global hotkey (`) - check if another app is using it.", "Jarvis HUD Launcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Register Hotkey: Ctrl + Shift + C
            uint ctrlShift = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
            bool success2 = NativeMethods.RegisterHotKey(helper.Handle, CTRL_SHIFT_C_ID, ctrlShift, VK_C);
            if (!success2)
            {
                MessageBox.Show("Could not register global hotkey (Ctrl+Shift+C) - check if another app is using it.", "Jarvis HUD Launcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Register Hotkey: Ctrl + Shift + R
            bool success3 = NativeMethods.RegisterHotKey(helper.Handle, CTRL_SHIFT_R_ID, ctrlShift, VK_R);
            if (!success3)
            {
                MessageBox.Show("Could not register global hotkey (Ctrl+Shift+R) - check if another app is using it.", "Jarvis HUD Launcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Register Hotkey: Ctrl + Alt + M (Mobile Companion Hub Overlay)
            uint ctrlAlt = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
            NativeMethods.RegisterHotKey(helper.Handle, CTRL_ALT_M_ID, ctrlAlt, VK_M);

            // Register Hotkey: Ctrl + Shift + A (AI Chat Overlay)
            NativeMethods.RegisterHotKey(helper.Handle, CTRL_SHIFT_A_ID, ctrlShift, VK_A);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (hotkeyId == HOTKEY_ID)
                {
                    ToggleHUD();
                    handled = true;
                }
                else if (hotkeyId == CTRL_ALT_M_ID)
                {
                    handled = true;
                    MobileOverlay.ShowOverlay();
                }
                else if (hotkeyId == CTRL_SHIFT_A_ID)
                {
                    handled = true;
                    ChatOverlay.ShowChat();
                }
                else if (hotkeyId == CTRL_SHIFT_C_ID)
                {
                    // Ctrl + Shift + C was pressed globally!
                    handled = true;
                    System.Environment.Exit(0);
                }
                else if (hotkeyId == CTRL_SHIFT_R_ID)
                {
                    handled = true;
                    // Display visual overlay directly (Layer 4 can call Layer 2 directly!)
                    TextOverlay.Show("Restarting Jarvis...", 1000);

                    // Wait 1 second before performing the actual process restart
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += (s, ev) =>
                    {
                        timer.Stop();
                        NativeMethods.Restart();
                    };
                    timer.Start();
                }
            }
            return IntPtr.Zero;
        }

        public void ToggleHUD()
        {
            if (this.Visibility == Visibility.Visible && !_isHiding)
            {
                HideHUD();
            }
            else
            {
                ShowHUD();
            }
        }

        public void ShowHUD()
        {
            _isHiding = false;

            // Capture the current foreground window so we can restore focus back to it later
            IntPtr activeWnd = NativeMethods.GetForegroundWindow();
            var helper = new WindowInteropHelper(this);
            if (activeWnd != helper.Handle && activeWnd != IntPtr.Zero)
            {
                _previousForegroundWindow = activeWnd;
            }

            // Bring launcher window to the front
            this.Visibility = Visibility.Visible;
            this.Activate();

            // Set focus immediately to search field
            SearchInput.Focus();
            Keyboard.Focus(SearchInput);

            // Play Slide In animation
            if (SettingsManager.Current.EnableAnimations)
            {
                if (Resources["SlideOut"] is Storyboard slideOut)
                {
                    slideOut.Stop(this);
                }
                if (Resources["SlideIn"] is Storyboard slideIn)
                {
                    slideIn.Begin(this);
                }
            }
            else
            {
                WindowTranslate.Y = 0;
                MainBorder.Opacity = 1.0;
                this.Opacity = 1.0;
            }
        }

        public void HideHUD()
        {
            if (_isHiding) return;
            _isHiding = true;

            // Play Slide Out animation
            if (SettingsManager.Current.EnableAnimations)
            {
                if (Resources["SlideIn"] is Storyboard slideIn)
                {
                    slideIn.Stop(this);
                }
                if (Resources["SlideOut"] is Storyboard slideOut)
                {
                    EventHandler? completedHandler = null;
                    completedHandler = (s, e) =>
                    {
                        slideOut.Completed -= completedHandler;
                        if (_isHiding)
                        {
                            this.Visibility = Visibility.Collapsed;
                            SearchInput.Text = string.Empty; // Clear text for next launch
                            _isHiding = false;

                            // Restore focus back to the previous application window
                            if (_previousForegroundWindow != IntPtr.Zero)
                            {
                                NativeMethods.SetForegroundWindow(_previousForegroundWindow);
                                _previousForegroundWindow = IntPtr.Zero;
                            }
                        }
                    };
                    slideOut.Completed += completedHandler;
                    slideOut.Begin(this);
                }
                else
                {
                    this.Visibility = Visibility.Collapsed;
                    _isHiding = false;
                }
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
                SearchInput.Text = string.Empty;
                _isHiding = false;
                WindowTranslate.Y = -50;
                MainBorder.Opacity = 0.0;

                // Restore focus back to the previous application window
                if (_previousForegroundWindow != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(_previousForegroundWindow);
                    _previousForegroundWindow = IntPtr.Zero;
                }
            }
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchInput.Text;
            PlaceholderText.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            var suggestions = CommandParser.GetSuggestions(query);

            if (suggestions.Count > 0)
            {
                ResultsList.ItemsSource = suggestions;
                ResultsList.Visibility = Visibility.Visible;
                DividerLine.Visibility = Visibility.Visible;
                ResultsList.SelectedIndex = 0;

                // Inline ghost autocomplete hint: show suffix faintly after typed text
                if (suggestions[0] is CommandResult top)
                {
                    string hint = GetAutocompleteSuffix(query, top);
                    AutocompleteGhost.Text = hint;
                    AutocompleteGhost.Visibility = string.IsNullOrEmpty(hint)
                        ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            else
            {
                ResultsList.ItemsSource = null;
                ResultsList.Visibility = Visibility.Collapsed;
                DividerLine.Visibility = Visibility.Collapsed;
                AutocompleteGhost.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>Extracts a Tab-completable suffix from the top suggestion title.</summary>
        private static string GetAutocompleteSuffix(string query, CommandResult top)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            // Strip emoji prefixes like "⭐ ", "⚡ Command: "
            string clean = top.Title
                .Replace("⭐ ", "")
                .Replace("⚡ Command: ", "")
                .Replace("⚡ ", "")
                .Trim();

            return SearchUtil.GetAutocompleteSuffix(query, clean);
        }

        private void SearchInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideHUD();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Tab: accept the inline ghost autocomplete
                if (AutocompleteGhost.Visibility == Visibility.Visible && !string.IsNullOrEmpty(AutocompleteGhost.Text))
                {
                    SearchInput.Text += AutocompleteGhost.Text;
                    SearchInput.CaretIndex = SearchInput.Text.Length;
                    AutocompleteGhost.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
                else if (ResultsList.Visibility == Visibility.Visible &&
                         ResultsList.Items.Count > 0 &&
                         ResultsList.SelectedItem is CommandResult top)
                {
                    // Fallback: Tab fills the top result's cleaned command name
                    string clean = top.Title
                        .Replace("⭐ ", "")
                        .Replace("⚡ Command: ", "")
                        .Replace("⚡ ", "")
                        .Trim();
                    SearchInput.Text = clean;
                    SearchInput.CaretIndex = SearchInput.Text.Length;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down)
            {
                if (ResultsList.Visibility == Visibility.Visible && ResultsList.Items.Count > 0)
                {
                    int nextIndex = ResultsList.SelectedIndex + 1;
                    if (nextIndex < ResultsList.Items.Count)
                    {
                        ResultsList.SelectedIndex = nextIndex;
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (ResultsList.Visibility == Visibility.Visible && ResultsList.Items.Count > 0)
                {
                    int prevIndex = ResultsList.SelectedIndex - 1;
                    if (prevIndex >= 0)
                    {
                        ResultsList.SelectedIndex = prevIndex;
                        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelection();
                e.Handled = true;
            }
        }

        private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideHUD();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && ResultsList.SelectedIndex == 0)
            {
                SearchInput.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelection();
                e.Handled = true;
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelection();
        }

        private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelection();
        }

        private void ExecuteSelection()
        {
            CommandResult? selectedItem = ResultsList.SelectedItem as CommandResult;
            if (selectedItem == null && ResultsList.Items.Count > 0)
            {
                selectedItem = ResultsList.Items[0] as CommandResult;
            }

            if (selectedItem != null)
            {
                // Record this (query → result) pair in the ML learner for future ranking
                QueryLearner.RecordSelection(SearchInput.Text.Trim(), selectedItem.Title);

                try
                {
                    selectedItem.Execute?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error running command: {ex.Message}", "Jarvis HUD Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                HideHUD();
            }
            else
            {
                string query = SearchInput.Text.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    CommandParser.ExecuteFirstSuggestion(query);
                    HideHUD();
                }
            }
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if (this.Visibility == Visibility.Visible && !_isHiding)
            {
                HideHUD();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_hwndSource != null)
            {
                var helper = new WindowInteropHelper(this);
                NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID);
                NativeMethods.UnregisterHotKey(helper.Handle, CTRL_SHIFT_C_ID);
                NativeMethods.UnregisterHotKey(helper.Handle, CTRL_SHIFT_R_ID);
                _hwndSource.RemoveHook(HwndHook);
                _hwndSource = null;
            }
            base.OnClosed(e);
        }
    }
}
