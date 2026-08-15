// Developer: heaplyn
// Date: 2026-08-13
// Summary: Universal OAuth2 Engine for Google / Gemini AI and GitHub.
// Enhanced with automated session persistence checking and silent token renewal.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public static class OAuth2Manager
    {
        private static readonly HttpClient _http = new HttpClient();

        // Public Default OAuth2 Client IDs (Can be overridden by user settings)
        public const string DefaultGoogleClientId = "1092610474410-idgtl29cts68fq4vblaa3ibvuj09ph0t.apps.googleusercontent.com";
        public const string DefaultGithubClientId = "Ov23liXXXXXXXXXXXXXX";

        /// <summary>
        /// Orchestrates the authentication check. Checks local tokens first, 
        /// attempts silent renewal if expired, and falls back to a full browser login if required.
        /// </summary>
        public static async Task<bool> EnsureGoogleAuthenticatedAsync(Action<string>? statusCallback = null)
        {
            // Case 1: No local session footprints exist at all -> Prompt full login
            if (string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN) &&
                string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_REFRESH_TOKEN))
            {
                return await LoginGoogleOAuth2Async(statusCallback);
            }

            statusCallback?.Invoke("🔍 Verifying current login session...");

            // Case 2: Access token exists, check with Google endpoint to see if it is still valid
            bool tokenIsValid = await VerifyGoogleTokenValidityAsync(SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN);
            if (tokenIsValid)
            {
                statusCallback?.Invoke($"🟢 Active: {SettingsManager.Current.GOOGLE_OAUTH_USER_EMAIL}");
                return true;
            }

            // Case 3: Access token is dead but a refresh token is present -> Attempt background renewal
            if (!string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_REFRESH_TOKEN))
            {
                statusCallback?.Invoke("🔄 Session expired. Renewing credentials...");
                bool refreshSuccess = await RefreshGoogleTokenAsync();
                if (refreshSuccess)
                {
                    statusCallback?.Invoke($"🟢 Session Restored: {SettingsManager.Current.GOOGLE_OAUTH_USER_EMAIL}");
                    return true;
                }
            }

            // Case 4: Token renewal rejected or failed -> Fallback to explicit interaction
            statusCallback?.Invoke("⚠️ Session verification failed. Re-authenticating...");
            return await LoginGoogleOAuth2Async(statusCallback);
        }

        /// <summary>
        /// Initiates Google OAuth2 login flow with PKCE on a dynamically selected local port.
        /// </summary>
        public static async Task<bool> LoginGoogleOAuth2Async(Action<string>? statusCallback = null)
        {
            string clientId = string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_CLIENT_ID)
                ? DefaultGoogleClientId
                : SettingsManager.Current.GOOGLE_OAUTH_CLIENT_ID;

            int port = GetRandomUnusedPort();
            string redirectUri = $"http://127.0.0.1:{port}/oauth/callback/";

            // Generate PKCE Verifier and Challenge
            string codeVerifier = GeneratePkceVerifier();
            string codeChallenge = GeneratePkceChallenge(codeVerifier);

            statusCallback?.Invoke("🔑 Opening Google login in browser...");

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                             $"client_id={Uri.EscapeDataString(clientId)}&" +
                             $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                             $"response_type=code&" +
                             $"scope={Uri.EscapeDataString("https://www.googleapis.com/auth/generative-language.tuning email profile openid")}&" +
                             $"code_challenge={Uri.EscapeDataString(codeChallenge)}&" +
                             $"code_challenge_method=S256&" +
                             $"access_type=offline&" +
                             $"prompt=consent";

            string code = await ListenForAuthorizationCodeAsync(authUrl, redirectUri, "Google");
            if (string.IsNullOrEmpty(code))
            {
                statusCallback?.Invoke("⚠️ Google login cancelled or timed out.");
                return false;
            }

            statusCallback?.Invoke("⏳ Exchanging code for access token...");
            bool ok = await ExchangeGoogleCodeAsync(code, clientId, redirectUri, codeVerifier);
            statusCallback?.Invoke(ok ? $"🟢 Connected: {SettingsManager.Current.GOOGLE_OAUTH_USER_EMAIL}" : "❌ Google token exchange failed.");
            return ok;
        }

        /// <summary>
        /// Initiates GitHub OAuth2 login flow on a dynamically selected local port.
        /// </summary>
        public static async Task<bool> LoginGithubOAuth2Async(Action<string>? statusCallback = null)
        {
            string clientId = string.IsNullOrWhiteSpace(SettingsManager.Current.GITHUB_OAUTH_CLIENT_ID)
                ? DefaultGithubClientId
                : SettingsManager.Current.GITHUB_OAUTH_CLIENT_ID;

            int port = GetRandomUnusedPort();
            string redirectUri = $"http://127.0.0.1:{port}/oauth/callback/";

            statusCallback?.Invoke("🐙 Opening GitHub authorization in browser...");

            string authUrl = $"https://github.com/login/oauth/authorize?" +
                             $"client_id={Uri.EscapeDataString(clientId)}&" +
                             $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                             $"scope={Uri.EscapeDataString("repo user gist workflow")}";

            string code = await ListenForAuthorizationCodeAsync(authUrl, redirectUri, "GitHub");
            if (string.IsNullOrEmpty(code))
            {
                statusCallback?.Invoke("⚠️ GitHub login cancelled or timed out.");
                return false;
            }

            statusCallback?.Invoke("⏳ Exchanging code for access token...");
            bool ok = await ExchangeGithubCodeAsync(code, clientId, redirectUri);
            statusCallback?.Invoke(ok ? $"🟢 Connected: @{SettingsManager.Current.GITHUB_OAUTH_USER_LOGIN}" : "❌ GitHub token exchange failed.");
            return ok;
        }

        /// <summary>
        /// Listens locally on HTTP for the authorization code returned by the OAuth redirect.
        /// </summary>
        private static async Task<string> ListenForAuthorizationCodeAsync(string authUrl, string redirectUri, string providerName)
        {
            HttpListener? listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(redirectUri);
                listener.Start();

                // Launch system browser
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

                // Set 120-second timeout for login redirect
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    listener.Stop();
                    return string.Empty;
                }

                var req = context.Request;
                var res = context.Response;

                string code = req.QueryString["code"] ?? string.Empty;

                // Send success response page back to browser
                string html = $"<html><body style='font-family:sans-serif;background:#0f172a;color:#38bdf8;text-align:center;padding-top:50px;'>" +
                              $"<h1>✅ {providerName} Authentication Successful!</h1>" +
                              $"<p>You can close this tab and return to Jarvis Launcher.</p></body></html>";

                byte[] buffer = Encoding.UTF8.GetBytes(html);
                res.ContentLength64 = buffer.Length;
                res.ContentType = "text/html";
                await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                res.OutputStream.Close();

                listener.Stop();
                return code;
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("OAuth Listener Error", ex.Message);
                try { listener?.Stop(); } catch { }
                return string.Empty;
            }
        }

        private static async Task<bool> ExchangeGoogleCodeAsync(string code, string clientId, string redirectUri, string codeVerifier)
        {
            try
            {
                string clientSecret = SettingsManager.Current.GOOGLE_OAUTH_CLIENT_SECRET;

                var values = new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = codeVerifier
                };

                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    values["client_secret"] = clientSecret;
                }

                var content = new FormUrlEncodedContent(values);
                var response = await _http.PostAsync("https://oauth2.googleapis.com/token", content);

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tok))
                {
                    string accessToken = tok.GetString() ?? "";
                    SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN = accessToken;

                    if (doc.RootElement.TryGetProperty("refresh_token", out var refTok))
                    {
                        SettingsManager.Current.GOOGLE_OAUTH_REFRESH_TOKEN = refTok.GetString() ?? "";
                    }

                    await FetchGoogleUserInfoAsync(accessToken);
                    SettingsManager.Save();
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Google Token Exchange Error", ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Silently provisions a brand new short-lived access token using the stored refresh token.
        /// </summary>
        private static async Task<bool> RefreshGoogleTokenAsync()
        {
            try
            {
                string clientId = string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_CLIENT_ID)
                    ? DefaultGoogleClientId
                    : SettingsManager.Current.GOOGLE_OAUTH_CLIENT_ID;

                var values = new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["refresh_token"] = SettingsManager.Current.GOOGLE_OAUTH_REFRESH_TOKEN,
                    ["grant_type"] = "refresh_token"
                };

                if (!string.IsNullOrWhiteSpace(SettingsManager.Current.GOOGLE_OAUTH_CLIENT_SECRET))
                {
                    values["client_secret"] = SettingsManager.Current.GOOGLE_OAUTH_CLIENT_SECRET;
                }

                var content = new FormUrlEncodedContent(values);
                var response = await _http.PostAsync("https://oauth2.googleapis.com/token", content);
                if (!response.IsSuccessStatusCode) return false;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tok))
                {
                    string accessToken = tok.GetString() ?? "";
                    SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN = accessToken;

                    await FetchGoogleUserInfoAsync(accessToken);
                    SettingsManager.Save();
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("Google Token Refresh Error", ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Hits Google's tokeninfo utility service endpoint to check token health.
        /// </summary>
        private static async Task<bool> VerifyGoogleTokenValidityAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return false;
            try
            {
                var res = await _http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?access_token={Uri.EscapeDataString(accessToken)}");
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private static async Task FetchGoogleUserInfoAsync(string accessToken)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("email", out var email))
                    {
                        SettingsManager.Current.GOOGLE_OAUTH_USER_EMAIL = email.GetString() ?? "";
                    }
                }
            }
            catch { }
        }

        private static async Task<bool> ExchangeGithubCodeAsync(string code, string clientId, string redirectUri)
        {
            try
            {
                string clientSecret = SettingsManager.Current.GITHUB_OAUTH_CLIENT_SECRET;
                var values = new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri
                };

                if (!string.IsNullOrWhiteSpace(clientSecret))
                {
                    values["client_secret"] = clientSecret;
                }

                var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
                {
                    Content = new FormUrlEncodedContent(values)
                };
                req.Headers.Add("Accept", "application/json");

                var res = await _http.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tok))
                {
                    string accessToken = tok.GetString() ?? "";
                    SettingsManager.Current.GITHUB_TOKEN = accessToken;

                    await FetchGithubUserInfoAsync(accessToken);
                    SettingsManager.Save();
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugConsoleOverlay.Log("GitHub Token Exchange Error", ex.Message);
            }
            return false;
        }

        private static async Task FetchGithubUserInfoAsync(string accessToken)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
                req.Headers.UserAgent.ParseAdd("JarvisLauncher/1.0");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var res = await _http.SendAsync(req);
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("login", out var login))
                    {
                        SettingsManager.Current.GITHUB_OAUTH_USER_LOGIN = login.GetString() ?? "";
                    }
                }
            }
            catch { }
        }

        // --- Helpers ---
        private static int GetRandomUnusedPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string GeneratePkceVerifier()
        {
            byte[] bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string GeneratePkceChallenge(string verifier)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        public static void SignOutGoogle()
        {
            SettingsManager.Current.GOOGLE_OAUTH_ACCESS_TOKEN = string.Empty;
            SettingsManager.Current.GOOGLE_OAUTH_REFRESH_TOKEN = string.Empty;
            SettingsManager.Current.GOOGLE_OAUTH_USER_EMAIL = string.Empty;
            SettingsManager.Save();
        }

        public static void SignOutGithub()
        {
            SettingsManager.Current.GITHUB_TOKEN = string.Empty;
            SettingsManager.Current.GITHUB_OAUTH_USER_LOGIN = string.Empty;
            SettingsManager.Save();
        }
    }
}