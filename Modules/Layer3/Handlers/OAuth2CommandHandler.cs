// Developer: heaplyn
// Date: 2026-08-13
// Summary: Handles CLI/HUD commands to open the OAuth2 Studio or trigger Google / GitHub login.

using System;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public class OAuth2CommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "oauth" || query == "auth" || query == "login" ||
                   query == "google login" || query == "github login" || query == "oauth2" ||
                   query.StartsWith("set google_client_id ") || query.StartsWith("set google_client_secret ") ||
                   query.StartsWith("set github_client_id ") || query.StartsWith("set github_client_secret ");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string lower = query.Trim().ToLower();

            // Option 1: Open OAuth2 Studio
            suggestions.Add(new CommandResult
            {
                Title = "🔑 Open OAuth2 Account Authentication Studio",
                Description = "Manage Google Gemini AI & GitHub OAuth2 account logins and tokens",
                Similarity = 6.0,
                Execute = () => OAuth2StudioOverlay.ShowOverlay()
            });

            // Option 2: Direct Google Login
            if (lower.Contains("google"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🔑 Sign In with Google OAuth2",
                    Description = "Launch browser to authorize Google Gemini AI access token",
                    Similarity = 6.5,
                    Execute = async () => await OAuth2Manager.LoginGoogleOAuth2Async(status => TextOverlay.Show(status, 2500))
                });
            }

            // Option 3: Direct GitHub Login
            if (lower.Contains("github"))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "🐙 Sign In with GitHub OAuth2",
                    Description = "Launch browser to authorize GitHub access token",
                    Similarity = 6.5,
                    Execute = async () => await OAuth2Manager.LoginGithubOAuth2Async(status => TextOverlay.Show(status, 2500))
                });
            }

            // Custom Credentials Setter Suggesters
            if (lower.StartsWith("set google_client_id "))
            {
                string val = query.Substring("set google_client_id ".Length).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"💾 Set Google OAuth2 Client ID to: \"{val}\"",
                    Description = "Saves custom Google Cloud Client ID for Gemini authorization",
                    Similarity = 7.0,
                    Execute = () => { SettingsManager.Current.GoogleOAuthClientId = val; SettingsManager.Save(); TextOverlay.Show("✅ Google Client ID saved!", 2500); }
                });
            }
            if (lower.StartsWith("set google_client_secret "))
            {
                string val = query.Substring("set google_client_secret ".Length).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"💾 Set Google OAuth2 Client Secret to: \"{val}\"",
                    Description = "Saves custom Google Cloud Client Secret",
                    Similarity = 7.0,
                    Execute = () => { SettingsManager.Current.GoogleOAuthClientSecret = val; SettingsManager.Save(); TextOverlay.Show("✅ Google Client Secret saved!", 2500); }
                });
            }
            if (lower.StartsWith("set github_client_id "))
            {
                string val = query.Substring("set github_client_id ".Length).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"💾 Set GitHub OAuth2 Client ID to: \"{val}\"",
                    Description = "Saves custom GitHub Client ID",
                    Similarity = 7.0,
                    Execute = () => { SettingsManager.Current.GithubOAuthClientId = val; SettingsManager.Save(); TextOverlay.Show("✅ GitHub Client ID saved!", 2500); }
                });
            }
            if (lower.StartsWith("set github_client_secret "))
            {
                string val = query.Substring("set github_client_secret ".Length).Trim();
                suggestions.Add(new CommandResult
                {
                    Title = $"💾 Set GitHub OAuth2 Client Secret to: \"{val}\"",
                    Description = "Saves custom GitHub Client Secret",
                    Similarity = 7.0,
                    Execute = () => { SettingsManager.Current.GithubOAuthClientSecret = val; SettingsManager.Save(); TextOverlay.Show("✅ GitHub Client Secret saved!", 2500); }
                });
            }

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("oauth / auth / login", "Open OAuth2 Account Authentication Studio", "oauth"),
                new CommandDesc("google login", "Sign in with Google OAuth2 in default browser", "google login"),
                new CommandDesc("github login", "Sign in with GitHub OAuth2 in default browser", "github login")
            };
        }
    }
}
