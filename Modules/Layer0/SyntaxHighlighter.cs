// Developer: heaplyn
// Date: 2026-08-16
// Summary: High-performance synchronous syntax highlighter for WPF RichTextBox.
//          Uses a single-pass character pointer walk to apply formatting efficiently.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Linq;

namespace JarvisLauncher
{
    public static class SyntaxHighlighter
    {
        public static void Highlight(RichTextBox rtb, string extension)
        {
            if (!EditorIntelligenceManager.SyntaxHighlightingRules.TryGetValue(extension, out var rules)) return;

            var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
            string text = range.Text;

            // Limit processing for huge files to prevent UI hang
            if (text.Length > 100000) return;

            // 1. Clear existing formatting
            range.ClearAllProperties();

            // 2. Collect all matches
            var matches = new List<(int Index, int Length, SyntaxRule Rule)>();
            foreach (var rule in rules)
            {
                var regexMatches = Regex.Matches(text, rule.Pattern, RegexOptions.Compiled);
                foreach (Match m in regexMatches)
                    matches.Add((m.Index, m.Length, rule));
            }

            // 3. Sort matches by start position
            var sortedMatches = matches.OrderBy(m => m.Index).ToList();

            // 4. Walk pointers and apply colors
            TextPointer currentPointer = rtb.Document.ContentStart;
            int currentTextOffset = 0;

            foreach (var m in sortedMatches)
            {
                // Move pointer to the start of the match
                TextPointer? start = GetPoint(currentPointer, m.Index - currentTextOffset);
                if (start == null) break;

                // Move pointer to the end of the match
                TextPointer? end = GetPoint(start, m.Length);
                if (end == null) break;

                var matchRange = new TextRange(start, end);
                try {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(m.Rule.ColorHex));
                    matchRange.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    if (m.Rule.IsBold) matchRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                } catch { }

                // Optimization: Keep track of where we are to avoid re-scanning from start
                currentPointer = start;
                currentTextOffset = m.Index;
            }
        }

        private static TextPointer? GetPoint(TextPointer start, int characterOffset)
        {
            TextPointer pointer = start;
            int remaining = characterOffset;

            while (pointer != null && remaining > 0)
            {
                var context = pointer.GetPointerContext(LogicalDirection.Forward);
                if (context == TextPointerContext.Text)
                {
                    int runLength = pointer.GetTextInRun(LogicalDirection.Forward).Length;
                    if (remaining <= runLength)
                        return pointer.GetPositionAtOffset(remaining);

                    remaining -= runLength;
                }

                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
            }

            return (remaining == 0) ? pointer : null;
        }
    }
}
