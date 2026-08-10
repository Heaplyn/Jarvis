// Developer: heaplyn
// Date: 2026-08-10
// Summary: Draggable, resizable glassmorphic Sticky Note overlay.
//          Saves changes with a 500ms debounce to Data/Instructions/sticky_notes.txt,
//          making the content instantly accessible to Jarvis AI companion instructions.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JarvisLauncher
{
    public class StickyNotesOverlay : BaseOverlay
    {
        private static StickyNotesOverlay? _instance;
        private readonly TextBox _noteTextBox;
        private readonly DispatcherTimer _debounceTimer;

        public static void Open()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new StickyNotesOverlay();
                    _instance.Closed += (s, e) => _instance = null;
                }
                _instance.Show();
                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }
                _instance.Activate();
                _instance.Focus();
            });
        }

        public static void Toggle()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    Open();
                }
                else
                {
                    _instance.FadeOutAndClose();
                    _instance = null;
                }
            });
        }

        private StickyNotesOverlay()
            : base("📌 JARVIS DESKTOP STICKY NOTE", width: 300, height: 300)
        {
            _noteTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Padding = new Thickness(4)
            };
            _noteTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _noteTextBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");

            // Load initial text
            LoadNoteContent();

            // Setup 500ms debounce timer for disk writes
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            _noteTextBox.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            var grid = new Grid { Margin = new Thickness(4) };
            grid.Children.Add(_noteTextBox);
            this.UserContent = grid;

            this.Loaded += (s, e) =>
            {
                _noteTextBox.Focus();
                // Put caret at end
                _noteTextBox.SelectionStart = _noteTextBox.Text.Length;
            };
        }

        private string GetNoteFilePath()
        {
            string dir = InstructionsManager.InstructionsDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "sticky_notes.txt");
        }

        private void LoadNoteContent()
        {
            try
            {
                string file = GetNoteFilePath();
                if (File.Exists(file))
                {
                    _noteTextBox.Text = File.ReadAllText(file);
                }
                else
                {
                    _noteTextBox.Text = "Write your notes here... Jarvis reads this automatically!";
                }
            }
            catch { }
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            try
            {
                string file = GetNoteFilePath();
                File.WriteAllText(file, _noteTextBox.Text);
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Ensure any pending changes are written before closing
            if (_debounceTimer.IsEnabled)
            {
                DebounceTimer_Tick(null, EventArgs.Empty);
            }
            base.OnClosed(e);
        }
    }
}
