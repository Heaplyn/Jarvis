// Developer: heaplyn
// Date: 2026-08-08
// Summary: Utility class containing character intersection fuzzy-matching algorithm and string similarity score calculations.

using System;
using System.Linq;
using System.Collections.Generic;

namespace JarvisLauncher
{
    public static class SearchUtil
    {
        public static bool IsClose(string Query1, string Query2)
        {
            Query1 = Query1.ToLower();
            Query2 = Query2.ToLower();
            int MaxLength = Math.Max(Query1.Length, Query2.Length);
            int MaxDistance = (int)(MaxLength * 0.4);

            if (Query1.Length < Query2.Length - MaxDistance)
            {
                return false;
            }
            List<char> diffChars = new List<char>();
            foreach (char c in Query1)
            {
                if (!Query2.Contains(c) && !(diffChars.Contains(c)))
                {
                    diffChars.Add(c);
                }
            }


            char[] SharedChars = Query1.Intersect(Query2).ToArray();
            int CurrentDistance = SharedChars.Length;

            return CurrentDistance > MaxDistance;
        }

        public static double GetSimilarity(string query1, string query2)
        {
            if (string.IsNullOrEmpty(query1) || string.IsNullOrEmpty(query2))
                return 0.0;

            query1 = query1.ToLower().Trim();
            query2 = query2.ToLower().Trim();

            if (query2.StartsWith(query1))
            {
                return 1.0 + ((double)query1.Length / query2.Length);
            }

            int maxLength = Math.Max(query1.Length, query2.Length);
            char[] sharedChars = query1.Intersect(query2).ToArray();
            return (double)sharedChars.Length / maxLength;
        }
    }
}
