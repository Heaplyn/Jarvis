// Developer: heaplyn
// Date: 2026-08-09
// Summary: Command handler to open the Mobile Companion Hub overlay.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JarvisLauncher
{   
    public class PhoneControlCommandHandler : ICommandHandler
    {
        private static List<string> Aliases = new List<string>
        {
            "phone",
            "mobile",
            "remote",
            "bridge",
            "sync",
            "control"
        };

        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return Aliases.Any(a => SearchUtil.IsClose(query, a));
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim().ToLower();

            if (query.StartsWith("phone vibrate"))
            {
                suggestions.Add(new CommandResult {
                    TITLE = "📳 Vibrate Phone",
                    DESCRIPTION = "Trigger haptic feedback on the connected mobile device",
                    SIMILARITY = 9.0,
                    EXECUTE = () => _ = PhoneRemoteService.VibrateAsync(MobileBridgeServer.LastConnectedPhoneIp ?? "127.0.0.1")
                });
            }
            else if (query.StartsWith("phone torch") || query.StartsWith("phone light"))
            {
                suggestions.Add(new CommandResult {
                    TITLE = "🔦 Toggle Phone Flashlight",
                    DESCRIPTION = "Remotely turn phone torch on/off",
                    SIMILARITY = 9.0,
                    EXECUTE = () => _ = PhoneRemoteService.ToggleFlashlightAsync(MobileBridgeServer.LastConnectedPhoneIp ?? "127.0.0.1")
                });
            }
            else if (query.StartsWith("phone alert ") || query.StartsWith("phone msg "))
            {
                string msg = query.Split(' ', 3).Last();
                suggestions.Add(new CommandResult {
                    TITLE = $"🔔 Alert Phone: {msg}",
                    DESCRIPTION = "Send a remote push toast notification",
                    SIMILARITY = 9.0,
                    EXECUTE = () => _ = PhoneRemoteService.ShowToastAsync(MobileBridgeServer.LastConnectedPhoneIp ?? "127.0.0.1", msg)
                });
            }

            double similarity = 0;
            foreach (var alias in Aliases)
            {
                similarity = Math.Max(similarity, SearchUtil.GetSimilarity(query, alias));
            }
            
            suggestions.Add(new CommandResult
            {
                TITLE = "📱 Mobile Companion Hub",
                DESCRIPTION = "Open connection links and remote control settings",
                EXECUTE = () =>
                {
                    MobileOverlay.ShowOverlay();
                },
                SIMILARITY = similarity + 0.5 // Boost it slightly
            });

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("phone", "Open Mobile Companion Hub", "phone"),
                new CommandDesc("phone vibrate", "Make phone vibrate", "phone vibrate"),
                new CommandDesc("phone torch", "Toggle phone flashlight", "phone torch"),
                new CommandDesc("phone alert <msg>", "Send toast to phone", "phone alert hello"),
                new CommandDesc("remote", "Manage phone connectivity", "remote")
            };
        }
    }
}
