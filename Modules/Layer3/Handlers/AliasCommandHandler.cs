// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles CLI commands to manage custom command shortcuts/aliases persistent in settings.

using System;
using System.Collections.Generic;

namespace JarvisLauncher.Modules.Layer3.Handlers
{
    public class AliasCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query.StartsWith("alias") || query.StartsWith("unalias");
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = 2.0; // High priority match

            if (cmd == "alias")
            {
                if (parts.Length > 2)
                {
                    string shortcut = parts[1].ToLower();
                    // Extract expansion (everything after the shortcut keyword)
                    string expansion = query[(query.IndexOf(parts[1]) + parts[1].Length)..].Trim();

                    suggestions.Add(new CommandResult
                    {
                        Title = $"Create Alias: {shortcut} -> \"{expansion}\"",
                        Description = "Save this custom command shortcut",
                        Execute = () => SetAlias(shortcut, expansion),
                        Similarity = similarity
                    });
                }
                else if (parts.Length > 1)
                {
                    string shortcut = parts[1].ToLower();
                    suggestions.Add(new CommandResult
                    {
                        Title = $"Create Alias for '{shortcut}'...",
                        Description = $"Type the target command (e.g. 'alias {shortcut} empty')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
                else
                {
                    // Show all configured aliases
                    var currentAliases = SettingsManager.Current.Aliases;
                    if (currentAliases.Count > 0)
                    {
                        foreach (var alias in currentAliases)
                        {
                            suggestions.Add(new CommandResult
                            {
                                Title = $"Alias: {alias.Key} -> \"{alias.Value}\"",
                                Description = $"Type 'unalias {alias.Key}' to remove this shortcut",
                                Execute = null,
                                Similarity = similarity - 0.1
                            });
                        }
                    }

                    suggestions.Add(new CommandResult
                    {
                        Title = "Create Alias...",
                        Description = "Type 'alias <shortcut> <command>' (e.g. 'alias clean empty')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
            }
            else if (cmd == "unalias")
            {
                if (parts.Length > 1)
                {
                    string shortcut = parts[1].ToLower();
                    if (SettingsManager.Current.Aliases.TryGetValue(shortcut, out string? expansion))
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"Remove Alias: '{shortcut}'",
                            Description = $"Delete the shortcut mapping to: \"{expansion}\"",
                            Execute = () => RemoveAlias(shortcut),
                            Similarity = similarity
                        });
                    }
                    else
                    {
                        suggestions.Add(new CommandResult
                        {
                            Title = $"Remove Alias: '{shortcut}' (Not Found)",
                            Description = "No such alias is currently configured",
                            Execute = null,
                            Similarity = similarity
                        });
                    }
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title = "Remove Alias...",
                        Description = "Type the alias shortcut to remove (e.g. 'unalias clean')",
                        Execute = null,
                        Similarity = similarity
                    });
                }
            }

            return suggestions;
        }

        private static void SetAlias(string shortcut, string expansion)
        {
            try
            {
                // Prevent circular aliases
                if (shortcut == expansion || expansion.StartsWith(shortcut + " "))
                {
                    TextOverlay.Show("⚠️ Cannot map an alias to itself or to a command starting with itself!", 3000);
                    return;
                }

                SettingsManager.Current.Aliases[shortcut] = expansion;
                SettingsManager.Save();
                TextOverlay.Show($"🏷️ Configured alias '{shortcut}' successfully!", 2500);
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to save alias: {ex.Message}", 3000);
            }
        }

        private static void RemoveAlias(string shortcut)
        {
            try
            {
                if (SettingsManager.Current.Aliases.Remove(shortcut))
                {
                    SettingsManager.Save();
                    TextOverlay.Show($"🏷️ Removed alias '{shortcut}' successfully!", 2500);
                }
            }
            catch (Exception ex)
            {
                TextOverlay.Show($"⚠️ Failed to remove alias: {ex.Message}", 3000);
            }
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("alias <n> <cmd>", "Create persistent command alias", "alias gp push"),
                new CommandDesc("alias list", "List registered custom aliases", "alias list"),
                new CommandDesc("alias remove <n>", "Delete a custom command alias", "alias remove gp")
            };
        }
    }
}
