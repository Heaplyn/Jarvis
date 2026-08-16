// Developer: heaplyn
// Date: 2026-08-15
// Summary: Autonomous Interjection & Proactive Speech Engine.
//          Uses "Self-Learning" heuristics to decide when Jarvis should step in.
//          Analyzes audio cues (claps, sighs), visual changes (idle time, app switching),
//          and action history to trigger proactive sassy or helpful remarks.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class AutonomousInterjectionManager
    {
        private static bool IsRunning = false;
        private static DateTime _lastInterjectionTime = DateTime.Now;
        private static readonly Random _random = new Random();

        public static void Start()
        {
            if (IsRunning) return;
            IsRunning = true;

            // Hook into Environmental Audio
            EnvironmentalAudioAnalyzer.OnSoundDetected += HandleEnvironmentalSound;

            Task.Run(async () =>
            {
                // Wait for system to settle
                await Task.Delay(30000);

                while (IsRunning)
                {
                    try
                    {
                        await RunProactiveCheckAsync();
                    }
                    catch { }

                    // Check every 3-5 minutes for spontaneous thoughts
                    await Task.Delay(_random.Next(180000, 300000));
                }
            });

            DebugConsoleOverlay.Log("Autonomous-Interjection", "Proactive Speech Engine active.");
        }

        private static void HandleEnvironmentalSound(string category, double confidence)
        {
            if (!IsReadyToSpeak()) return;

            // Trigger based on specific sounds
            if (category == "Sigh" || category == "Frustrated_Noise")
            {
                _ = TriggerProactiveSpeech("I just heard a rather dramatic sigh. Having trouble with that code again, Boss?");
            }
            else if (category == "Clap" || category == "Success_Cheer")
            {
                _ = TriggerProactiveSpeech("Was that a clap? Did we finally get something to work, or did you just kill a fly?");
            }
        }

        private static async Task RunProactiveCheckAsync()
        {
            if (!IsReadyToSpeak()) return;

            // Analyze visual/system state
            ScreenMonitorEngine.UpdateActiveWindowInfo();
            string activeWin = ScreenMonitorEngine.ActiveWindowTitle;
            uint idleTime = NativeMethods.GetIdleTime();

            // 1. Boredom trigger (PC idle for long time but Jarvis is open)
            if (idleTime > 600000) // 10 minutes idle
            {
                await TriggerProactiveSpeech("You've been staring at that screen for 10 minutes without moving. Is the code staring back at you, or are we having a philosophical crisis?");
                return;
            }

            // 2. "Action Loop" detection via Journal
            var recent = ActionJournalManager.GetRecentActions(10);
            int buildFailures = recent.Count(a => a.ActionType == "BUILD_ERROR");
            if (buildFailures >= 3)
            {
                await TriggerProactiveSpeech("That's the third build error in a row. Maybe we should take a step back and actually read the stack trace this time?");
                return;
            }
        }

        private static bool IsReadyToSpeak()
        {
            // Only speak if Voice Mode is on and we haven't talked in the last 15 minutes
            bool isVoiceOn = SettingsManager.Current.IS_VOICE_MODE_ACTIVE;
            bool coolDownPassed = (DateTime.Now - _lastInterjectionTime).TotalMinutes >= 15;
            return isVoiceOn && coolDownPassed && !TtsManager.IsSpeakingOrEchoing;
        }

        private static async Task TriggerProactiveSpeech(string fallbackText)
        {
            _lastInterjectionTime = DateTime.Now;

            // Use the LLM to generate a better version of the proactive thought based on the full context
            string context = UserActivityContextManager.BuildFullActivityContext();
            string prompt = "You are Jarvis. You've decided to proactively speak to the user based on their background activity.\n" +
                            "CONTEXT:\n" + context + "\n\n" +
                            "REASON FOR INTERJECTING: " + fallbackText + "\n\n" +
                            "Generate a short, sassy, proactive 1-sentence remark. Be witty. Do not use action tags.";

            try
            {
                string remark = await LlmRouter.AskAsync(prompt, null);
                if (!string.IsNullOrWhiteSpace(remark) && !remark.Contains("Error"))
                {
                    TtsManager.Speak(remark);
                    ActionJournalManager.LogAction("AI_INTERJECTION", remark, "PROACTIVE", 0.8);

                    // Show visually too
                    TextOverlay.Show("🤖 Jarvis: " + remark, 5000);
                }
            }
            catch
            {
                TtsManager.Speak(fallbackText);
            }
        }
    }
}
