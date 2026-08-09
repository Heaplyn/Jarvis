// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to launch the glassmorphic text editor for a specific local file or default scratch note.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class EditCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "edit" || query.StartsWith("edit ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            double similarity = SearchUtil.GetSimilarity(query.ToLower(), "edit");

            if (parts.Length > 1)
            {
                string targetFile = parts[1].Trim();
                suggestions.Add(new CommandResult
                {
                    Title       = $"Edit: {targetFile}",
                    Description = $"Open \"{targetFile}\" inside the built-in Jarvis Text Editor",
                    Similarity  = 2.0, // High priority match
                    Execute     = () => TextEditorOverlay.OpenFile(targetFile)
                });
            }
            else
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "Edit File (Prompt)...",
                    Description = "Type a custom file name to open in the Text Editor",
                    Similarity  = similarity + 0.8,
                    Execute     = () => InputPromptOverlay.Show("Enter file name to edit:", (fileName) => TextEditorOverlay.OpenFile(fileName))
                });

                suggestions.Add(new CommandResult
                {
                    Title       = "Browse Files...",
                    Description = "Open a Windows file explorer dialog to select any file to edit",
                    Similarity  = similarity + 0.6,
                    Execute     = () => TextEditorOverlay.PromptAndOpenFile()
                });

                suggestions.Add(new CommandResult
                {
                    Title       = "Open Scratch Note",
                    Description = "Open a blank scratch.txt notepad inside the Jarvis Text Editor",
                    Similarity  = similarity + 0.3,
                    Execute     = () => TextEditorOverlay.OpenFile("scratch.txt")
                });
            }

            return suggestions;
        }
    }
}
