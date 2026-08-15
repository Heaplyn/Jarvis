// Developer: heaplyn
// Date: 2026-08-09
// Summary: Persistent singleton console terminal output window. Upgraded with RichText support for high-visibility system logging.

using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace JarvisLauncher
{
    public class CliOutputOverlay : BaseOverlay
    {
        private static CliOutputOverlay? _instance;
        private readonly RichTextBox _richTextBox;

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
            : base("JARVIS SYSTEM TERMINAL", width: 750, height: 480)
        {
            this.Closed += (s, e) => { _instance = null; };

            _richTextBox = new RichTextBox
            {
                IsReadOnly = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Document = new FlowDocument()
            };
            _richTextBox.Document.PagePadding = new Thickness(6);
            _richTextBox.SetResourceReference(RichTextBox.ForegroundProperty, "TextPrimaryBrush");

            this.UserContent = _richTextBox;
        }

        private void AppendOutput(string commandTitle, string outputContent)
        {
            var paragraph = new Paragraph();

            // 1. Command Header (High Visibility)
            paragraph.Inlines.Add(new Run($"\n>>> [{DateTime.Now:HH:mm:ss}] EXEC: {commandTitle.ToUpper()}\n")
            {
                Foreground = Brushes.Lime,
                FontWeight = FontWeights.Bold
            });
            
            paragraph.Inlines.Add(new Run(new string('-', 80) + "\n") { Foreground = Brushes.DimGray });

            // 2. Output Body
            string cleanOutput = string.IsNullOrEmpty(outputContent) ? "[No Output Returned]" : outputContent;

            // Heuristic coloring: if output looks like an error, make it red-ish
            Brush outputColor = Brushes.White;
            string lowerOutput = cleanOutput.ToLowerInvariant();
            if (lowerOutput.Contains("error") || lowerOutput.Contains("fail") || lowerOutput.Contains("exception"))
                outputColor = Brushes.Tomato;
            else if (lowerOutput.Contains("warning"))
                outputColor = Brushes.Gold;

            paragraph.Inlines.Add(new Run(cleanOutput + "\n") { Foreground = outputColor });
            
            paragraph.Inlines.Add(new Run(new string('-', 80) + "\n") { Foreground = Brushes.DimGray });

            _richTextBox.Document.Blocks.Add(paragraph);

            // Auto-scroll to the bottom
            _richTextBox.ScrollToEnd();
        }

        private static void WriteLogToDisk(string commandTitle, string outputContent)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                string logPath = Path.Combine(dataDir, "Jarvis.log");
                string logEntry = $"\n>>> [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EXEC: {commandTitle.ToUpper()}\n" +
                                  "--------------------------------------------------------------------------------\n" +
                                  $"{outputContent}\n" +
                                  "--------------------------------------------------------------------------------\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }
    }
}
