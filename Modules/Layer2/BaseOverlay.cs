// Developer: heaplyn
// Date: 2026-08-16
// Summary: Master glassmorphic base overlay with robust window management and high-performance styles.

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
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Button = System.Windows.Controls.Button;

namespace JarvisLauncher
{
    public abstract class BaseOverlay : Window
    {
        private static readonly List<BaseOverlay> _openOverlays = new();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        public void BringToFront()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, HWND_TOP, 0, 0, 0, 0, SWP_FLAGS);
            }
            catch { }
        }

        public static Button CreateStyledButton(string content, RoutedEventHandler? onClick = null, bool isPrimary = false, double fontSize = 12)
        {
            var btn = new Button { Content = content, Margin = new Thickness(4, 2, 4, 2), Padding = new Thickness(12, 6, 12, 6), Cursor = Cursors.Hand, FontSize = fontSize, FontFamily = new FontFamily("Segoe UI"), Focusable = false };
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.Name = "Border"; factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6)); factory.SetValue(Border.BorderThicknessProperty, new Thickness(1)); factory.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
            if (isPrimary) { factory.SetResourceReference(Border.BackgroundProperty, "SelectedBackgroundBrush"); factory.SetResourceReference(Border.BorderBrushProperty, "SelectedBorderBrush"); }
            else { factory.SetResourceReference(Border.BackgroundProperty, "HoverBackgroundBrush"); factory.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush"); }
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center); factory.AppendChild(presenter); template.VisualTree = factory;
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("SelectedBackgroundBrush"), "Border"));
            template.Triggers.Add(hoverTrigger);
            btn.Template = template; btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            if (onClick != null) btn.Click += onClick; return btn;
        }

        public static TextBlock CreateLabel(string text, double fontSize = 11, bool isBold = true) { var tb = new TextBlock { Text = text, FontSize = fontSize, FontWeight = isBold ? FontWeights.SemiBold : FontWeights.Normal, Margin = new Thickness(0, 4, 0, 4) }; tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush"); return tb; }
        public static TextBox CreateTextBox() { var box = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(1), Padding = new Thickness(6, 4, 6, 4), FontSize = 12, FontFamily = new FontFamily("Segoe UI"), Margin = new Thickness(0, 0, 0, 8) }; box.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush"); box.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush"); box.SetResourceReference(TextBox.BorderBrushProperty, "WindowBorderBrush"); return box; }

        protected Grid _mainGrid = null!;
        protected Border _mainBorder = null!;
        protected ContentPresenter _contentPresenter = null!;
        protected TextBlock _titleTextBlock = null!;
        protected StackPanel _controlStack = null!;
        protected Button _standardMinButton = null!;
        protected Button _maximizeButton = null!;
        protected Button _miniModeButton = null!;
        protected Button _closeButton = null!;

        protected string _originalTitle = string.Empty;
        protected double _restoreWidth;
        protected double _restoreHeight;
        protected bool _isMiniMode = false;
        protected bool _forceClose = false;

        public void ForceClose() { _forceClose = true; this.Close(); }

        protected BaseOverlay(string title, double width, double height, string? d1 = null, string? d2 = null, string? d3 = null, string? d4 = null, string? d5 = null)
        {
            this.Width = width; this.Height = height; this._restoreWidth = width; this._restoreHeight = height; this._originalTitle = title;
            this.WindowStyle = WindowStyle.None; this.AllowsTransparency = true; this.Background = Brushes.Transparent; this.ShowInTaskbar = true;
            this.Topmost = SettingsManager.Current.ALWAYS_ON_TOP; this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _openOverlays.Add(this); this.Closed += (s, e) => _openOverlays.Remove(this);

            var windowChrome = new WindowChrome { ResizeBorderThickness = new Thickness(6), CaptionHeight = 0, GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(12) };
            WindowChrome.SetWindowChrome(this, windowChrome);

            _mainBorder = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), IsHitTestVisible = true, Effect = new DropShadowEffect { BlurRadius = 15, Color = Colors.Black, Opacity = 0.5, ShadowDepth = 2 } };
            _mainBorder.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush"); _mainBorder.SetResourceReference(Border.BorderBrushProperty, "WindowBorderBrush");

            _mainGrid = new Grid { Margin = new Thickness(12, 10, 12, 12) };
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var bgMedia = new Image { Stretch = Stretch.UniformToFill, Opacity = 0.5, IsHitTestVisible = false };
            bgMedia.SetResourceReference(Image.VisibilityProperty, "WindowMediaVisibility");
            this.Loaded += (s, e) => {
                if (Application.Current.Resources["WindowBackgroundMediaSource"] is ImageSource imgSource) {
                    try { WpfAnimatedGif.ImageBehavior.SetAnimatedSource(bgMedia, imgSource); WpfAnimatedGif.ImageBehavior.SetRepeatBehavior(bgMedia, RepeatBehavior.Forever); } catch { }
                }
                if (WindowMemoryManager.RestoreWindowBounds(_originalTitle, this, out bool storedMiniMode)) {
                    if (storedMiniMode && !_isMiniMode) ToggleMiniMode();
                }
                BringToFront();
            };

            this.LocationChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
            this.SizeChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
            this.StateChanged += (s, e) => WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
            Grid.SetRowSpan(bgMedia, 2); _mainGrid.Children.Add(bgMedia);

            var headerGrid = new Grid { Height = 30, Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _titleTextBlock = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
            _titleTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetColumn(_titleTextBlock, 0); headerGrid.Children.Add(_titleTextBlock);

            _controlStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnStyle = new Style(typeof(Button));
            var btnTemplate = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(presenter); btnTemplate.VisualTree = factory; btnStyle.Setters.Add(new Setter(Button.TemplateProperty, btnTemplate));

            _standardMinButton = CreateHeaderButton("—", (s, e) => this.WindowState = WindowState.Minimized, btnStyle);
            _maximizeButton = CreateHeaderButton("⬜", (s, e) => this.WindowState = (this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized), btnStyle, fontSize: 10);
            _miniModeButton = CreateHeaderButton("◱", (s, e) => ToggleMiniMode(), btnStyle);
            _closeButton = CreateHeaderButton("×", (s, e) => FadeOutAndHide(), btnStyle, fontSize: 18);

            _controlStack.Children.Add(_standardMinButton);
            _controlStack.Children.Add(_maximizeButton);
            _controlStack.Children.Add(_miniModeButton);
            _controlStack.Children.Add(_closeButton);

            Grid.SetColumn(_controlStack, 1); headerGrid.Children.Add(_controlStack);
            _mainGrid.Children.Add(headerGrid);

            _contentPresenter = new ContentPresenter(); Grid.SetRow(_contentPresenter, 1); _mainGrid.Children.Add(_contentPresenter);
            _mainBorder.Child = _mainGrid; this.Content = _mainBorder;

            headerGrid.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) try { this.DragMove(); } catch { } };
        }

        private Button CreateHeaderButton(string content, RoutedEventHandler onClick, Style style, double fontSize = 14)
        {
            var btn = new Button { Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = fontSize, Width = 28, Height = 28, Cursor = Cursors.Hand, Style = style, Margin = new Thickness(4, 0, 0, 0), Focusable = false };
            btn.SetResourceReference(Button.ForegroundProperty, "TextPrimaryBrush");
            btn.Click += onClick; return btn;
        }

        public void ToggleMiniMode()
        {
            if (!_isMiniMode) {
                if (this.WindowState == WindowState.Maximized) this.WindowState = WindowState.Normal;
                _restoreWidth = this.ActualWidth; _restoreHeight = this.ActualHeight;
                _contentPresenter.Visibility = Visibility.Collapsed;
                _miniModeButton.Content = "+";
                _standardMinButton.Visibility = Visibility.Collapsed;
                _maximizeButton.Visibility = Visibility.Collapsed;
                this.ResizeMode = ResizeMode.NoResize; this.MinWidth = 0; this.MinHeight = 0;
                this.Width = 210; this.Height = 52; _isMiniMode = true;
            } else {
                _standardMinButton.Visibility = Visibility.Visible;
                _maximizeButton.Visibility = Visibility.Visible;
                _contentPresenter.Visibility = Visibility.Visible;
                _miniModeButton.Content = "◱";
                this.MinWidth = 400; this.MinHeight = 300;
                this.Width = _restoreWidth; this.Height = _restoreHeight;
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                this.WindowState = WindowState.Normal; _isMiniMode = false;
            }
            WindowMemoryManager.SaveWindowBounds(_originalTitle, this, _isMiniMode);
        }

        public new void Show()
        {
            if (this.WindowState == WindowState.Maximized) this.WindowState = WindowState.Normal;
            base.Show(); this.Activate(); BringToFront();
        }

        public void FadeOutAndClose() { _forceClose = true; this.Close(); }
        public virtual void FadeOutAndHide() { this.Hide(); }
        protected object UserContent { get => _contentPresenter.Content; set => _contentPresenter.Content = value; }

        public static void StyleTabControl(TabControl tabControl)
        {
            tabControl.Background = Brushes.Transparent;
            tabControl.BorderThickness = new Thickness(0);

            var itemStyle = new Style(typeof(TabItem));
            itemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(10, 5, 10, 5)));
            itemStyle.Setters.Add(new Setter(TabItem.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(TabItem));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Bd";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TabItem.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(TabItem.PaddingProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4, 4, 0, 0));

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            border.AppendChild(presenter);
            template.VisualTree = border;

            template.Triggers.Add(new Trigger { Property = TabItem.IsSelectedProperty, Value = true, Setters = {
                new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), "Bd"),
                new Setter(TabItem.ForegroundProperty, Brushes.Cyan)
            }});

            itemStyle.Setters.Add(new Setter(TabItem.TemplateProperty, template));
            tabControl.ItemContainerStyle = itemStyle;
        }

        public static void StyleTreeView(TreeView tv)
        {
            tv.Background = Brushes.Transparent; tv.BorderThickness = new Thickness(0);
            var itemStyle = new Style(typeof(TreeViewItem));
            itemStyle.Setters.Add(new Setter(TreeViewItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(TreeViewItem.FontSizeProperty, 12.0));

            var template = new ControlTemplate(typeof(TreeViewItem));
            var outerStack = new FrameworkElementFactory(typeof(StackPanel));

            var headerStack = new FrameworkElementFactory(typeof(StackPanel));
            headerStack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.Name = "Expander"; toggle.SetValue(ToggleButton.IsCheckedProperty, new TemplateBindingExtension(TreeViewItem.IsExpandedProperty));
            toggle.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
            var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
            var tBorder = new FrameworkElementFactory(typeof(Border)); tBorder.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var tPath = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path)); tPath.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 4 4 L 0 8 Z")); tPath.SetValue(System.Windows.Shapes.Path.FillProperty, Brushes.Gray); tPath.SetValue(System.Windows.Shapes.Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            tBorder.AppendChild(tPath); toggleTemplate.VisualTree = tBorder;
            var tExpanded = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true }; tExpanded.Setters.Add(new Setter(System.Windows.Shapes.Path.RenderTransformProperty, new RotateTransform(90, 2, 4))); toggleTemplate.Triggers.Add(tExpanded);
            toggle.SetValue(ToggleButton.TemplateProperty, toggleTemplate);

            var contentBorder = new FrameworkElementFactory(typeof(Border));
            contentBorder.Name = "Bd";
            contentBorder.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));
            contentBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            var headerContent = new FrameworkElementFactory(typeof(ContentPresenter));
            headerContent.Name = "PART_Header";
            headerContent.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            headerContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentBorder.AppendChild(headerContent);

            headerStack.AppendChild(toggle);
            headerStack.AppendChild(contentBorder);

            var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            itemsPresenter.Name = "ItemsHost";
            itemsPresenter.SetValue(ItemsPresenter.MarginProperty, new Thickness(15, 0, 0, 0));

            outerStack.AppendChild(headerStack);
            outerStack.AppendChild(itemsPresenter);
            template.VisualTree = outerStack;

            template.Triggers.Add(new Trigger { Property = TreeViewItem.IsExpandedProperty, Value = false, Setters = { new Setter(ItemsPresenter.VisibilityProperty, Visibility.Collapsed, "ItemsHost") } });
            template.Triggers.Add(new Trigger { Property = TreeViewItem.HasItemsProperty, Value = false, Setters = { new Setter(ToggleButton.VisibilityProperty, Visibility.Hidden, "Expander") } });
            template.Triggers.Add(new Trigger { Property = TreeViewItem.IsSelectedProperty, Value = true, Setters = { new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xFF, 0xFF)), "Bd"), new Setter(TreeViewItem.ForegroundProperty, Brushes.White) } });
            itemStyle.Setters.Add(new Setter(TreeViewItem.TemplateProperty, template)); tv.ItemContainerStyle = itemStyle;
        }
    }
}
