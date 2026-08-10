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
    private static void AddCmd(StringBuilder sb, string command, string description, string example)
    {
        sb.AppendLine($"{command,-22} {description,-36} {example}");
    }

    private static (string Cmd, string Desc, string Example)[] commands = new (string Cmd, string Desc, string Example)[]
    {
        ("commands / help", "List all supported commands", "commands"),
        ("alias <n> <cmd>", "Create persistent command alias", "alias gp push"),
        ("alias list", "List registered custom aliases", "alias list"),
        ("alias remove <n>", "Delete a custom command alias", "alias remove gp"),
        ("apikey <key>", "Configure Gemini API key", "apikey AIzaSy..."),
        ("math <expression>", "Evaluate mathematical formulas", "math (25 * 4) + 12"),
        ("volume <0-100>", "Set master system audio volume", "volume 40"),
        ("brightness <0-100>", "Set screen backlight intensity", "brightness 75"),
        ("lock", "Lock the Windows session", "lock"),
        ("sleep", "Suspend computer execution", "sleep"),
        ("restart / shutdown", "PC power execution commands", "restart"),
        ("stats", "Display CPU and RAM diagnostics", "stats"),
        ("ip", "Display local network address", "ip"),
        ("bin clean", "Empty the Windows Recycle Bin", "bin clean"),
        ("kill <proc_name>", "Force terminate active process", "kill chrome"),
        ("textopacity <0-1>", "Adjust HUD text opacity dynamically", "textopacity 0.7"),
        ("download <url>", "Download audio via Lucida/YT-DLP", "download https://deezer..."),
        ("push \"<message>\"", "Safe-sync repository with GitHub", "push \"added command\""),
        ("gitsetup", "Configure Git and Auth wizard", "gitsetup"),
        ("update / pull", "Pull codebase updates from GitHub", "update"),
        ("logs", "View persistent execution logs", "logs"),
        ("getdlpath / setdlpath", "View or set downloads folder", "setdlpath C:\\Music"),
        ("sysinfo / specs", "Check hardware/OS specifications", "specs"),
        ("google <query>", "Search Google in default browser", "google WPF layouts"),
        ("screenshot", "Save screen capture to Pictures", "screenshot"),
        ("mute", "Toggle audio mute status", "mute"),
        ("clipboard / cb", "View or clear clipboard text", "cb"),
        ("todo <add/done/list>", "Manage local Todo tasks list", "todo list"),
        ("theme <name>", "Switch interface color theme", "theme blue"),
        ("edit <filename>", "Open file in built-in Text Editor", "edit notes.txt"),
        ("open <filename>", "Open file via Windows default app", "open report.pdf"),
        ("grid / files", "View pinned files launchpad grid", "grid"),
        ("pin <filename>", "Pin file to launchpad dashboard", "pin C:\\notes.txt"),
        ("unpin <filename>", "Remove file from launchpad grid", "unpin C:\\notes.txt"),
        ("note <text>", "Quickly append text to notes.txt", "note Meeting at 3pm"),
        ("remind <time> <msg>", "Set popup alert (e.g. 5m, 30s)", "remind 10m Break"),
        ("settings / options", "Open visual Options & Settings GUI", "settings"),
        ("search <query>", "Search files across Desktop/Documents", "search report"),
        ("snippet / snip", "Manage and copy saved text snippets", "snip"),
        ("app <name>", "Launch software application shortcut", "app chrome"),
        ("fetch <url>", "Scrape & summarize webpage with AI", "fetch https://..."),
        ("monitor / stats", "Toggle live floating system monitor", "monitor"),
        ("vol night/gaming", "Quick volume preset profiles", "vol night"),
        ("snap left/right", "Snap foreground window to screen half", "snap left"),
        ("macro <name>", "Execute multi-command action chain", "macro focus"),
        ("ping <host>", "Check network roundtrip latency", "ping 8.8.8.8"),
        ("jump <folder>", "Quick jump to system folder path", "jump downloads"),
        ("procs / taskmgr", "Open interactive Process Manager GUI", "procs"),
        ("time <city>", "Look up global time & UTC offset", "time Tokyo"),
        ("hash <file>", "Compute file SHA-256 checksum", "hash notes.txt"),
        ("music / playlist", "Open Music Player & Playlist GUI", "music"),
        ("download <url>", "Download audio/link to playlist on disk", "download https://..."),
        ("tabs / browser", "Inspect active browser tab titles", "tabs"),
        ("> <cmd>", "Run terminal command in cmd.exe", ">dir"),
        ("$ <cmd>", "Run command in PowerShell", "$Get-Process")
    };

        public (string Cmd, string Desc, string Example)[] Commands { get => commands; set => commands = value; }


        private static void ShowCommandsList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=======================================================================");
            sb.AppendLine("                      JARVIS LAUNCHER COMMAND HANDBOOK                 ");
            sb.AppendLine("=======================================================================");
            sb.AppendLine(string.Format("{0,-18} {1,-32} {2}", "COMMAND", "DESCRIPTION", "EXAMPLE"));
            sb.AppendLine("-----------------------------------------------------------------------");
            
                foreach (var (cmd, desc, ex) in commands)
                {
                    AddCmd(sb, cmd, desc, ex);
                }
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
