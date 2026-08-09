// Developer: heaplyn
// Date: 2026-08-08
// Summary: Coordinates query dispatching, executes active handlers, and ranks the suggestion list by similarity index.

using System;
using System.Collections.Generic;

using CommandDictType = System.Tuple<string, JarvisLauncher.ICommandHandler>;

namespace JarvisLauncher
{
    public enum CommandType
    {
        MATH,
        VOLUME,
        LOCK,
        RESTART,
        APP_LAUNCHER
    };
    public static class CommandParser
    {
        public static Dictionary<CommandType, CommandDictType> Handlers = new Dictionary<CommandType, CommandDictType>
        {
            { CommandType.MATH,
            new CommandDictType("Perform mathematical calculations", new MathCommandHandler()) },
            { CommandType.VOLUME,
            new CommandDictType("Control system volume", new VolumeCommandHandler()) },
            { CommandType.LOCK,
            new CommandDictType ("Lock the system", new LockCommandHandler()) },
            { CommandType.RESTART,
            new CommandDictType ("Restart the application", new RestartCommandHandler()) },
            { CommandType.APP_LAUNCHER,
            new CommandDictType ("Launch applications", new AppLauncherCommandHandler()) }
        };
        private static CommandType GetCommandType(string query)
        {
            query = query.Replace(' ', '_').ToUpper();

            if (Enum.TryParse<CommandType>(query, out CommandType commandType))
            {
                return commandType;
            }
            else
            {
                return CommandType.APP_LAUNCHER;
            }
        }



        public static List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();

            if (string.IsNullOrWhiteSpace(query))
            {
                return suggestions;
            }

            query = query.Trim();

            foreach (var (type, handler) in Handlers)
            {
                try
                {
                    if (handler.Item2.CanHandle(query))
                    {

                        var results = handler.Item2.GetSuggestions(query);
                        if (results != null && results.Count > 0)
                        {
                            suggestions.AddRange(results);
                        }
                    }
                }
                catch
                {
                    // Fail-safe for individual handler errors
                }
            }

            // Sort suggestions in descending order of similarity
            suggestions.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

            return suggestions;
        }
    }
}
