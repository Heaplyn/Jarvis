// Developer: heaplyn
// Date: 2026-08-09
// Summary: Router dispatcher coordinating handler resolutions and aggregations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        INSTALL,
        TTS,
        BIOMETRICS,
        TEMPLATE,
        TEACHER,
        UNINSTALL,
        WEB_OP
    };

    public static class CommandParser
    {
        public static event Action<double>? OnTextOpacityChanged;

        public static void TriggerTextOpacityChange(double Opacity)
        {
            OnTextOpacityChanged?.Invoke(Opacity);
        }

        public static Dictionary<CommandType, CommandDictType> HANDLERS = new Dictionary<CommandType, CommandDictType>();

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
            RegisterHandler(CommandType.TTS, "Voice text-to-speech output and file reading", () => new TtsCommandHandler());
            RegisterHandler(CommandType.BIOMETRICS, "Manage speaker voice biometrics", () => new EnrollVoiceCommandHandler());
            RegisterHandler(CommandType.TEMPLATE, "Manage custom code templates", () => new TemplateCommandHandler());
            RegisterHandler(CommandType.TEACHER, "Interactive programming assistance teacher", () => new TeacherCommandHandler());
            RegisterHandler(CommandType.UNINSTALL, "Uninstall packages or remove Jarvis", () => new UninstallCommandHandler());
            RegisterHandler(CommandType.WEB_OP, "Download, scrape, or search the web", () => new WebOperationCommandHandler());
        }

        private static void RegisterHandler(CommandType Type, string Description, Func<ICommandHandler> Factory)
        {
            try
            {
                var Handler = Factory();
                HANDLERS[Type] = new CommandDictType(Description, Handler);
            }
            catch (Exception Ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load command handler {Type}: {Ex.Message}");
            }
        }

        public static bool IsKnownLocalCommand(string Query)
        {
            if (string.IsNullOrWhiteSpace(Query)) return false;
            string Q = Query.Trim().ToLower();

            foreach (var Pair in HANDLERS.Values)
            {
                try
                {
                    if (Pair.Item2.CanHandle(Q))
                    {
                        return true;
                    }
                }
                catch { }
            }

            try
            {
                var MatchingApps = WindowsAppScanner.GetMatchingApps(Q);
                if (MatchingApps != null && MatchingApps.Count > 0) return true;
            }
            catch { }

            return false;
        }

        public static List<CommandResult> GetSuggestions(string Query)
        {
            var Suggestions = new List<CommandResult>();

            if (string.IsNullOrWhiteSpace(Query))
            {
                return Suggestions;
            }

            Query = Query.Trim();

            // Handle inline command chaining via '|' or '&&'
            if (Query.Contains(" | ") || Query.Contains(" && "))
            {
                string[] ChainParts = Query.Split(new[] { " | ", " && " }, StringSplitOptions.RemoveEmptyEntries);
                if (ChainParts.Length > 1)
                {
                    Suggestions.Add(new CommandResult
                    {
                        TITLE       = $"⚡ Execute Chained Pipeline ({ChainParts.Length} Commands)",
                        DESCRIPTION = $"Run: {Query}",
                        SIMILARITY  = 5.0,
                        EXECUTE     = () => ExecuteChainedPipeline(ChainParts)
                    });
                }
            }

            // 1. Expand aliases before evaluation
            string ExpandedQuery = Query;
            var Parts = Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (Parts.Length > 0)
            {
                string FirstWord = Parts[0].ToLower();
                var CurrentAliases = SettingsManager.Current.ALIASES;
                if (CurrentAliases.TryGetValue(FirstWord, out string? Expansion))
                {
                    string Remainder = Query.Substring(Parts[0].Length).Trim();
                    ExpandedQuery = string.IsNullOrEmpty(Remainder) ? Expansion : $"{Expansion} {Remainder}";
                }
            }

            // 2. Inject ML-learned history suggestions for short queries (fast recall)
            if (ExpandedQuery.Length <= 4)
            {
                var TopResults = QueryLearner.GetTopResults(ExpandedQuery, topN: 3);
                foreach (var (Title, OrigQuery, Count) in TopResults)
                {
                    string TargetQuery = OrigQuery;
                    Suggestions.Add(new CommandResult
                    {
                        TITLE       = Title.StartsWith("⭐ ") ? Title : $"⭐ {Title}",
                        DESCRIPTION = $"Recently used ({Count}×) — click or press Enter to run",
                        SIMILARITY  = 7.0 + Math.Min(3.0, Math.Sqrt(Count) * 0.5), // Always at top
                        EXECUTE     = () => ExecuteFirstSuggestion(TargetQuery)
                    });
                }
            }

            // 3. Handler suggestions (check both raw query and space-stripped query e.g. "pre cache" -> "precache")
            string NoSpacesQuery = ExpandedQuery.Replace(" ", "").Replace("-", "");

            // 3a. Dynamic Skills (Higher priority than generic handlers)
            try { Suggestions.AddRange(SkillManager.GetSkillSuggestions(ExpandedQuery)); } catch { }

            foreach (var (Type, Handler) in HANDLERS)
            {
                try
                {
                    if (Handler.Item2.CanHandle(ExpandedQuery) || Handler.Item2.CanHandle(NoSpacesQuery))
                    {
                        var Results = Handler.Item2.GetSuggestions(ExpandedQuery);
                        if (Results == null || Results.Count == 0)
                        {
                            Results = Handler.Item2.GetSuggestions(NoSpacesQuery);
                        }
                        if (Results != null && Results.Count > 0)
                        {
                            Suggestions.AddRange(Results);
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
                var AppMatches = WindowsAppScanner.GetMatchingApps(ExpandedQuery);
                if (AppMatches.Count > 0)
                {
                    Suggestions.AddRange(AppMatches);
                }
            }
            catch { }

            // 4. Global fuzzy & partial prefix matching against ALL registered Jarvis command definitions
            try
            {
                var AllDescs = GetAllCommandDescriptions();
                string LowerQuery = ExpandedQuery.ToLower().Trim();
                string LowerNoSpaces = LowerQuery.Replace(" ", "").Replace("-", "");

                foreach (var Cd in AllDescs)
                {
                    if (Cd == null || string.IsNullOrWhiteSpace(Cd.COMMAND_NAME)) continue;

                    string CmdName = Cd.COMMAND_NAME.ToLower();
                    string Example = (Cd.COMMAND_EXAMPLE ?? "").ToLower();
                    string Desc    = (Cd.COMMAND_DESCRIPTION ?? "").ToLower();

                    // Calculate similarity for each field
                    double nameSim = SearchUtil.GetSimilarity(LowerQuery, CmdName);
                    double exSim   = SearchUtil.GetSimilarity(LowerQuery, Example);
                    double descSim = SearchUtil.GetSimilarity(LowerQuery, Desc) * 0.8; // Penalty for description match

                    // Space-insensitive fallbacks for Name and Example
                    double nameSimNoSpace = SearchUtil.GetSimilarity(LowerNoSpaces, CmdName.Replace(" ", "").Replace("-", "")) * 0.9;
                    double exSimNoSpace   = SearchUtil.GetSimilarity(LowerNoSpaces, Example.Replace(" ", "").Replace("-", "")) * 0.9;

                    double finalSim = Math.Max(nameSim, Math.Max(exSim, Math.Max(descSim, Math.Max(nameSimNoSpace, exSimNoSpace))));

                    // Acronym boost
                    if (SearchUtil.IsAcronymMatch(LowerQuery, CmdName)) finalSim = Math.Max(finalSim, 4.0);

                    if (finalSim > 0.45) // Threshold for relevance
                    {
                        // Avoid duplicates if specific handler already produced exact card
                        if (!Suggestions.Any(s => s.TITLE.IndexOf(Cd.COMMAND_NAME, StringComparison.OrdinalIgnoreCase) >= 0 || (!string.IsNullOrEmpty(Cd.COMMAND_EXAMPLE) && s.TITLE.IndexOf(Cd.COMMAND_EXAMPLE, StringComparison.OrdinalIgnoreCase) >= 0)))
                        {
                            string RunTarget = !string.IsNullOrWhiteSpace(Cd.COMMAND_EXAMPLE) ? Cd.COMMAND_EXAMPLE : Cd.COMMAND_NAME;
                            Suggestions.Add(new CommandResult
                            {
                                TITLE       = $"⚡ Command: {Cd.COMMAND_NAME}",
                                DESCRIPTION = $"{Cd.COMMAND_DESCRIPTION} (Example: {Cd.COMMAND_EXAMPLE})",
                                SIMILARITY  = finalSim,
                                EXECUTE     = () => ExecuteFirstSuggestion(RunTarget)
                            });
                        }
                    }
                }
            }
            catch { }

            // 5. Apply ML learned boost on top of every suggestion's raw similarity
            foreach (var S in Suggestions)
            {
                double Boost = QueryLearner.GetBoost(ExpandedQuery, S.TITLE);
                if (Boost > 0) S.SIMILARITY += Boost;
            }

            // 6. Deduplicate by title (keep highest scoring) then sort descending
            var Deduped = Suggestions
                .GroupBy(s => s.TITLE, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.SIMILARITY).First())
                .OrderByDescending(s => s.SIMILARITY)
                .ToList();

            if (Deduped.Count > 0)
            {
                var SbDebug = new StringBuilder();
                SbDebug.AppendLine($"Command Matching Results for '{Query}':");
                foreach (var S in Deduped.Take(5))
                    SbDebug.AppendLine($"- {S.TITLE} (Score: {S.SIMILARITY:F2})");
                DebugConsoleOverlay.LogVerbose("Parser-Match", SbDebug.ToString(), isMinimal: true);
            }

            // 7. Last Resort: If no high-confidence command or app exists, suggest AI Chat
            if (!string.IsNullOrWhiteSpace(ExpandedQuery) && !Deduped.Any(s => s.SIMILARITY >= 8.0))
            {
                Deduped.Add(new CommandResult
                {
                    TITLE = $"🧠 Ask Assistant: \"{ExpandedQuery}\"",
                    DESCRIPTION = "No exact command match found. Route this query to Jarvis AI Chat.",
                    SIMILARITY = 1.0, // Low but present
                    EXECUTE = () => _ = ChatOverlay.SubmitTextMessage(ExpandedQuery)
                });
            }

            return Deduped.Take(12).ToList();
        }

        public static void ExecuteFirstSuggestion(string Query)
        {
            if (string.IsNullOrWhiteSpace(Query)) return;

            string CleanQuery = CleanTitlePrefixes(Query);
            string LowerClean = CleanQuery.ToLower().Trim();

            // CRITICAL POWER SAFETY SHIELD: NEVER auto-execute power-state operations from fuzzy or star queries!
            if (LowerClean.Contains("shutdown") || LowerClean.Contains("sleep") ||
                LowerClean.Contains("reboot") || LowerClean.Contains("restart") ||
                LowerClean.Contains("power off") || LowerClean.Contains("turn off"))
            {
                // Instead of executing, paste it into the HUD search box so the user can explicitly trigger it
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow Mw)
                    {
                        Mw.SearchInput.Text = CleanQuery;
                        Mw.SearchInput.CaretIndex = CleanQuery.Length;
                        Mw.SearchInput.Focus();
                    }
                });
                TextOverlay.Show("⚠️ Power action requires manual execution.", 3000);
                return;
            }

            // Trigger parallel Dual-LLM Co-Pilot analysis if enabled (default disabled)
            DualLlmCopilot.ProcessQueryParallel(Query);

            // Get all suggestions sorted by Similarity score descending
            var Suggestions = GetSuggestions(CleanQuery);
            foreach (var S in Suggestions)
            {
                if (S.TITLE.StartsWith("⭐ ")) continue; // Skip circular star items

                // Only invoke if it is a highly similar or exact match to prevent executing random commands
                string CleanSuggestionTitle = CleanTitlePrefixes(S.TITLE).ToLower();
                if (CleanSuggestionTitle.Contains(LowerClean) || LowerClean.Contains(CleanSuggestionTitle) || S.SIMILARITY >= 7.5)
                {
                    if (S.EXECUTE != null)
                    {
                        S.EXECUTE.Invoke();
                        return;
                    }
                }
            }

            // Fallback: If no local command or app is found, route to Gemini AI to parse intent!
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await ChatOverlay.SubmitVoiceCommand(CleanQuery, showUi: true);
                }
                catch (Exception Ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AI Fallback error: {Ex.Message}");
                }
            });
        }

        public static void ExecuteSuggestionByTitle(string Title)
        {
            if (string.IsNullOrWhiteSpace(Title)) return;
            string TargetTitleClean = CleanTitlePrefixes(Title).ToLower().Trim();

            // CRITICAL POWER SAFETY SHIELD: NEVER auto-execute power-state operations from fuzzy or star queries!
            if (TargetTitleClean.Contains("shutdown") || TargetTitleClean.Contains("sleep") ||
                TargetTitleClean.Contains("reboot") || TargetTitleClean.Contains("restart pc") ||
                TargetTitleClean.Contains("power off") || TargetTitleClean.Contains("turn off"))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (System.Windows.Application.Current.MainWindow is MainWindow Mw)
                    {
                        Mw.SearchInput.Text = Title;
                        Mw.SearchInput.CaretIndex = Title.Length;
                        Mw.SearchInput.Focus();
                    }
                });
                TextOverlay.Show("⚠️ Power action requires manual execution.", 3000);
                return;
            }

            // Look up exact suggestion by iterating through all registered handlers
            foreach (var (Type, Handler) in HANDLERS)
            {
                try
                {
                    var Descs = Handler.Item2.GetCommandDescriptions();
                    foreach (var Desc in Descs)
                    {
                        var Results = Handler.Item2.GetSuggestions(Desc.COMMAND_NAME);
                        if (Results != null)
                        {
                            foreach (var S in Results)
                            {
                                string CleanS = CleanTitlePrefixes(S.TITLE).ToLower().Trim();
                                if (CleanS == TargetTitleClean || TargetTitleClean.Contains(CleanS) || CleanS.Contains(TargetTitleClean))
                                {
                                    if (S.EXECUTE != null)
                                    {
                                        S.EXECUTE.Invoke();
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
            ExecuteFirstSuggestion(Title);
        }

        public static string CleanTitlePrefixes(string Title)
        {
            if (string.IsNullOrWhiteSpace(Title)) return string.Empty;

            string Clean = Title.Trim();
            string[] Prefixes = new[] { "⭐ ", "⚡ Command: ", "⚡ ", "🎙️ ", "🤖 ", "🌐 ", "📥 ", "⚙️ ", "🎵 ", "🔲 ", "🏷️ ", "📶 ", "🎨 ", "🧠 ", "🦙 ", "💻 ", "🔬 ", "📐 ", "🚀 " };
            foreach (var P in Prefixes)
            {
                if (Clean.StartsWith(P, StringComparison.OrdinalIgnoreCase))
                {
                    Clean = Clean.Substring(P.Length).Trim();
                }
            }

            // Strip action verbs from queries like "open settings & options gui" -> "settings & options gui"
            string[] VerbPrefixes = new[] { "open ", "launch ", "run ", "start " };
            foreach (var Vp in VerbPrefixes)
            {
                if (Clean.StartsWith(Vp, StringComparison.OrdinalIgnoreCase))
                {
                    Clean = Clean.Substring(Vp.Length).Trim();
                    break;
                }
            }

            return Clean;
        }

        private static void ExecuteChainedPipeline(string[] ChainParts)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                int Count = 0;
                foreach (var Cmd in ChainParts)
                {
                    string TrimmedCmd = Cmd.Trim();
                    if (string.IsNullOrEmpty(TrimmedCmd)) continue;

                    var SubSuggestions = GetSuggestions(TrimmedCmd);
                    if (SubSuggestions.Count > 0 && SubSuggestions[0].EXECUTE != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SubSuggestions[0].EXECUTE?.Invoke();
                        });
                        Count++;
                        await System.Threading.Tasks.Task.Delay(300); // Brief delay between actions
                    }
                }
                TextOverlay.Show($"⚡ Chained Pipeline Executed ({Count} actions completed)", 3000);
            });
        }

        public static List<CommandDesc> GetAllCommandDescriptions()
        {
            var Descs = new List<CommandDesc>();
            var SeenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var Kvp in HANDLERS)
            {
                try
                {
                    var Handler = Kvp.Value.Item2;
                    var HandlerDescs = Handler.GetCommandDescriptions();
                    if (HandlerDescs != null && HandlerDescs.Count > 0)
                    {
                        foreach (var Cd in HandlerDescs)
                        {
                            if (Cd != null && Cd.SHOW && !string.IsNullOrWhiteSpace(Cd.COMMAND_NAME))
                            {
                                if (SeenCommands.Add(Cd.COMMAND_NAME))
                                {
                                    Descs.Add(Cd);
                                }
                            }
                        }
                    }
                    else
                    {
                        string RegDesc = Kvp.Value.Item1;
                        if (!string.IsNullOrWhiteSpace(RegDesc))
                        {
                            string Name = Kvp.Key.ToString().ToLower().Replace("_", " ");
                            if (SeenCommands.Add(Name))
                            {
                                Descs.Add(new CommandDesc(Name, RegDesc, Name));
                            }
                        }
                    }
                }
                catch
                {
                    // Fail-safe for individual handler description gathering
                }
            }

            return Descs;
        }

        // Groups each handler's commands under a display category for the categorized command browser overlay.
        public static readonly Dictionary<CommandType, string> CATEGORIES = new Dictionary<CommandType, string>
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
            { CommandType.ANIMATION_OPTIONS, "Customization" },
            { CommandType.TTS, "Audio & Media" }
        };

        // Preferred display order for categories in the browser overlay.
        public static readonly List<string> CATEGORY_ORDER = new List<string>
        {
            "AI & Automation", "System & Power", "Files & Editing", "Apps & Launcher",
            "Network & Mobile", "Web Scraping", "Audio & Media", "Media Processing", "Productivity",
            "Developer Tools", "Diagnostics", "Customization", "Utilities", "Other"
        };

        public static List<string> CategoryOrder => CATEGORY_ORDER;

        public static Dictionary<string, List<CommandDesc>> GetCommandDescriptionsByCategory()
        {
            var Result = new Dictionary<string, List<CommandDesc>>();
            var SeenCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var Kvp in HANDLERS)
            {
                string Category = CATEGORIES.TryGetValue(Kvp.Key, out string? Cat) ? Cat : "Other";
                try
                {
                    var HandlerDescs = Kvp.Value.Item2.GetCommandDescriptions();
                    if (HandlerDescs == null) continue;

                    foreach (var Cd in HandlerDescs)
                    {
                        if (Cd == null || !Cd.SHOW || string.IsNullOrWhiteSpace(Cd.COMMAND_NAME)) continue;
                        if (!SeenCommands.Add(Cd.COMMAND_NAME)) continue;

                        if (!Result.TryGetValue(Category, out var List))
                        {
                            List = new List<CommandDesc>();
                            Result[Category] = List;
                        }
                        List.Add(Cd);
                    }
                }
                catch
                {
                    // Fail-safe for individual handler description gathering
                }
            }

            return Result;
        }

        public static void Initialize()
        {
            foreach (var (Type, Handler) in HANDLERS)
            {
                try
                {
                    Handler.Item2.OnStart();
                }
                catch
                {
                    // Fail-safe for individual handler initialization errors
                }
            }
        }
    }
}
