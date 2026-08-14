// Developer: heaplyn
// Date: 2026-08-09
// Summary: Base class for draggable, glassmorphic, resizable overlays. Minimize shrinks windows to tiny draggable widget pills.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using Button = System.Windows.Controls.Button;

namespace JarvisLauncher
{
    public abstract class BaseOverlay : Window
    {
        // ── Z-Order tracking ─────────────────────────────────────────────────────
        private static readonly List<BaseOverlay> _openOverlays = new();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOP    = IntPtr.Zero;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE  = 0x0002;
        private const uint SWP_NOSIZE  = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FLAGS   = SWP_NOMOVE | SWP_NOSIZE;

        /// <summary>Brings this overlay to the top of the z-stack above all other overlays.</summary>
        public void BringToFront()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_FLAGS);
                }
            }
            catch { }
        }

        /// <summary>
        /// Creates a glassmorphic styled button that automatically reacts to active theme colors and hover states.
        /// </summary>
        public static Button CreateStyledButton(string content, RoutedEventHandler? onClick = null, bool isPrimary = false, double fontSize = 12)
        {
            var btn = new Button
            {
                Content = content,
                Margin = new Thickness(4, 2, 4, 2),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                FontSize = fontSize,
                FontFamily = new FontFamily("Segoe UI"),
                Focusable = false
            };

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "Border";
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            if (isPrimary)
            {
                factory.SetResourceReference(Border.BackgroundProperty, "SelectedBackgroundBrush");
                factory.SetResourceReference(Border.BorderBrushProperty, "SelectedBorderBrush");
            }
            else
            {
                factory.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush");
                factory.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            }

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(presenter);
            template.VisualTree = factory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SelectedBackgroundBrush"), "Border"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("AccentCaretBrush"), "Border"));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");

            if (onClick != null) btn.Click += onClick;
            return btn;
        }

        protected TextBlock CreateLabel(string text, double fontSize = 11, bool isBold = true)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, 4, 0, 4)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            return tb;
        }

        protected TextBox CreateTextBox()
        {
            var box = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            box.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            box.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");
            box.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush");
            return box;
        }
        // ─────────────────────────────────────────────────────────────────────────

        private Grid _mainGrid;
        private Border _mainBorder;
        private ContentPresenter _contentPresenter;
        private TextBlock _titleTextBlock;
        private Button _minimizeButton;
        private Button _closeButton;

        private string _originalTitle;
        private double _restoreWidth;
        private double _restoreHeight;
        private bool _isMiniMode = false;
        private bool _forceClose = false;

        /// <summary>
        /// Call this when the overlay truly needs to be destroyed (e.g. on app shutdown).
        /// Normally pressing [X] just hides the window to background.
        /// </summary>
        public void ForceClose()
        {
            _forceClose = true;
            this.Close();
        }

        protected BaseOverlay(
            string title, 
            double width, 
            double height, 
            string bgColor = "#F2140D24", 
            string txtColor = "#FFFFFF",
            string bdrColor = "#4D8050E6")
        {
            this.Width = width;
            this.Height = height;
            this._restoreWidth = width;
            this._restoreHeight = height;
            this._originalTitle = title;

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ShowInTaskbar = true;
            this.Topmost = SettingsManager.Current.AlwaysOnTop;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Register this overlay for z-order tracking & screen position persistence
            _openOverlays.Add(this);
            this.Closed += (s, e) => _openOverlays.Remove(this);
            try { WindowPositionManager.RegisterWindow(this, this.GetType().Name); } catch { }
            if (SettingsManager.Current.EnableAnimations)
            {
                this.Opacity = 0; // Hidden initially for fade-in
            }
            else
            {
                this.Opacity = SettingsManager.Current.WindowOpacity;
            }

            var brushConverter = new BrushConverter();
            var bgBrush = (Brush)(brushConverter.ConvertFromString(bgColor) ?? Brushes.Black);
            var txtBrush = (Brush)(brushConverter.ConvertFromString(txtColor) ?? Brushes.White);
            var borderBrush = (Brush)(brushConverter.ConvertFromString(bdrColor) ?? Brushes.Purple);

            // Configure native window borders and resize capabilities using WindowChrome
            var windowChrome = new WindowChrome
            {
                ResizeBorderThickness = new Thickness(6), // 6px resize zone on all edges
                CaptionHeight = 0,
                GlassFrameThickness = new Thickness(0),
                CornerRadius = SettingsManager.Current.UseRoundedCorners ? new CornerRadius(12) : new CornerRadius(0)
            };
            WindowChrome.SetWindowChrome(this, windowChrome);

            // 1. Drop shadow container
            _mainBorder = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                IsHitTestVisible = true, // Explicitly enable hit testing
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    Color = Colors.Black,
                    Opacity = 0.5,
                    ShadowDepth = 2
                }
            };
            _mainBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");
            _mainBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");
            _mainBorder.SetResourceReference(Border.CornerRadiusProperty, "WindowCornerRadius");

            // 2. Main Grid Layout
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 2b. GIF Background (if enabled)
            var bgMedia = new Image
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 0.5,
                IsHitTestVisible = false // Ensure clicks pass through to controls
            };
            bgMedia.SetResourceReference(Image.VisibilityProperty, "WindowMediaVisibility");
            // We'll set the source in the Loaded event to ensure resources are ready
            this.Loaded += (s, e) =>
            {
                if (Application.Current.Resources["WindowBackgroundMediaSource"] is ImageSource imgSource)
                {
                    try
                    {
                        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(bgMedia, imgSource);
                        WpfAnimatedGif.ImageBehavior.SetRepeatBehavior(bgMedia, RepeatBehavior.Forever);
                    }
                    catch { }
                }
            };
            Grid.SetRowSpan(bgMedia, 2);
            _mainGrid.Children.Add(bgMedia);

            // 3. Header Panel (Title + Minimize/Close control stack)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title Block
            _titleTextBlock = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            _titleTextBlock.SetResourceReference(TextBlock.FontFamilyProperty, "ActiveFontFamily");
            _titleTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(_titleTextBlock, 0);
            headerGrid.Children.Add(_titleTextBlock);

            // Control Stack (Minimize + Close)
            var controlStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(controlStack, 1);

            // Shared button hover style
            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(presenter);
            template.VisualTree = factory;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            // Minimize Button [-] (Toggle Mini-Widget mode)
            _minimizeButton = new Button
            {
                Content = "—", // Em-dash
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontFamily = new FontFamily("Arial"),
                Width = 24,
                Height = 24,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -4, 0, 0),
                Focusable = false,
                Style = style,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _minimizeButton.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _minimizeButton.Click += (s, e) => ToggleMiniMode();
            controlStack.Children.Add(_minimizeButton);

            // Close Button [X]
            _closeButton = new Button
            {
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 18,
                FontFamily = new FontFamily("Arial"),
                Width = 24,
                Height = 24,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, -2, 0, 0),
                Focusable = false,
                Style = style
            };
            _closeButton.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            _closeButton.Click += (s, e) => FadeOutAndHide();
            controlStack.Children.Add(_closeButton);

            headerGrid.Children.Add(controlStack);

            Grid.SetRow(headerGrid, 0);
            _mainGrid.Children.Add(headerGrid);

            // 4. Content presenter
            _contentPresenter = new ContentPresenter();
            Grid.SetRow(_contentPresenter, 1);
            _mainGrid.Children.Add(_contentPresenter);

            _mainBorder.Child = _mainGrid;
            this.Content = _mainBorder;

            // Bring to front whenever user clicks anywhere on this overlay
            // We use MouseLeftButtonDown instead of Preview to avoid blocking child interactions (Buttons, TextBoxes)
            this.MouseLeftButtonDown += (s, e) =>
            {
                BringToFront();
            };

            // Allow dragging from the header area ONLY.
            headerGrid.MouseLeftButtonDown += (s, e) =>
            {
                // Only initiate drag if the user clicked the header background or title,
                // not a child element like the Close or Minimize buttons.
                if (e.LeftButton == MouseButtonState.Pressed && (e.OriginalSource == headerGrid || e.OriginalSource is TextBlock || e.OriginalSource is Border))
                {
                    try { this.DragMove(); } catch { }
                }
            };

            // Bring to front on focus (e.g. Alt+Tab or programmatic Activate())
            this.Activated += (s, e) => BringToFront();

            // Window Bounds & Minimized State Memory Persistence Hooks
            this.LocationChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
            this.SizeChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
            this.StateChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);

            // Hook Fade-in + initial z-order elevation + restore window memory
            this.Loaded += (s, e) =>
            {
                BringToFront();
                AttachPasteContextMenuToAllTextBoxes(this);

                if (WindowMemoryManager.RestoreWindowBounds(_originalTitle, this, out bool storedMiniMode))
                {
                    if (storedMiniMode && !_isMiniMode) ToggleMiniMode();
                }

                if (SettingsManager.Current.EnableAnimations)
                {
                    var fadeIn = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(200));
                    this.BeginAnimation(Window.OpacityProperty, fadeIn);
                }
                else
                {
                    this.Opacity = SettingsManager.Current.WindowOpacity;
                }
            };

            // Bring to front when made visible again (re-shown from hidden)
            this.IsVisibleChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    BringToFront();
                    AttachPasteContextMenuToAllTextBoxes(this);
                }
            };
        }

        private static void AttachPasteContextMenuToAllTextBoxes(DependencyObject parent)
        {
            if (parent == null) return;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBox box)
                {
                    if (box.ContextMenu == null)
                    {
                        var menu = new ContextMenu();

                        var pasteItem = new MenuItem { Header = "📋 Paste (Ctrl+V)" };
                        pasteItem.Click += (s, e) => { if (Clipboard.ContainsText()) box.Paste(); };
                        menu.Items.Add(pasteItem);

                        var copyItem = new MenuItem { Header = "📄 Copy (Ctrl+C)" };
                        copyItem.Click += (s, e) => box.Copy();
                        menu.Items.Add(copyItem);

                        var cutItem = new MenuItem { Header = "✂️ Cut (Ctrl+X)" };
                        cutItem.Click += (s, e) => box.Cut();
                        menu.Items.Add(cutItem);

                        var selectAllItem = new MenuItem { Header = "🔍 Select All (Ctrl+A)" };
                        selectAllItem.Click += (s, e) => box.SelectAll();
                        menu.Items.Add(selectAllItem);

                        box.ContextMenu = menu;
                    }
                }
                AttachPasteContextMenuToAllTextBoxes(child);
            }
        }

        private void ToggleMiniMode()
        {
            if (!_isMiniMode)
            {
                // Cache user-adjusted dimensions
                _restoreWidth = this.Width;
                _restoreHeight = this.Height;

                // Collapse content & disable resizing
                _contentPresenter.Visibility = Visibility.Collapsed;
                _minimizeButton.Content = "+";
                this.ResizeMode = ResizeMode.NoResize;

                // Format compact title text
                string compactTitle = _originalTitle
                    .Replace("JARVIS ", "")
                    .Replace(" SYSTEM", "")
                    .Replace(" COMPANION", "")
                    .Trim();
                _titleTextBlock.Text = $"{compactTitle} (MINI)";
                _titleTextBlock.FontSize = 10;

                // Shrink window layout
                this.Width = 195;
                this.Height = 48;

                _isMiniMode = true;
            }
            else
            {
                // Restore user-adjusted dimensions
                this.Width = _restoreWidth;
                this.Height = _restoreHeight;

                // Restore content & enable resizing
                _contentPresenter.Visibility = Visibility.Visible;
                _minimizeButton.Content = "—";
                this.ResizeMode = ResizeMode.CanResizeWithGrip;

                // Restore header text
                _titleTextBlock.Text = _originalTitle;
                _titleTextBlock.FontSize = 12;

                _isMiniMode = false;
            }

            WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
        }

        protected object UserContent
        {
            get => _contentPresenter.Content;
            set => _contentPresenter.Content = value;
        }

        /// <summary>Hides the window to background (default [X] button behaviour).</summary>
        public void FadeOutAndHide()
        {
            if (SettingsManager.Current.EnableAnimations)
            {
                var fadeOut = new DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) =>
                {
                    this.Hide();
                    // Clear the animation so it doesn't "lock" the opacity at 0
                    this.BeginAnimation(Window.OpacityProperty, null);
                    this.Opacity = 0;
                };
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
            else
            {
                this.Hide();
            }
        }

        /// <summary>Standard way to show any overlay, ensuring it fades in if animations are on.</summary>
        public new void Show()
        {
            // Stop any running animations
            this.BeginAnimation(Window.OpacityProperty, null);

            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;

            base.Show();
            this.Activate();

            if (SettingsManager.Current.EnableAnimations)
            {
                this.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(250));
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            }
            else
            {
                this.Opacity = SettingsManager.Current.WindowOpacity;
            }

            BringToFront();
        }

        /// <summary>Fades out and truly closes (destroys) the window.</summary>
        public void FadeOutAndClose()
        {
            _forceClose = true;
            if (SettingsManager.Current.EnableAnimations)
            {
                var fadeOut = new DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) => this.Close();
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
            else
            {
                this.Close();
            }
        }

        public static void StyleTabControl(TabControl tabControl)
        {
            tabControl.Background = Brushes.Transparent;
            tabControl.BorderThickness = new Thickness(0);

            // Create TabItem Style
            var tabItemStyle = new Style(typeof(TabItem));
            
            // Set Default Properties
            tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))));
            tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, Brushes.Transparent));
            tabItemStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
            tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(14, 8, 14, 8)));
            tabItemStyle.Setters.Add(new Setter(TabItem.MarginProperty, new Thickness(0, 0, 6, 0)));
            tabItemStyle.Setters.Add(new Setter(TabItem.CursorProperty, Cursors.Hand));
            tabItemStyle.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeights.SemiBold));
            tabItemStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, 12.0));

            // Create ControlTemplate
            var template = new ControlTemplate(typeof(TabItem));
            
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "TabBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6, 6, 0, 0));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TabItem.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TabItem.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TabItem.BorderThicknessProperty));
            borderFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TabItem.PaddingProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.Name = "ContentSite";
            contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);

            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            // Trigger: MouseOver
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(15, 255, 255, 255))));
            hoverTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            template.Triggers.Add(hoverTrigger);

            // Trigger: Selected
            var selectedTrigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(TabItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 0, 255, 255))));
            selectedTrigger.Setters.Add(new Setter(TabItem.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(255, 0, 255, 255))));
            selectedTrigger.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0, 0, 0, 3)));
            selectedTrigger.Setters.Add(new Setter(TabItem.ForegroundProperty, new SolidColorBrush(Color.FromArgb(255, 0, 255, 255))));
            template.Triggers.Add(selectedTrigger);

            tabItemStyle.Setters.Add(new Setter(TabItem.TemplateProperty, template));

            // Apply style to all items dynamically
            tabControl.ItemContainerStyle = tabItemStyle;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClose)
            {
                // Intercept close and hide to background instead
                e.Cancel = true;
                FadeOutAndHide();
                return;
            }
            base.OnClosing(e);
        }
    }

    public static class ObjectExtensions
    {
        public static T Also<T>(this T self, Action<T> action)
        {
            action?.Invoke(self);
            return self;
        }
    }
}
