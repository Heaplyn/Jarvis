// Developer: heaplyn
// Date: 2026-08-09
// Summary: Base class for draggable, glassmorphic, resizable overlays. Minimize shrinks windows to tiny draggable widget pills.

using System;
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
            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Opacity = 0; // Hidden initially for fade-in

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
                CornerRadius = new CornerRadius(12)
            };
            WindowChrome.SetWindowChrome(this, windowChrome);

            // 1. Drop shadow container
            _mainBorder = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
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

            // 2. Main Grid Layout
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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
                FontFamily = new FontFamily("Segoe UI Semibold, Arial"),
                VerticalAlignment = VerticalAlignment.Center
            };
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
            _closeButton.Click += (s, e) => FadeOutAndClose();
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

            // Trigger dragging on mouse down, ignoring resize border click regions (6px)
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point clickPos = e.GetPosition(this);
                    if (clickPos.X > 6 && clickPos.X < this.ActualWidth - 6 &&
                        clickPos.Y > 6 && clickPos.Y < this.ActualHeight - 6)
                    {
                        try { this.DragMove(); } catch { }
                    }
                }
            };

            // Hook Fade-in
            this.Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(200));
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            };
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
        }

        protected object UserContent
        {
            get => _contentPresenter.Content;
            set => _contentPresenter.Content = value;
        }

        public void FadeOutAndClose()
        {
            var fadeOut = new DoubleAnimation(this.Opacity, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) => this.Close();
            this.BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}
