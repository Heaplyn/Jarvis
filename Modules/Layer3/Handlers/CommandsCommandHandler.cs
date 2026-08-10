// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles 'commands' and 'help' queries by listing all available system command keywords, descriptions, and examples.

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace JarvisLauncher
{
    public class CommandsCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "commands" || query == "help" || query == "?";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            double similarity = SearchUtil.GetSimilarity(query.Trim().ToLower(), "commands");

            suggestions.Add(new CommandResult
            {
                Title       = "View System Commands",
                Description = "List all available Jarvis command actions, shortcuts, and parameter guidelines",
                Similarity  = similarity,
                Execute     = ShowCommandsList
            });

            return suggestions;
        }

        private static void ShowCommandsList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=======================================================================");
            sb.AppendLine("                      JARVIS LAUNCHER COMMAND HANDBOOK                 ");
            sb.AppendLine("=======================================================================");
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "COMMAND", "DESCRIPTION", "EXAMPLE"));
            sb.AppendLine("-----------------------------------------------------------------------");
            
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "commands / help", "List all supported commands", "commands"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "alias <n> <cmd>", "Create persistent command alias", "alias gp push"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "alias list", "List registered custom aliases", "alias list"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "alias remove <n>", "Delete a custom command alias", "alias remove gp"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "apikey <key>", "Configure Gemini API key", "apikey AIzaSy..."));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "math <expression>", "Evaluate mathematical formulas", "math (25 * 4) + 12"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "volume <0-100>", "Set master system audio volume", "volume 40"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "brightness <0-100>", "Set screen backlight intensity", "brightness 75"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "lock", "Lock the Windows session", "lock"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "sleep", "Suspend computer execution", "sleep"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "restart / shutdown", "PC power execution commands", "restart"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "stats", "Display CPU and RAM diagnostics", "stats"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "ip", "Display local network address", "ip"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "bin clean", "Empty the Windows Recycle Bin", "bin clean"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "kill <proc_name>", "Force terminate active process", "kill chrome"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "textopacity <0-1>", "Adjust HUD text opacity dynamically", "textopacity 0.7"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "download <url>", "Download audio via Lucida/YT-DLP", "download https://deezer..."));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "push \"<message>\"", "Safe-sync repository with GitHub", "push \"added command\""));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "gitsetup", "Configure Git and Auth wizard", "gitsetup"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "update / pull", "Pull codebase updates from GitHub", "update"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "logs", "View persistent execution logs", "logs"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "getdlpath / setdlpath", "View or set downloads folder", "setdlpath C:\\Music"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "sysinfo / specs", "Check hardware/OS specifications", "specs"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "google <query>", "Search Google in default browser", "google WPF layouts"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "screenshot", "Save screen capture to Pictures", "screenshot"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "mute", "Toggle audio mute status", "mute"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "clipboard / cb", "View or clear clipboard text", "cb"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "todo <add/done/list>", "Manage local Todo tasks list", "todo list"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "theme <name>", "Switch interface color theme", "theme blue"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "edit <filename>", "Open file in built-in Text Editor", "edit notes.txt"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "open <filename>", "Open file via Windows default app", "open report.pdf"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "grid / files", "View pinned files launchpad grid", "grid"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "pin <filename>", "Pin file to launchpad dashboard", "pin C:\\notes.txt"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "unpin <filename>", "Remove file from launchpad grid", "unpin C:\\notes.txt"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "note <text>", "Quickly append text to notes.txt", "note Meeting at 3pm"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "remind <time> <msg>", "Set popup alert (e.g. 5m, 30s)", "remind 10m Break"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "settings / options", "Open visual Options & Settings GUI", "settings"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "> <cmd>", "Run terminal command in cmd.exe", ">dir"));
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "$ <cmd>", "Run command in PowerShell", "$Get-Process"));

            sb.AppendLine("-----------------------------------------------------------------------");
            sb.AppendLine("💡 Tips:");
            sb.AppendLine("- You can press 'Enter' on any suggestion to execute it immediately.");
            sb.AppendLine("- Running 'push' automatically cleans build directories and resolves");
            sb.AppendLine("  Git index conflicts / credentials leak attempts dynamically.");
            sb.AppendLine("=======================================================================");

            string output = sb.ToString();

            // Run on UI Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CliOutputOverlay.Show("Command Handbook", output);
            });
        }
    }
}
