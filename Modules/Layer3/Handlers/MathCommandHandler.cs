// Developer: heaplyn
// Date: 2026-08-08
// Summary: Parses and calculates mathematical string queries using the modular MathEngine.

using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public class MathCommandHandler : ICommandHandler
    {
        private static readonly Regex _mathRegex = new Regex(@"^[0-9\s\+\-\*\/\(\)\.E]+$");

        public bool CanHandle(string query)
        {
            query = query.Trim().ToLower();
            if (query == "calc" || query == "calculus" || query.StartsWith("calc ") || query.Contains("integrate") || query.Contains("derivative")) return true;
            return _mathRegex.IsMatch(query) && (query.Contains('+') || query.Contains('-') || query.Contains('*') || query.Contains('/') || query.Contains('('));
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            string clean = query.Trim().ToLower();

            // 1. Explicit Calculus Studio Command
            if (clean == "calc" || clean == "calculus")
            {
                suggestions.Add(new CommandResult {
                    TITLE = "📐 Open Calculus Studio",
                    DESCRIPTION = "Launch the advanced symbolic math and calculus solver",
                    EXECUTE = () => CalculusStudioOverlay.ShowStudio(),
                    SIMILARITY = 10.0
                });
                return suggestions;
            }

            // 2. Complex Query Redirection
            if (clean.Contains("integrate") || clean.Contains("derivative") || clean.Contains("limit of"))
            {
                suggestions.Add(new CommandResult {
                    TITLE = "🧠 Solve in Calculus Studio",
                    DESCRIPTION = $"Solve '{query}' using the advanced AI math engine",
                    EXECUTE = () => CalculusStudioOverlay.ShowStudio(),
                    SIMILARITY = 9.5
                });
            }

            // 3. Simple Arithmetic
            try
            {
                string result = CoreRegistry.Math.Evaluate(query);
                if (result != "Expression too complex for offline engine." && !result.StartsWith("Math Error"))
                {
                    suggestions.Add(new CommandResult
                    {
                        TITLE = result,
                        DESCRIPTION = $"Result: {query} (Click to copy)",
                        EXECUTE = () => System.Windows.Clipboard.SetText(result),
                        SIMILARITY = 1.5
                    });
                }
            }
            catch { }

            return suggestions;
        }

        public List<CommandDesc> GetCommandDescriptions()
        {
            return new List<CommandDesc>
            {
                new CommandDesc("calc", "Launch Calculus Studio", "calc"),
                new CommandDesc("5 + 5 * 2", "Quick math result", "10 + 10"),
                new CommandDesc("diff 3x^2", "Offline derivative", "diff x^3")
            };
        }

        public void OnStart() { }
    }
}
