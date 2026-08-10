// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles commands to display the visual file launchpad grid overlay (grid / files) and manage pinned file entries (pin / unpin).

using System;
using System.Collections.Generic;
using System.IO;

namespace JarvisLauncher
{
    public class GridCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            string cmd = parts[0];
            return cmd == "grid" || cmd == "files" || cmd == "pin" || cmd == "unpin";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();
            var parts = query.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return suggestions;

            string cmd = parts[0].ToLower();
            double similarity = SearchUtil.GetSimilarity(cmd, "grid");

            if (cmd == "grid" || cmd == "files")
            {
                suggestions.Add(new CommandResult
                {
                    Title       = "File Launchpad Grid",
                    Description = "Open visual dashboard layout of saved/pinned files",
                    Similarity  = similarity + 1.0,
                    Execute     = () => FileGridOverlay.OpenDashboard()
                });
            }
            else if (cmd == "pin")
            {
                if (parts.Length > 1)
                {
                    string targetPath = parts[1].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Pin File: {Path.GetFileName(targetPath)}",
                        Description = $"Pin \"{targetPath}\" persistently to the File Launchpad Dashboard",
                        Similarity  = 2.0,
                        Execute     = () => PinFileNatively(targetPath)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Pin File (Prompt)...",
                        Description = "Type a local file path to pin to your file launchpad grid",
                        Similarity  = similarity + 0.6,
                        Execute     = () => InputPromptOverlay.Show("Enter file path to pin:", (path) => PinFileNatively(path))
                    });

                    suggestions.Add(new CommandResult
                    {
                        Title       = "Browse File to Pin...",
                        Description = "Open Windows file explorer to select a file to pin",
                        Similarity  = similarity + 0.3,
                        Execute     = () => PromptAndPinFile()
                    });
                }
            }
            else if (cmd == "unpin")
            {
                if (parts.Length > 1)
                {
                    string targetPath = parts[1].Trim();
                    suggestions.Add(new CommandResult
                    {
                        Title       = $"Unpin File: {Path.GetFileName(targetPath)}",
                        Description = $"Remove \"{targetPath}\" from the File Launchpad Dashboard",
                        Similarity  = 2.0,
                        Execute     = () => FileGridOverlay.UnpinFile(targetPath)
                    });
                }
                else
                {
                    suggestions.Add(new CommandResult
                    {
                        Title       = "Unpin File (Prompt)...",
                        Description = "Type a file path to unpin from your file grid dashboard",
                        Similarity  = similarity + 0.5,
                        Execute     = () => InputPromptOverlay.Show("Enter file path to unpin:", (path) => FileGridOverlay.UnpinFile(path))
                    });
                }
            }

            return suggestions;
        }

        private static void PinFileNatively(string filePath)
        {
            string projectRoot = GetProjectRoot();
            string absolutePath = Path.IsPathRooted(filePath) 
                ? filePath 
                : Path.GetFullPath(Path.Combine(projectRoot, filePath));

            FileGridOverlay.PinFile(absolutePath);
        }

        private static void PromptAndPinFile()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select File to Pin",
                Filter = "All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FileGridOverlay.PinFile(openFileDialog.FileName);
            }
        }

        private static string GetProjectRoot()
        {
            string devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\.."));
            if (Directory.Exists(Path.Combine(devPath, "Modules")))
            {
                return devPath;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
