// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles quick note appending (`note <text>`) and reminder popups (`remind <time> <msg>`).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class ProductivityCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "note" || query.StartsWith("note ") || 
                   query == "remind" || query.StartsWith("remind ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLower();

            if (cmd == "note")
            {
                if (parts.Length > 1)
                {
                    string text = parts[1];
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Append Note: \"{text}\"",
                        Description = "Save quick timestamped entry into notes.txt",
                        Similarity  = 2.0,
                        Execute     = () => AppendNote(text)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Quick Note (Prompt)...",
                        Description = "Prompt for text entry to append into notes.txt",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter note text to save:", (text) => AppendNote(text))
                    });

                    suggestions.Add(new CommandResult
                    {
                        Title       = "View / Edit notes.txt",
                        Description = "Open notes.txt in Jarvis text editor",
                        Similarity  = 1.0,
                        Execute     = () => TextEditorOverlay.OpenFile("notes.txt")
                    });
                }
            }
            else if (cmd == "remind")
            {
                if (parts.Length > 1)
                {
                    string args = parts[1];
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Set Reminder: {args}",
                        Description = "e.g. '10s Check oven' or '5m Take a break'",
                        Similarity  = 2.0,
                        Execute     = () => ParseAndSetReminder(args)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Set Reminder (Prompt)...",
                        Description = "Prompt for reminder format: <duration> <message> (e.g. 5m Take break)",
                        Similarity  = 1.5,
                        Execute     = () => InputPromptOverlay.Show("Enter reminder (e.g. 10s Take break, 5m Rest):", (args) => ParseAndSetReminder(args))
                    });
                }
            }

            return suggestions;
        }

        private static void AppendNote(string noteText)
        {
            try
            {
                string projectRoot = GetProjectRoot();
                string notesPath = Path.Combine(projectRoot, "notes.txt");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {noteText}{Environment.NewLine}";
                File.AppendAllText(notesPath, entry);
                TextOverlay.Show("📝 Note saved to notes.txt!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save note: {ex.Message}", 3000);
            }
        }

        private static void ParseAndSetReminder(string args)
        {
            var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                TextOverlay.Show("⚠️ Invalid format! Use: remind <time><s|m|h> <message>", 3500);
                return;
            }

            string timeStr = parts[0].ToLower();
            string message = parts[1];

            int seconds = 0;
            if (timeStr.EndsWith("s") && int.TryParse(timeStr.TrimEnd('s'), out int s))
            {
                seconds = s;
            }
            else if (timeStr.EndsWith("m") && int.TryParse(timeStr.TrimEnd('m'), out int m))
            {
                seconds = m * 60;
            }
            else if (timeStr.EndsWith("h") && int.TryParse(timeStr.TrimEnd('h'), out int h))
            {
                seconds = h * 3600;
            }
            else if (int.TryParse(timeStr, out int defaultSec))
            {
                seconds = defaultSec;
            }
            else
            {
                TextOverlay.Show("⚠️ Invalid time duration (e.g. 30s, 5m, 1h)", 3500);
                return;
            }

            TextOverlay.Show($"⏰ Reminder set for {timeStr} from now", 2500);

            Task.Run(async () =>
            {
                await Task.Delay(seconds * 1000);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TextOverlay.Show($"🔔 REMINDER: {message}", 6000);
                });
            });
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, "Modules")))
            {
                return devPath;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
