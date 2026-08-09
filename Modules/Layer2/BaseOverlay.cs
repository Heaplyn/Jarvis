// Developer: heaplyn
// Date: 2026-08-09
// Summary: Base class for draggable, glassmorphic overlays with header title bar, close buttons, and fade transitions.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using Button = System.Windows.Controls.Button;

namespace JarvisLauncher
{
    public abstract class BaseOverlay : Window
    {
        private Grid _mainGrid;
        private Border _mainBorder;
        private ContentPresenter _contentPresenter;
        private TextBlock _titleTextBlock;
        private Button _closeButton;

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
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ShowInTaskbar = false;
            this.Topmost = true;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Opacity = 0; // Hidden initially for fade-in

            var brushConverter = new BrushConverter();
            var bgBrush = (Brush)(brushConverter.ConvertFromString(bgColor) ?? Brushes.Black);
            var txtBrush = (Brush)(brushConverter.ConvertFromString(txtColor) ?? Brushes.White);
            var borderBrush = (Brush)(brushConverter.ConvertFromString(bdrColor) ?? Brushes.Purple);

            // 1. Drop shadow container
            _mainBorder = new Border
            {
                Background = bgBrush,
                BorderBrush = borderBrush,
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

            // 2. Main Grid Layout
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 3. Header Panel (Title + Close button)
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title Block
            _titleTextBlock = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.FromArgb(178, 255, 255, 255)), // Semi-translucent white
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Semibold, Arial"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_titleTextBlock, 0);
            headerGrid.Children.Add(_titleTextBlock);

            // Close Button [X]
            _closeButton = new Button
            {
                Content = "×",
                Foreground = txtBrush,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 18,
                FontFamily = new FontFamily("Arial"),
                Width = 24,
                Height = 24,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(0, -2, 0, 0),
                Focusable = false
            };

            // Style override to make it transparent on hover
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
            _closeButton.Style = style;

            _closeButton.Click += (s, e) => FadeOutAndClose();
            Grid.SetColumn(_closeButton, 1);
            headerGrid.Children.Add(_closeButton);

            Grid.SetRow(headerGrid, 0);
            _mainGrid.Children.Add(headerGrid);

            // 4. Content presenter
            _contentPresenter = new ContentPresenter();
            Grid.SetRow(_contentPresenter, 1);
            _mainGrid.Children.Add(_contentPresenter);

            _mainBorder.Child = _mainGrid;
            this.Content = _mainBorder;

            // Trigger dragging on mouse down
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            // Hook Fade-in
            this.Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(0, 1.0, TimeSpan.FromMilliseconds(200));
                this.BeginAnimation(Window.OpacityProperty, fadeIn);
            };
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
