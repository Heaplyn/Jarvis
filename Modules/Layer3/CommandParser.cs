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
        DIAGNOSTICS,
        WEB_SCRAPING,
        CALENDAR,
        REMINDERS,
        FILE_ORGANIZER,
        SCREEN_ANALYSIS,
        BACKGROUND,
        VOICE_STUDIO,
        HELP_CENTER,
        ANIMATION_OPTIONS,
        EXPANDED_COMMANDS,
        ORGANIZATION_TOOLS,
        ADHD_FOCUS_SUITE,
        MCP,
        OAUTH2,
        CODE_ASSIST,
        IPA_COMPILER,
        INSTALL
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
            RegisterHandler(CommandType.MCP, "Model Context Protocol (MCP) server registry studio and tools", () => new McpCommandHandler());
            RegisterHandler(CommandType.OAUTH2, "Manage OAuth2 authentication credentials", () => new OAuth2CommandHandler());
            RegisterHandler(CommandType.CODE_ASSIST, "Real-time AI screen and workspace file coding assistance sidebar", () => new CodeAssistCommandHandler());
            RegisterHandler(CommandType.IPA_COMPILER, "Compile C# projects targeting iOS into IPA packages and transfer to mobile", () => new IpaCompilerCommandHandler());
            RegisterHandler(CommandType.INSTALL, "Install packages and tools via winget, npm, dotnet workloads, or web scraper", () => new InstallCommandHandler());
            RegisterHandler(CommandType.LLM, "LLM Gui", () => new LLMCommandHandler());
            RegisterHandler(CommandType.PHONE, "Manage mobile companion connectivity", () => new PhoneControlCommandHandler());
            RegisterHandler(CommandType.DIAGNOSTICS, "System and network connectivity diagnostics hub", () => new DiagnosticsCommandHandler());
            RegisterHandler(CommandType.WEB_SCRAPING, "Scrape webpages and read Discord servers via official Bot API", () => new WebScrapingCommandHandler());
            RegisterHandler(CommandType.CALENDAR, "Visual month calendar and event planner", () => new CalendarCommandHandler());
            RegisterHandler(CommandType.REMINDERS, "Schedule notifications and alarms", () => new ReminderCommandHandler());
            RegisterHandler(CommandType.FILE_ORGANIZER, "Visual file organizer utility and cleaner", () => new FileOrganizerCommandHandler());
            RegisterHandler(CommandType.SCREEN_ANALYSIS, "Extract palette colors and tile open desktop windows", () => new ScreenAnalysisCommandHandler());
            RegisterHandler(CommandType.BACKGROUND, "Manage UI background modes (GIF, Gradient, Solid)", () => new BackgroundCommandHandler());
            RegisterHandler(CommandType.VOICE_STUDIO, "Train AI voice profiles, audio recorder, and voice shortcuts", () => new VoiceStudioCommandHandler());
            RegisterHandler(CommandType.HELP_CENTER, "Interactive command directory, hotkeys cheat sheet, and documentation", () => new HelpCommandHandler());
            RegisterHandler(CommandType.ANIMATION_OPTIONS, "Configure HUD animations, transition speeds, and visual effects", () => new AnimationCommandHandler());
            RegisterHandler(CommandType.EXPANDED_COMMANDS, "Access 50+ system power, security, file, media, developer, and productivity commands", () => new ExpandedCommandsHandler());
            RegisterHandler(CommandType.ORGANIZATION_TOOLS, "Organize desktop, downloads, deduplicate files, sort by date/extension, and backup folders", () => new OrganizationCommandsHandler());
            RegisterHandler(CommandType.ADHD_FOCUS_SUITE, "ADHD focus Pomodoro sprints, task micro-chunking, dopamine check-ins, and TTS voice alerts", () => new AdhdFocusSuiteHandler());
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

        public static bool IsKnownLocalCommand(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            string q = query.Trim().ToLower();

            foreach (var pair in Handlers.Values)
            {
                try
                {
                    if (pair.Item2.CanHandle(q))
                    {
                        return true;
                    }
                }
                catch { }
            }

            try
            {
                var matchingApps = WindowsAppScanner.GetMatchingApps(q);
                if (matchingApps != null && matchingApps.Count > 0) return true;
            }
            catch { }

            return false;
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
                        Similarity  = 5.0,
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

            // 2. Inject ML-learned history suggestions for short queries (fast recall)
            if (expandedQuery.Length <= 4)
            {
                var topResults = QueryLearner.GetTopResults(expandedQuery, topN: 3);
                foreach (var (title, origQuery, count) in topResults)
                {
                    string targetQuery = origQuery;
                    suggestions.Add(new CommandResult
                    {
                        Title       = title.StartsWith("⭐ ") ? title : $"⭐ {title}",
                        Description = $"Recently used ({count}×) — click or press Enter to run",
                        Similarity  = 7.0 + Math.Min(3.0, Math.Sqrt(count) * 0.5), // Always at top
                        Execute     = () => ExecuteFirstSuggestion(targetQuery)
                    });
                }
            }

            // 3. Handler suggestions (check both raw query and space-stripped query e.g. "pre cache" -> "precache")
            string noSpacesQuery = expandedQuery.Replace(" ", "").Replace("-", "");
            foreach (var (type, handler) in Handlers)
            {
                try
                {
                    if (handler.Item2.CanHandle(expandedQuery) || handler.Item2.CanHandle(noSpacesQuery))
                    {
                        var results = handler.Item2.GetSuggestions(expandedQuery);
                        if (results == null || results.Count == 0)
                        {
                            results = handler.Item2.GetSuggestions(noSpacesQuery);
                        }
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

            // 3b. Windows Installed Apps autocomplete
            try
            {
                var appMatches = WindowsAppScanner.GetMatchingApps(expandedQuery);
                if (appMatches.Count > 0)
                {
                    suggestions.AddRange(appMatches);
                }
            }
            catch { }

            // 4. Global fuzzy & partial prefix matching against ALL registered Jarvis command definitions
            try
            {
                var allDescs = GetAllCommandDescriptions();
                string lowerQuery = expandedQuery.ToLower().Trim();
                string lowerNoSpaces = lowerQuery.Replace(" ", "").Replace("-", "");

                foreach (var cd in allDescs)
                {
                    if (cd == null || string.IsNullOrWhiteSpace(cd.CommandName)) continue;

                    string cmdName = cd.CommandName.ToLower();
                    string cmdNameNoSpaces = cmdName.Replace(" ", "").Replace("-", "");
                    string example = (cd.CommandExample ?? "").ToLower();
                    string exampleNoSpaces = example.Replace(" ", "").Replace("-", "");
                    string desc    = (cd.CommandDescription ?? "").ToLower();

                    // Enhanced matching: prefix, substring, acronym, word-boundary, description, & space-insensitive
                    bool isMatch = cmdName.StartsWith(lowerQuery)      || cmdNameNoSpaces.StartsWith(lowerNoSpaces) ||
                                   example.StartsWith(lowerQuery)      || exampleNoSpaces.StartsWith(lowerNoSpaces) ||
                                   cmdName.Contains(lowerQuery)        || cmdNameNoSpaces.Contains(lowerNoSpaces) ||
                                   desc.Contains(lowerQuery)           || desc.Contains(lowerNoSpaces) ||
                                   SearchUtil.IsAcronymMatch(lowerQuery, cmdName) ||
                                   SearchUtil.IsClose(lowerQuery, cmdName) ||
                                   SearchUtil.IsClose(lowerNoSpaces, cmdNameNoSpaces);

                    if (isMatch)
                    {
                        double sim = SearchUtil.GetSimilarity(lowerQuery, cmdName);
                        if (sim < 1.0) sim = (cmdName.StartsWith(lowerQuery) || cmdNameNoSpaces.StartsWith(lowerNoSpaces)) ? 4.5 : (example.StartsWith(lowerQuery) ? 4.0 : 2.5);

                        // Avoid duplicates if specific handler already produced exact card
                        if (!suggestions.Any(s => s.Title.IndexOf(cd.CommandName, StringComparison.OrdinalIgnoreCase) >= 0 || (!string.IsNullOrEmpty(cd.CommandExample) && s.Title.IndexOf(cd.CommandExample, StringComparison.OrdinalIgnoreCase) >= 0)))
                        {
                            string runTarget = !string.IsNullOrWhiteSpace(cd.CommandExample) ? cd.CommandExample : cd.CommandName;
                            suggestions.Add(new CommandResult
                            {
                                Title       = $"⚡ Command: {cd.CommandName}",
                                Description = $"{cd.CommandDescription} (Example: {cd.CommandExample})",
                                Similarity  = sim,
                                Execute     = () => ExecuteFirstSuggestion(runTarget)
                            });
                        }
                    }
                }
            }
            catch { }

            // 5. Apply ML learned boost on top of every suggestion's raw similarity
            foreach (var s in suggestions)
            {
                double boost = QueryLearner.GetBoost(expandedQuery, s.Title);
                if (boost > 0) s.Similarity += boost;
            }

            // 6. Deduplicate by title (keep highest scoring) then sort descending
            var deduped = suggestions
                .GroupBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.Similarity).First())
                .OrderByDescending(s => s.Similarity)
                .ToList();

            // 7. Last Resort: If no high-confidence command or app exists, suggest AI Chat
            if (!string.IsNullOrWhiteSpace(expandedQuery) && !deduped.Any(s => s.Similarity >= 8.0))
            {
                deduped.Add(new CommandResult
                {
                    Title = $"🧠 Ask Assistant: \"{expandedQuery}\"",
                    Description = "No exact command match found. Route this query to Jarvis AI Chat.",
                    Similarity = 1.0, // Low but present
                    Execute = () => ChatOverlay.SubmitTextMessage(expandedQuery)
                });
            }

            return deduped.Take(12).ToList();
        }

        public static void ExecuteFirstSuggestion(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            string cleanQuery = CleanTitlePrefixes(query);
            string lowerClean = cleanQuery.ToLower().Trim();

            // CRITICAL POWER SAFETY SHIELD: NEVER auto-execute power-state operations from fuzzy or star queries!
            if (lowerClean.Contains("shutdown") || lowerClean.Contains("sleep") || 
                lowerClean.Contains("reboot") || lowerClean.Contains("restart") || 
                lowerClean.Contains("power off") || lowerClean.Contains("turn off"))
            {
                // Instead of executing, paste it into the HUD search box so the user can explicitly trigger it
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.SearchInput.Text = cleanQuery;
                        mw.SearchInput.CaretIndex = cleanQuery.Length;
                        mw.SearchInput.Focus();
                    }
                });
                TextOverlay.Show("⚠️ Power action requires manual execution.", 3000);
                return;
            }

            // Trigger parallel Dual-LLM Co-Pilot analysis if enabled (default disabled)
            DualLlmCopilot.ProcessQueryParallel(query);

            // Get all suggestions sorted by Similarity score descending
            var suggestions = GetSuggestions(cleanQuery);
            foreach (var s in suggestions)
            {
                if (s.Title.StartsWith("⭐ ")) continue; // Skip circular star items

                // Only invoke if it is a highly similar or exact match to prevent executing random commands
                string cleanSuggestionTitle = CleanTitlePrefixes(s.Title).ToLower();
                if (cleanSuggestionTitle.Contains(lowerClean) || lowerClean.Contains(cleanSuggestionTitle) || s.Similarity >= 7.5)
                {
                    if (s.Execute != null)
                    {
                        s.Execute.Invoke();
                        return;
                    }
                }
            }

            // Fallback: If no local command or app is found, route to Gemini AI to parse intent!
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await ChatOverlay.SubmitVoiceCommand(cleanQuery, showUi: true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI Fallback error: {ex.Message}");
                }
            });
        }

        public static void ExecuteSuggestionByTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            string targetTitleClean = CleanTitlePrefixes(title).ToLower().Trim();

            // CRITICAL POWER SAFETY SHIELD: NEVER auto-execute power-state operations from fuzzy or star queries!
            if (targetTitleClean.Contains("shutdown") || targetTitleClean.Contains("sleep") || 
                targetTitleClean.Contains("reboot") || targetTitleClean.Contains("restart pc") ||
                targetTitleClean.Contains("power off") || targetTitleClean.Contains("turn off"))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                    {
                        mw.SearchInput.Text = title;
                        mw.SearchInput.CaretIndex = title.Length;
                        mw.SearchInput.Focus();
                    }
                });
                TextOverlay.Show("⚠️ Power action requires manual execution.", 3000);
                return;
            }

            // Look up exact suggestion by iterating through all registered handlers
            foreach (var (type, handler) in Handlers)
            {
                try
                {
                    var descs = handler.Item2.GetCommandDescriptions();
                    foreach (var desc in descs)
                    {
                        var results = handler.Item2.GetSuggestions(desc.CommandName);
                        if (results != null)
                        {
                            foreach (var s in results)
                            {
                                string cleanS = CleanTitlePrefixes(s.Title).ToLower().Trim();
                                if (cleanS == targetTitleClean || targetTitleClean.Contains(cleanS) || cleanS.Contains(targetTitleClean))
                                {
                                    if (s.Execute != null)
                                    {
                                        s.Execute.Invoke();
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Fallback: search for it using ExecuteFirstSuggestion
            ExecuteFirstSuggestion(title);
        }

        public static string CleanTitlePrefixes(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            string clean = title.Trim();
            string[] prefixes = new[] { "⭐ ", "⚡ Command: ", "⚡ ", "🎙️ ", "🤖 ", "🌐 ", "📥 ", "⚙️ ", "🎵 ", "🔲 ", "🏷️ ", "📶 ", "🎨 ", "🧠 ", "🦙 ", "💻 ", "🔬 ", "📐 ", "🚀 " };
            foreach (var p in prefixes)
            {
                if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(p.Length).Trim();
                }
            }

            // Strip action verbs from queries like "open settings & options gui" -> "settings & options gui"
            string[] verbPrefixes = new[] { "open ", "launch ", "run ", "start " };
            foreach (var vp in verbPrefixes)
            {
                if (clean.StartsWith(vp, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean.Substring(vp.Length).Trim();
                    break;
                }
            }

            return clean;
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

        // Groups each handler's commands under a display category for the categorized command browser overlay.
        public static readonly Dictionary<CommandType, string> Categories = new Dictionary<CommandType, string>
        {
            { CommandType.MATH, "Utilities" },
            { CommandType.VOLUME, "Audio & Media" },
            { CommandType.LOCK, "System & Power" },
            { CommandType.RESTART, "System & Power" },
            { CommandType.OPACITY, "Customization" },
            { CommandType.TIMER, "Utilities" },
            { CommandType.SYSTEM_STATS, "Diagnostics" },
            { CommandType.LOCAL_IP, "Network & Mobile" },
            { CommandType.BRIGHTNESS, "System & Power" },
            { CommandType.CLI_RUNNER, "Developer Tools" },
            { CommandType.APP_LAUNCHER, "Apps & Launcher" },
            { CommandType.VIEW_FILE, "Files & Editing" },
            { CommandType.SETTINGS, "Customization" },
            { CommandType.AI, "AI & Automation" },
            { CommandType.RECYCLE_BIN, "Files & Editing" },
            { CommandType.PROCESS_KILLER, "System & Power" },
            { CommandType.POWER, "System & Power" },
            { CommandType.ALIAS, "Customization" },
            { CommandType.TEXT_OPACITY, "Customization" },
            { CommandType.GIT_PUSH, "Developer Tools" },
            { CommandType.COMMANDS, "Utilities" },
            { CommandType.GIT_SETUP, "Developer Tools" },
            { CommandType.LOGS, "Diagnostics" },
            { CommandType.DOWNLOAD_PATH, "Customization" },
            { CommandType.EXIT, "System & Power" },
            { CommandType.UPDATE, "System & Power" },
            { CommandType.POWERSHELL, "Developer Tools" },
            { CommandType.UPDATE_COMPUTER, "System & Power" },
            { CommandType.SYS_INFO, "Diagnostics" },
            { CommandType.SEARCH_LAUNCHER, "Apps & Launcher" },
            { CommandType.SCREENSHOT, "Utilities" },
            { CommandType.MUTE, "Audio & Media" },
            { CommandType.CLIPBOARD, "Utilities" },
            { CommandType.TODO, "Productivity" },
            { CommandType.THEME, "Customization" },
            { CommandType.EDIT, "Files & Editing" },
            { CommandType.OPEN, "Files & Editing" },
            { CommandType.GRID, "Apps & Launcher" },
            { CommandType.PRODUCTIVITY, "Productivity" },
            { CommandType.EXTRA_FEATURES, "Network & Mobile" },
            { CommandType.NEW_IDEAS, "Productivity" },
            { CommandType.MUSIC_PLAYLIST, "Audio & Media" },
            { CommandType.STICKY_NOTE, "Productivity" },
            { CommandType.GAME_DEV_TOOLBOX, "Developer Tools" },
            { CommandType.FFMPEG, "Media Processing" },
            { CommandType.LLM, "AI & Automation" },
            { CommandType.PHONE, "Network & Mobile" },
            { CommandType.DIAGNOSTICS, "Diagnostics" },
            { CommandType.WEB_SCRAPING, "Web Scraping" },
            { CommandType.FILE_ORGANIZER, "Visual file organizer utility and cleaner" },
            { CommandType.SCREEN_ANALYSIS, "Customization" },
            { CommandType.BACKGROUND, "Customization" },
            { CommandType.VOICE_STUDIO, "Audio & Media" },
            { CommandType.HELP_CENTER, "Utilities" },
            { CommandType.ANIMATION_OPTIONS, "Customization" }
        };

        // Preferred display order for categories in the browser overlay.
        public static readonly List<string> CategoryOrder = new List<string>
        {
            "AI & Automation", "System & Power", "Files & Editing", "Apps & Launcher",
            "Network & Mobile", "Web Scraping", "Audio & Media", "Media Processing", "Productivity",
            "Developer Tools", "Diagnostics", "Customization", "Utilities", "Other"
        };

        public static Dictionary<string, List<CommandDesc>> GetCommandDescriptionsByCategory()
        {
            var result = new Dictionary<string, List<CommandDesc>>();
            var seenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in Handlers)
            {
                string category = Categories.TryGetValue(kvp.Key, out string? cat) ? cat : "Other";
                try
                {
                    var handlerDescs = kvp.Value.Item2.GetCommandDescriptions();
                    if (handlerDescs == null) continue;

                    foreach (var cd in handlerDescs)
                    {
                        if (cd == null || !cd.Show || string.IsNullOrWhiteSpace(cd.CommandName)) continue;
                        if (!seenCommands.Add(cd.CommandName)) continue;

                        if (!result.TryGetValue(category, out var list))
                        {
                            list = new List<CommandDesc>();
                            result[category] = list;
                        }
                        list.Add(cd);
                    }
                }
                catch
                {
                    // Fail-safe for individual handler description gathering
                }
            }

            return result;
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
