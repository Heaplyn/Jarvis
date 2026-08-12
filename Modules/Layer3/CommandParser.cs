// Developer: heaplyn
// Date: 2026-08-09
// Summary: Router dispatcher coordinating handler resolutions and aggregations.

using System;
using System.Collections.Generic;
using System.Linq;
using JarvisLauncher.Modules.Layer3.Handlers;

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
        NEW_IDEAS,
        MUSIC_PLAYLIST,
        STICKY_NOTE,
        GAME_DEV_TOOLBOX,
        FFMPEG,
        LLM,
        PHONE,
        DIAGNOSTICS
    };

    public static class CommandParser
    {
        public static event Action<double>? OnTextOpacityChanged;

        public static void TriggerTextOpacityChange(double opacity)
        {
            OnTextOpacityChanged?.Invoke(opacity);
        }

        public static Dictionary<CommandType, CommandDictType> Handlers = new Dictionary<CommandType, CommandDictType>();

        static CommandParser()
        {
            RegisterHandler(CommandType.MATH, "Perform mathematical calculations", () => new MathCommandHandler());
            RegisterHandler(CommandType.VOLUME, "Control system volume", () => new VolumeCommandHandler());
            RegisterHandler(CommandType.LOCK, "Lock the system", () => new LockCommandHandler());
            RegisterHandler(CommandType.RESTART, "Restart the application", () => new RestartCommandHandler());
            RegisterHandler(CommandType.OPACITY, "Control HUD transparency", () => new OpacityCommandHandler());
            RegisterHandler(CommandType.TIMER, "Set alarm timers", () => new TimerCommandHandler());
            RegisterHandler(CommandType.SYSTEM_STATS, "Monitor CPU and RAM", () => new SystemStatsCommandHandler());
            RegisterHandler(CommandType.LOCAL_IP, "Query network connections", () => new LocalIpCommandHandler());
            RegisterHandler(CommandType.BRIGHTNESS, "Control monitor brightness", () => new BrightnessCommandHandler());
            RegisterHandler(CommandType.CLI_RUNNER, "Run shell terminal queries", () => new CliRunnerCommandHandler());
            RegisterHandler(CommandType.APP_LAUNCHER, "Launch applications", () => new AppLauncherCommandHandler());
            RegisterHandler(CommandType.VIEW_FILE, "View text file contents", () => new ViewFileCommandHandler());
            RegisterHandler(CommandType.SETTINGS, "Manage configuration settings", () => new SettingsCommandHandler());
            RegisterHandler(CommandType.AI, "Ask Jarvis AI assistant", () => new AiCommandHandler());
            RegisterHandler(CommandType.RECYCLE_BIN, "Empty Recycle Bin", () => new RecycleBinCommandHandler());
            RegisterHandler(CommandType.PROCESS_KILLER, "Kill running processes", () => new ProcessKillerCommandHandler());
            RegisterHandler(CommandType.POWER, "PC Power operations", () => new PowerCommandHandler());
            RegisterHandler(CommandType.ALIAS, "Manage command shortcuts", () => new AliasCommandHandler());
            RegisterHandler(CommandType.TEXT_OPACITY, "Adjust UI text transparency", () => new TextOpacityCommandHandler());
            RegisterHandler(CommandType.GIT_PUSH, "Push project repository to GitHub", () => new GitPushCommandHandler());
            RegisterHandler(CommandType.COMMANDS, "View all system commands", () => new CommandsCommandHandler());
            RegisterHandler(CommandType.GIT_SETUP, "Set up Git repository and credentials", () => new GitSetupCommandHandler());
            RegisterHandler(CommandType.LOGS, "Manage system execution logs", () => new LogCommandHandler());
            RegisterHandler(CommandType.DOWNLOAD_PATH, "Configure custom download destination folder path", () => new DownloadPathCommandHandler());
            RegisterHandler(CommandType.EXIT, "Exit Jarvis Launcher application", () => new ExitCommandHandler());
            RegisterHandler(CommandType.UPDATE, "Pull latest codebase updates from GitHub", () => new UpdateCommandHandler());
            RegisterHandler(CommandType.POWERSHELL, "Execute PowerShell CLI commands", () => new PowerShellRunnerCommandHandler());
            RegisterHandler(CommandType.UPDATE_COMPUTER, "Update computer software", () => new UpdateComputerCommandHandler());
            RegisterHandler(CommandType.SYS_INFO, "View system specifications", () => new SysInfoCommandHandler());
            RegisterHandler(CommandType.SEARCH_LAUNCHER, "Open web searches", () => new SearchLauncherCommandHandler());
            RegisterHandler(CommandType.SCREENSHOT, "Capture screen captures", () => new ScreenshotCommandHandler());
            RegisterHandler(CommandType.MUTE, "Mute system sound device", () => new MuteCommandHandler());
            RegisterHandler(CommandType.CLIPBOARD, "Clear or check clipboard texts", () => new ClipboardCommandHandler());
            RegisterHandler(CommandType.TODO, "Persistently manage lists of tasks", () => new TodoCommandHandler());
            RegisterHandler(CommandType.THEME, "Change HUD appearance theme colors", () => new ThemeCommandHandler());
            RegisterHandler(CommandType.EDIT, "Edit files in built-in Text Editor", () => new EditCommandHandler());
            RegisterHandler(CommandType.OPEN, "Open file using Windows default program", () => new OpenNativeCommandHandler());
            RegisterHandler(CommandType.GRID, "Manage visual files launchpad dashboard", () => new GridCommandHandler());
            RegisterHandler(CommandType.PRODUCTIVITY, "Manage quick notes and desktop reminders", () => new ProductivityCommandHandler());
            RegisterHandler(CommandType.EXTRA_FEATURES, "Desktop file search, snippets, apps, and web summarizer", () => new ExtraFeaturesCommandHandler());
            RegisterHandler(CommandType.NEW_IDEAS, "Window snap, macros, ping, jumps, task manager GUI, time, and hash", () => new NewIdeasCommandHandler());
            RegisterHandler(CommandType.MUSIC_PLAYLIST, "Interactive Music Player & Playlist Manager GUI", () => new MusicPlaylistCommandHandler());
            RegisterHandler(CommandType.STICKY_NOTE, "Visual Desktop Sticky Notes widget", () => new StickyNotesCommandHandler());
            RegisterHandler(CommandType.GAME_DEV_TOOLBOX, "Roblox Studio and Blender developer utilities dashboard", () => new GameDevToolboxCommandHandler());
            RegisterHandler(CommandType.FFMPEG, "FFmpeg video, audio, GIF, and media processing tools", () => new FFMpegCommandHandler());
            RegisterHandler(CommandType.LLM, "LLM Gui", () => new LLMCommandHandler());
            RegisterHandler(CommandType.PHONE, "Manage mobile companion connectivity", () => new PhoneControlCommandHandler());
            RegisterHandler(CommandType.DIAGNOSTICS, "System and network connectivity diagnostics hub", () => new DiagnosticsCommandHandler());
        }

        private static void RegisterHandler(CommandType type, string description, Func<ICommandHandler> factory)
        {
            try
            {
                var handler = factory();
                Handlers[type] = new CommandDictType(description, handler);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load command handler {type}: {ex.Message}");
            }
        }

        public static List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return suggestions;
            }

            query = query.Trim();

            // Handle inline command chaining via '|' or '&&'
            if (query.Contains(" | ") || query.Contains(" && "))
            {
                string[] chainParts = query.Split(new[] { " | ", " && " }, StringSplitOptions.RemoveEmptyEntries);
                if (chainParts.Length > 1)
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"⚡ Execute Chained Pipeline ({chainParts.Length} Commands)",
                        Description = $"Run: {query}",
                        Similarity  = 5.0, // High priority match
                        Execute     = () => ExecuteChainedPipeline(chainParts)
                    });
                }
            }

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

            // 2. Global fuzzy & partial prefix matching against ALL registered Jarvis command definitions
            try
            {
                var allDescs = GetAllCommandDescriptions();
                string lowerQuery = expandedQuery.ToLower().Trim();

                foreach (var cd in allDescs)
                {
                    if (cd == null || string.IsNullOrWhiteSpace(cd.CommandName)) continue;

                    string cmdName = cd.CommandName.ToLower();
                    string example = (cd.CommandExample ?? "").ToLower();
                    string desc = (cd.CommandDescription ?? "").ToLower();

                    bool isMatch = cmdName.StartsWith(lowerQuery) ||
                                   example.StartsWith(lowerQuery) ||
                                   cmdName.Contains(lowerQuery) ||
                                   desc.Contains(lowerQuery);

                    if (isMatch)
                    {
                        double sim = cmdName.StartsWith(lowerQuery) ? 4.5 : (example.StartsWith(lowerQuery) ? 4.0 : 2.5);

                        // Avoid duplicates if specific handler already produced exact card
                        if (!suggestions.Any(s => s.Title.IndexOf(cd.CommandName, StringComparison.OrdinalIgnoreCase) >= 0 || (!string.IsNullOrEmpty(cd.CommandExample) && s.Title.IndexOf(cd.CommandExample, StringComparison.OrdinalIgnoreCase) >= 0)))
                        {
                            string runTarget = !string.IsNullOrWhiteSpace(cd.CommandExample) ? cd.CommandExample : cd.CommandName;
                            suggestions.Add(new CommandResult
                            {
                                Title = $"⚡ Command: {cd.CommandName}",
                                Description = $"{cd.CommandDescription} (Example: {cd.CommandExample})",
                                Similarity = sim,
                                Execute = () => ExecuteFirstSuggestion(runTarget)
                            });
                        }
                    }
                }
            }
            catch { }

            // Sort suggestions in descending order of similarity
            suggestions.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

            return suggestions;
        }

        public static void ExecuteFirstSuggestion(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            var suggestions = GetSuggestions(query);
            if (suggestions.Count > 0 && suggestions[0].Execute != null)
            {
                suggestions[0].Execute?.Invoke();
            }
        }

        private static void ExecuteChainedPipeline(string[] chainParts)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                int count = 0;
                foreach (var cmd in chainParts)
                {
                    string trimmedCmd = cmd.Trim();
                    if (string.IsNullOrEmpty(trimmedCmd)) continue;

                    var subSuggestions = GetSuggestions(trimmedCmd);
                    if (subSuggestions.Count > 0 && subSuggestions[0].Execute != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            subSuggestions[0].Execute?.Invoke();
                        });
                        count++;
                        await System.Threading.Tasks.Task.Delay(300); // Brief delay between actions
                    }
                }
                TextOverlay.Show($"⚡ Chained Pipeline Executed ({count} actions completed)", 3000);
            });
        }

        public static List<CommandDesc> GetAllCommandDescriptions()
        {
            var descs = new List<CommandDesc>();
            var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in Handlers)
            {
                try
                {
                    var handler = kvp.Value.Item2;
                    var handlerDescs = handler.GetCommandDescriptions();
                    if (handlerDescs != null && handlerDescs.Count > 0)
                    {
                        foreach (var cd in handlerDescs)
                        {
                            if (cd != null && cd.Show && !string.IsNullOrWhiteSpace(cd.CommandName))
                            {
                                if (seenCommands.Add(cd.CommandName))
                                {
                                    descs.Add(cd);
                                }
                            }
                        }
                    }
                    else
                    {
                        string regDesc = kvp.Value.Item1;
                        if (!string.IsNullOrWhiteSpace(regDesc))
                        {
                            string name = kvp.Key.ToString().ToLower().Replace("_", " ");
                            if (seenCommands.Add(name))
                            {
                                descs.Add(new CommandDesc(name, regDesc, name));
                            }
                        }
                    }
                }
                catch
                {
                    // Fail-safe for individual handler description gathering
                }
            }

            return descs;
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
