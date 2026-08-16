// Developer: heaplyn
// Date: 2026-08-16
// Summary: Advanced Editor Intelligence Manager.
//          Provides local symbol extraction, language keywords, and hybrid AI autocomplete orchestration.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JarvisLauncher
{
    public class AutocompleteSuggestion
    {
        public string Text { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "📄";
        public double Score { get; set; } = 0;
    }

    public class SyntaxRule
    {
        public string Pattern { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#FFFFFF";
        public bool IsBold { get; set; } = false;
        public string Category { get; set; } = "General";
    }

    public static class EditorIntelligenceManager
    {
        public static Dictionary<string, List<SyntaxRule>> SyntaxHighlightingRules = new Dictionary<string, List<SyntaxRule>>(StringComparer.OrdinalIgnoreCase)
        {
            { ".cs", new List<SyntaxRule> {
                new SyntaxRule { Pattern = @"\b(public|private|protected|internal|static|void|string|int|bool|var|if|else|foreach|while|return|class|namespace|using|async|await|task|override|virtual|new|get|set|value|delegate|event)\b", ColorHex = "#569CD6", IsBold = true, Category = "Keyword" },
                new SyntaxRule { Pattern = @"\b(Console|Task|List|Dictionary|Enumerable|DateTime|Guid|Thread|Regex|HttpClient|JsonSerializer|File|Directory|Path|Math|Exception)\b", ColorHex = "#4EC9B0", Category = "Type" },
                new SyntaxRule { Pattern = @"//.*", ColorHex = "#6A9955", Category = "Comment" },
                new SyntaxRule { Pattern = @"@""[^""]*""|""[^""\\]*(?:\\.[^""\\]*)*""", ColorHex = "#D69D85", Category = "String" },
                new SyntaxRule { Pattern = @"\b\d+\b", ColorHex = "#B5CEA8", Category = "Number" }
            }},
            { ".js", new List<SyntaxRule> {
                new SyntaxRule { Pattern = @"\b(const|let|var|function|return|if|else|for|while|import|export|default|async|await|try|catch|throw|new|this|super|class)\b", ColorHex = "#569CD6", IsBold = true, Category = "Keyword" },
                new SyntaxRule { Pattern = @"//.*|/\*[\s\S]*?\*/", ColorHex = "#6A9955", Category = "Comment" },
                new SyntaxRule { Pattern = @"'[^']*'|""[^""]*""|`[^`]*`", ColorHex = "#D69D85", Category = "String" }
            }},
            { ".py", new List<SyntaxRule> {
                new SyntaxRule { Pattern = @"\b(def|class|return|if|else|elif|for|while|import|from|as|try|except|with|async|await|print|yield|lambda|None|True|False)\b", ColorHex = "#569CD6", IsBold = true, Category = "Keyword" },
                new SyntaxRule { Pattern = @"#.*", ColorHex = "#6A9955", Category = "Comment" },
                new SyntaxRule { Pattern = @"'[^']*'|""[^""]*""", ColorHex = "#D69D85", Category = "String" }
            }},
            { ".asm", new List<SyntaxRule> {
                new SyntaxRule { Pattern = @"\b(mov|add|sub|inc|dec|mul|div|jmp|je|jne|jg|jl|jge|jle|cmp|push|pop|call|ret|int|syscall|nop)\b", ColorHex = "#569CD6", IsBold = true, Category = "Keyword" },
                new SyntaxRule { Pattern = @"\b(eax|ebx|ecx|edx|esi|edi|esp|ebp|rax|rbx|rcx|rdx|rsi|rdi|rsp|rbp|al|ah|bl|bh|cl|ch|dl|dh)\b", ColorHex = "#9CDCFE", Category = "Type" },
                new SyntaxRule { Pattern = @";.*", ColorHex = "#6A9955", Category = "Comment" },
                new SyntaxRule { Pattern = @"'[^']*'|""[^""]*""", ColorHex = "#D69D85", Category = "String" },
                new SyntaxRule { Pattern = @"\b(section|global|extern|db|dw|dd|dq|resb|resw|resd|resq)\b", ColorHex = "#C586C0", Category = "Keyword" },
                new SyntaxRule { Pattern = @"\b(0x[0-9a-fA-F]+|[0-9]+)\b", ColorHex = "#B5CEA8", Category = "Number" }
            }}
        };

        private static readonly Dictionary<string, string[]> LanguageKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { ".cs", new[] { "public", "private", "protected", "internal", "static", "void", "string", "int", "bool", "var", "if", "else", "foreach", "while", "return", "class", "namespace", "using", "async", "await", "task", "override", "virtual", "new", "get", "set", "value", "delegate", "event" } },
            { ".js", new[] { "const", "let", "var", "function", "return", "if", "else", "for", "while", "import", "export", "default", "async", "await", "try", "catch", "throw", "new", "this", "super", "class" } },
            { ".ts", new[] { "const", "let", "var", "function", "return", "if", "else", "for", "while", "import", "export", "default", "async", "await", "try", "catch", "interface", "type", "enum", "class", "namespace", "private", "public", "protected" } },
            { ".py", new[] { "def", "class", "return", "if", "else", "elif", "for", "while", "import", "from", "as", "try", "except", "with", "async", "await", "print", "yield", "lambda", "None", "True", "False" } },
            { ".json", new[] { "true", "false", "null" } },
            { ".md", new[] { "TODO", "FIXME", "NOTE", "WARNING", "IMPORTANT", "HINT" } },
            { ".asm", new[] { "mov", "add", "sub", "inc", "dec", "mul", "div", "jmp", "je", "jne", "jg", "jl", "jge", "jle", "cmp", "push", "pop", "call", "ret", "int", "syscall", "section", "global", "extern", "db", "dw", "dd", "dq", "resb", "resw", "resd", "resq" } },
            { ".xaml", new[] { "Grid", "Border", "StackPanel", "TextBlock", "TextBox", "Button", "ComboBox", "CheckBox", "Canvas", "ScrollViewer", "Grid.Row", "Grid.Column", "IsVisible", "Visibility", "Background", "Foreground", "HorizontalAlignment", "VerticalAlignment", "Margin", "Padding" } }
        };

        /// <summary>
        /// Extracts local symbols (variable names, method names) from the current file text using regex.
        /// </summary>
        public static List<AutocompleteSuggestion> ExtractLocalSymbols(string text, string extension)
        {
            var symbols = new HashSet<string>();

            // Generic word-based symbol extraction (variable-like strings)
            var matches = Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]{3,}\b");
            foreach (Match m in matches) symbols.Add(m.Value);

            // Language-specific patterns
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                // Extract Method names: void MethodName(
                var methodMatches = Regex.Matches(text, @"\b(?:public|private|protected|internal|static)?\s+\w+\s+(?<name>\w+)\s*\(");
                foreach (Match m in methodMatches) symbols.Add(m.Groups["name"].Value);
            }

            return symbols.Select(s => new AutocompleteSuggestion
            {
                Text = s,
                Description = "Local Symbol",
                Icon = "💎"
            }).ToList();
        }

        private static readonly string[] DotNetBaseTypes = new[] { "Task", "List", "Dictionary", "Enumerable", "Console", "StringBuilder", "DateTime", "Guid", "Thread", "Regex", "HttpClient", "JsonSerializer", "File", "Directory", "Path", "Math", "Exception" };
        private static readonly string[] AsmRegisters = new[] { "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rsp", "rbp", "eax", "ebx", "ecx", "edx", "esi", "edi", "esp", "ebp", "ax", "bx", "cx", "dx", "al", "ah", "bl", "bh", "cl", "ch", "dl", "dh" };

        public static List<AutocompleteSuggestion> GetSuggestions(string currentLinePrefix, string extension, string fullText)
        {
            var results = new List<AutocompleteSuggestion>();
            string lastWord = Regex.Match(currentLinePrefix, @"\b\w*$").Value;

            if (string.IsNullOrEmpty(lastWord)) return results;

            // 1. Language Keywords (Top Priority)
            if (LanguageKeywords.TryGetValue(extension, out var keywords))
            {
                foreach (var kw in keywords.Where(k => k.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new AutocompleteSuggestion { Text = kw, Description = "Keyword", Icon = "🔑", Score = 1.0 });
                }
            }

            // 2. Language Specific Extras
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var type in DotNetBaseTypes.Where(t => t.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new AutocompleteSuggestion { Text = type, Description = "System Type", Icon = "🏛️", Score = 0.95 });
                }
            }
            else if (extension.Equals(".asm", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var reg in AsmRegisters.Where(r => r.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new AutocompleteSuggestion { Text = reg, Description = "Register", Icon = "📟", Score = 0.95 });
                }
            }

            // 3. Project-wide Symbols (Compiler-like behavior)
            var projectSymbols = ProjectSymbolIndexer.GetProjectSuggestions(lastWord);
            results.AddRange(projectSymbols);

            // 4. Local Symbols (In-file variables)
            var localSymbols = ExtractLocalSymbols(fullText, extension);
            foreach (var s in localSymbols.Where(s => s.Text.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)))
            {
                if (results.Any(r => r.Text == s.Text)) continue;
                s.Score = 0.8;
                results.Add(s);
            }

            return results.OrderByDescending(r => r.Score).Take(15).ToList();
        }

        public static string GetOfflineGhostPrediction(string currentLine, string extension)
        {
            // Simple logic: if line ends with '(', suggest ')' or 'arg'
            if (currentLine.EndsWith("(") && extension == ".cs") return ")";
            if (currentLine.EndsWith("{") && extension == ".cs") return "\n    \n}";
            return "";
        }

        /// <summary>
        /// Orchestrates an AI call to get a prediction for the next block of code.
        /// </summary>
        public static async Task<string> GetAiAutocompleteAsync(string contextBefore, string contextAfter, string extension)
        {
            try
            {
                string prompt = $"## TASK\nPredict the next 1-3 lines of code for a {extension} file.\n\n" +
                               "## CONTEXT BEFORE CURSOR\n" + contextBefore.TakeLast(2000) + "\n" +
                               "## CONTEXT AFTER CURSOR\n" + contextAfter.Take(500) + "\n\n" +
                               "## RULES\n" +
                               "1. Provide ONLY the raw code to be inserted.\n" +
                               "2. Do NOT use markdown backticks.\n" +
                               "3. If unsure, return an empty string.";

                string prediction = await LlmRouter.AskAsync(prompt);
                return AiAPI.SanitizeText(prediction).Trim();
            }
            catch { return ""; }
        }
    }
}
