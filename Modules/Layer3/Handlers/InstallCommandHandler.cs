// Developer: heaplyn
// Date: 2026-08-14
// Summary: Handles packages install commands (winget, npm, python, dotnet, or universal website scraping installer).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class InstallCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("install ") || query == "install";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string args = query.Length > 8 ? query.Substring(8).Trim() : "";

            if (string.IsNullOrEmpty(args))
            {
                suggestions.Add(new CommandResult
                {
                    Title = "📥 Install Packages & Tools",
                    Description = "Syntax: install [winget/npm/python/dotnet/url] [package_name]",
                    Similarity = 5.0,
                    Execute = () => TextOverlay.Show("Example: install winget sideloadly", 4000)
                });
                return suggestions;
            }

            // Route 1: Web Installer Scraper
            if (args.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || args.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                suggestions.Add(new CommandResult
                {
                    Title = $"🌐 Scrape & Install from: {args}",
                    Description = "Downloads and executes Windows installer binary from this webpage",
                    Similarity = 7.0,
                    Execute = () =>
                    {
                        Task.Run(async () =>
                        {
                            string result = await UniversalInstaller.InstallFromUrlAsync(args);
                            TextOverlay.Show(result, 5000);
                        });
                    }
                });
                return suggestions;
            }

            // Split action parameters
            int spaceIdx = args.IndexOf(' ');
            string provider = spaceIdx != -1 ? args.Substring(0, spaceIdx).ToLower() : args.ToLower();
            string pkg = spaceIdx != -1 ? args.Substring(spaceIdx + 1).Trim() : "";

            // Route 2: Winget installer
            if (provider == "winget")
            {
                suggestions.Add(new CommandResult
                {
                    Title = $"📦 Install Winget Package: {pkg}",
                    Description = $"Runs: winget install {pkg} --silent",
                    Similarity = 6.8,
                    Execute = () => RunInstallProcess("winget", $"install {pkg} --silent")
                });
            }
            // Route 3: NPM installer
            else if (provider == "npm")
            {
                suggestions.Add(new CommandResult
                {
                    Title = $"📦 Install NPM Package: {pkg}",
                    Description = $"Runs: npm install -g {pkg}",
                    Similarity = 6.8,
                    Execute = () => RunInstallProcess("cmd.exe", $"/c npm install -g {pkg}")
                });
            }
            // Route 4: Python installer
            else if (provider == "python" || provider == "pip")
            {
                suggestions.Add(new CommandResult
                {
                    Title = $"🐍 Install Python pip Package: {pkg}",
                    Description = $"Runs: pip install {pkg}",
                    Similarity = 6.8,
                    Execute = () => RunInstallProcess("cmd.exe", $"/c pip install {pkg}")
                });
            }
            // Route 5: Dotnet workloads installer
            else if (provider == "dotnet" || provider == "workload")
            {
                suggestions.Add(new CommandResult
                {
                    Title = $"🛠️ Install .NET Workload: {pkg}",
                    Description = $"Runs: dotnet workload install {pkg} --source https://api.nuget.org/v3/index.json",
                    Similarity = 6.8,
                    Execute = () => RunInstallProcess("dotnet", $"workload install {pkg} --source https://api.nuget.org/v3/index.json", runAsAdmin: true)
                });
            }
            else
            {
                // General fallback: Winget search install
                suggestions.Add(new CommandResult
                {
                    Title = $"📥 Install '{args}' via Winget",
                    Description = $"Runs: winget install {args}",
                    Similarity = 6.0,
                    Execute = () => RunInstallProcess("winget", $"install {args}")
                });
                suggestions.Add(new CommandResult
                {
                    Title = $"🌐 Search and download installer for '{args}' from Web",
                    Description = $"Opens Google search for '{args} download setup'",
                    Similarity = 5.8,
                    Execute = () => Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://www.google.com/search?q={Uri.EscapeDataString(args + " download windows setup msi")}",
                        UseShellExecute = true
                    })
                });
            }

            return suggestions;
        }

        private void RunInstallProcess(string command, string args, bool runAsAdmin = false)
        {
            try
            {
                TextOverlay.Show($"📥 Executing package installer: {command} {args}...", 3000);
                var psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    UseShellExecute = runAsAdmin,
                    Verb = runAsAdmin ? "runas" : "",
                    CreateNoWindow = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Installation Failed: {ex.Message}", 4000);
            }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("install winget [pkg]", "Install a package silently using winget command line", "install winget sideloadly"),
                new CommandDesc("install npm [pkg]", "Install global NPM package dependency", "install npm vite"),
                new CommandDesc("install [url]", "Scrape and download/run installer from target webpage", "install https://sideloadly.io/index.html")
            };
        }
    }
}
