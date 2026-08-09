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
        private const uint VK_OEM_3 = 0xC0; // Backtick (`) / Tilde (~) key on US keyboard
        private const uint VK_C = 0x43;      // 'C' key
        private const uint VK_R = 0x52;      // 'R' key

        private HwndSource? _hwndSource;
        private IntPtr _previousForegroundWindow = IntPtr.Zero;
        private bool _isHiding = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionWindowAtTopCenter();

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

        private void PositionWindowAtTopCenter()
        {
            var workArea = SystemParameters.WorkArea;
            this.Left = (workArea.Width - this.Width) / 2 + workArea.Left;
            this.Top = workArea.Top + 10; // 10px offset from the very top
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
            if (Resources["SlideIn"] is Storyboard slideIn)
            {
                slideIn.Begin(this);
            }
        }

        public void HideHUD()
        {
            if (_isHiding) return;
            _isHiding = true;

            // Play Slide Out animation
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
            }
            else
            {
                ResultsList.ItemsSource = null;
                ResultsList.Visibility = Visibility.Collapsed;
                DividerLine.Visibility = Visibility.Collapsed;
            }
        }

        private void SearchInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideHUD();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (ResultsList.Visibility == Visibility.Visible && ResultsList.Items.Count > 0)
                {
                    ResultsList.Focus();
                    var container = ResultsList.ItemContainerGenerator.ContainerFromIndex(ResultsList.SelectedIndex) as ListBoxItem;
                    container?.Focus();
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

        private void ExecuteSelection()
        {
            if (ResultsList.SelectedItem is CommandResult selectedItem)
            {
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
