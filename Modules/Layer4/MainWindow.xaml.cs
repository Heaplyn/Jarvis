// Developer: heaplyn
// Date: 2026-08-08
// Summary: Main HUD window code-behind.

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
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000;
        private const int CTRL_SHIFT_C_ID = 9001;
        private const int CTRL_SHIFT_R_ID = 9002;
        private const uint VK_OEM_3 = 0xC0; // Backtick
        private const uint VK_C = 0x43;
        private const uint VK_R = 0x52;

        private HwndSource? SourceHwnd;
        private IntPtr PreviousForegroundWindow = IntPtr.Zero;
        private bool IsHiding = false;
        private DateTime StartupTime = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object Sender, RoutedEventArgs E)
        {
            PositionWindowAtTopCenter();
            try { StartMenuRegistrar.EnsureStartMenuShortcut(); } catch { }

            CommandParser.OnTextOpacityChanged += (OpacityValue) =>
            {
                Dispatcher.Invoke(() => {
                    SearchInput.Opacity = OpacityValue;
                    PlaceholderText.Opacity = OpacityValue;
                    ResultsList.Opacity = OpacityValue;
                });
            };
        }

        public void PositionWindowAtTopCenter()
        {
            // Use WPF SystemParameters (DIPs) to ensure perfect centering regardless of monitor scaling (125%, 150%, etc.)
            // workArea.Width and workArea.Left are already scaled for WPF.
            var WorkArea = SystemParameters.WorkArea;
            double WindowWidth = 680;

            this.Left = WorkArea.Left + (WorkArea.Width - WindowWidth) / 2;
            this.Top = WorkArea.Top + 10;
        }

        protected override void OnSourceInitialized(EventArgs E)
        {
            base.OnSourceInitialized(E);
            var Helper = new WindowInteropHelper(this);
            SourceHwnd = HwndSource.FromHwnd(Helper.Handle);
            SourceHwnd.AddHook(HwndHook);

            NativeMethods.RegisterHotKey(Helper.Handle, HOTKEY_ID, 0, VK_OEM_3);
            NativeMethods.RegisterHotKey(Helper.Handle, CTRL_SHIFT_C_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, VK_C);
            NativeMethods.RegisterHotKey(Helper.Handle, CTRL_SHIFT_R_ID, NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT, VK_R);
        }

        private IntPtr HwndHook(IntPtr Hwnd, int Msg, IntPtr WParam, IntPtr LParam, ref bool Handled)
        {
            if (Msg == NativeMethods.WM_HOTKEY)
            {
                int Id = WParam.ToInt32();
                if (Id == HOTKEY_ID) { ToggleHUD(); Handled = true; }
                else if (Id == CTRL_SHIFT_C_ID) { Application.Current.Shutdown(); Handled = true; }
                else if (Id == CTRL_SHIFT_R_ID)
                {
                    TextOverlay.Show("Syncing & Rebuilding...", 2000);
                    NativeMethods.Restart(freshBoot: true, pullFirst: true);
                    Handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void ToggleHUD() { if (this.Visibility == Visibility.Visible && !IsHiding) HideHUD(); else ShowHUD(); }

        public void ShowHUD()
        {
            IsHiding = false;
            PositionWindowAtTopCenter();

            this.Visibility = Visibility.Visible;
            this.Opacity = 1.0;
            MainBorder.Opacity = 1.0;
            WindowTranslate.Y = 0;

            this.Topmost = true;
            this.Activate();
            SearchInput.Focus();
            Keyboard.Focus(SearchInput);

            if (SettingsManager.Current.ENABLE_ANIMATIONS)
            {
                if (Resources["SlideIn"] is Storyboard SlideIn) SlideIn.Begin(this);
            }
        }

        public void HideHUD()
        {
            if (IsHiding) return;
            IsHiding = true;
            if (SettingsManager.Current.ENABLE_ANIMATIONS && Resources["SlideOut"] is Storyboard SlideOut)
            {
                SlideOut.Completed += (S, E) => { if (IsHiding) { this.Visibility = Visibility.Collapsed; SearchInput.Text = ""; IsHiding = false; } };
                SlideOut.Begin(this);
            }
            else { this.Visibility = Visibility.Collapsed; IsHiding = false; }
        }

        private void SearchInput_TextChanged(object Sender, TextChangedEventArgs E)
        {
            string Query = SearchInput.Text;
            PlaceholderText.Visibility = string.IsNullOrEmpty(Query) ? Visibility.Visible : Visibility.Collapsed;
            var Suggestions = CommandParser.GetSuggestions(Query);
            if (Suggestions.Count > 0)
            {
                ResultsList.ItemsSource = Suggestions;
                ResultsList.Visibility = Visibility.Visible;
                DividerLine.Visibility = Visibility.Visible;
                ResultsList.SelectedIndex = 0;
            }
            else { ResultsList.ItemsSource = null; ResultsList.Visibility = Visibility.Collapsed; DividerLine.Visibility = Visibility.Collapsed; }
        }

        private void SearchInput_PreviewKeyDown(object Sender, KeyEventArgs E)
        {
            if (E.Key == Key.Escape) { HideHUD(); E.Handled = true; }
            else if (E.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift)) { ExecuteSelection(); E.Handled = true; }
            else if (E.Key == Key.Down && ResultsList.Items.Count > 0) { ResultsList.SelectedIndex = (ResultsList.SelectedIndex + 1) % ResultsList.Items.Count; E.Handled = true; }
            else if (E.Key == Key.Up && ResultsList.Items.Count > 0) { ResultsList.SelectedIndex = (ResultsList.SelectedIndex - 1 + ResultsList.Items.Count) % ResultsList.Items.Count; E.Handled = true; }
        }

        private void ResultsList_PreviewKeyDown(object Sender, KeyEventArgs E)
        {
            if (E.Key == Key.Escape) { HideHUD(); E.Handled = true; }
            else if (E.Key == Key.Enter) { ExecuteSelection(); E.Handled = true; }
            else if (E.Key == Key.Up && ResultsList.SelectedIndex == 0) { SearchInput.Focus(); E.Handled = true; }
        }

        private void ResultsList_MouseDoubleClick(object Sender, MouseButtonEventArgs E) => ExecuteSelection();
        private void ResultsList_PreviewMouseLeftButtonUp(object Sender, MouseButtonEventArgs E) => ExecuteSelection();

        private void ExecuteSelection()
        {
            if (ResultsList.SelectedItem is CommandResult Sel) { try { Sel.EXECUTE?.Invoke(); } catch (Exception Ex) { MessageBox.Show("Error: " + Ex.Message); } HideHUD(); }
            else { string Q = SearchInput.Text.Trim(); if (!string.IsNullOrEmpty(Q)) { CommandParser.ExecuteFirstSuggestion(Q); HideHUD(); } }
        }

        protected override void OnDeactivated(EventArgs E)
        {
            base.OnDeactivated(E);
            if ((DateTime.Now - StartupTime).TotalSeconds < 5) return;
            if (this.Visibility == Visibility.Visible && !IsHiding) HideHUD();
        }
    }
}
