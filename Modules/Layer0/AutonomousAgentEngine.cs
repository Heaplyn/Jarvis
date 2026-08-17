// Developer: heaplyn
// Date: 2026-08-17
// Summary: Autonomous Background Agent implementation.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;

namespace JarvisLauncher
{
    public static class AutonomousAgentEngine
    {
        private static DateTime LastAppIndexAudit = DateTime.MinValue;
        private static int DistractionMinutes = 0;
        private static readonly string[] DISTRACTION_KEYWORDS = { "youtube", "netflix", "twitter", "reddit", "facebook", "gaming", "steam" };

        public static void Start()
        {
            Task.Run(async () => {
                while (true) {
                    try {
                        AuditAppIndex();
                        await AuditAuthErrors();
                        AuditFocus();
                        AuditUpcomingReminders();
                        await AuditScreenSurveillance();
                        if (new Random().Next(100) < 5) await RunDeepAutonomousReflect();
                    } catch { }
                    await Task.Delay(TimeSpan.FromMinutes(CoreRegistry.Settings.Current.AUTONOMOUS_INTERVAL_MINUTES));
                }
            });
        }

        private static void AuditAppIndex()
        {
            if ((DateTime.Now - LastAppIndexAudit).TotalMinutes >= 30) {
                LastAppIndexAudit = DateTime.Now;
                WindowsAppScanner.IndexApplicationsGlobal(true);
            }
        }

        private static async Task AuditAuthErrors()
        {
            string active = CoreRegistry.Memory.GetCurrentWindowTitle().ToLower();
            if (active.Contains("login") || active.Contains("sign in")) {
                string? b64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                if (!string.IsNullOrEmpty(b64)) {
                    string res = await AiAPI.AnalyzeImageAsync("Check for auth errors.", b64);
                    if (!res.Contains("CLEAR")) {
                        Application.Current.Dispatcher.Invoke(() => {
                            TtsManager.Speak("I noticed an authentication error on screen.");
                            ChatOverlay.SubmitTextMessage("I see an auth error: " + res);
                        });
                    }
                }
            }
        }

        private static async Task RunDeepAutonomousReflect()
        {
            string prompt = $"Subconscious reflection on project: {CoreRegistry.Memory.GetCurrentWindowTitle()}. Decide on background tasks like [CLEAN_LOGS] or [UPDATE_MEMORIES].";
            string res = await CoreRegistry.Llm.AskAsync(prompt);
            if (!res.Contains("QUIET")) await AiAPI.ExecuteAgentLoopAsync(res);
        }

        private static void AuditFocus()
        {
            string win = CoreRegistry.Memory.GetCurrentWindowTitle().ToLower();
            if (DISTRACTION_KEYWORDS.Any(k => win.Contains(k))) {
                DistractionMinutes += 2;
                if (DistractionMinutes >= 15) {
                    DistractionMinutes = 0;
                    TtsManager.Speak("Focused on distractions for a while. Ready to switch back?");
                }
            } else DistractionMinutes = 0;
        }

        private static void AuditUpcomingReminders()
        {
            foreach (var r in ReminderManager.GetActiveReminders().Where(r => (r.TargetTime - DateTime.Now).TotalMinutes <= 5)) {
                if (AppDomain.CurrentDomain.GetData("Alert_" + r.Id) == null) {
                    AppDomain.CurrentDomain.SetData("Alert_" + r.Id, true);
                    TtsManager.Speak("Upcoming reminder: " + r.Message);
                }
            }
        }

        private static async Task AuditScreenSurveillance()
        {
            if (!CoreRegistry.Settings.Current.IS_TEACHER_MODE_ENABLED) return;
            string win = CoreRegistry.Memory.GetCurrentWindowTitle().ToLower();
            if (win.Contains("studio") || win.Contains("code")) {
                string? b64 = ScreenCaptureUtil.CapturePrimaryScreenToBase64();
                if (!string.IsNullOrEmpty(b64)) {
                    string res = await AiAPI.AnalyzeImageAsync("Jarvis Code Teacher: Spot syntax bugs or bad practices.", b64);
                    if (res != "CLEAR") {
                        TtsManager.Speak("I spotted a potential coding issue on your screen.");
                        await ChatOverlay.SubmitTextMessage("educational advice:\n" + res);
                    }
                }
            }
        }
    }
}
