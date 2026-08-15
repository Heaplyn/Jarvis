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
