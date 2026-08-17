// Developer: heaplyn
// Date: 2026-08-17
// Summary: High-performance purely offline Math & Symbolic Engine.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data;
using System.Linq;

namespace JarvisLauncher
{
    public class MathEngine : IMathEngine
    {
        private readonly DataTable _table = new DataTable();

        public static readonly Dictionary<string, double> ConstantsMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "pi", Math.PI }, { "e", Math.E }, { "phi", 1.61803398874989 }, { "tau", Math.PI * 2 }
        };

        public static readonly Dictionary<string, Func<double, double>> FunctionsMap = new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
        {
            { "sin", Math.Sin }, { "cos", Math.Cos }, { "tan", Math.Tan },
            { "sqrt", Math.Sqrt }, { "abs", Math.Abs }, { "ln", Math.Log },
            { "log", Math.Log10 }, { "floor", Math.Floor }, { "ceil", Math.Ceiling }
        };

        public IReadOnlyDictionary<string, double> GetConstants() => ConstantsMap;
        public IReadOnlyDictionary<string, Func<double, double>> GetFunctions() => FunctionsMap;

        public string Evaluate(string expression)
        {
            try { return EvaluateRecursive(expression, 0); }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        private string EvaluateRecursive(string expr, int depth)
        {
            if (depth > 10) throw new Exception("Max recursion depth exceeded");
            string clean = expr.ToLower().Trim();

            // Handle Differentiation
            if (clean.StartsWith("diff ") || clean.StartsWith("derivative")) return SolveDerivative(clean);

            // Constants
            foreach (var c in ConstantsMap) clean = Regex.Replace(clean, $@"\b{c.Key}\b", c.Value.ToString());

            // Functions
            bool found;
            int loopGuard = 0;
            do {
                found = false;
                foreach (var f in FunctionsMap) {
                    string pattern = $@"\b{f.Key}\((?<val>[^()]+)\)";
                    clean = Regex.Replace(clean, pattern, m => {
                        found = true;
                        if (double.TryParse(EvaluateRecursive(m.Groups["val"].Value, depth + 1), out double d)) return f.Value(d).ToString();
                        return m.Value;
                    });
                }
            } while (found && ++loopGuard < 20);

            // Powers
            clean = Regex.Replace(clean, @"(?<base>[\d\.\-]+)\^(?<exp>[\d\.\-]+)", m => Math.Pow(double.Parse(m.Groups["base"].Value), double.Parse(m.Groups["exp"].Value)).ToString());

            // Final Arithmetic
            if (Regex.IsMatch(clean, @"^[0-9\s\+\-\*\/\(\)\.E]+$")) return _table.Compute(clean, null).ToString() ?? "0";
            return "Too complex for offline.";
        }

        private string SolveDerivative(string expr)
        {
            string target = expr.Replace("diff", "").Replace("derivative of", "").Trim();
            var match = Regex.Match(target, @"(?<coeff>[\d\.\-]*)\s*x\^?(?<pow>[\d\.\-]*)");
            if (match.Success) {
                double a = (match.Groups["coeff"].Value == "" || match.Groups["coeff"].Value == "-") ? (match.Groups["coeff"].Value == "-" ? -1 : 1) : double.Parse(match.Groups["coeff"].Value);
                double n = (match.Groups["pow"].Value == "") ? (target.Contains("^") ? 0 : 1) : double.Parse(match.Groups["pow"].Value);
                if (n == 0) return "0";
                double nc = a * n; double np = n - 1;
                return np == 0 ? nc.ToString() : (np == 1 ? $"{nc}x" : $"{nc}x^{np}");
            }
            return "Power rule only offline.";
        }
    }
}
