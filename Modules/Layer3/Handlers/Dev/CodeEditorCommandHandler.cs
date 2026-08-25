using System;
using System.Collections.Generic;
using System.IO;

namespace JarvisLauncher
{
    public class CodeEditorCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("code ") || query.StartsWith("vscode ") || query.StartsWith("cursor ") ||
                   query.StartsWith("vs ") || query.StartsWith("ide ") || query == "editors";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            string path = parts.Length > 1 ? query.Substring(cmd.Length).Trim() : "";

            if (cmd == "editors")
            {
                var editors = CodeEditorManager.GetInstalledEditors();
                suggestions.Add(new CommandResult
                {
                    TITLE = "Installed Code Editors",
                    DESCRIPTION = editors.Count > 0 ? string.Join(", ", editors) : "No major IDEs detected in PATH",
                    SIMILARITY = 5.0
                });
                return suggestions;
            }

            if (cmd == "code" || cmd == "vscode")
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = $"Open in VS Code: {Path.GetFileName(path)}",
                    DESCRIPTION = $"Launch Visual Studio Code for {path}",
                    SIMILARITY = 4.5,
                    EXECUTE = () => CodeEditorManager.OpenInVSCode(path)
                });
            }
            else if (cmd == "cursor")
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = $"Open in Cursor: {Path.GetFileName(path)}",
                    DESCRIPTION = $"Launch Cursor IDE for {path}",
                    SIMILARITY = 4.5,
                    EXECUTE = () => CodeEditorManager.OpenInCursor(path)
                });
            }
            else if (cmd == "vs")
            {
                suggestions.Add(new CommandResult
                {
                    TITLE = $"Open in Visual Studio: {Path.GetFileName(path)}",
                    DESCRIPTION = $"Launch MS Visual Studio for {path}",
                    SIMILARITY = 4.5,
                    EXECUTE = () => CodeEditorManager.OpenInVisualStudio(path)
                });
            }

            return suggestions;
        }
    }
}
