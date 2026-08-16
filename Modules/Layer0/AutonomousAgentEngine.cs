// Developer: heaplyn
// Date: 2026-08-14
// Summary: Autonomous Background Agent Engine.
//          Runs a low-priority thread checking active focus, upcoming tasks/reminders,
//          and system resource thresholds periodically to proactively assist the user.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace JarvisLauncher
{
    public static class AutonomousAgentEngine
    {
        private static bool IsRunning = false;
        private static string LastWindow = string.Empty;
        private static int DistractionMinutes = 0;
        private static readonly string[] DISTRACTION_KEYWORDS = new[]
        {
            "youtube", "netflix", "facebook", "twitter", "reddit", "instagram", "tiktok", "steam", "gaming", "discord", "spotify"
        };

        private static DateTime LastDeepReflection = DateTime.MinValue;

        public static void Start()
        {
            if (IsRunning) return;
            IsRunning = true;

            Task.Run(async () =>
            {
                while (IsRunning)
                {
                    try
                    {
                        var Settings = SettingsManager.Current;
                        if (Settings.IS_AUTONOMOUS_MODE_ENABLED && Settings.IS_JARVIS_ENABLED)
                        {
                            await RunAutonomousAudit();

                            // Run Deep Reflection every 15 minutes
                            if ((DateTime.Now - LastDeepReflection).TotalMinutes >= 15)
                            {
                                LastDeepReflection = DateTime.Now;
                                await RunDeepAutonomousReflect();
                            }
                        }
                    }
                    catch (Exception Ex)
                    {
                        DebugConsoleOverlay.Log("Autonomous Error", Ex.Message);
                    }

                    // Sleep for interval (default: 2 minutes)
                    int SleepMinutes = Math.Max(1, SettingsManager.Current.AUTONOMOUS_INTERVAL_MINUTES);
                    await Task.Delay(SleepMinutes * 60 * 1000);
                }
            });

            DebugConsoleOverlay.Log("Autonomous", "Autonomous background agent loop started.");
        }

        public static void Stop()
        {
            IsRunning = false;
        }

        private static async Task RunAutonomousAudit()
        {
            // 1. Audit Focus (Active Window distraction checker)
            AuditFocus();

            // 2. Audit System Resources (CPU / RAM peaks)
            AuditSystemResources();

            // 3. Audit Reminders (Alert user of upcoming items)
            AuditUpcomingReminders();

            // 4. Audit Screen Surveillance (AI code teacher screen analysis)
            await AuditScreenSurveillance();
        }

        private static async Task RunDeepAutonomousReflect()
        {
            try
            {
                DebugConsoleOverlay.Log("Autonomous-Mind", "Jarvis is entering a background reflection cycle...");

                string journal = ActionJournalManager.GetJournalSummaryForAi();
                string memory = SemanticMemoryManager.GetMemoryContextForAi();
                string activeWin = MemoryManager.GetCurrentWindowTitle();

                string prompt = "## IDENTITY\n" +
                               "You are the autonomous subconscious of Jarvis. You are analyzing your recent session to decide if any background tasks are needed.\n\n" +
                               "## CONTEXT\n" +
                               $"{journal}\n{memory}\nActive Window: {activeWin}\n\n" +
                               "## TASK\n" +
                               "Based on the above, do you need to perform any background actions? You can use your standard [TAGS] to act.\n" +
                               "POSSIBLE ACTIONS:\n" +
                               "- [CLEAN_LOGS]: If logs are getting too large.\n" +
                               "- [ORGANIZE_FILES: path]: If you see the user working in a cluttered folder.\n" +
                               "- [UPDATE_MEMORIES]: Consolidate recent facts.\n" +
                               "- [REFRESH_PROJECT_MAP]: If the user switched projects.\n" +
                               "- [PROACTIVE_NUDGE: message]: If you have a helpful suggestion.\n\n" +
                               "If no action is needed, respond with 'QUIET'. Otherwise, execute the tags. Be decisive.";

                string response = await LlmRouter.AskAsync(prompt);

                if (string.IsNullOrWhiteSpace(response) || response.Contains("QUIET"))
                {
                    DebugConsoleOverlay.LogVerbose("Autonomous-Mind", "Reflection complete: No action required.", isMinimal: true);
                    return;
                }

                DebugConsoleOverlay.Log("Autonomous-Mind", $"Subconscious Decision: {response}");

                // Handle Proactive Nudge specifically to show it visually
                var nudgeRegex = new Regex(@"\[PROACTIVE_NUDGE:\s*(.+?)\]", RegexOptions.IgnoreCase);
                var match = nudgeRegex.Match(response);
                if (match.Success)
                {
                    string msg = match.Groups[1].Value.Trim();
                    Application.Current.Dispatcher.Invoke(() => {
                        TextOverlay.Show("💡 Jarvis Suggestion: " + msg, 6000);
                        TtsManager.Speak(msg, isShortSpeech: true);
                    });
                }

                // The AiAPI.AskGeminiInternal loop will automatically catch and execute other standard tags
                // if we were to pipe this through a specialized 'ExecuteAutonomousTask' method,
                // but since we called LlmRouter.AskAsync, if it returned tags, we might need a dedicated execution pass.

                await ProcessAutonomousResponse(response);
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Autonomous Error", "Deep reflection failed: " + ex.Message);
            }
        }

        private static async Task ProcessAutonomousResponse(string response)
        {
            // If the response contains standard executable tags, we need to make sure they run.
            // We'll reuse the logic in AiAPI by "acting" as if the user sent this response.
            if (response.Contains("[") && response.Contains("]"))
            {
                // We use a internal trigger that doesn't show in the chat UI
                await AiAPI.ExecuteAgentLoopAsync(response);
            }
        }

        private static void AuditFocus()
        {
            try
            {
                string ActiveWin = MemoryManager.GetCurrentWindowTitle().ToLower().Trim();
                if (string.IsNullOrEmpty(ActiveWin)) return;

                bool IsDistracting = DISTRACTION_KEYWORDS.Any(k => ActiveWin.Contains(k));
                if (IsDistracting)
                {
                    DistractionMinutes += SettingsManager.Current.AUTONOMOUS_INTERVAL_MINUTES;

                    // If user has been distracted for 15+ minutes, trigger a nudge
                    if (DistractionMinutes >= 15)
                    {
                        DistractionMinutes = 0; // Reset counter
                        string Msg = "Excuse me, I noticed you've been focused on distracting media for a while. Perhaps a quick focus sprint?";
                        TtsManager.Speak(Msg, isShortSpeech: true);
                        TextOverlay.Show("⚠️ Focus Alert: Time for a micro-break?", 5000);
                        DebugConsoleOverlay.Log("Autonomous Focus", $"ADHD Nudge sent: user active in '{ActiveWin}' for 15+ mins.");
                    }
                }
                else
                {
                    // User is focused on work (e.g. VS Code, Roblox, terminal)
                    DistractionMinutes = 0;
                }
            }
            catch { }
        }

        private static void AuditSystemResources()
        {
            /*
            try
            {
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    // Performance counters require a small delay between samples
                    cpuCounter.NextValue();
                    System.Threading.Thread.Sleep(100);
                    float cpuLoad = cpuCounter.NextValue();

                    if (cpuLoad > 90f)
                    {
                        DebugConsoleOverlay.Log("Autonomous Health", $"High CPU Usage Detected: {cpuLoad:F0}%");
                        TextOverlay.Show($"⚠️ System Alert: High CPU load ({cpuLoad:F0}%) detected.", 4000);
                    }
                }
            }
            catch { }
            */
        }

        private static void AuditUpcomingReminders()
        {
            try
            {
                var Active = ReminderManager.GetActiveReminders();
                var Now = DateTime.Now;

                foreach (var Reminder in Active)
                {
                    double MinsLeft = (Reminder.TargetTime - Now).TotalMinutes;
                    
                    // Alert user if a reminder is due in exactly 2-5 minutes
                    if (MinsLeft > 0 && MinsLeft <= 5.0)
                    {
                        // We use a custom key to make sure we don't spam the alert multiple times
                        string CacheKey = $"UpcomingAlert_{Reminder.Id}";
                        if (AppDomain.CurrentDomain.GetData(CacheKey) == null)
                        {
                            AppDomain.CurrentDomain.SetData(CacheKey, true);
                            string Msg = $"Heads up: You have a scheduled reminder in {Math.Round(MinsLeft)} minutes: '{Reminder.Message}'.";
                            TtsManager.Speak(Msg, isShortSpeech: true);
                            TextOverlay.Show($"🔔 Upcoming: {Reminder.Message} (in {Math.Round(MinsLeft)}m)", 5000);
                            DebugConsoleOverlay.Log("Autonomous Reminders", $"Proactive upcoming reminder alert: '{Reminder.Message}'");
                        }
                    }
                }
            }
            catch { }
        }

        private static async Task AuditScreenSurveillance()
        {
            try
            {
                var Settings = SettingsManager.Current;
                if (!Settings.IS_TEACHER_MODE_ENABLED) return;

                string ActiveWin = MemoryManager.GetCurrentWindowTitle().ToLower().Trim();
                if (string.IsNullOrEmpty(ActiveWin)) return;

                // Only capture screen if focused on common code editors / IDEs
                bool IsCoding = ActiveWin.Contains("visual studio") ||
                                ActiveWin.Contains("vs code") ||
                                ActiveWin.Contains("roblox studio") ||
                                ActiveWin.Contains("rider") ||
                                ActiveWin.Contains("notepad++") ||
                                ActiveWin.Contains("sublime");

                if (!IsCoding) return;

                // Take a screenshot of the primary monitor
                string? Base64Image = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                if (string.IsNullOrEmpty(Base64Image)) return;

                string Prompt = "You are the Jarvis Code Teacher. Surveil this screenshot of the user's screen. If the user is editing code (in Visual Studio, VS Code, Roblox Studio, etc.), inspect the visible code, compilation squiggles, or error messages.\n\n" +
                                "CRITICAL RULES:\n" +
                                "1. If there are no clear syntax errors, compilation bugs, or deprecated API usages visible, respond with EXACTLY the word 'CLEAR'.\n" +
                                "2. If you spot a bug, error, deprecated route, or bad practice, write a short, high-impact educational lesson explaining the issue, why it happens, and showing the 'better method'.\n" +
                                "3. Keep your advice brief, constructive, and educational.";

                string Response = await AiAPI.AnalyzeImageAsync(Prompt, Base64Image);

                if (!string.IsNullOrWhiteSpace(Response) && Response.Trim().ToUpper() != "CLEAR")
                {
                    // Alert the user via overlay and speech
                    TextOverlay.Show("🎓 Code Teacher: Visible coding warning spotted on screen!", 5000);
                    TtsManager.Speak("I noticed a potential coding issue on your screen. I've posted an educational breakdown in the chat companion.", isShortSpeech: true);
                    
                    // Log details to chat companion
                    ChatOverlay.LogConsoleAction("Screen Surveillance Nudge", "Observed potential programming anti-pattern.");
                    
                    // Post the detailed tutorial block to companion chat history
                    await ChatOverlay.SubmitTextMessage("educational advice:\n" + Response);
                }
            }
            catch (Exception Ex)
            {
                DebugConsoleOverlay.Log("Surveillance Error", Ex.Message);
            }
        }
    }
}
