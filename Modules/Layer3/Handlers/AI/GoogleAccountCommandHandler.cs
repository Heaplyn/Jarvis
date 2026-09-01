// Developer: heaplyn
// Date: 2026-09-01
// Summary: HUD commands to connect Google, switch/add/remove accounts, and see who's active.
//          One click runs the browser OAuth flow (bundled client id — nothing to configure), which
//          also lights up Gemini via the login token (no API key needed).

using System;
using System.Collections.Generic;
using System.Linq;

namespace JarvisLauncher
{
    public class GoogleAccountCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            string q = query.Trim().ToLower();
            return q.StartsWith("google") || q.StartsWith("connect google") || q.StartsWith("gmail login") || q.StartsWith("sign in google");
        }

        public List<CommandDesc> GetCommandDescriptions() => new()
        {
            new CommandDesc { COMMAND_NAME = "google connect", COMMAND_DESCRIPTION = "Sign in with Google (enables Gemini + Gmail/Calendar/Drive)", COMMAND_EXAMPLE = "google connect" },
            new CommandDesc { COMMAND_NAME = "google accounts", COMMAND_DESCRIPTION = "Switch, add, or remove connected Google accounts", COMMAND_EXAMPLE = "google accounts" },
        };

        public List<CommandResult> GetSuggestions(string query)
        {
            var list = new List<CommandResult>();
            string q = query.Trim().ToLower();
            string active = GoogleAccountManager.ActiveEmail;

            // Connect / re-connect
            list.Add(new CommandResult
            {
                TITLE = string.IsNullOrEmpty(active) ? "🔗 Connect Google account" : $"🔗 Connect another Google account",
                DESCRIPTION = string.IsNullOrEmpty(active)
                    ? "Sign in — enables Gemini (no API key needed), Gmail, Calendar, Drive"
                    : $"Active: {active} — click to add a different account",
                SIMILARITY = 9.6,
                EXECUTE = () => _ = OAuth2Manager.AddGoogleAccountAsync(st => Notify(st))
            });

            // Switch between existing accounts
            foreach (var acc in GoogleAccountManager.All)
            {
                var a = acc;
                bool isActive = a.Email.Equals(active, StringComparison.OrdinalIgnoreCase);
                list.Add(new CommandResult
                {
                    TITLE = (isActive ? "✅ " : "👤 ") + a.Email,
                    DESCRIPTION = isActive ? "Active account" : "Switch to this account",
                    SIMILARITY = 9.0,
                    EXECUTE = () =>
                    {
                        if (GoogleAccountManager.Activate(a.Email)) Notify($"Switched to {a.Email}");
                    }
                });
            }

            // Remove active
            if (!string.IsNullOrEmpty(active) && (q.Contains("remove") || q.Contains("logout") || q.Contains("sign out") || q.Contains("disconnect")))
            {
                list.Add(new CommandResult
                {
                    TITLE = $"🚪 Disconnect {active}",
                    DESCRIPTION = "Remove this account from Jarvis",
                    SIMILARITY = 8.5,
                    EXECUTE = () => { GoogleAccountManager.Remove(active); Notify($"Disconnected {active}"); }
                });
            }

            return list;
        }

        private static void Notify(string msg)
        {
            try { TextOverlay.Show(msg, 3500); } catch { }
            try { DebugConsoleOverlay.Log("Google", msg); } catch { }
        }
    }
}
