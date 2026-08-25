// Developer: heaplyn
// Date: 2026-08-17
// Summary: High-performance purely offline Math & Symbolic Engine.
//          Recursive descent parser with cycle protection and constant mapping.

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
            try { return EvaluateInternal(expression, 0); }
            catch (Exception ex) { return "Math Error: " + ex.Message; }
        }

        private string EvaluateInternal(string expr, int depth)
        {
            if (depth > 5) return "0"; // Depth limit to prevent hang
            string clean = expr.ToLower().Trim();

            // 1. Symbolic (Return string)
            if (clean.StartsWith("diff ") || clean.StartsWith("derivative")) return SolveDerivative(clean);

            // 2. Constants Replacement
            foreach (var c in ConstantsMap) clean = Regex.Replace(clean, $@"\b{c.Key}\b", c.Value.ToString());

            // 3. Recursive Function Resolution
            foreach (var f in FunctionsMap) {
                string pattern = $@"\b{f.Key}\((?<val>[^()]+)\)";
                clean = Regex.Replace(clean, pattern, m => {
                    string inner = m.Groups["val"].Value;
                    if (double.TryParse(EvaluateInternal(inner, depth + 1), out double d)) return f.Value(d).ToString();
                    return "0";
                });
            }

            // 4. Powers
            clean = Regex.Replace(clean, @"(?<base>[\d\.\-]+)\^(?<exp>[\d\.\-]+)", m => {
                try { return Math.Pow(double.Parse(m.Groups["base"].Value), double.Parse(m.Groups["exp"].Value)).ToString(); } catch { return "0"; }
            });

            // 5. Final Pass via DataTable
            if (Regex.IsMatch(clean, @"^[0-9\s\+\-\*\/\(\)\.E]+$")) {
                try { return _table.Compute(clean, null).ToString() ?? "0"; } catch { }
            }

            return "Complex/Variables detected.";
        }

        private string SolveDerivative(string expr)
        {
            string target = expr.Replace("diff", "").Replace("derivative of", "").Trim();
            var match = Regex.Match(target, @"(?<coeff>[\d\.\-]*)\s*x\^?(?<pow>[\d\.\-]*)");
            if (match.Success) {
                double a = 1;
                string sc = match.Groups["coeff"].Value;
                if (sc == "-") a = -1; else if (!string.IsNullOrEmpty(sc)) a = double.Parse(sc);

                double n = 1;
                string sp = match.Groups["pow"].Value;
                if (string.IsNullOrEmpty(sp)) n = target.Contains("^") ? 0 : 1; else n = double.Parse(sp);

                if (n == 0) return "0";
                double nc = a * n; double np = n - 1;
                if (np == 0) return nc.ToString();
                if (np == 1) return $"{nc}x";
                return $"{nc}x^{np}";
            }
            return "Power rule (ax^n) only.";
        }
    }
}
