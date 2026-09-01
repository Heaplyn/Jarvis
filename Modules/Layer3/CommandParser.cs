// Developer: heaplyn
// Date: 2026-08-22
// Summary: Dynamic router dispatcher coordinating handler resolutions and aggregations.
//          Uses reflection to dynamically register all concrete implementations of ICommandHandler.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class CommandParser
    {
        public static event Action<double>? OnTextOpacityChanged;
        public static void TriggerTextOpacityChange(double o) => OnTextOpacityChanged?.Invoke(o);

        // Keyed by class name to maintain Dictionary shape for compatibility
        public static readonly Dictionary<string, System.Tuple<string, ICommandHandler>> HANDLERS = new();
        private static readonly Dictionary<ICommandHandler, List<CommandDesc>> _descCache = new();
        private static readonly List<CommandDesc> _allDescsCache = new();

        static CommandParser()
        {
            try
            {
                var handlerTypes = typeof(CommandParser).Assembly.GetTypes()
                    .Where(t => typeof(ICommandHandler).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in handlerTypes)
                {
                    try
                    {
                        var handler = (ICommandHandler)Activator.CreateInstance(type)!;
                        string desc = type.Name.Replace("CommandHandler", "");
                        var descs = handler.GetCommandDescriptions() ?? new List<CommandDesc>();
                        _descCache[handler] = descs;
                        _allDescsCache.AddRange(descs);
                        if (descs.Count > 0)
                        {
                            desc = descs[0].COMMAND_NAME;
                        }
                        HANDLERS[type.Name] = new System.Tuple<string, ICommandHandler>(desc, handler);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CommandParser dynamic loader error: {ex.Message}");
            }
        }

        public static bool IsKnownLocalCommand(string Query)
        {
            string Q = Query.Trim().ToLower();
            foreach (var Pair in HANDLERS.Values) {
                if (Pair.Item2.CanHandle(Q)) return true;
                }
            return false;//CoreRegistry.System.Apps.GetMatchingApps(Q).Any();
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
                var Aliases = CoreRegistry.Data.Settings.Current.ALIASES;
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
                    bool match = h.Item2.CanHandle(LowerExpanded) || h.Item2.CanHandle(NoSpacesQuery);

                    if (!match)
                    {
                        if (_descCache.TryGetValue(h.Item2, out var descs))
                        {
                            if (descs.Any(d => SearchUtil.IsClose(LowerExpanded, d.COMMAND_NAME) || 
                                               (!string.IsNullOrEmpty(d.COMMAND_DESCRIPTION) && d.COMMAND_DESCRIPTION.ToLower().Contains(LowerExpanded))))
                                match = true;
                        }
                    }

                    if (match)
                    {
                        var res = h.Item2.GetSuggestions(ExpandedQuery);
                        if (res != null) Suggestions.AddRange(res);
                    }
                } catch { }
            }

            // 3. App Suggestions
            var apps = CoreRegistry.System.Apps.GetMatchingApps(LowerExpanded);
            foreach (var a in apps) {
                Suggestions.Add(new CommandResult {
                    TITLE = "📱 App: " + a.Name,
                    DESCRIPTION = "Launch application",
                    SIMILARITY = a.SIMILARITY > 0 ? a.SIMILARITY : 4.5,
                    EXECUTE = () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = a.TargetPath, UseShellExecute = true })
                });
            }

            // 4. Global Fuzzy Command Matching (Lightning Suggestions)
            var AllDescs = GetAllCommandDescriptions();
            foreach (var Cd in AllDescs)
            {
                if (Cd == null || string.IsNullOrWhiteSpace(Cd.COMMAND_NAME)) continue;
                string CmdName = Cd.COMMAND_NAME.ToLower();
                double finalSim = SearchUtil.GetSimilarity(LowerExpanded, CmdName);

                if (SearchUtil.IsAcronymMatch(LowerExpanded, CmdName)) finalSim = Math.Max(finalSim, 6.0);

                // Search inside the command description for keyword relevance
                if (!string.IsNullOrWhiteSpace(Cd.COMMAND_DESCRIPTION))
                {
                    string descLower = Cd.COMMAND_DESCRIPTION.ToLower();
                    if (descLower.Contains(LowerExpanded))
                    {
                        double descSim = 4.5 + ((double)LowerExpanded.Length / descLower.Length);
                        finalSim = Math.Max(finalSim, descSim);
                    }
                }

                if (finalSim > 0.45)
                {
                    if (!Suggestions.Any(s => s.TITLE.Contains(Cd.COMMAND_NAME, StringComparison.OrdinalIgnoreCase)))
                    {
                        string RunTarget = !string.IsNullOrWhiteSpace(Cd.COMMAND_EXAMPLE) ? Cd.COMMAND_EXAMPLE : Cd.COMMAND_NAME;

                        Suggestions.Add(new CommandResult
                        {
                            TITLE       = $"⚡ {Cd.COMMAND_NAME}",
                            DESCRIPTION = Cd.COMMAND_DESCRIPTION,
                            SIMILARITY  = finalSim - 0.1,
                            EXECUTE     = () =>
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                                    var win = System.Windows.Application.Current.MainWindow;
                                    if (win != null) {
                                        var input = win.FindName("SearchInput") as System.Windows.Controls.TextBox;
                                        if (input != null) {
                                            input.Text = RunTarget;
                                            input.CaretIndex = input.Text.Length;
                                            input.Focus();
                                        }
                                    }
                                });
                            }
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

            ChronoLogManager.LogEvent("Command", Query);

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
            return _allDescsCache;
        }

        public static List<string> CategoryOrder => new List<string> { "AI & Automation", "System & Power", "Files & Editing", "Apps & Launcher", "Audio & Media", "Productivity", "Utilities" };

        public static Dictionary<string, List<CommandDesc>> GetCommandDescriptionsByCategory() {
            var res = new Dictionary<string, List<CommandDesc>>();
            foreach (var cat in CategoryOrder) res[cat] = new List<CommandDesc>();

            foreach (var kvp in HANDLERS) {
                string cat = GetCategoryForHandler(kvp.Value.Item2);
                if (!res.ContainsKey(cat)) res[cat] = new List<CommandDesc>();
                if (_descCache.TryGetValue(kvp.Value.Item2, out var descs))
                {
                    res[cat].AddRange(descs);
                }
            }
            return res;
        }

        private static string GetCategoryForHandler(ICommandHandler handler) {
            string ns = handler.GetType().Namespace ?? "";
            if (ns.EndsWith(".AI") || ns.Contains(".AI")) return "AI & Automation";
            if (ns.EndsWith(".System") || ns.Contains(".System")) return "System & Power";
            if (ns.EndsWith(".Dev") || ns.Contains(".Dev")) return "Files & Editing";
            if (ns.EndsWith(".Apps") || ns.Contains(".Apps")) return "Apps & Launcher";
            if (ns.EndsWith(".Media") || ns.Contains(".Media")) return "Audio & Media";
            if (ns.EndsWith(".Productivity") || ns.Contains(".Productivity")) return "Productivity";
            
            string name = handler.GetType().Name;
            if (name.Contains("Ai") || name.Contains("Llm") || name.Contains("Teacher") || name.Contains("Assistant") || name.Contains("Gcloud") || name.Contains("Claw")) return "AI & Automation";
            if (name.Contains("Lock") || name.Contains("Restart") || name.Contains("Power") || name.Contains("Exit") || name.Contains("Stats") || name.Contains("Brightness") || name.Contains("Opacity") || name.Contains("Diagnostics") || name.Contains("Update")) return "System & Power";
            if (name.Contains("Edit") || name.Contains("Open") || name.Contains("View") || name.Contains("Git") || name.Contains("Build") || name.Contains("Template") || name.Contains("Organizer") || name.Contains("Obfuscator") || name.Contains("Obf") || name.Contains("Compiler") || name.Contains("Disassembler") || name.Contains("Bin")) return "Files & Editing";
            if (name.Contains("Launcher") || name.Contains("Grid") || name.Contains("Search") || name.Contains("Powershell") || name.Contains("Cli")) return "Apps & Launcher";
            if (name.Contains("Volume") || name.Contains("Mute") || name.Contains("Tts") || name.Contains("Voice") || name.Contains("Ffmpeg") || name.Contains("Music") || name.Contains("Playlist") || name.Contains("Biometrics") || name.Contains("Theme") || name.Contains("Animation") || name.Contains("Background") || name.Contains("Screenshot") || name.Contains("Vision") || name.Contains("Visuals")) return "Audio & Media";
            if (name.Contains("Todo") || name.Contains("Reminder") || name.Contains("Timer") || name.Contains("Note") || name.Contains("Focus") || name.Contains("Calendar") || name.Contains("Storage") || name.Contains("Clean")) return "Productivity";
            
            return "Utilities";
        }

        public static void Initialize() { }
    }
}
