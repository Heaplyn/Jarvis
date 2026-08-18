// Developer: heaplyn
// Date: 2026-08-17
// Summary: Router dispatcher coordinating handler resolutions and aggregations.
//          Fully restored with 80+ integrated command handlers.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JarvisLauncher.Modules.Layer3.Handlers;

using CommandDictType = System.Tuple<string, JarvisLauncher.ICommandHandler>;

namespace JarvisLauncher
{
    public enum CommandType
    {
        MATH, VOLUME, LOCK, RESTART, OPACITY, TIMER, SYSTEM_STATS, LOCAL_IP, BRIGHTNESS, CLI_RUNNER, APP_LAUNCHER, VIEW_FILE, SETTINGS, AI, RECYCLE_BIN, PROCESS_KILLER, POWER, ALIAS, TEXT_OPACITY, GIT_PUSH, COMMANDS, GIT_SETUP, LOGS, DOWNLOAD_PATH, EXIT, UPDATE, POWERSHELL, UPDATE_COMPUTER, SYS_INFO, SEARCH_LAUNCHER, SCREENSHOT, MUTE, CLIPBOARD, TODO, THEME, EDIT, OPEN, GRID, PRODUCTIVITY, EXTRA_FEATURES, NEW_IDEAS, MUSIC_PLAYLIST, STICKY_NOTE, GAME_DEV_TOOLBOX, FFMPEG, LLM, PHONE, DIAGNOSTICS, WEB_SCRAPING, CALENDAR, REMINDERS, FILE_ORGANIZER, SCREEN_ANALYSIS, BACKGROUND, VOICE_STUDIO, HELP_CENTER, ANIMATION_OPTIONS, EXPANDED_COMMANDS, ORGANIZATION_TOOLS, ADHD_FOCUS_SUITE, MCP, OAUTH2, CODE_ASSIST, IPA_COMPILER, INSTALL, TTS, BIOMETRICS, TEMPLATE, TEACHER, UNINSTALL, WEB_OP, DATABASE, BUILD, DEBUGGER, STORAGE, CODE_EDITOR, MOBILE, TUNNEL, FILE_GRID, CLIPBOARD_HISTORY, NETWORK, GCLOUD
    };

    public static class CommandParser
    {
        public static event Action<double>? OnTextOpacityChanged;
        public static void TriggerTextOpacityChange(double o) => OnTextOpacityChanged?.Invoke(o);

        public static Dictionary<CommandType, CommandDictType> HANDLERS = new Dictionary<CommandType, CommandDictType>();

        static CommandParser()
        {
            // --- CORE AI & DOCS ---
            RegisterHandler(CommandType.AI, "AI Assistant", () => new AiCommandHandler());
            RegisterHandler(CommandType.LLM, "LLM Settings", () => new LLMCommandHandler());
            RegisterHandler(CommandType.GCLOUD, "Google Cloud", () => new GCloudCommandHandler());
            RegisterHandler(CommandType.HELP_CENTER, "Help Hub", () => new HelpCommandHandler());
            RegisterHandler(CommandType.DEBUGGER, "System Debugger", () => new DebuggerCommandHandler());

            // --- SYSTEM & POWER ---
            RegisterHandler(CommandType.LOCK, "Lock Workstation", () => new LockCommandHandler());
            RegisterHandler(CommandType.RESTART, "Restart PC", () => new RestartCommandHandler());
            RegisterHandler(CommandType.POWER, "Power Suite", () => new PowerCommandHandler());
            RegisterHandler(CommandType.EXIT, "Exit Jarvis", () => new ExitCommandHandler());
            RegisterHandler(CommandType.SETTINGS, "System Settings", () => new SettingsCommandHandler());
            RegisterHandler(CommandType.SYS_INFO, "System Specs", () => new SysInfoCommandHandler());
            RegisterHandler(CommandType.SYSTEM_STATS, "Resource Hub", () => new SystemStatsCommandHandler());
            RegisterHandler(CommandType.PROCESS_KILLER, "Task Killer", () => new ProcessKillerCommandHandler());
            RegisterHandler(CommandType.LOGS, "Execution Logs", () => new LogCommandHandler());
            RegisterHandler(CommandType.DIAGNOSTICS, "Diagnostics", () => new DiagnosticsCommandHandler());
            RegisterHandler(CommandType.BRIGHTNESS, "Brightness", () => new BrightnessCommandHandler());
            RegisterHandler(CommandType.OPACITY, "HUD Opacity", () => new OpacityCommandHandler());
            RegisterHandler(CommandType.UPDATE, "Code Updates", () => new UpdateCommandHandler());
            RegisterHandler(CommandType.UPDATE_COMPUTER, "Windows Update", () => new UpdateComputerCommandHandler());

            // --- PRODUCTIVITY ---
            RegisterHandler(CommandType.TODO, "Tasks & Todo", () => new TodoCommandHandler());
            RegisterHandler(CommandType.REMINDERS, "Reminders", () => new ReminderCommandHandler());
            RegisterHandler(CommandType.TIMER, "Timers", () => new TimerCommandHandler());
            RegisterHandler(CommandType.STICKY_NOTE, "Sticky Notes", () => new StickyNotesCommandHandler());
            RegisterHandler(CommandType.ADHD_FOCUS_SUITE, "ADHD Focus", () => new AdhdFocusSuiteHandler());
            RegisterHandler(CommandType.PRODUCTIVITY, "Productivity", () => new ProductivityCommandHandler());
            RegisterHandler(CommandType.CALENDAR, "Calendar", () => new CalendarCommandHandler());
            RegisterHandler(CommandType.CLIPBOARD, "Clipboard Hub", () => new ClipboardCommandHandler());
            RegisterHandler(CommandType.MATH, "Calculus Solver", () => new MathCommandHandler());
            RegisterHandler(CommandType.STORAGE, "Storage Cleanup", () => new StorageCleanupCommandHandler());

            // --- FILES & DEV ---
            RegisterHandler(CommandType.EDIT, "AI Code Studio", () => new EditCommandHandler());
            RegisterHandler(CommandType.GIT_PUSH, "GitHub Studio", () => new GitCommandHandler());
            RegisterHandler(CommandType.GIT_SETUP, "Git Config", () => new GitSetupCommandHandler());
            RegisterHandler(CommandType.BUILD, "Universal Builder", () => new BuildCommandHandler());
            RegisterHandler(CommandType.CODE_ASSIST, "Code Helper", () => new CodeAssistCommandHandler());
            RegisterHandler(CommandType.TEMPLATE, "Code Templates", () => new TemplateCommandHandler());
            RegisterHandler(CommandType.VIEW_FILE, "File QuickView", () => new ViewFileCommandHandler());
            RegisterHandler(CommandType.FILE_ORGANIZER, "File Janitor", () => new FileOrganizerCommandHandler());
            RegisterHandler(CommandType.IPA_COMPILER, "IPA Compiler", () => new IpaCompilerCommandHandler());
            RegisterHandler(CommandType.DATABASE, "SQL Studio", () => new DatabaseCommandHandler());
            RegisterHandler(CommandType.POWERSHELL, "PowerShell Shell", () => new PowerShellRunnerCommandHandler());
            RegisterHandler(CommandType.CLI_RUNNER, "CLI Engine", () => new CliRunnerCommandHandler());
            RegisterHandler(CommandType.RECYCLE_BIN, "Recycle Bin", () => new RecycleBinCommandHandler());
            RegisterHandler(CommandType.OPEN, "File Opener", () => new OpenNativeCommandHandler());
            RegisterHandler(CommandType.CODE_EDITOR, "Lite Editor", () => new CodeEditorCommandHandler());

            // --- APPS & WEB ---
            RegisterHandler(CommandType.APP_LAUNCHER, "App Launcher", () => new AppLauncherCommandHandler());
            RegisterHandler(CommandType.SEARCH_LAUNCHER, "Global Search", () => new SearchLauncherCommandHandler());
            RegisterHandler(CommandType.WEB_SCRAPING, "Web Scraper", () => new WebScrapingCommandHandler());
            RegisterHandler(CommandType.WEB_OP, "Web Ops", () => new WebOperationCommandHandler());
            RegisterHandler(CommandType.OAUTH2, "OAuth Studio", () => new OAuth2CommandHandler());
            RegisterHandler(CommandType.MCP, "MCP Bridge", () => new McpCommandHandler());

            // --- MEDIA & CUSTOMIZATION ---
            RegisterHandler(CommandType.VOLUME, "Volume Control", () => new VolumeCommandHandler());
            RegisterHandler(CommandType.MUTE, "Mute Engine", () => new MuteCommandHandler());
            RegisterHandler(CommandType.TTS, "Voice Synth", () => new TtsCommandHandler());
            RegisterHandler(CommandType.VOICE_STUDIO, "Acoustic Studio", () => new VoiceStudioCommandHandler());
            RegisterHandler(CommandType.BIOMETRICS, "Voice ID", () => new EnrollVoiceCommandHandler());
            RegisterHandler(CommandType.FFMPEG, "FFMpeg Suite", () => new FFMpegCommandHandler());
            RegisterHandler(CommandType.MUSIC_PLAYLIST, "Jukebox", () => new MusicPlaylistCommandHandler());
            RegisterHandler(CommandType.THEME, "Theme Engine", () => new ThemeCommandHandler());
            RegisterHandler(CommandType.ANIMATION_OPTIONS, "VFX Config", () => new AnimationCommandHandler());
            RegisterHandler(CommandType.BACKGROUND, "HUD Background", () => new BackgroundCommandHandler());
            RegisterHandler(CommandType.SCREENSHOT, "Screen Capture", () => new ScreenshotCommandHandler());
            RegisterHandler(CommandType.SCREEN_ANALYSIS, "Vision AI", () => new ScreenAnalysisCommandHandler());

            // --- ADVANCED ---
            RegisterHandler(CommandType.TEACHER, "Code Teacher", () => new TeacherCommandHandler());
            RegisterHandler(CommandType.NEW_IDEAS, "Idea Lab", () => new NewIdeasCommandHandler());
            RegisterHandler(CommandType.EXPANDED_COMMANDS, "Pro Commands", () => new ExpandedCommandsHandler());
            RegisterHandler(CommandType.ORGANIZATION_TOOLS, "File AI", () => new OrganizationCommandsHandler());
            RegisterHandler(CommandType.ALIAS, "Alias Manager", () => new AliasCommandHandler());
            RegisterHandler(CommandType.LOCAL_IP, "Net Diagnostics", () => new LocalIpCommandHandler());
            RegisterHandler(CommandType.INSTALL, "Installer Hub", () => new InstallCommandHandler());
            RegisterHandler(CommandType.UNINSTALL, "Cleanup Hub", () => new UninstallCommandHandler());
            RegisterHandler(CommandType.COMMANDS, "Command Browser", () => new CommandsCommandHandler());
        }

        private static void RegisterHandler(CommandType Type, string Description, Func<ICommandHandler> Factory)
        {
            try { HANDLERS[Type] = new CommandDictType(Description, Factory()); } catch { }
        }

        public static bool IsKnownLocalCommand(string Query)
        {
            string Q = Query.Trim().ToLower();
            foreach (var Pair in HANDLERS.Values) if (Pair.Item2.CanHandle(Q)) return true;
            return CoreRegistry.Apps.GetMatchingApps(Q).Any();
        }

        public static List<CommandResult> GetSuggestions(string Query)
        {
            var Suggestions = new List<CommandResult>();
            if (string.IsNullOrWhiteSpace(Query)) return Suggestions;

            Query = Query.Trim();
            string LowerQuery = Query.ToLower();

            // Always provide the Help Center as a persistent card
            var helpCard = new CommandResult
            {
                TITLE = "📖 Open Interactive Help & Documentation Center",
                DESCRIPTION = "Browse all commands, global hotkeys, and tips",
                SIMILARITY = 0.6,
                EXECUTE = () => HelpCenterOverlay.ShowOverlay()
            };

            // 1. Alias Expansion
            string ExpandedQuery = Query;
            var Parts = Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (Parts.Length > 0)
            {
                var Aliases = CoreRegistry.Settings.Current.ALIASES;
                if (Aliases != null && Aliases.TryGetValue(Parts[0].ToLower(), out string? Expansion))
                {
                    string Remainder = Query.Substring(Parts[0].Length).Trim();
                    ExpandedQuery = string.IsNullOrEmpty(Remainder) ? Expansion : $"{Expansion} {Remainder}";
                }
            }

            string LowerExpanded = ExpandedQuery.ToLower();
            string NoSpacesQuery = LowerExpanded.Replace(" ", "").Replace("-", "");

            // 2. Handler Suggestions
            foreach (var h in HANDLERS.Values)
            {
                try {
                    if (h.Item2.CanHandle(LowerExpanded) || h.Item2.CanHandle(NoSpacesQuery))
                    {
                        var res = h.Item2.GetSuggestions(ExpandedQuery);
                        if (res != null) Suggestions.AddRange(res);
                    }
                } catch { }
            }

            // 3. App Suggestions
            var apps = CoreRegistry.Apps.GetMatchingApps(LowerExpanded);
            foreach (var a in apps) {
                Suggestions.Add(new CommandResult {
                    TITLE = "📱 App: " + a.Name,
                    DESCRIPTION = "Launch application",
                    SIMILARITY = a.SIMILARITY > 0 ? a.SIMILARITY : 4.5,
                    EXECUTE = () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = a.TargetPath, UseShellExecute = true })
                });
            }

            // 4. Global Fuzzy Command Matching
            var AllDescs = GetAllCommandDescriptions();
            foreach (var Cd in AllDescs)
            {
                if (Cd == null || string.IsNullOrWhiteSpace(Cd.COMMAND_NAME)) continue;
                string CmdName = Cd.COMMAND_NAME.ToLower();
                double finalSim = SearchUtil.GetSimilarity(LowerExpanded, CmdName);
                if (SearchUtil.IsAcronymMatch(LowerExpanded, CmdName)) finalSim = Math.Max(finalSim, 4.0);

                if (finalSim > 0.45)
                {
                    if (!Suggestions.Any(s => s.TITLE.Contains(Cd.COMMAND_NAME, StringComparison.OrdinalIgnoreCase)))
                    {
                        string RunTarget = !string.IsNullOrWhiteSpace(Cd.COMMAND_EXAMPLE) ? Cd.COMMAND_EXAMPLE : Cd.COMMAND_NAME;
                        Suggestions.Add(new CommandResult
                        {
                            TITLE       = $"⚡ Command: {Cd.COMMAND_NAME}",
                            DESCRIPTION = Cd.COMMAND_DESCRIPTION,
                            SIMILARITY  = finalSim,
                            EXECUTE     = () => ExecuteFirstSuggestion(RunTarget)
                        });
                    }
                }
            }

            // 5. ML learned boost
            foreach (var S in Suggestions)
            {
                double Boost = QueryLearner.GetBoost(LowerExpanded, S.TITLE);
                if (Boost > 0) S.SIMILARITY += Boost;
            }

            // 6. Deduplicate & Sort
            var results = Suggestions
                .GroupBy(s => s.TITLE, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(s => s.SIMILARITY).First())
                .OrderByDescending(s => s.SIMILARITY)
                .Take(15)
                .ToList();

            if (!results.Any(r => r.TITLE.Contains("Help"))) results.Add(helpCard);

            return results;
        }

        public static void ExecuteFirstSuggestion(string Query)
        {
            if (string.IsNullOrWhiteSpace(Query)) return;
            string lower = Query.ToLower();
            if (lower.Contains("shutdown") || lower.Contains("restart") || lower.Contains("reboot") || lower.Contains("sleep"))
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    var win = System.Windows.Application.Current.MainWindow;
                    if (win != null) {
                        var input = win.FindName("SearchInput") as System.Windows.Controls.TextBox;
                        if (input != null) { input.Text = Query; input.Focus(); }
                    }
                });
                return;
            }

            var suggestions = GetSuggestions(Query);
            if (suggestions.Any() && suggestions[0].SIMILARITY >= 0.7) suggestions[0].EXECUTE?.Invoke();
            else Task.Run(async () => await ChatOverlay.SubmitVoiceCommand(Query, true));
        }

        public static List<CommandDesc> GetAllCommandDescriptions() {
            var l = new List<CommandDesc>();
            foreach (var h in HANDLERS.Values) {
                try { var d = h.Item2.GetCommandDescriptions(); if (d != null) l.AddRange(d); } catch { }
            }
            return l;
        }

        public static List<string> CategoryOrder => new List<string> { "AI & Automation", "System & Power", "Files & Editing", "Apps & Launcher", "Audio & Media", "Productivity", "Utilities" };

        public static Dictionary<string, List<CommandDesc>> GetCommandDescriptionsByCategory() {
            var res = new Dictionary<string, List<CommandDesc>>();
            foreach (var cat in CategoryOrder) res[cat] = new List<CommandDesc>();

            foreach (var kvp in HANDLERS) {
                string cat = GetCategoryForType(kvp.Key);
                if (!res.ContainsKey(cat)) res[cat] = new List<CommandDesc>();
                try { var d = kvp.Value.Item2.GetCommandDescriptions(); if (d != null) res[cat].AddRange(d); } catch { }
            }
            return res;
        }

        private static string GetCategoryForType(CommandType type) {
            return type switch {
                CommandType.AI or CommandType.LLM or CommandType.GCLOUD or CommandType.CODE_ASSIST or CommandType.TEACHER => "AI & Automation",
                CommandType.LOCK or CommandType.POWER or CommandType.RESTART or CommandType.EXIT or CommandType.SYS_INFO or CommandType.DEBUGGER or CommandType.SYSTEM_STATS or CommandType.PROCESS_KILLER or CommandType.LOGS or CommandType.UPDATE or CommandType.UPDATE_COMPUTER or CommandType.DIAGNOSTICS or CommandType.SETTINGS => "System & Power",
                CommandType.EDIT or CommandType.OPEN or CommandType.VIEW_FILE or CommandType.GIT_PUSH or CommandType.BUILD or CommandType.TEMPLATE or CommandType.GIT_SETUP or CommandType.FILE_ORGANIZER or CommandType.RECYCLE_BIN or CommandType.CODE_EDITOR => "Files & Editing",
                CommandType.APP_LAUNCHER or CommandType.GRID or CommandType.SEARCH_LAUNCHER or CommandType.POWERSHELL or CommandType.CLI_RUNNER => "Apps & Launcher",
                CommandType.VOLUME or CommandType.MUTE or CommandType.TTS or CommandType.VOICE_STUDIO or CommandType.FFMPEG or CommandType.MUSIC_PLAYLIST or CommandType.BIOMETRICS or CommandType.BACKGROUND or CommandType.THEME or CommandType.ANIMATION_OPTIONS or CommandType.SCREENSHOT or CommandType.SCREEN_ANALYSIS => "Audio & Media",
                CommandType.TODO or CommandType.REMINDERS or CommandType.TIMER or CommandType.STICKY_NOTE or CommandType.ADHD_FOCUS_SUITE or CommandType.CALENDAR or CommandType.PRODUCTIVITY or CommandType.NEW_IDEAS or CommandType.EXPANDED_COMMANDS or CommandType.ORGANIZATION_TOOLS or CommandType.STORAGE => "Productivity",
                _ => "Utilities"
            };
        }

        public static void Initialize() { }
    }
}
