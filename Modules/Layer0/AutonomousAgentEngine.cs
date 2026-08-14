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
        private static bool _isRunning = false;
        private static string _lastWindow = string.Empty;
        private static int _distractionMinutes = 0;
        private static readonly string[] DistractionKeywords = new[]
        {
            "youtube", "netflix", "facebook", "twitter", "reddit", "instagram", "tiktok", "steam", "gaming", "discord", "spotify"
        };

        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Task.Run(async () =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var settings = SettingsManager.Current;
                        if (settings.IsAutonomousModeEnabled && settings.IsJarvisEnabled)
                        {
                            await RunAutonomousAudit();
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugConsoleOverlay.Log("Autonomous Error", ex.Message);
                    }

                    // Sleep for interval (default: 2 minutes)
                    int sleepMinutes = Math.Max(1, SettingsManager.Current.AutonomousIntervalMinutes);
                    await Task.Delay(sleepMinutes * 60 * 1000);
                }
            });

            DebugConsoleOverlay.Log("Autonomous", "Autonomous background agent loop started.");
        }

        public static void Stop()
        {
            _isRunning = false;
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
                string activeWin = MemoryManager.GetCurrentWindowTitle().ToLower().Trim();
                if (string.IsNullOrEmpty(activeWin)) return;

                bool isDistracting = DistractionKeywords.Any(k => activeWin.Contains(k));
                if (isDistracting)
                {
                    _distractionMinutes += SettingsManager.Current.AutonomousIntervalMinutes;

                    // If user has been distracted for 15+ minutes, trigger a nudge
                    if (_distractionMinutes >= 15)
                    {
                        _distractionMinutes = 0; // Reset counter
                        string msg = "Excuse me, I noticed you've been focused on distracting media for a while. Perhaps a quick focus sprint?";
                        TtsManager.Speak(msg, isShortSpeech: true);
                        TextOverlay.Show("⚠️ Focus Alert: Time for a micro-break?", 5000);
                        DebugConsoleOverlay.Log("Autonomous Focus", $"ADHD Nudge sent: user active in '{activeWin}' for 15+ mins.");
                    }
                }
                else
                {
                    // User is focused on work (e.g. VS Code, Roblox, terminal)
                    _distractionMinutes = 0;
                }
            }
            catch { }
        }

        private static void AuditSystemResources()
        {
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
        }

        private static void AuditUpcomingReminders()
        {
            try
            {
                var active = ReminderManager.GetActiveReminders();
                var now = DateTime.Now;

                foreach (var reminder in active)
                {
                    double minsLeft = (reminder.TargetTime - now).TotalMinutes;
                    
                    // Alert user if a reminder is due in exactly 2-5 minutes
                    if (minsLeft > 0 && minsLeft <= 5.0)
                    {
                        // We use a custom key to make sure we don't spam the alert multiple times
                        string cacheKey = $"UpcomingAlert_{reminder.Id}";
                        if (AppDomain.CurrentDomain.GetData(cacheKey) == null)
                        {
                            AppDomain.CurrentDomain.SetData(cacheKey, true);
                            string msg = $"Heads up: You have a scheduled reminder in {Math.Round(minsLeft)} minutes: '{reminder.Message}'.";
                            TtsManager.Speak(msg, isShortSpeech: true);
                            TextOverlay.Show($"🔔 Upcoming: {reminder.Message} (in {Math.Round(minsLeft)}m)", 5000);
                            DebugConsoleOverlay.Log("Autonomous Reminders", $"Proactive upcoming reminder alert: '{reminder.Message}'");
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
                var settings = SettingsManager.Current;
                if (!settings.IsTeacherModeEnabled) return;

                string activeWin = MemoryManager.GetCurrentWindowTitle().ToLower().Trim();
                if (string.IsNullOrEmpty(activeWin)) return;

                // Only capture screen if focused on common code editors / IDEs
                bool isCoding = activeWin.Contains("visual studio") || 
                                activeWin.Contains("vs code") || 
                                activeWin.Contains("roblox studio") || 
                                activeWin.Contains("rider") || 
                                activeWin.Contains("notepad++") ||
                                activeWin.Contains("sublime");

                if (!isCoding) return;

                // Take a screenshot of the primary monitor
                string? base64Image = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                if (string.IsNullOrEmpty(base64Image)) return;

                string prompt = "You are the Jarvis Code Teacher. Surveil this screenshot of the user's screen. If the user is editing code (in Visual Studio, VS Code, Roblox Studio, etc.), inspect the visible code, compilation squiggles, or error messages.\n\n" +
                                "CRITICAL RULES:\n" +
                                "1. If there are no clear syntax errors, compilation bugs, or deprecated API usages visible, respond with EXACTLY the word 'CLEAR'.\n" +
                                "2. If you spot a bug, error, deprecated route, or bad practice, write a short, high-impact educational lesson explaining the issue, why it happens, and showing the 'better method'.\n" +
                                "3. Keep your advice brief, constructive, and educational.";

                string response = await AiAPI.AnalyzeImageAsync(prompt, base64Image);

                if (!string.IsNullOrWhiteSpace(response) && response.Trim().ToUpper() != "CLEAR")
                {
                    // Alert the user via overlay and speech
                    TextOverlay.Show("🎓 Code Teacher: Visible coding warning spotted on screen!", 5000);
                    TtsManager.Speak("I noticed a potential coding issue on your screen. I've posted an educational breakdown in the chat companion.", isShortSpeech: true);
                    
                    // Log details to chat companion
                    ChatOverlay.LogConsoleAction("Screen Surveillance Nudge", "Observed potential programming anti-pattern.");
                    
                    // Post the detailed tutorial block to companion chat history
                    await ChatOverlay.SubmitTextMessage("educational advice:\n" + response);
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Surveillance Error", ex.Message);
            }
        }
    }
}
