// Developer: heaplyn
// Date: 2026-08-09
// Summary: Handles 'commands' and 'help' queries by listing all available system command keywords, descriptions, and examples.

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace JarvisLauncher
{
    public class CommandsCommandHandler : ICommandHandler
    {
        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            return query == "commands" || query == "help" || query == "?";
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            double similarity = SearchUtil.GetSimilarity(query.Trim().ToLower(), "commands");

            suggestions.Add(new CommandResult
            {
                Title       = "View System Commands",
                Description = "List all available Jarvis command actions, shortcuts, and parameter guidelines",
                Similarity  = similarity,
                Execute     = ShowCommandsList
            });

            return suggestions;
        }
        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("commands / help / ?", "List all supported commands", "commands")
            };
        }

        private static void AddCmd(StringBuilder sb, string command, string description, string example)
        {
            sb.AppendLine($"{command,-24} {description,-38} {example}");
        }

        private static void ShowCommandsList()
        {
            var allDescs = CommandParser.GetAllCommandDescriptions();

            var sb = new StringBuilder();
            sb.AppendLine("=========================================================================================");
            sb.AppendLine("                           JARVIS LAUNCHER COMMAND HANDBOOK                              ");
            sb.AppendLine("=========================================================================================");
            sb.AppendLine(string.Format("{0,-24} {1,-38} {2}", "COMMAND", "DESCRIPTION", "EXAMPLE"));
            sb.AppendLine("-----------------------------------------------------------------------------------------");
            
            foreach (var cd in allDescs)
            {
                if (cd != null && cd.Show)
                {
                    AddCmd(sb, cd.CommandName, cd.CommandDescription, cd.CommandExample);
                }
            }

            sb.AppendLine("-----------------------------------------------------------------------------------------");
            sb.AppendLine("💡 Tips:");
            sb.AppendLine("- You can press 'Enter' on any suggestion to execute it immediately.");
            sb.AppendLine("- Running 'push' automatically cleans build directories and resolves");
            sb.AppendLine("  Git index conflicts / credentials leak attempts dynamically.");
            sb.AppendLine("=========================================================================================");

            string output = sb.ToString();

            // Run on UI Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CliOutputOverlay.Show("Command Handbook", output);
            });
        }
    }
}
