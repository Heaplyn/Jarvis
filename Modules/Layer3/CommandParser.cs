// Developer: heaplyn
// Date: 2026-08-09
// Summary: Router dispatcher coordinating handler resolutions and aggregations.

using System;
using System.Collections.Generic;

using CommandDictType = System.Tuple<string, JarvisLauncher.ICommandHandler>;

namespace JarvisLauncher
{
    public enum CommandType
    {
        MATH,
        VOLUME,
        LOCK,
        RESTART,
        OPACITY,
        TIMER,
        SYSTEM_STATS,
        LOCAL_IP,
        BRIGHTNESS,
        CLI_RUNNER,
        APP_LAUNCHER,
        VIEW_FILE,
        SETTINGS,
        AI,
        RECYCLE_BIN,
        PROCESS_KILLER,
        POWER,
        ALIAS,
        TEXT_OPACITY,
        DOWNLOAD,
        GIT_PUSH,
        COMMANDS,
        GIT_SETUP,
        LOGS,
        DOWNLOAD_PATH,
        EXIT,
        UPDATE,
        POWERSHELL,
        UPDATE_COMPUTER,
        SYS_INFO,
        SEARCH_LAUNCHER,
        SCREENSHOT,
        MUTE,
        CLIPBOARD,
        TODO,
        THEME,
        EDIT,
        OPEN,
        GRID,
        PRODUCTIVITY,
        EXTRA_FEATURES,
        NEW_IDEAS
    };

    public static class CommandParser
    {
        public static event Action<double>? OnTextOpacityChanged;

        public static void TriggerTextOpacityChange(double opacity)
        {
            OnTextOpacityChanged?.Invoke(opacity);
        }

        public static Dictionary<CommandType, CommandDictType> Handlers = new Dictionary<CommandType, CommandDictType>
        {
            { CommandType.MATH,
            new CommandDictType("Perform mathematical calculations", new MathCommandHandler()) },
            { CommandType.VOLUME,
            new CommandDictType("Control system volume", new VolumeCommandHandler()) },
            { CommandType.LOCK,
            new CommandDictType("Lock the system", new LockCommandHandler()) },
            { CommandType.RESTART,
            new CommandDictType("Restart the application", new RestartCommandHandler()) },
            { CommandType.OPACITY,
            new CommandDictType("Control HUD transparency", new OpacityCommandHandler()) },
            { CommandType.TIMER,
            new CommandDictType("Set alarm timers", new TimerCommandHandler()) },
            { CommandType.SYSTEM_STATS,
            new CommandDictType("Monitor CPU and RAM", new SystemStatsCommandHandler()) },
            { CommandType.LOCAL_IP,
            new CommandDictType("Query network connections", new LocalIpCommandHandler()) },
            { CommandType.BRIGHTNESS,
            new CommandDictType("Control monitor brightness", new BrightnessCommandHandler()) },
            { CommandType.CLI_RUNNER,
            new CommandDictType("Run shell terminal queries", new CliRunnerCommandHandler()) },
            { CommandType.APP_LAUNCHER,
            new CommandDictType("Launch applications", new AppLauncherCommandHandler()) },
            { CommandType.VIEW_FILE,
            new CommandDictType("View text file contents", new ViewFileCommandHandler()) },
            { CommandType.SETTINGS,
            new CommandDictType("Manage configuration settings", new SettingsCommandHandler()) },
            { CommandType.AI,
            new CommandDictType("Ask Jarvis AI assistant", new AiCommandHandler()) },
            { CommandType.RECYCLE_BIN,
            new CommandDictType("Empty Recycle Bin", new RecycleBinCommandHandler()) },
            { CommandType.PROCESS_KILLER,
            new CommandDictType("Kill running processes", new ProcessKillerCommandHandler()) },
            { CommandType.POWER,
            new CommandDictType("PC Power operations", new PowerCommandHandler()) },
            { CommandType.ALIAS,
            new CommandDictType("Manage command shortcuts", new AliasCommandHandler()) },
            { CommandType.TEXT_OPACITY,
            new CommandDictType("Adjust UI text transparency", new TextOpacityCommandHandler()) },
            { CommandType.DOWNLOAD,
            new CommandDictType("Download media from URL", new DownloadCommandHandler()) },
            { CommandType.GIT_PUSH,
            new CommandDictType("Push project repository to GitHub", new GitPushCommandHandler()) },
            { CommandType.COMMANDS,
            new CommandDictType("View all system commands", new CommandsCommandHandler()) },
            { CommandType.GIT_SETUP,
            new CommandDictType("Set up Git repository and credentials", new GitSetupCommandHandler()) },
            { CommandType.LOGS,
            new CommandDictType("Manage system execution logs", new LogCommandHandler()) },
            { CommandType.DOWNLOAD_PATH,
            new CommandDictType("Configure custom download destination folder path", new DownloadPathCommandHandler()) },
            { CommandType.EXIT,
            new CommandDictType("Exit Jarvis Launcher application", new ExitCommandHandler()) },
            { CommandType.UPDATE,
            new CommandDictType("Pull latest codebase updates from GitHub", new UpdateCommandHandler()) },
            { CommandType.POWERSHELL,
            new CommandDictType("Execute PowerShell CLI commands", new PowerShellRunnerCommandHandler()) },
            { CommandType.UPDATE_COMPUTER,
            new CommandDictType("Update computer software", new UpdateComputerCommandHandler()) },
            { CommandType.SYS_INFO,
            new CommandDictType("View system specifications", new SysInfoCommandHandler()) },
            { CommandType.SEARCH_LAUNCHER,
            new CommandDictType("Open web searches", new SearchLauncherCommandHandler()) },
            { CommandType.SCREENSHOT,
            new CommandDictType("Capture screen captures", new ScreenshotCommandHandler()) },
            { CommandType.MUTE,
            new CommandDictType("Mute system sound device", new MuteCommandHandler()) },
            { CommandType.CLIPBOARD,
            new CommandDictType("Clear or check clipboard texts", new ClipboardCommandHandler()) },
            { CommandType.TODO,
            new CommandDictType("Persistently manage lists of tasks", new TodoCommandHandler()) },
            { CommandType.THEME,
            new CommandDictType("Change HUD appearance theme colors", new ThemeCommandHandler()) },
            { CommandType.EDIT,
            new CommandDictType("Edit files in built-in Text Editor", new EditCommandHandler()) },
            { CommandType.OPEN,
            new CommandDictType("Open file using Windows default program", new OpenNativeCommandHandler()) },
            { CommandType.GRID,
            new CommandDictType("Manage visual files launchpad dashboard", new GridCommandHandler()) },
            { CommandType.PRODUCTIVITY,
            new CommandDictType("Manage quick notes and desktop reminders", new ProductivityCommandHandler()) },
            { CommandType.EXTRA_FEATURES,
            new CommandDictType("Desktop file search, snippets, apps, and web summarizer", new ExtraFeaturesCommandHandler()) },
            { CommandType.NEW_IDEAS,
            new CommandDictType("Window snap, macros, ping, jumps, task manager GUI, time, and hash", new NewIdeasCommandHandler()) }
        };

        public static List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return suggestions;
            }

            query = query.Trim();

            // 1. Expand aliases before evaluation
            string expandedQuery = query;
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                string firstWord = parts[0].ToLower();
                var currentAliases = SettingsManager.Current.Aliases;
                if (currentAliases.TryGetValue(firstWord, out string? expansion))
                {
                    string remainder = query.Substring(parts[0].Length).Trim();
                    expandedQuery = string.IsNullOrEmpty(remainder) ? expansion : $"{expansion} {remainder}";
                }
            }

            foreach (var (type, handler) in Handlers)
            {
                try
                {
                    if (handler.Item2.CanHandle(expandedQuery))
                    {
                        var results = handler.Item2.GetSuggestions(expandedQuery);
                        if (results != null && results.Count > 0)
                        {
                            suggestions.AddRange(results);
                        }
                    }
                }
                catch
                {
                    // Fail-safe for individual handler errors
                }
            }

            // Sort suggestions in descending order of similarity
            suggestions.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

            return suggestions;
        }

        public static void Initialize()
        {
            foreach (var (type, handler) in Handlers)
            {
                try
                {
                    handler.Item2.OnStart();
                }
                catch
                {
                    // Fail-safe for individual handler initialization errors
                }
            }
        }
    }
}
