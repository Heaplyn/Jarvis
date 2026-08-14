// Developer: heaplyn
// Date: 2026-08-14
// Summary: Handles commands to enroll or train speaker verification voiceprints.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class EnrollVoiceCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.StartsWith("enroll voice") || q.StartsWith("enroll my voice") || q.StartsWith("biometric enroll") || q.StartsWith("voice enrollment") || q.StartsWith("train voice");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string q = query.Trim();
            
            // Extract target speaker name
            string name = "Owner";
            string[] parts = q.Split(' ');
            if (parts.Length > 2)
            {
                name = parts[parts.Length - 1];
            }

            suggestions.Add(new CommandResult
            {
                Title = $"🎙️ Enroll Voiceprint for '{name}'",
                Description = "Train speaker verification biometrics to secure voice activation",
                Execute = () =>
                {
                    Task.Run(async () => await VoiceActivationManager.EnrollVoiceAsync(name));
                },
                Similarity = 8.5
            });

            suggestions.Add(new CommandResult
            {
                Title = "🛡️ Enable Speaker Voice Verification",
                Description = "GATES voice activations: only runs commands if speaker biometrics match Kyle",
                Execute = () =>
                {
                    SettingsManager.Current.IsSpeakerVerificationEnabled = true;
                    SettingsManager.Save();
                    TextOverlay.Show("✅ Speaker Verification Enabled!", 3000);
                    TtsManager.Speak("Speaker verification is now active. Only enrolled users may trigger commands.");
                },
                Similarity = q.ToLower().Contains("enable") ? 8.0 : 4.0
            });

            suggestions.Add(new CommandResult
            {
                Title = "🔓 Disable Speaker Voice Verification",
                Description = "ALLOWS any speaker to trigger voice commands (disabled owner gate)",
                Execute = () =>
                {
                    SettingsManager.Current.IsSpeakerVerificationEnabled = false;
                    SettingsManager.Save();
                    TextOverlay.Show("🔓 Speaker Verification Disabled", 3000);
                    TtsManager.Speak("Speaker verification has been disabled. Anyone can now control the HUD.");
                },
                Similarity = q.ToLower().Contains("disable") ? 8.0 : 4.0
            });

            return suggestions;
        }
    }
}
