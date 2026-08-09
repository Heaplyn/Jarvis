// Developer: heaplyn
// Date: 2026-08-09
// Summary: Order-aware string similarity and search scoring using exact, prefix, substring, and Levenshtein edit distance algorithms.

using System;
using System.Linq;

namespace JarvisLauncher
{
    public static class SearchUtil
    {
        /// <summary>
        /// Determines if the query is relevant/close enough to the target string to suggest it.
        /// </summary>
        public static bool IsClose(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return false;

            query = query.ToLower().Trim();
            target = target.ToLower().Trim();

            // Direct start or contains match is always close
            if (target.StartsWith(query) || target.Contains(query))
                return true;

            // Fuzzy check using edit distance ratio
            double similarity = GetSimilarity(query, target);
            return similarity > 0.45; // Relatable similarity threshold
        }

        /// <summary>
        /// Calculates a relevance score from 0.0 (no match) to 3.0+ (perfect match / prefix boost).
        /// </summary>
        public static double GetSimilarity(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0.0;

            query = query.ToLower().Trim();
            target = target.ToLower().Trim();

            // 1. Exact match gets highest priority
            if (query == target)
            {
                return 3.0;
            }

            // 2. Starts with query gets second highest priority (Prefix Match)
            if (target.StartsWith(query))
            {
                // Boost score based on how close the length matches the target
                return 2.0 + ((double)query.Length / target.Length);
            }

            // 3. Contains query gets third highest priority (Substring Match)
            if (target.Contains(query))
            {
                return 1.5 + ((double)query.Length / target.Length);
            }

            // 4. Fuzzy Levenshtein edit distance for typos and spelling errors
            int distance = LevenshteinDistance(query, target);
            int maxLength = Math.Max(query.Length, target.Length);
            
            // Normalize distance to a 0.0 - 1.0 similarity score
            double similarity = 1.0 - ((double)distance / maxLength);

            // Jaro-Winkler style prefix boost (up to 4 matching initial characters)
            int commonPrefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(query.Length, target.Length)); i++)
            {
                if (query[i] == target[i])
                    commonPrefix++;
                else
                    break;
            }

            // Add prefix boost weight
            similarity += commonPrefix * 0.1;

            return similarity;
        }

        private static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            return d[n, m];
        }
    }
}
