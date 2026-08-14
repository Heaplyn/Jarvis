// Developer: heaplyn
// Date: 2026-08-09
// Summary: Persistent singleton console terminal output window styled in retro green and monospaced font. Resizable & minimizable. Writes logs to disk.

using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class CliOutputOverlay : BaseOverlay
    {
        private static CliOutputOverlay? _instance;
        private TextBox _textBox;

        public static void Show(string commandTitle, string outputContent)
        {
            // Write logs persistently to disk first
            WriteLogToDisk(commandTitle, outputContent);

            // Execute on UI Dispatcher Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_instance == null)
                {
                    _instance = new CliOutputOverlay();
                }

                _instance.AppendOutput(commandTitle, outputContent);
                _instance.Show();
            });
        }

        private CliOutputOverlay()
            : base("JARVIS SYSTEM TERMINAL", width: 650, height: 420)
        {
            this.Closed += (s, e) => { _instance = null; };

            _textBox = new TextBox
            {
                Text = "",
                IsReadOnly = true,
                AcceptsReturn = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(4)
            };
            _textBox.SetResourceReference(TextBox.ForegroundProperty, "TextPrimaryBrush");
            _textBox.SetResourceReference(TextBox.CaretBrushProperty, "AccentCaretBrush");

            this.UserContent = _textBox;
        }

        private void AppendOutput(string commandTitle, string outputContent)
        {
            var sb = new StringBuilder(_textBox.Text);
            
            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine($">>> [{DateTime.Now:HH:mm:ss}] EXEC: {commandTitle.ToUpper()}");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(string.IsNullOrEmpty(outputContent) ? "[No Output Returned]" : outputContent);
            sb.AppendLine("--------------------------------------------------------------------------------");

            _textBox.Text = sb.ToString();
            
            // Auto-scroll to the bottom to display the latest execution logs
            _textBox.ScrollToEnd();
        }

        private static void WriteLogToDisk(string commandTitle, string outputContent)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataDir))
                {
                    string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Data"));
                    if (Directory.Exists(devPath))
                    {
                        dataDir = devPath;
                    }
                    else
                    {
                        Directory.CreateDirectory(dataDir);
                    }
                }

                string logPath = Path.Combine(dataDir, "Jarvis.log");
                string logEntry = $"\n>>> [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EXEC: {commandTitle.ToUpper()}\n" +
                                  "--------------------------------------------------------------------------------\n" +
                                  $"{outputContent}\n" +
                                  "--------------------------------------------------------------------------------\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }

        internal static void GetWindow()
        {
            throw new NotImplementedException();
        }

    }
}
