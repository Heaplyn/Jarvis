// Developer: heaplyn
// Date: 2026-08-08
// Summary: Parses and calculates mathematical string queries using DataTable.Compute and Regex filters.

using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace JarvisLauncher
{
    public class MathCommandHandler : ICommandHandler
    {
        private static readonly DataTable _mathTable = new DataTable();
        private static readonly Regex _mathRegex = new Regex(@"^[0-9\s\+\-\*\/\(\)\.]+$");

        public bool CanHandle(string query)
        {
            query = query.Trim();
            return _mathRegex.IsMatch(query) && HasOperatorOrMultipleNumbers(query);
        }

        public List<CommandResult> GetSuggestions(string query)
        {
            var suggestions = new List<CommandResult>();
            query = query.Trim();

            try
            {
                var result = _mathTable.Compute(query, string.Empty);
                if (result != null && result != DBNull.Value)
                {
                    var resultStr = result.ToString() ?? "";
                    suggestions.Add(new CommandResult
                    {
                        Title = resultStr,
                        Description = $"Math expression: {query} (Press Enter to copy)",
                        Execute = () => System.Windows.Clipboard.SetText(resultStr),
                        Similarity = 1.5
                    });
                }
            }
            catch
            {
                // Ignore incomplete expressions
            }

            return suggestions;
        }

        private static bool HasOperatorOrMultipleNumbers(string expr)
        {
            return expr.Contains('+') || expr.Contains('-') || expr.Contains('*') || expr.Contains('/') || expr.Contains('(') || expr.Contains(')');
        }
    }
}
