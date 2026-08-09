// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to check, clear, or interact with the system clipboard.

using System;
using System.Collections.Generic;
using System.Windows;

namespace JarvisLauncher
{
    public class ClipboardCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "clipboard" || query == "cb" || query == "clip" || query == "clearclip";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            double similarity = Math.Max(
                SearchUtil.GetSimilarity(query, "clipboard"),
                SearchUtil.GetSimilarity(query, "clip")
            );

            // Suggestion 1: View clipboard contents
            suggestions.Add(new CommandResult
            {
                Title       = "View Clipboard Text",
                Description = "Display current text stored in the system clipboard in the terminal",
                Similarity  = similarity + 0.3,
                Execute     = () => ShowClipboard()
            });

            // Suggestion 2: Clear clipboard
            suggestions.Add(new CommandResult
            {
                Title       = "Clear Clipboard",
                Description = "Empty the system clipboard completely",
                Similarity  = similarity,
                Execute     = () => ClearClipboard()
            });

            return suggestions;
        }

        private static void ShowClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    CliOutputOverlay.Show("Clipboard Contents", text);
                }
                else
                {
                    CliOutputOverlay.Show("Clipboard Contents", "[Clipboard does not contain any text data]");
                }
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to read clipboard: {ex.Message}", 3000);
            }
        }

        private static void ClearClipboard()
        {
            try
            {
                Clipboard.Clear();
                TextOverlay.Show("🧹 Clipboard cleared successfully!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to clear clipboard: {ex.Message}", 3000);
            }
        }
    }
}
