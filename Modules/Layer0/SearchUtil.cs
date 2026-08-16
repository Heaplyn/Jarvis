// Developer: heaplyn
// Date: 2026-08-13
// Summary: Enhanced string similarity and autocomplete scoring.
// Algorithms: exact, prefix, substring, word-boundary, acronym, bigram token overlap, Levenshtein with Jaro-Winkler prefix boost.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JarvisLauncher
{
    public static class SearchUtil
    {
        // ── Synonym Map ─────────────────────────────────────────────────────────
        private static readonly Dictionary<string, string[]> SynonymMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "kill",     new[] { "stop", "terminate", "close", "end", "exit", "quit" } },
            { "stop",     new[] { "kill", "terminate", "close", "end", "pause", "halt" } },
            { "start",    new[] { "launch", "open", "run", "execute", "begin" } },
            { "open",     new[] { "launch", "run", "start", "view", "show" } },
            { "search",   new[] { "find", "lookup", "query", "google", "browser" } },
            { "find",     new[] { "search", "lookup", "locate", "where" } },
            { "view",     new[] { "show", "display", "open", "read" } },
            { "show",     new[] { "view", "display", "open", "list" } },
            { "sound",    new[] { "volume", "audio", "speaker", "mute" } },
            { "music",    new[] { "audio", "song", "playlist", "spotify", "track" } },
            { "network",  new[] { "ip", "connection", "wifi", "internet", "bridge" } },
            { "pc",       new[] { "system", "computer", "machine", "power" } },
            { "memory",   new[] { "ram", "storage", "space" } },
            { "text",     new[] { "edit", "write", "note", "type" } },
            { "capture",  new[] { "screenshot", "screen", "snip", "image" } },
            { "lock",     new[] { "secure", "logout", "protect" } },
            { "shutdown", new[] { "power off", "turn off", "exit" } }
        };

        // ── Primary Relevance Gate ───────────────────────────────────────────────

        /// <summary>
        /// Determines if the query is relevant enough to the target to show it as a suggestion.
        /// </summary>
        public static bool IsClose(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return false;

            query  = query.ToLower().Trim();
            target = target.ToLower().Trim();

            if (target.StartsWith(query) || target.Contains(query))
                return true;

            // Acronym shorthand: "mp" → "music playlist", "sc" → "screenshot"
            if (IsAcronymMatch(query, target))
                return true;

            // Word-level token: any query word starts a word in target
            if (HasWordBoundaryMatch(query, target))
                return true;

            double similarity = GetSimilarity(query, target);
            return similarity > 0.40;
        }

        // ── Scoring ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a relevance score from 0.0 to 6.0+.
        /// Higher = better match. Scores stack across multiple match types.
        /// </summary>
        public static double GetSimilarity(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0.0;

            query  = query.ToLower().Trim();
            target = target.ToLower().Trim();

            // 1. Exact match
            if (query == target)
                return 6.0;

            double score = 0.0;

            // 2. Prefix: target starts with full query
            if (target.StartsWith(query))
                score = Math.Max(score, 5.0 + ((double)query.Length / target.Length));

            // 3. Substring: target contains full query
            if (target.Contains(query))
                score = Math.Max(score, 4.0 + ((double)query.Length / target.Length));

            // 4. Acronym match: "mp" → "music playlist"
            if (IsAcronymMatch(query, target))
                score = Math.Max(score, 3.8);

            // 5. Word boundary: every query token starts a word in target
            double wbScore = WordBoundaryScore(query, target);
            if (wbScore > 0)
                score = Math.Max(score, wbScore);

            // 6. Bigram token overlap: fraction of query tokens that prefix-match target words
            double bigramScore = BigramTokenScore(query, target);
            if (bigramScore > 0)
                score = Math.Max(score, bigramScore);

            // 6b. Synonym expansion boost
            double synonymBoost = GetSynonymMatchScore(query, target);
            if (synonymBoost > 0)
                score = Math.Max(score, synonymBoost);

            // 7. Fuzzy Damerau-Levenshtein with Jaro-Winkler prefix boost
            if (score < 1.0)
            {
                int distance   = DamerauLevenshteinDistance(query, target);
                int maxLength  = Math.Max(query.Length, target.Length);
                double fuzzy   = 1.0 - ((double)distance / maxLength);

                int commonPrefix = 0;
                for (int i = 0; i < Math.Min(4, Math.Min(query.Length, target.Length)); i++)
                {
                    if (query[i] == target[i]) commonPrefix++;
                    else break;
                }
                fuzzy += commonPrefix * 0.15; // Increased boost
                score  = Math.Max(score, fuzzy);
            }

            return score;
        }

        private static double GetSynonymMatchScore(string query, string target)
        {
            var qTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tWords = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var qt in qTokens)
            {
                if (SynonymMap.TryGetValue(qt, out var synonyms))
                {
                    foreach (var syn in synonyms)
                    {
                        if (tWords.Any(tw => tw.Equals(syn, StringComparison.OrdinalIgnoreCase) || tw.StartsWith(syn, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Partial match for synonym found
                            return 3.5;
                        }
                    }
                }
            }
            return 0;
        }

        // ── Autocomplete Inline Suggestion ───────────────────────────────────────

        /// <summary>
        /// Given the typed query and a candidate command name, returns the suffix
        /// that should be shown as a ghost/inline autocomplete hint (or empty string).
        /// e.g. query="mu", target="music playlist" → returns "sic playlist"
        /// </summary>
        public static string GetAutocompleteSuffix(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target)) return string.Empty;

            string q = query.ToLower().Trim();
            string t = target.ToLower().Trim();

            if (t.StartsWith(q) && t.Length > q.Length)
                return target.Substring(query.Length);

            // Acronym expand: "mp" → autocomplete to "music playlist" minus the "mp" typed
            var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (q.Length == words.Length && IsAcronymMatch(q, t))
                return " " + string.Join(" ", words.Select(w => w));

            return string.Empty;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="query"/> is an acronym for the words in <paramref name="target"/>.
        /// e.g. "mp" → "music playlist", "sc" → "screen capture"
        /// </summary>
        public static bool IsAcronymMatch(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target)) return false;

            var words = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || query.Length > words.Length) return false;

            // Every char in query must match the start char of successive words
            for (int i = 0; i < query.Length; i++)
            {
                if (i >= words.Length || words[i][0] != query[i])
                    return false;
            }
            return true;
        }

        private static bool HasWordBoundaryMatch(string query, string target)
        {
            return WordBoundaryScore(query, target) > 0;
        }

        /// <summary>
        /// Splits query into tokens and checks how many are word-boundary prefixes of target words.
        /// Returns a score proportional to coverage.
        /// </summary>
        private static double WordBoundaryScore(string query, string target)
        {
            var qTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tWords  = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (qTokens.Length == 0 || tWords.Length == 0) return 0;

            int matched = 0;
            foreach (var qt in qTokens)
            {
                if (tWords.Any(tw => tw.StartsWith(qt)))
                    matched++;
            }

            if (matched == 0) return 0;

            double coverage = (double)matched / qTokens.Length;
            // Full token coverage scores around 3.5; partial coverage scales down
            return 3.5 * coverage;
        }

        /// <summary>
        /// Bigram/token overlap: what fraction of query character bigrams appear in target.
        /// Useful for catching reordered or partially typed compound words.
        /// </summary>
        private static double BigramTokenScore(string query, string target)
        {
            if (query.Length < 2) return 0;

            var qBigrams = GetBigrams(query);
            var tBigrams = GetBigrams(target);

            if (qBigrams.Count == 0) return 0;

            int hits = qBigrams.Count(b => tBigrams.Contains(b));
            double overlap = (double)hits / qBigrams.Count;

            // Only return meaningful score if bigram overlap is substantial (>50%)
            return overlap > 0.5 ? overlap * 2.0 : 0;
        }

        private static HashSet<string> GetBigrams(string s)
        {
            var bigrams = new HashSet<string>();
            for (int i = 0; i < s.Length - 1; i++)
                bigrams.Add($"{s[i]}{s[i + 1]}");
            return bigrams;
        }

        // ── Damerau-Levenshtein ──────────────────────────────────────────────────

        private static int DamerauLevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);

                    // Transposition check
                    if (i > 1 && j > 1 && s[i - 1] == t[j - 2] && s[i - 2] == t[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + cost);
                    }
                }
            }
            return d[n, m];
        }
    }
}
